using System.Numerics;

namespace Unshrill.Dsp;

public static class HarshnessAnalyzer
{
	private const double Epsilon = 1e-20;

	public static HarshnessAnalysisResult Analyze(
		float[] interleavedSamples,
		int sampleRate,
		int channelCount,
		HarshnessAnalysisOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(interleavedSamples);
		if (channelCount is < 1 or > 32)
			throw new ArgumentOutOfRangeException(nameof(channelCount));
		if (interleavedSamples.Length % channelCount != 0)
			throw new ArgumentException("The sample count must contain complete channel frames.", nameof(interleavedSamples));

		options ??= new HarshnessAnalysisOptions();
		options.Validate(sampleRate);

		var mono = Downmix(interleavedSamples, channelCount);
		if (mono.Length < options.WindowSize)
			return new HarshnessAnalysisResult(sampleRate, channelCount, mono.Length / (double)sampleRate, 0, -120, 0, []);

		var window = CreateHannWindow(options.WindowSize);
		var spectrum = new Complex[options.WindowSize];
		var previousMagnitude = new double[options.WindowSize / 2 + 1];
		var magnitude = new double[previousMagnitude.Length];
		var candidateFrames = new List<CandidateFrame>();
		var frameCount = 0;
		var peakRmsDbfs = -120d;
		var focusBandRatioTotal = 0d;
		double? backgroundPower = null;

		for (var offset = 0; offset + options.WindowSize <= mono.Length; offset += options.HopSize)
		{
			var frame = AnalyzeFrame(
				mono,
				offset,
				sampleRate,
				window,
				spectrum,
				magnitude,
				previousMagnitude,
				backgroundPower,
				options);

			backgroundPower = UpdateBackground(backgroundPower, frame.LinearPower, options.HopSize / (double)sampleRate);
			peakRmsDbfs = Math.Max(peakRmsDbfs, frame.RmsDbfs);
			focusBandRatioTotal += frame.FocusBandRatio;
			frameCount++;

			if (frame.IsCandidate)
				candidateFrames.Add(frame.Candidate!);
		}

		return new HarshnessAnalysisResult(
			sampleRate,
			channelCount,
			mono.Length / (double)sampleRate,
			frameCount,
			peakRmsDbfs,
			frameCount == 0 ? 0 : focusBandRatioTotal / frameCount,
			Merge(candidateFrames, options.MergeGapSeconds));
	}

