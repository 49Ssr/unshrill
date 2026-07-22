using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Unshrill.Core;
using Unshrill.WindowsAudio;

namespace Unshrill.App;

public partial class MainWindow : Window, IAsyncDisposable
{
	private const int RememberedRulePriority = 100;
	private readonly WindowsAudioSessionService _audioService = new();
	private readonly EqualizerApoBackend _treatmentBackend = new();
	private readonly JsonSettingsStore _settingsStore = new();
	private readonly SemaphoreSlim _settingsGate = new(1, 1);
	private readonly Dictionary<string, CancellationTokenSource> _volumeUpdates = new(StringComparer.Ordinal);
	private readonly HashSet<string> _ruleApplications = new(StringComparer.Ordinal);
	private readonly List<AudioRule> _rules = [];
	private ComfortSettings _comfort = new();
	private string? _currentEndpointId;
	private string? _currentEndpointName;
	private bool _applyingComfortControls;
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
			var loadResult = await _settingsStore.LoadAsync();
			_rules.AddRange(loadResult.Settings.Rules);
			_comfort = loadResult.Settings.Comfort;
			ApplyComfortControls();
			RefreshTreatmentStatus();

			if (loadResult.Warning is not null)
				SetStatus($"{loadResult.Warning} A recovery copy was preserved.", true);
			else
				SetStatus($"Watching audio sessions. Saved rules: {_rules.Count}.");

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
			RefreshTreatmentStatus();
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
		if (!_shutdownStarted)
			Dispatcher.BeginInvoke(() => ApplySnapshot(e.EndpointName, e.EndpointId, e.Sessions));
	}

	private void AudioService_Faulted(object? sender, AudioSessionServiceFaultedEventArgs e)
	{
		if (!_shutdownStarted)
			Dispatcher.BeginInvoke(() => SetStatus($"Audio monitor will retry: {e.Exception.Message}", true));
	}

	private void ApplySnapshot(
		string endpointName,
		string? endpointId,
		IReadOnlyList<AudioSessionDescriptor> descriptors)
	{
		_currentEndpointId = endpointId;
		_currentEndpointName = endpointName;
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
			var rule = ResolveRememberedRule(descriptor);
			var existing = Sessions.FirstOrDefault(session =>
				string.Equals(session.SessionId, descriptor.SessionId, StringComparison.Ordinal));

			if (existing is not null)
			{
				existing.ApplySnapshot(descriptor);
				existing.ApplyRememberedState(rule is not null);
			}
			else
			{
				existing = new AudioSessionRowViewModel(descriptor, rule is not null);
				existing.VolumeRequested += Session_VolumeRequested;
				existing.MuteRequested += Session_MuteRequested;
				existing.RememberRequested += Session_RememberRequested;
				Sessions.Add(existing);
			}

			if (rule is not null)
				_ = ApplyRememberedRuleAsync(existing, rule);
		}

		EmptyState.Visibility = Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		SessionCountText.Text = Sessions.Count == 1 ? "1 session" : $"{Sessions.Count} sessions";
		RefreshTreatmentStatus();
		SetStatus($"Live controls active. {_rules.Count} application rule{(_rules.Count == 1 ? string.Empty : "s")} saved.");
	}

	private async Task ApplyRememberedRuleAsync(AudioSessionRowViewModel row, AudioRule rule)
	{
		if (!_ruleApplications.Add(row.SessionId))
			return;

		try
		{
			if (rule.Volume is { } targetVolume && Math.Abs(row.Descriptor.Volume - targetVolume) > 0.005f)
				await _audioService.SetVolumeAsync(row.Descriptor, targetVolume);
			if (rule.IsMuted is { } targetMute && row.Descriptor.IsMuted != targetMute)
				await _audioService.SetMuteAsync(row.Descriptor, targetMute);
		}
		catch (Exception exception)
		{
			SetStatus($"Could not restore {row.DisplayName}: {exception.Message}", true);
		}
		finally
		{
			_ruleApplications.Remove(row.SessionId);
		}
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
			if (row.IsRemembered)
			{
				UpsertRememberedRule(row, volume, row.IsMuted);
				await SaveSettingsAsync(cancellation.Token);
			}
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
			if (row.IsRemembered)
			{
				UpsertRememberedRule(row, (float)(row.VolumePercent / 100), isMuted);
				await SaveSettingsAsync();
			}
		}
		catch (Exception exception)
		{
			SetStatus($"Could not change {row.DisplayName}: {exception.Message}", true);
		}
	}

	private async void Session_RememberRequested(AudioSessionRowViewModel row, bool remember)
	{
		try
		{
			if (remember)
				UpsertRememberedRule(row, (float)(row.VolumePercent / 100), row.IsMuted);
			else
				_rules.RemoveAll(rule => IsRememberedRuleFor(rule, row.ExecutableName));

			await SaveSettingsAsync();
			RefreshRememberedStates();
			SetStatus(remember
				? $"{row.ExecutableName} will return at this level."
				: $"Stopped remembering {row.ExecutableName}.");
		}
		catch (Exception exception)
		{
			row.ApplyRememberedState(!remember);
			SetStatus($"Could not save the rule: {exception.Message}", true);
		}
	}

	private AudioRule? ResolveRememberedRule(AudioSessionDescriptor descriptor) =>
		AudioRuleResolver.Resolve(_rules.Where(rule => IsRememberedRuleFor(rule, descriptor.ExecutableName)), descriptor);

	private static bool IsRememberedRuleFor(AudioRule rule, string executableName) =>
		rule.Priority == RememberedRulePriority &&
		rule.EndpointId is null &&
		rule.ComfortProfileId is null &&
		string.Equals(rule.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase);

	private void UpsertRememberedRule(AudioSessionRowViewModel row, float volume, bool isMuted)
	{
		var existing = _rules.FirstOrDefault(rule => IsRememberedRuleFor(rule, row.ExecutableName));
		var replacement = new AudioRule(
			existing?.Id ?? Guid.NewGuid(),
			$"Remembered {row.ExecutableName}",
			row.ExecutableName,
			Volume: Math.Clamp(volume, 0, 1),
			IsMuted: isMuted,
			Priority: RememberedRulePriority);

		if (existing is not null)
			_rules[_rules.IndexOf(existing)] = replacement;
		else
			_rules.Add(replacement);
	}

	private void RefreshRememberedStates()
	{
		foreach (var row in Sessions)
			row.ApplyRememberedState(_rules.Any(rule => IsRememberedRuleFor(rule, row.ExecutableName)));
	}

	private async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
	{
		await _settingsGate.WaitAsync(cancellationToken);
		try
		{
			await _settingsStore.SaveAsync(
				new UnshrillSettings(UnshrillSettings.CurrentSchemaVersion, _rules.ToArray(), _comfort),
				cancellationToken);
		}
		finally
		{
			_settingsGate.Release();
		}
	}

	private void ApplyComfortControls()
	{
		_applyingComfortControls = true;
		try
		{
			FrequencySlider.Value = _comfort.FrequencyHz;
			GainSlider.Value = _comfort.GainDb;
			QSlider.Value = _comfort.Q;
			MercyToggle.IsChecked = _comfort.IsEnabled;
			UpdateComfortLabels();
		}
		finally
		{
			_applyingComfortControls = false;
		}
	}

	private void ComfortParameter_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_applyingComfortControls || !IsLoaded)
			return;

		UpdateComfortLabels();
		ApplyComfortButton.IsEnabled = _treatmentBackend.Inspect().IsAvailable && _currentEndpointId is not null;
		TreatmentStatusText.Text = "Settings changed. Apply when ready.";
	}

	private void UpdateComfortLabels()
	{
		FrequencyValueText.Text = $"{FrequencySlider.Value / 1000:0.0} kHz";
		GainValueText.Text = $"{GainSlider.Value:0.0} dB";
		QValueText.Text = $"Q {QSlider.Value:0.00}";
	}

	private async void MercyToggle_Click(object sender, RoutedEventArgs e) => await ApplyComfortAsync();

	private async void ApplyComfort_Click(object sender, RoutedEventArgs e) => await ApplyComfortAsync();

	private async Task ApplyComfortAsync()
	{
		if (_applyingComfortControls)
			return;
		if (_currentEndpointId is null)
		{
			SetStatus("Wait for a default output before applying Mercy Mode.", true);
			return;
		}

		var requested = new ComfortSettings(
			MercyToggle.IsChecked == true,
			FrequencySlider.Value,
			GainSlider.Value,
			QSlider.Value,
			_currentEndpointId,
			_currentEndpointName);

		SetComfortControlsEnabled(false);
		try
		{
			var status = await _treatmentBackend.ApplyAsync(requested, _currentEndpointId);
			_comfort = requested;
			await SaveSettingsAsync();
			TreatmentStatusText.Text = status.Message;
			SetStatus(requested.IsEnabled
				? $"Mercy Mode enabled for {_currentEndpointName}."
				: "Mercy Mode bypassed. The audio path remains available.");
		}
		catch (Exception exception)
		{
			MercyToggle.IsChecked = _comfort.IsEnabled;
			SetStatus($"Could not update Equalizer APO: {exception.Message}", true);
			TreatmentStatusText.Text = "No audio configuration was intentionally removed. Check permissions or Configurator.";
		}
		finally
		{
			SetComfortControlsEnabled(true);
			RefreshTreatmentStatus();
		}
	}

	private void RefreshTreatmentStatus()
	{
		var status = _treatmentBackend.Inspect();
		TreatmentStatusText.Text = status.Message;
		BackendButton.Content = status.IsAvailable ? "Open config folder" : "Get Equalizer APO";
		MercyToggle.IsEnabled = status.IsAvailable && _currentEndpointId is not null;
		ApplyComfortButton.IsEnabled = status.IsAvailable && _currentEndpointId is not null;
		_applyingComfortControls = true;
		try
		{
			MercyToggle.IsChecked = status.IsManaged && status.IsEnabled;
		}
		finally
		{
			_applyingComfortControls = false;
		}
	}

	private void Backend_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var status = _treatmentBackend.Inspect();
			var target = status.ConfigDirectory ?? "https://sourceforge.net/projects/equalizerapo/";
			Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
		}
		catch (Exception exception)
		{
			SetStatus($"Could not open the treatment backend: {exception.Message}", true);
		}
	}

	private void SetComfortControlsEnabled(bool enabled)
	{
		FrequencySlider.IsEnabled = enabled;
		GainSlider.IsEnabled = enabled;
		QSlider.IsEnabled = enabled;
		BackendButton.IsEnabled = enabled;
		if (!enabled)
		{
			MercyToggle.IsEnabled = false;
			ApplyComfortButton.IsEnabled = false;
			return;
		}

		RefreshTreatmentStatus();
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
		row.RememberRequested -= Session_RememberRequested;
	}

	private void SetStatus(string message, bool isError = false)
	{
		StatusText.Text = message;
		StatusDot.Fill = isError ? Brushes.IndianRed : (Brush)FindResource("AccentBrush");
	}
}
