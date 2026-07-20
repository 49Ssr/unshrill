namespace Unshrill.Core;

public sealed record AudioRule(
	Guid Id,
	string Name,
	string? ExecutableName = null,
	string? EndpointId = null,
	float? Volume = null,
	bool? IsMuted = null,
	string? ComfortProfileId = null,
	int Priority = 0,
	bool IsEnabled = true)
{
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Name))
			throw new InvalidOperationException("A rule needs a name.");

		if (Volume is < 0 or > 1)
			throw new InvalidOperationException("Volume must be between 0 and 1.");

		if (ExecutableName is null && EndpointId is null)
			throw new InvalidOperationException("A rule needs an application or endpoint match.");
	}
}

