namespace Unshrill.Core;

public sealed record UnshrillSettings(
	int SchemaVersion,
	IReadOnlyList<AudioRule> Rules,
	ComfortSettings Comfort)
{
	public const int CurrentSchemaVersion = 1;

	public static UnshrillSettings Default { get; } =
		new(CurrentSchemaVersion, [], new ComfortSettings());

	public void Validate()
	{
		if (SchemaVersion != CurrentSchemaVersion)
			throw new InvalidOperationException($"Unsupported settings schema {SchemaVersion}.");

		foreach (var rule in Rules)
			rule.Validate();

		Comfort.Validate();
	}
}