	private static FrameMetrics AnalyzeFrame(
		float[] samples,
		int offset,
		int sampleRate,
		double[] window,
		Complex[] spectrum,
		double[] magnitude,
		double[] previousMagnitude,
		double? backgroundPower,
		HarshnessAnalysisOptions options)
	{
		var sumSquares = 0d;
		var peak = 0d;
		for (var index = 0; index < window.Length; index++)
		{
			var sample = samples[offset + index];
			sumSquares += sample * sample;
			peak = Math.Max(peak, Math.Abs(sample));
			spectrum[index] = new Complex(sample * window[index], 0);
		}

		var linearPower = sumSquares / window.Length;
		var rmsDbfs = ToDb(Math.Sqrt(linearPower));
		var crestFactorDb = ToDb(peak / Math.Max(Math.Sqrt(linearPower), Epsilon));
		var emergenceDb = backgroundPower is null ? 0 : 10 * Math.Log10(Math.Max(linearPower, Epsilon) / Math.Max(backgroundPower.Value, Epsilon));

		Radix2Fft.Forward(spectrum);
		var magnitudeSum = 0d;
		var totalPower = 0d;
		var weightedFrequency = 0d;
		var logarithmicPower = 0d;
		var flux = 0d;
		for (var bin = 1; bin < magnitude.Length; bin++)
		{
			var value = spectrum[bin].Magnitude;
			magnitude[bin] = value;
			magnitudeSum += value;
			var power = value * value;
			totalPower += power;
			weightedFrequency += value * bin * sampleRate / spectrum.Length;
			logarithmicPower += Math.Log(Math.Max(power, Epsilon));
		}

		var normalization = Math.Max(magnitudeSum, Epsilon);
		for (var bin = 1; bin < magnitude.Length; bin++)
		{
			var normalized = magnitude[bin] / normalization;
			var delta = normalized - previousMagnitude[bin];
			if (delta > 0)
				flux += delta;
			previousMagnitude[bin] = normalized;
		}

		var highFrequency = Math.Min(options.FocusBandHighHz, sampleRate / 2d);
		var lowBin = Math.Max(1, (int)Math.Ceiling(options.FocusBandLowHz * spectrum.Length / sampleRate));
		var highBin = Math.Min(magnitude.Length - 1, (int)Math.Floor(highFrequency * spectrum.Length / sampleRate));
		var focusPower = 0d;
		for (var bin = lowBin; bin <= highBin; bin++)
			focusPower += magnitude[bin] * magnitude[bin];

		var tonalLowBin = Math.Max(1, (int)Math.Ceiling(1_000d * spectrum.Length / sampleRate));
		var tonalHighBin = Math.Min(magnitude.Length - 1, (int)Math.Floor(Math.Min(12_000d, sampleRate / 2d) * spectrum.Length / sampleRate));
		var dominantBin = tonalLowBin;
		var dominantPower = 0d;
		var tonalPower = 0d;
		for (var bin = tonalLowBin; bin <= tonalHighBin; bin++)
		{
			var power = magnitude[bin] * magnitude[bin];
			tonalPower += power;
			if (power <= dominantPower)
				continue;
			dominantPower = power;
			dominantBin = bin;
		}

		var tonalBinCount = Math.Max(1, tonalHighBin - tonalLowBin + 1);
		var tonalProminenceDb = 10 * Math.Log10(Math.Max(dominantPower, Epsilon) / Math.Max(tonalPower / tonalBinCount, Epsilon));
		var focusBandRatio = focusPower / Math.Max(totalPower, Epsilon);
		var spectralCentroidHz = weightedFrequency / Math.Max(magnitudeSum, Epsilon);
		var spectralFlatness = Math.Exp(logarithmicPower / Math.Max(1, magnitude.Length - 1)) / Math.Max(totalPower / Math.Max(1, magnitude.Length - 1), Epsilon);
		var dominantFrequencyHz = dominantBin * sampleRate / (double)spectrum.Length;
		var candidate = ScoreCandidate(
			offset,
			window.Length,
			sampleRate,
			rmsDbfs,
			emergenceDb,
			focusBandRatio,
			spectralCentroidHz,
			spectralFlatness,
			dominantFrequencyHz,
			tonalProminenceDb,
			flux,
			crestFactorDb,
			options);

		return new FrameMetrics(linearPower, rmsDbfs, focusBandRatio, candidate);
	}

	private static CandidateFrame? ScoreCandidate(
		int offset,
		int windowSize,
		int sampleRate,
		double rmsDbfs,
		double emergenceDb,
		double focusBandRatio,
		double centroidHz,
		double spectralFlatness,
		double dominantFrequencyHz,
		double tonalProminenceDb,
		double flux,
		double crestFactorDb,
		HarshnessAnalysisOptions options)
	{
		if (rmsDbfs < options.MinimumRmsDbfs)
			return null;

		var brightness = Math.Max(Normalize(focusBandRatio, 0.08, 0.45), Normalize(centroidHz, 3_000, 9_000) * 0.75);
		var tonality = dominantFrequencyHz >= 1_500 ? Normalize(tonalProminenceDb, 7, 22) : 0;
		var transient = Math.Max(Normalize(flux, 0.08, 0.45), Normalize(crestFactorDb, 8, 22) * 0.7);
		var emergence = Normalize(emergenceDb, 4, 18);
		var level = Normalize(rmsDbfs, -50, -15);
		var brightScore = 0.5 * brightness + 0.22 * transient + 0.18 * emergence + 0.1 * level;
		var tonalScore = 0.45 * tonality + 0.25 * emergence + 0.15 * transient + 0.15 * level;
		var impactScore = 0.45 * emergence + 0.35 * transient + 0.2 * level;
		var score = Math.Clamp(Math.Max(brightScore, Math.Max(tonalScore, impactScore)), 0, 1);

		var reasons = AudioCandidateReason.None;
		if (brightness >= 0.45)
			reasons |= AudioCandidateReason.Bright;
		if (tonality >= 0.45)
			reasons |= AudioCandidateReason.Tonal;
		if (transient >= 0.45)
			reasons |= AudioCandidateReason.Transient;
		if (emergence >= 0.35)
			reasons |= AudioCandidateReason.SuddenEmergence;

		var plausible =
			(reasons.HasFlag(AudioCandidateReason.Bright) && (reasons.HasFlag(AudioCandidateReason.Transient) || reasons.HasFlag(AudioCandidateReason.Tonal) || reasons.HasFlag(AudioCandidateReason.SuddenEmergence))) ||
			(reasons.HasFlag(AudioCandidateReason.Tonal) && reasons.HasFlag(AudioCandidateReason.SuddenEmergence)) ||
			(reasons.HasFlag(AudioCandidateReason.Transient) && reasons.HasFlag(AudioCandidateReason.SuddenEmergence) && rmsDbfs > -35);

		if (!plausible || score < options.CandidateThreshold)
			return null;

		return new CandidateFrame(
			offset / (double)sampleRate,
			(offset + windowSize) / (double)sampleRate,
			score,
			reasons,
			rmsDbfs,
			emergenceDb,
			focusBandRatio,
			centroidHz,
			spectralFlatness,
			dominantFrequencyHz,
			tonalProminenceDb,
			flux,
			crestFactorDb);
	}

