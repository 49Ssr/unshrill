using System.IO;
using System.Globalization;
using System.Text;
using System.Windows;
using Unshrill.Dsp;

namespace Unshrill.App;

public partial class AnalysisWindow : Window
{
	private readonly string _report;

	public AnalysisWindow(string path, HarshnessAnalysisResult result)
	{
		InitializeComponent();
		var rows = result.Events.Select(item => new AnalysisEventRow(item)).ToArray();
		FileNameText.Text = Path.GetFileName(path);
		FormatText.Text = $"{result.SampleRate:N0} Hz / {result.ChannelCount} ch";
		DurationText.Text = TimeSpan.FromSeconds(result.DurationSeconds).ToString(@"m\:ss\.fff", CultureInfo.CurrentCulture);
		PeakText.Text = $"{result.PeakRmsDbfs:0.0} dBFS";
		CandidateCountText.Text = $"{rows.Length:N0}";
		EventsGrid.ItemsSource = rows;
		EventsGrid.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
		EmptyState.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		_report = BuildReport(path, result);
	}

	private void CopyReport_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Clipboard.SetText(_report);
		}
		catch (Exception exception)
		{
			MessageBox.Show(this, $"Windows could not copy the report: {exception.Message}", "Copy failed", MessageBoxButton.OK, MessageBoxImage.Warning);
		}
	}

	private void Close_Click(object sender, RoutedEventArgs e) => Close();

	private static string BuildReport(string path, HarshnessAnalysisResult result)
	{
		var report = new StringBuilder().AppendLine("Unshrill offline candidate report");
		AppendInvariant(report, $"File: {Path.GetFileName(path)}");
		AppendInvariant(report, $"Format: {result.SampleRate} Hz, {result.ChannelCount} channels");
		AppendInvariant(report, $"Duration: {result.DurationSeconds:0.000} s");
		AppendInvariant(report, $"Frames: {result.AnalyzedFrameCount}");
		AppendInvariant(report, $"Peak frame RMS: {result.PeakRmsDbfs:0.0} dBFS");
		AppendInvariant(report, $"Average 5-10 kHz share: {result.AverageFocusBandRatio:P2}");
		AppendInvariant(report, $"Candidates: {result.Events.Count}");
		report.AppendLine();

		foreach (var item in result.Events)
		{
			AppendInvariant(report, $"{item.StartSeconds:0.000}-{item.EndSeconds:0.000}s | {item.CandidateScore:P0} | {item.Reasons} | level {item.RmsDbfs:0.0} dBFS | rise {item.EmergenceDb:+0.0;-0.0;0.0} dB | focus {item.FocusBandRatio:P1} | centroid {item.SpectralCentroidHz:0} Hz | flatness {item.SpectralFlatness:0.000} | peak {item.DominantFrequencyHz:0} Hz | prominence {item.TonalProminenceDb:0.0} dB");
		}

		return report.ToString();
	}

	private static void AppendInvariant(StringBuilder target, FormattableString line) =>
		target.AppendLine(FormattableString.Invariant(line));
}
