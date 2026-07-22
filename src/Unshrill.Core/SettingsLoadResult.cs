namespace Unshrill.Core;

public sealed record SettingsLoadResult(
	UnshrillSettings Settings,
	string Path,
	string? Warning = null,
	string? RecoveryCopyPath = null);
