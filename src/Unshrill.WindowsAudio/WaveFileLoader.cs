using NAudio.Wave;

namespace Unshrill.WindowsAudio;

public static class WaveFileLoader
{
	private const int MaximumDecodedSamples = 50_000_000;

	public static WaveFileSamples Load(string path, TimeSpan? maximumDuration = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		maximumDuration ??= TimeSpan.FromMinutes(5);
		if (maximumDuration <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(maximumDuration));

		using var reader = new WaveFileReader(path);
		var provider = reader.ToSampleProvider();
		var format = provider.WaveFormat;
		if (reader.TotalTime > maximumDuration)
			throw new InvalidDataException($"Choose a WAV shorter than {maximumDuration.Value.TotalMinutes:0.#} minutes for this first workbench.");

		var maximumSamples = checked((long)Math.Ceiling(maximumDuration.Value.TotalSeconds * format.SampleRate * format.Channels));
		var estimatedSamples = Math.Min(maximumSamples, checked((long)Math.Ceiling(reader.TotalTime.TotalSeconds * format.SampleRate * format.Channels)));
		if (estimatedSamples > MaximumDecodedSamples)
			throw new InvalidDataException("The decoded WAV would exceed the workbench's 200 MB sample limit. Choose a shorter excerpt.");
		var samples = new List<float>((int)estimatedSamples);
		var buffer = new float[65_536];

		while (true)
		{
			var count = provider.Read(buffer, 0, buffer.Length);
			if (count == 0)
				break;
			if (samples.Count + (long)count > maximumSamples || samples.Count + (long)count > MaximumDecodedSamples)
				throw new InvalidDataException("The decoded WAV is too large for this first workbench.");
			for (var index = 0; index < count; index++)
				samples.Add(buffer[index]);
		}

		return new WaveFileSamples(format.SampleRate, format.Channels, [.. samples]);
	}
}
