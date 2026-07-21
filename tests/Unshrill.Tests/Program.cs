using Unshrill.Core;
using Unshrill.Dsp;

var failures = new List<string>();

Run("rule priority", RulePriority);
Run("rule specificity", RuleSpecificity);
Run("rule validation", RuleValidation);
Run("high-shelf response", HighShelfResponse);

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
