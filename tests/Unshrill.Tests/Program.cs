using NAudio.Wave;
using Unshrill.Core;
using Unshrill.Dsp;
using Unshrill.WindowsAudio;

var failures = new List<string>();

Run("rule priority", RulePriority);
Run("rule specificity", RuleSpecificity);
Run("rule validation", RuleValidation);
Run("high-shelf response", HighShelfResponse);
Run("bright transient candidate", BrightTransientCandidate);
Run("sudden impact candidate", SuddenImpactCandidate);
Run("steady low tone stays clear", SteadyLowToneStaysClear);
Run("inaudible digital tone stays clear", InaudibleDigitalToneStaysClear);
Run("WAV loading", WaveLoading);
Run("settings round trip", SettingsRoundTrip);
Run("invalid settings recovery", InvalidSettingsRecovery);
Run("Equalizer APO managed configuration", EqualizerApoManagedConfiguration);

if (failures.Count == 0)
{
	Console.WriteLine("All Unshrill smoke tests passed.");
	return 0;
}

foreach (var failure in failures)
	Console.Error.WriteLine(failure);

return 1;

void Run(string name, Action test)
{
	try
	{
		test();
		Console.WriteLine($"PASS {name}");
	}
	catch (Exception exception)
	{
		failures.Add($"FAIL {name}: {exception.Message}");
	}
}

