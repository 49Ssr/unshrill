using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public sealed class WindowsAudioSessionService : IAudioSessionService
{
	private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(750);
	private readonly SemaphoreSlim _operationGate = new(1, 1);
	private readonly object _lifecycleGate = new();
	private CancellationTokenSource? _pollCancellation;
	private Task? _pollTask;
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
		SessionsChanged?.Invoke(this, new AudioSessionsChangedEventArgs(snapshot.EndpointName, snapshot.Sessions));
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
			if (_pollTask is not null)
				return Task.CompletedTask;

			_pollCancellation = new CancellationTokenSource();
			_pollTask = PollAsync(_pollCancellation.Token);
		}

		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		Task? pollTask;
		CancellationTokenSource? pollCancellation;

		lock (_lifecycleGate)
		{
			pollTask = _pollTask;
			pollCancellation = _pollCancellation;
			_pollTask = null;
			_pollCancellation = null;
		}

		if (pollTask is null || pollCancellation is null)
			return;

		await pollCancellation.CancelAsync().ConfigureAwait(false);

		try
		{
			await pollTask.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (pollCancellation.IsCancellationRequested)
		{
		}
		finally
		{
			pollCancellation.Dispose();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_disposed = true;
		await StopAsync().ConfigureAwait(false);
		_operationGate.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task PollAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await RefreshAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				Faulted?.Invoke(this, new AudioSessionServiceFaultedEventArgs(exception));
			}

			await Task.Delay(RefreshInterval, cancellationToken).ConfigureAwait(false);
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
			return new AudioSessionSnapshot("No default output", []);

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
		IReadOnlyList<AudioSessionDescriptor> Sessions);
}
