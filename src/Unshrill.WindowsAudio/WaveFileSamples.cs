namespace Unshrill.WindowsAudio;

public sealed record WaveFileSamples(
	int SampleRate,
	int ChannelCount,
	float[] InterleavedSamples)
{
	public double DurationSeconds => InterleavedSamples.Length / (double)(SampleRate * ChannelCount);
}
