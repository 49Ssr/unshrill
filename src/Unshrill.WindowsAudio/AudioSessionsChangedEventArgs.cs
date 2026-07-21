using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public sealed class AudioSessionsChangedEventArgs(
	string endpointName,
	IReadOnlyList<AudioSessionDescriptor> sessions) : EventArgs
{
	public string EndpointName { get; } = endpointName;
	public IReadOnlyList<AudioSessionDescriptor> Sessions { get; } = sessions;
}

