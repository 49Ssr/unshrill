namespace Unshrill.Dsp;

public sealed record HarshAudioEvent(
	double StartSeconds,
	double EndSeconds,
	double CandidateScore,
	AudioCandidateReason Reasons,
	double RmsDbfs,
	double EmergenceDb,
	double FocusBandRatio,
	double SpectralCentroidHz,
	double SpectralFlatness,
	double DominantFrequencyHz,
	double TonalProminenceDb,
	double SpectralFlux,
	double CrestFactorDb)
{
	public double DurationSeconds => EndSeconds - StartSeconds;
}