static void RulePriority()
{
	var session = new AudioSessionDescriptor("session-1", 10, "game.exe", "Game", "speakers", 0.5f, false, false);
	var low = new AudioRule(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Low", "game.exe", Priority: 1);
	var high = new AudioRule(Guid.Parse("00000000-0000-0000-0000-000000000002"), "High", "game.exe", Priority: 2);

	AssertEqual(high, AudioRuleResolver.Resolve([low, high], session));
}

static void RuleSpecificity()
{
	var session = new AudioSessionDescriptor("session-1", 10, "game.exe", "Game", "headphones", 0.5f, false, false);
	var application = new AudioRule(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Application", "game.exe");
	var specific = new AudioRule(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Specific", "game.exe", "headphones");

	AssertEqual(specific, AudioRuleResolver.Resolve([application, specific], session));
}

static void RuleValidation()
{
	var invalid = new AudioRule(Guid.NewGuid(), "Invalid", "game.exe", Volume: 1.1f);
	AssertThrows<InvalidOperationException>(invalid.Validate);
}

static void HighShelfResponse()
{
	var coefficients = HighShelfDesigner.Design(48_000, 5_000, -6);
	var lowDb = 20 * Math.Log10(coefficients.MagnitudeAt(100, 48_000));
	var highDb = 20 * Math.Log10(coefficients.MagnitudeAt(18_000, 48_000));

	AssertNear(0, lowDb, 0.1);
	AssertNear(-6, highDb, 0.25);
}

static void BrightTransientCandidate()
{
	const int sampleRate = 48_000;
	var samples = new float[sampleRate * 2];
	for (var index = 0; index < samples.Length; index++)
	{
		var time = index / (double)sampleRate;
		var value = 0.025 * Math.Sin(2 * Math.PI * 220 * time);
		if (time is >= 0.75 and < 0.9)
		{
			var local = time - 0.75;
			var envelope = Math.Min(1, local / 0.004) * Math.Min(1, (0.9 - time) / 0.015);
			value += 0.65 * envelope * Math.Sin(2 * Math.PI * 7_000 * time);
		}
		samples[index] = (float)value;
	}

	var result = HarshnessAnalyzer.Analyze(samples, sampleRate, 1);
	var candidate = result.Events.FirstOrDefault(item => item.StartSeconds < 0.9 && item.EndSeconds > 0.75) ??
		throw new InvalidOperationException("The synthetic 7 kHz burst should produce a candidate event.");

	AssertTrue(candidate.Reasons.HasFlag(AudioCandidateReason.Bright), "The burst should be identified as bright.");
	AssertTrue(candidate.Reasons.HasFlag(AudioCandidateReason.Tonal), "The burst should be identified as tonal.");
	AssertTrue(candidate.CandidateScore > 0.6, "The isolated burst should produce a strong candidate score.");
	AssertTrue(candidate.FocusBandRatio > 0.3, $"The burst should place substantial energy in the focus band; got {candidate.FocusBandRatio:P1}.");
	AssertNear(7_000, candidate.DominantFrequencyHz, 30);
}

static void SteadyLowToneStaysClear()
{
	const int sampleRate = 48_000;
	var samples = new float[sampleRate * 2];
	for (var index = 0; index < samples.Length; index++)
		samples[index] = (float)(0.2 * Math.Sin(2 * Math.PI * 220 * index / sampleRate));

	var result = HarshnessAnalyzer.Analyze(samples, sampleRate, 1);
	AssertEqual(0, result.Events.Count);
}

static void SuddenImpactCandidate()
{
	const int sampleRate = 48_000;
	var samples = new float[sampleRate * 2];
	for (var index = 0; index < samples.Length; index++)
		samples[index] = (float)(0.01 * Math.Sin(2 * Math.PI * 220 * index / sampleRate));
	for (var index = 0; index < 96; index++)
		samples[sampleRate + index] += (float)(0.9 * Math.Exp(-index / 18d) * (index % 2 == 0 ? 1 : -1));

	var result = HarshnessAnalyzer.Analyze(samples, sampleRate, 1);
	var candidate = result.Events.FirstOrDefault(item => item.StartSeconds < 1.05 && item.EndSeconds > 1) ??
		throw new InvalidOperationException("The synthetic impact should produce a candidate event.");
	AssertTrue(candidate.Reasons.HasFlag(AudioCandidateReason.Transient), "The impact should be identified as transient.");
	AssertTrue(candidate.Reasons.HasFlag(AudioCandidateReason.SuddenEmergence), "The impact should rise above its background.");
}

static void InaudibleDigitalToneStaysClear()
{
	const int sampleRate = 48_000;
	var samples = new float[sampleRate];
	for (var index = 0; index < samples.Length; index++)
		samples[index] = (float)(0.0001 * Math.Sin(2 * Math.PI * 7_000 * index / sampleRate));

	var result = HarshnessAnalyzer.Analyze(samples, sampleRate, 1);
	AssertEqual(0, result.Events.Count);
}

static void WaveLoading()
{
	WithTemporaryDirectory(directory =>
	{
		const int sampleRate = 48_000;
		var path = Path.Combine(directory, "input.wav");
		var written = new float[sampleRate / 10 * 2];
		for (var index = 0; index < written.Length; index += 2)
		{
			var value = (float)(0.25 * Math.Sin(2 * Math.PI * 440 * index / 2 / sampleRate));
			written[index] = value;
			written[index + 1] = value;
		}

		using (var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 2)))
			writer.WriteSamples(written, 0, written.Length);

		var loaded = WaveFileLoader.Load(path);
		AssertEqual(sampleRate, loaded.SampleRate);
		AssertEqual(2, loaded.ChannelCount);
		AssertEqual(written.Length, loaded.InterleavedSamples.Length);
		AssertNear(0.1, loaded.DurationSeconds, 0.001);
		AssertNear(written[2_000], loaded.InterleavedSamples[2_000], 0.0001);
	});
}

static void SettingsRoundTrip()
{
	WithTemporaryDirectory(directory =>
	{
		var path = Path.Combine(directory, "settings.json");
		var store = new JsonSettingsStore(path);
		var rule = new AudioRule(Guid.NewGuid(), "Remembered game.exe", "game.exe", Volume: 0.35f, IsMuted: true, Priority: 100);
		var settings = new UnshrillSettings(
			UnshrillSettings.CurrentSchemaVersion,
			[rule],
			new ComfortSettings(true, 5_000, -6, 0.7, "endpoint", "Speakers"));

		store.SaveAsync(settings).GetAwaiter().GetResult();
		var loaded = store.LoadAsync().GetAwaiter().GetResult();

		AssertEqual<string?>(null, loaded.Warning);
		AssertEqual(rule, loaded.Settings.Rules.Single());
		AssertEqual(settings.Comfort, loaded.Settings.Comfort);
	});
}

static void InvalidSettingsRecovery()
{
	WithTemporaryDirectory(directory =>
	{
		var path = Path.Combine(directory, "settings.json");
		File.WriteAllText(path, "{ definitely-not-json }");
		var loaded = new JsonSettingsStore(path).LoadAsync().GetAwaiter().GetResult();

		AssertTrue(loaded.Warning is not null, "Invalid settings should produce a warning.");
		AssertTrue(loaded.RecoveryCopyPath is not null && File.Exists(loaded.RecoveryCopyPath), "A recovery copy should exist.");
		AssertEqual(0, loaded.Settings.Rules.Count);
	});
}

static void EqualizerApoManagedConfiguration()
{
	WithTemporaryDirectory(directory =>
	{
		const string original = "Preamp: -1 dB\r\nInclude: headphones.txt\r\n";
		var configPath = Path.Combine(directory, "config.txt");
		File.WriteAllText(configPath, original);
		var backend = new EqualizerApoBackend(directory);
		var enabled = new ComfortSettings(true, 5_000, -6, 0.7, "endpoint", "Speakers");

		var status = backend.ApplyAsync(enabled, "{0.0.0.00000000}.{TEST-ENDPOINT}").GetAwaiter().GetResult();
		var root = File.ReadAllText(configPath);
		var managed = File.ReadAllText(Path.Combine(directory, "unshrill.txt"));

		AssertTrue(status.IsEnabled, "The backend should report its filter as enabled.");
		AssertTrue(root.StartsWith(original.TrimEnd(), StringComparison.Ordinal), "Existing configuration must stay before the managed block.");
		AssertTrue(root.Contains("Device: {TEST-ENDPOINT}", StringComparison.Ordinal), "The filter must be endpoint-scoped by its stable GUID portion.");
		AssertTrue(root.Contains("Include: unshrill.txt", StringComparison.Ordinal), "The managed file must be included.");
		AssertTrue(managed.Contains("Filter: ON HSC Fc 5000 Hz Gain -6 dB Q 0.7", StringComparison.Ordinal), "The expected shelf must be generated.");
		AssertEqual(1, Directory.GetFiles(directory, "config.unshrill-backup-*.txt").Length);
		AssertEqual(original, File.ReadAllText(Directory.GetFiles(directory, "config.unshrill-backup-*.txt").Single()));

		backend.ApplyAsync(enabled with { IsEnabled = false }, "{0.0.0.00000000}.{TEST-ENDPOINT}").GetAwaiter().GetResult();
		AssertTrue(File.ReadAllText(Path.Combine(directory, "unshrill.txt")).Contains("Filter: OFF HSC", StringComparison.Ordinal), "Bypass should retain a valid OFF filter.");
		AssertEqual(1, Directory.GetFiles(directory, "config.unshrill-backup-*.txt").Length);
	});
}

static void WithTemporaryDirectory(Action<string> action)
{
	var directory = Path.Combine(Path.GetTempPath(), $"unshrill-tests-{Guid.NewGuid():N}");
	Directory.CreateDirectory(directory);
	try
	{
		action(directory);
	}
	finally
	{
		if (Directory.Exists(directory))
			Directory.Delete(directory, true);
	}
}

static void AssertEqual<T>(T expected, T actual)
{
	if (!EqualityComparer<T>.Default.Equals(expected, actual))
		throw new InvalidOperationException($"Expected {expected}; got {actual}.");
}

static void AssertNear(double expected, double actual, double tolerance)
{
	if (Math.Abs(expected - actual) > tolerance)
		throw new InvalidOperationException($"Expected {expected} +/- {tolerance}; got {actual}.");
}

static void AssertTrue(bool condition, string message)
{
	if (!condition)
		throw new InvalidOperationException(message);
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
	try
	{
		action();
	}
	catch (TException)
	{
		return;
	}

	throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
