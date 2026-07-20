using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public interface IAudioSessionService : IAsyncDisposable
{
	event EventHandler<AudioSessionsChangedEventArgs>? SessionsChanged;

	Task<IReadOnlyList<AudioSessionDescriptor>> GetSessionsAsync(CancellationToken cancellationToken = default);
	Task SetMuteAsync(AudioSessionDescriptor session, bool isMuted, CancellationToken cancellationToken = default);
	Task SetVolumeAsync(AudioSessionDescriptor session, float volume, CancellationToken cancellationToken = default);
	Task StartAsync(CancellationToken cancellationToken = default);
	Task StopAsync(CancellationToken cancellationToken = default);
}

