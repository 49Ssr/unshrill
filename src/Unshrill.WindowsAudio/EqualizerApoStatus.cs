namespace Unshrill.WindowsAudio;

public sealed record EqualizerApoStatus(
	bool IsAvailable,
	bool IsManaged,
	bool IsEnabled,
	string? ConfigDirectory,
	string Message);
