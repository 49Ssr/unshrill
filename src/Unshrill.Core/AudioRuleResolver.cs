namespace Unshrill.Core;

public static class AudioRuleResolver
{
	public static AudioRule? Resolve(IEnumerable<AudioRule> rules, AudioSessionDescriptor session)
	{
		ArgumentNullException.ThrowIfNull(rules);
		ArgumentNullException.ThrowIfNull(session);

		return rules
			.Where(rule => rule.IsEnabled && Matches(rule, session))
			.OrderByDescending(rule => rule.Priority)
			.ThenByDescending(Specificity)
			.ThenBy(rule => rule.Id)
			.FirstOrDefault();
	}

	private static bool Matches(AudioRule rule, AudioSessionDescriptor session)
	{
		var applicationMatches = rule.ExecutableName is null ||
			string.Equals(rule.ExecutableName, session.ExecutableName, StringComparison.OrdinalIgnoreCase);
		var endpointMatches = rule.EndpointId is null ||
			string.Equals(rule.EndpointId, session.EndpointId, StringComparison.OrdinalIgnoreCase);

		return applicationMatches && endpointMatches;
	}

	private static int Specificity(AudioRule rule) =>
		(rule.ExecutableName is null ? 0 : 1) + (rule.EndpointId is null ? 0 : 1);
}

