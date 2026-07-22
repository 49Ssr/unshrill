namespace Unshrill.Dsp;

public sealed record HarshnessAnalysisOptions(
	int WindowSize = 2_048,
	int HopSize = 512,
	double FocusBandLowHz = 5_000,
	double FocusBandHighHz = 10_000,
	double MinimumRmsDbfs = -60,
	double CandidateThreshold = 0.48,
	double MergeGapSeconds = 0.08)
{
	public void Validate(int sampleRate)
	{
		if (sampleRate is < 8_000 or > 384_000)
			throw new ArgumentOutOfRangeException(nameof(sampleRate));
		if (WindowSize < 256 || (WindowSize & (WindowSize - 1)) != 0)
			throw new InvalidOperationException("The analysis window must be a power of two and at least 256 samples.");
		if (HopSize <= 0 || HopSize > WindowSize)
			throw new InvalidOperationException("The hop size must be between one sample and the window size.");
		if (FocusBandLowHz <= 0 || FocusBandHighHz <= FocusBandLowHz || FocusBandLowHz >= sampleRate / 2d)
			throw new InvalidOperationException("The focus band must overlap the available spectrum.");
		if (CandidateThreshold is < 0 or > 1)
			throw new InvalidOperationException("The candidate threshold must be between zero and one.");
		if (MergeGapSeconds < 0)
			throw new InvalidOperationException("The merge gap cannot be negative.");
	}
}
