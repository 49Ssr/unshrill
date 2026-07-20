using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public sealed class AudioSessionsChangedEventArgs(
	IReadOnlyList<AudioSessionDescriptor> sessions) : EventArgs
{
	public IReadOnlyList<AudioSessionDescriptor> Sessions { get; } = sessions;
}

