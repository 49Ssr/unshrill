namespace Unshrill.Core;

public sealed record ComfortSettings(
	bool IsEnabled = false,
	double FrequencyHz = 5_000,
	double GainDb = -6,
	double Q = 0.7,
	string? EndpointId = null,
	string? EndpointName = null)
{
	public void Validate()
	{
		if (FrequencyHz is < 2_000 or > 18_000)
			throw new InvalidOperationException("The shelf frequency must be between 2 and 18 kHz.");
		if (GainDb is < -18 or > 0)
			throw new InvalidOperationException("The shelf gain must be between -18 and 0 dB.");
		if (Q is < 0.1 or > 2)
			throw new InvalidOperationException("The shelf Q must be between 0.1 and 2.");
	}
}
