using Unshrill.Dsp;

namespace Unshrill.App;

public sealed record AnalysisEventRow(HarshAudioEvent Event)
{
	public string Time => $"{Event.StartSeconds:0.000}-{Event.EndSeconds:0.000} s";
	public string Duration => $"{Event.DurationSeconds * 1000:0} ms";
	public string Score => $"{Event.CandidateScore:P0}";
	public string Signature => Event.Reasons.ToString().Replace(",", " +", StringComparison.Ordinal);
	public string Level => $"{Event.RmsDbfs:0.0} dBFS";
	public string Emergence => $"{Event.EmergenceDb:+0.0;-0.0;0.0} dB";
	public string FocusShare => $"{Event.FocusBandRatio:P0}";
	public string Centroid => $"{Event.SpectralCentroidHz / 1000:0.0} kHz";
	public string Flatness => $"{Event.SpectralFlatness:0.000}";
	public string Dominant => $"{Event.DominantFrequencyHz / 1000:0.00} kHz";
	public string Prominence => $"{Event.TonalProminenceDb:0.0} dB";
}