	private static List<HarshAudioEvent> Merge(List<CandidateFrame> frames, double maximumGap)
	{
		if (frames.Count == 0)
			return [];

		var events = new List<HarshAudioEvent>();
		var start = frames[0].StartSeconds;
		var end = frames[0].EndSeconds;
		var reasons = frames[0].Reasons;
		var representative = frames[0];

		for (var index = 1; index < frames.Count; index++)
		{
			var frame = frames[index];
			if (frame.StartSeconds <= end + maximumGap)
			{
				end = Math.Max(end, frame.EndSeconds);
				reasons |= frame.Reasons;
				if (frame.Score > representative.Score)
					representative = frame;
				continue;
			}

			events.Add(ToEvent(start, end, reasons, representative));
			start = frame.StartSeconds;
			end = frame.EndSeconds;
			reasons = frame.Reasons;
			representative = frame;
		}

		events.Add(ToEvent(start, end, reasons, representative));
		return events;
	}

	private static HarshAudioEvent ToEvent(double start, double end, AudioCandidateReason reasons, CandidateFrame frame) =>
		new(
			start,
			end,
			frame.Score,
			reasons,
			frame.RmsDbfs,
			frame.EmergenceDb,
			frame.FocusBandRatio,
			frame.SpectralCentroidHz,
			frame.SpectralFlatness,
			frame.DominantFrequencyHz,
			frame.TonalProminenceDb,
			frame.SpectralFlux,
			frame.CrestFactorDb);

	private static float[] Downmix(float[] interleavedSamples, int channelCount)
	{
		var mono = new float[interleavedSamples.Length / channelCount];
		for (var frame = 0; frame < mono.Length; frame++)
		{
			var sum = 0d;
			for (var channel = 0; channel < channelCount; channel++)
			{
				var sample = interleavedSamples[frame * channelCount + channel];
				if (float.IsFinite(sample))
					sum += sample;
			}
			mono[frame] = (float)(sum / channelCount);
		}
		return mono;
	}

	private static double[] CreateHannWindow(int size)
	{
		var values = new double[size];
		for (var index = 0; index < size; index++)
			values[index] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * index / (size - 1));
		return values;
	}

	private static double UpdateBackground(double? current, double framePower, double hopSeconds)
	{
		if (current is null)
			return framePower;
		var alpha = 1 - Math.Exp(-hopSeconds / 0.75);
		if (framePower > current.Value)
			alpha *= 0.15;
		return current.Value + alpha * (framePower - current.Value);
	}

	private static double Normalize(double value, double low, double high) => Math.Clamp((value - low) / (high - low), 0, 1);

	private static double ToDb(double value) => 20 * Math.Log10(Math.Max(value, Epsilon));

	private sealed record FrameMetrics(
		double LinearPower,
		double RmsDbfs,
		double FocusBandRatio,
		CandidateFrame? Candidate)
	{
		public bool IsCandidate => Candidate is not null;
	}

	private sealed record CandidateFrame(
		double StartSeconds,
		double EndSeconds,
		double Score,
		AudioCandidateReason Reasons,
		double RmsDbfs,
		double EmergenceDb,
		double FocusBandRatio,
		double SpectralCentroidHz,
		double SpectralFlatness,
		double DominantFrequencyHz,
		double TonalProminenceDb,
		double SpectralFlux,
		double CrestFactorDb);
}
