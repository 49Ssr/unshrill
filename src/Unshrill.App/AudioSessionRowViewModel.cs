using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unshrill.Core;

namespace Unshrill.App;

public sealed class AudioSessionRowViewModel : INotifyPropertyChanged
{
	private AudioSessionDescriptor _descriptor;
	private bool _isMuted;
	private bool _isApplyingSnapshot;
	private double _volumePercent;

	public AudioSessionRowViewModel(AudioSessionDescriptor descriptor)
	{
		_descriptor = descriptor;
		_volumePercent = descriptor.Volume * 100;
		_isMuted = descriptor.IsMuted;
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	public event Action<AudioSessionRowViewModel, bool>? MuteRequested;
	public event Action<AudioSessionRowViewModel, float>? VolumeRequested;

	public AudioSessionDescriptor Descriptor => _descriptor;
	public string DisplayName => _descriptor.DisplayName;
	public string ExecutableName => _descriptor.ExecutableName;
	public string SessionId => _descriptor.SessionId;
	public string VolumeText => $"{VolumePercent:0}%";

	public bool IsMuted
	{
		get => _isMuted;
		set
		{
			if (!SetProperty(ref _isMuted, value) || _isApplyingSnapshot)
				return;

			MuteRequested?.Invoke(this, value);
		}
	}

	public double VolumePercent
	{
		get => _volumePercent;
		set
		{
			var constrained = Math.Clamp(value, 0, 100);
			if (!SetProperty(ref _volumePercent, constrained))
				return;

			OnPropertyChanged(nameof(VolumeText));
			if (!_isApplyingSnapshot)
				VolumeRequested?.Invoke(this, (float)(constrained / 100));
		}
	}

	public void ApplySnapshot(AudioSessionDescriptor descriptor)
	{
		_isApplyingSnapshot = true;

		try
		{
			_descriptor = descriptor;
			OnPropertyChanged(nameof(Descriptor));
			OnPropertyChanged(nameof(DisplayName));
			OnPropertyChanged(nameof(ExecutableName));
			VolumePercent = descriptor.Volume * 100;
			IsMuted = descriptor.IsMuted;
		}
		finally
		{
			_isApplyingSnapshot = false;
		}
	}

	private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
