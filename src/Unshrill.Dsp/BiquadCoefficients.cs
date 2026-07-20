namespace Unshrill.Dsp;

public readonly record struct BiquadCoefficients(
	double B0,
	double B1,
	double B2,
	double A1,
	double A2)
{
	public double MagnitudeAt(double frequencyHz, double sampleRate)
	{
		if (frequencyHz < 0 || frequencyHz > sampleRate / 2)
			throw new ArgumentOutOfRangeException(nameof(frequencyHz));

		var angle = 2 * Math.PI * frequencyHz / sampleRate;
		var z1 = System.Numerics.Complex.FromPolarCoordinates(1, -angle);
		var z2 = z1 * z1;
		var numerator = B0 + B1 * z1 + B2 * z2;
		var denominator = 1 + A1 * z1 + A2 * z2;

		return (numerator / denominator).Magnitude;
	}
}

