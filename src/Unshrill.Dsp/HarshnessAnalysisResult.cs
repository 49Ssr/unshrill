namespace Unshrill.Dsp;

public sealed record HarshnessAnalysisResult(
	int SampleRate,
	int ChannelCount,
	double DurationSeconds,
	int AnalyzedFrameCount,
	double PeakRmsDbfs,
	double AverageFocusBandRatio,
	IReadOnlyList<HarshAudioEvent> Events);
