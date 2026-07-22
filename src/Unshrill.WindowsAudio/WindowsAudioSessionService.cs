using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public sealed class WindowsAudioSessionService : IAudioSessionService
{
	private static readonly TimeSpan RecoveryRefreshInterval = TimeSpan.FromSeconds(5);
	private readonly SemaphoreSlim _operationGate = new(1, 1);
	private readonly SemaphoreSlim _refreshSignal = new(0, 1);
	private readonly object _lifecycleGate = new();
	private CancellationTokenSource? _monitorCancellation;
	private Task? _monitorTask;
	private int _endpointRebuildRequested = 1;
	private bool _disposed;

	public event EventHandler<AudioSessionsChangedEventArgs>? SessionsChanged;
	public event EventHandler<AudioSessionServiceFaultedEventArgs>? Faulted;

	public async Task<IReadOnlyList<AudioSessionDescriptor>> GetSessionsAsync(
		CancellationToken cancellationToken = default)
	{
		var snapshot = await CaptureAsync(cancellationToken).ConfigureAwait(false);
		return snapshot.Sessions;
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		var snapshot = await CaptureAsync(cancellationToken).ConfigureAwait(false);
		SessionsChanged?.Invoke(this, new AudioSessionsChangedEventArgs(
			snapshot.EndpointName,
			snapshot.EndpointId,
			snapshot.Sessions));
	}

	public async Task SetMuteAsync(
		AudioSessionDescriptor session,
		bool isMuted,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(session);
		await UpdateSessionAsync(session.SessionId, volume => volume.Mute = isMuted, cancellationToken)
			.ConfigureAwait(false);
	}

	public async Task SetVolumeAsync(
		AudioSessionDescriptor session,
		float volume,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(session);
		if (volume is < 0 or > 1)
			throw new ArgumentOutOfRangeException(nameof(volume));

		await UpdateSessionAsync(session.SessionId, simpleVolume => simpleVolume.Volume = volume, cancellationToken)
			.ConfigureAwait(false);
	}

	public Task StartAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

		lock (_lifecycleGate)
		{
			if (_monitorTask is not null)
				return Task.CompletedTask;

			_monitorCancellation = new CancellationTokenSource();
			_monitorTask = Task.Run(() => MonitorAsync(_monitorCancellation.Token), _monitorCancellation.Token);
		}

		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		Task? monitorTask;
		CancellationTokenSource? monitorCancellation;

		lock (_lifecycleGate)
		{
			monitorTask = _monitorTask;
			monitorCancellation = _monitorCancellation;
			_monitorTask = null;
			_monitorCancellation = null;
		}

		if (monitorTask is null || monitorCancellation is null)
			return;

		await monitorCancellation.CancelAsync().ConfigureAwait(false);

		try
		{
			await monitorTask.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (monitorCancellation.IsCancellationRequested)
		{
		}
		finally
		{
			monitorCancellation.Dispose();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_disposed = true;
		await StopAsync().ConfigureAwait(false);
		_operationGate.Dispose();
		_refreshSignal.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task MonitorAsync(CancellationToken cancellationToken)
	{
		using var enumerator = new MMDeviceEnumerator();
		var notificationClient = new EndpointNotificationClient(this);
		enumerator.RegisterEndpointNotificationCallback(notificationClient);
		MMDevice? watchedEndpoint = null;
		AudioSessionManager? watchedManager = null;

		void SessionCreated(object sender, IAudioSessionControl session)
		{
			SignalRefresh(false);
		}

		void RebuildSessionWatch()
		{
			if (watchedManager is not null)
			{
				watchedManager.OnSessionCreated -= SessionCreated;
				watchedManager.Dispose();
				watchedManager = null;
			}

			watchedEndpoint?.Dispose();
			watchedEndpoint = null;
			if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
				return;

			watchedEndpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			watchedManager = watchedEndpoint.AudioSessionManager;
			watchedManager.RefreshSessions(); // GetSessionEnumerator/GetCount arms session-created notifications.
			watchedManager.OnSessionCreated += SessionCreated;
		}

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					if (Interlocked.Exchange(ref _endpointRebuildRequested, 0) != 0)
						RebuildSessionWatch();

					await RefreshAsync(cancellationToken).ConfigureAwait(false);
					await _refreshSignal.WaitAsync(RecoveryRefreshInterval, cancellationToken).ConfigureAwait(false);
					await Task.Delay(80, cancellationToken).ConfigureAwait(false); // Let a just-created session finish publishing metadata.
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception exception)
				{
					Faulted?.Invoke(this, new AudioSessionServiceFaultedEventArgs(exception));
					await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
					Interlocked.Exchange(ref _endpointRebuildRequested, 1);
				}
			}
		}
		finally
		{
			if (watchedManager is not null)
			{
				watchedManager.OnSessionCreated -= SessionCreated;
				watchedManager.Dispose();
			}

			watchedEndpoint?.Dispose();
			try
			{
				enumerator.UnregisterEndpointNotificationCallback(notificationClient);
			}
			catch (Exception exception) when (exception is COMException or InvalidOperationException)
			{
				// The endpoint can vanish during shutdown; registration is already unusable then.
			}
		}
	}

	private void SignalRefresh(bool rebuildEndpoint)
	{
		if (_disposed)
			return;
		if (rebuildEndpoint)
			Interlocked.Exchange(ref _endpointRebuildRequested, 1);

		try
		{
			_refreshSignal.Release();
		}
		catch (SemaphoreFullException)
		{
			// Multiple Core Audio callbacks collapse into one refresh.
		}
		catch (ObjectDisposedException)
		{
			// A final callback can race with shutdown after the service has stopped.
		}
	}

	private async Task<AudioSessionSnapshot> CaptureAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return await Task.Run(Capture, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_operationGate.Release();
		}
	}

	private async Task UpdateSessionAsync(
		string sessionId,
		Action<SimpleAudioVolume> update,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await Task.Run(() => UpdateSession(sessionId, update), cancellationToken).ConfigureAwait(false);
			SignalRefresh(false);
		}
		finally
		{
			_operationGate.Release();
		}
	}

	private static AudioSessionSnapshot Capture()
	{
		using var enumerator = new MMDeviceEnumerator();
		if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
			return new AudioSessionSnapshot("No default output", null, []);

		using var endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		var manager = endpoint.AudioSessionManager;
		try
		{
			manager.RefreshSessions();

			var sessions = new List<AudioSessionDescriptor>(manager.Sessions.Count);
			for (var index = 0; index < manager.Sessions.Count; index++)
			{
				using var control = manager.Sessions[index];
				if (control.State == AudioSessionState.AudioSessionStateExpired)
					continue;

				try
				{
					using var volume = control.SimpleAudioVolume;
					var processId = unchecked((int)control.GetProcessID);
					var isSystemSounds = control.IsSystemSoundsSession;
					var executableName = ResolveExecutableName(processId, isSystemSounds);
					var displayName = ResolveDisplayName(control.DisplayName, executableName, isSystemSounds);

					sessions.Add(new AudioSessionDescriptor(
						control.GetSessionInstanceIdentifier,
						processId,
						executableName,
						displayName,
						endpoint.ID,
						volume.Volume,
						volume.Mute,
						isSystemSounds));
				}
				catch (Exception exception) when (IsTransientSessionFailure(exception))
				{
					// Sessions can disappear between enumeration and inspection.
				}
			}

			return new AudioSessionSnapshot(
				endpoint.FriendlyName,
				endpoint.ID,
				sessions.OrderBy(session => session.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray());
		}
		finally
		{
			manager.Dispose();
		}
	}

	private static void UpdateSession(string sessionId, Action<SimpleAudioVolume> update)
	{
		using var enumerator = new MMDeviceEnumerator();
		if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
			return;

		using var endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
		var manager = endpoint.AudioSessionManager;
		try
		{
			manager.RefreshSessions();

			for (var index = 0; index < manager.Sessions.Count; index++)
			{
				using var control = manager.Sessions[index];
				if (!string.Equals(control.GetSessionInstanceIdentifier, sessionId, StringComparison.Ordinal))
					continue;

				using var volume = control.SimpleAudioVolume;
				update(volume);
				return;
			}
		}
		finally
		{
			manager.Dispose();
		}
	}

	private static string ResolveExecutableName(int processId, bool isSystemSounds)
	{
		if (isSystemSounds || processId <= 0)
			return "System sounds";

		try
		{
			using var process = Process.GetProcessById(processId);
			try
			{
				var fileName = process.MainModule?.FileName;
				if (!string.IsNullOrWhiteSpace(fileName))
					return Path.GetFileName(fileName);
			}
			catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
			{
			}

			return $"{process.ProcessName}.exe";
		}
		catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
		{
			return $"Process {processId}";
		}
	}

	private static string ResolveDisplayName(string? displayName, string executableName, bool isSystemSounds)
	{
		if (isSystemSounds)
			return "System sounds";
		if (!string.IsNullOrWhiteSpace(displayName) && !displayName.StartsWith('@'))
			return displayName;

		return Path.GetFileNameWithoutExtension(executableName);
	}

	private static bool IsTransientSessionFailure(Exception exception) =>
		exception is InvalidOperationException or System.Runtime.InteropServices.COMException;

	private sealed record AudioSessionSnapshot(
		string EndpointName,
		string? EndpointId,
		IReadOnlyList<AudioSessionDescriptor> Sessions);

	[ComVisible(true)]
	private sealed class EndpointNotificationClient(WindowsAudioSessionService owner) : IMMNotificationClient
	{
		public void OnDeviceStateChanged(string deviceId, DeviceState newState) => owner.SignalRefresh(true);
		public void OnDeviceAdded(string pwstrDeviceId) => owner.SignalRefresh(true);
		public void OnDeviceRemoved(string deviceId) => owner.SignalRefresh(true);

		public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
		{
			if (flow == DataFlow.Render && role == Role.Multimedia)
				owner.SignalRefresh(true);
		}

		public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
		{
			// Friendly-name changes do not affect session identity; the recovery refresh will pick them up.
		}
	}
}
