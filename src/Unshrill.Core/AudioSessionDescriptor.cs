namespace Unshrill.Core;

public sealed record AudioSessionDescriptor(
	string SessionId,
	int ProcessId,
	string ExecutableName,
	string DisplayName,
	string EndpointId,
	float Volume,
	bool IsMuted,
	bool IsSystemSounds);

