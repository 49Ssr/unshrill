namespace Unshrill.Dsp;

public sealed class BiquadFilter(BiquadCoefficients coefficients)
{
	private double _x1;
	private double _x2;
	private double _y1;
	private double _y2;

	public BiquadCoefficients Coefficients { get; private set; } = coefficients;

	public float Process(float sample)
	{
		var value = Coefficients.B0 * sample + Coefficients.B1 * _x1 + Coefficients.B2 * _x2
			- Coefficients.A1 * _y1 - Coefficients.A2 * _y2;

		_x2 = _x1;
		_x1 = sample;
		_y2 = _y1;
		_y1 = value;

		return (float)value;
	}

	public void Process(Span<float> samples)
	{
		for (var index = 0; index < samples.Length; index++)
			samples[index] = Process(samples[index]);
	}

	public void Reset() => _x1 = _x2 = _y1 = _y2 = 0;

	public void SetCoefficients(BiquadCoefficients coefficients) => Coefficients = coefficients;
}

