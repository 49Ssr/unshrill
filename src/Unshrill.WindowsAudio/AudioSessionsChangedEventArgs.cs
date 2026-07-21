using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public sealed class AudioSessionsChangedEventArgs(
	string endpointName,
	string? endpointId,
	IReadOnlyList<AudioSessionDescriptor> sessions) : EventArgs
{
	public string EndpointName { get; } = endpointName;
	public string? EndpointId { get; } = endpointId;
	public IReadOnlyList<AudioSessionDescriptor> Sessions { get; } = sessions;
}

