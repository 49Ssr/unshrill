using Unshrill.Core;
using Unshrill.Dsp;
using Unshrill.WindowsAudio;

var failures = new List<string>();

Run("rule priority", RulePriority);
Run("rule specificity", RuleSpecificity);
Run("rule validation", RuleValidation);
Run("high-shelf response", HighShelfResponse);
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
