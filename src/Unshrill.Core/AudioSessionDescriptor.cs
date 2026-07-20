namespace Unshrill.Core;

public sealed record AudioSessionDescriptor(
	int ProcessId,
	string ExecutableName,
	string DisplayName,
	string EndpointId);

