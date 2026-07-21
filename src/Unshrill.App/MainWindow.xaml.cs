using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Unshrill.Core;
using Unshrill.WindowsAudio;

namespace Unshrill.App;

public partial class MainWindow : Window, IAsyncDisposable
{
	private readonly WindowsAudioSessionService _audioService = new();
	private readonly Dictionary<string, CancellationTokenSource> _volumeUpdates = new(StringComparer.Ordinal);
	private bool _shutdownComplete;
	private bool _shutdownStarted;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = this;
		_audioService.SessionsChanged += AudioService_SessionsChanged;
		_audioService.Faulted += AudioService_Faulted;
		Loaded += Window_Loaded;
		Closing += Window_Closing;
	}

	public ObservableCollection<AudioSessionRowViewModel> Sessions { get; } = [];

	public async ValueTask DisposeAsync()
	{
		await _audioService.DisposeAsync();
		GC.SuppressFinalize(this);
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			SetStatus("Watching the default output for audio sessions.");
			await _audioService.StartAsync();
		}
		catch (Exception exception)
		{
			SetStatus($"Could not start audio discovery: {exception.Message}", true);
		}
	}

	private async void Refresh_Click(object sender, RoutedEventArgs e)
	{
		RefreshButton.IsEnabled = false;

		try
		{
			await _audioService.RefreshAsync();
		}
		catch (Exception exception)
		{
			SetStatus($"Refresh failed: {exception.Message}", true);
		}
		finally
		{
			RefreshButton.IsEnabled = true;
		}
	}

	private void AudioService_SessionsChanged(object? sender, AudioSessionsChangedEventArgs e)
	{
		if (_shutdownStarted)
			return;

		Dispatcher.BeginInvoke(() => ApplySnapshot(e.EndpointName, e.Sessions));
	}

	private void AudioService_Faulted(object? sender, AudioSessionServiceFaultedEventArgs e)
	{
		if (_shutdownStarted)
			return;

		Dispatcher.BeginInvoke(() => SetStatus($"Audio monitor will retry: {e.Exception.Message}", true));
	}

	private void ApplySnapshot(string endpointName, IReadOnlyList<AudioSessionDescriptor> descriptors)
	{
		EndpointNameText.Text = endpointName;
		var incomingIds = descriptors.Select(descriptor => descriptor.SessionId).ToHashSet(StringComparer.Ordinal);

		for (var index = Sessions.Count - 1; index >= 0; index--)
		{
			if (incomingIds.Contains(Sessions[index].SessionId))
				continue;

			Detach(Sessions[index]);
			Sessions.RemoveAt(index);
		}

		foreach (var descriptor in descriptors)
		{
			var existing = Sessions.FirstOrDefault(session =>
				string.Equals(session.SessionId, descriptor.SessionId, StringComparison.Ordinal));

			if (existing is not null)
			{
				existing.ApplySnapshot(descriptor);
				continue;
			}

			var row = new AudioSessionRowViewModel(descriptor);
			row.VolumeRequested += Session_VolumeRequested;
			row.MuteRequested += Session_MuteRequested;
			Sessions.Add(row);
		}

		EmptyState.Visibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		SessionCountText.Text = Sessions.Count == 1 ? "1 session" : $"{Sessions.Count} sessions";
		SetStatus("Live controls are active. Changes apply immediately.");
	}

	private async void Session_VolumeRequested(AudioSessionRowViewModel row, float volume)
	{
		if (_volumeUpdates.Remove(row.SessionId, out var previous))
		{
			await previous.CancelAsync();
			previous.Dispose();
		}

		var cancellation = new CancellationTokenSource();
		_volumeUpdates[row.SessionId] = cancellation;

		try
		{
			await Task.Delay(120, cancellation.Token);
			await _audioService.SetVolumeAsync(row.Descriptor, volume, cancellation.Token);
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			SetStatus($"Could not change {row.DisplayName}: {exception.Message}", true);
		}
		finally
		{
			if (_volumeUpdates.TryGetValue(row.SessionId, out var current) && ReferenceEquals(current, cancellation))
				_volumeUpdates.Remove(row.SessionId);

			cancellation.Dispose();
		}
	}

	private async void Session_MuteRequested(AudioSessionRowViewModel row, bool isMuted)
	{
		try
		{
			await _audioService.SetMuteAsync(row.Descriptor, isMuted);
		}
		catch (Exception exception)
		{
			SetStatus($"Could not change {row.DisplayName}: {exception.Message}", true);
		}
	}

	private async void Window_Closing(object? sender, CancelEventArgs e)
	{
		if (_shutdownComplete)
			return;
		if (_shutdownStarted)
		{
			e.Cancel = true;
			return;
		}

		e.Cancel = true;
		_shutdownStarted = true;
		SetStatus("Stopping the audio monitor...");

		foreach (var cancellation in _volumeUpdates.Values)
			await cancellation.CancelAsync();
		foreach (var cancellation in _volumeUpdates.Values)
			cancellation.Dispose();
		_volumeUpdates.Clear();

		_audioService.SessionsChanged -= AudioService_SessionsChanged;
		_audioService.Faulted -= AudioService_Faulted;
		await DisposeAsync();

		_shutdownComplete = true;
		Close();
	}

	private void Detach(AudioSessionRowViewModel row)
	{
		row.VolumeRequested -= Session_VolumeRequested;
		row.MuteRequested -= Session_MuteRequested;
	}

	private void SetStatus(string message, bool isError = false)
	{
		StatusText.Text = message;
		StatusDot.Fill = isError ? Brushes.IndianRed : (Brush)FindResource("AccentBrush");
	}
}
