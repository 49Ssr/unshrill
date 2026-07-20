namespace Unshrill.Dsp;

public static class HighShelfDesigner
{
	public static BiquadCoefficients Design(
		double sampleRate,
		double frequencyHz,
		double gainDb,
		double slope = 0.7)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
		if (frequencyHz <= 0 || frequencyHz >= sampleRate / 2)
			throw new ArgumentOutOfRangeException(nameof(frequencyHz));
		if (slope <= 0 || slope > 1)
			throw new ArgumentOutOfRangeException(nameof(slope));

		var amplitude = Math.Pow(10, gainDb / 40);
		var omega = 2 * Math.PI * frequencyHz / sampleRate;
		var cosine = Math.Cos(omega);
		var sine = Math.Sin(omega);
		var alpha = sine / 2 * Math.Sqrt((amplitude + 1 / amplitude) * (1 / slope - 1) + 2);
		var beta = 2 * Math.Sqrt(amplitude) * alpha;

		var b0 = amplitude * ((amplitude + 1) + (amplitude - 1) * cosine + beta);
		var b1 = -2 * amplitude * ((amplitude - 1) + (amplitude + 1) * cosine);
		var b2 = amplitude * ((amplitude + 1) + (amplitude - 1) * cosine - beta);
		var a0 = (amplitude + 1) - (amplitude - 1) * cosine + beta;
		var a1 = 2 * ((amplitude - 1) - (amplitude + 1) * cosine);
		var a2 = (amplitude + 1) - (amplitude - 1) * cosine - beta;

		return new BiquadCoefficients(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
	}
}
