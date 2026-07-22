using System.Numerics;

namespace Unshrill.Dsp;

internal static class Radix2Fft
{
	public static void Forward(Complex[] values)
	{
		var count = values.Length;
		if (count == 0 || (count & (count - 1)) != 0)
			throw new ArgumentException("FFT input length must be a power of two.", nameof(values));

		for (int source = 1, target = 0; source < count; source++)
		{
			var bit = count >> 1;
			for (; (target & bit) != 0; bit >>= 1)
				target ^= bit;
			target ^= bit;

			if (source < target)
				(values[source], values[target]) = (values[target], values[source]);
		}

		for (var length = 2; length <= count; length <<= 1)
		{
			var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
			for (var offset = 0; offset < count; offset += length)
			{
				var factor = Complex.One;
				for (var index = 0; index < length / 2; index++)
				{
					var even = values[offset + index];
					var odd = values[offset + index + length / 2] * factor;
					values[offset + index] = even + odd;
					values[offset + index + length / 2] = even - odd;
					factor *= step;
				}
			}
		}
	}
}
