using System.Globalization;
using Microsoft.Win32;
using Unshrill.Core;

namespace Unshrill.WindowsAudio;

public sealed class EqualizerApoBackend
{
	private const string BeginMarker = "# BEGIN UNSHRILL MANAGED BLOCK";
	private const string EndMarker = "# END UNSHRILL MANAGED BLOCK";
	private const string ManagedFileName = "unshrill.txt";
	private readonly string? _explicitConfigDirectory;

	public EqualizerApoBackend(string? configDirectory = null) =>
		_explicitConfigDirectory = configDirectory;

	public EqualizerApoStatus Inspect()
	{
		var directory = ResolveConfigDirectory();
		if (directory is null)
		{
			return new EqualizerApoStatus(
				false,
				false,
				false,
				null,
				"Equalizer APO is not installed. Mercy Mode is available after its APO is installed on an output device.");
		}

		var rootPath = Path.Combine(directory, "config.txt");
		var managedPath = Path.Combine(directory, ManagedFileName);
		var root = File.Exists(rootPath) ? File.ReadAllText(rootPath) : string.Empty;
		var managed = File.Exists(managedPath) ? File.ReadAllText(managedPath) : string.Empty;
		var isManaged = root.Contains(BeginMarker, StringComparison.Ordinal) &&
			root.Contains(EndMarker, StringComparison.Ordinal);
		var isEnabled = isManaged && managed.Contains("Filter: ON HSC", StringComparison.OrdinalIgnoreCase);

		return new EqualizerApoStatus(
			true,
			isManaged,
			isEnabled,
			directory,
			isEnabled ? "Mercy Mode is active on its selected output." : "Equalizer APO is ready; Mercy Mode is bypassed.");
	}

	public Task<EqualizerApoStatus> ApplyAsync(
		ComfortSettings settings,
		string endpointId,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(settings);
		if (string.IsNullOrWhiteSpace(endpointId))
			throw new ArgumentException("An output endpoint is required.", nameof(endpointId));
		settings.Validate();

		return Task.Run(() => Apply(settings, endpointId), cancellationToken);
	}

	private EqualizerApoStatus Apply(ComfortSettings settings, string endpointId)
	{
		var directory = ResolveConfigDirectory()
			?? throw new InvalidOperationException("Equalizer APO was not found. Install it and select this output in Configurator first.");
		var rootPath = Path.Combine(directory, "config.txt");
		if (!File.Exists(rootPath))
			throw new InvalidOperationException($"Equalizer APO's config.txt was not found in {directory}.");

		var originalRoot = File.ReadAllText(rootPath);
		if (!originalRoot.Contains(BeginMarker, StringComparison.Ordinal))
		{
			var backupPath = Path.Combine(
				directory,
				$"config.unshrill-backup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
			File.Copy(rootPath, backupPath, false);
		}

		var managedPath = Path.Combine(directory, ManagedFileName);
		var hadManagedFile = File.Exists(managedPath);
		var originalManaged = hadManagedFile ? File.ReadAllText(managedPath) : null;
		try
		{
			WriteAtomically(managedPath, BuildManagedConfiguration(settings));
			WriteAtomically(rootPath, BuildRootConfiguration(originalRoot, GetDevicePattern(endpointId)));
		}
		catch
		{
			if (hadManagedFile)
				WriteAtomically(managedPath, originalManaged!);
			else if (File.Exists(managedPath))
				File.Delete(managedPath);

			throw;
		}

		return Inspect();
	}

	private string? ResolveConfigDirectory()
	{
		if (!string.IsNullOrWhiteSpace(_explicitConfigDirectory))
			return Directory.Exists(_explicitConfigDirectory) ? Path.GetFullPath(_explicitConfigDirectory) : null;

		using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");
		var configured = key?.GetValue("ConfigPath") as string;
		if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
			return Path.GetFullPath(configured);

		var conventional = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			"EqualizerAPO",
			"config");
		return Directory.Exists(conventional) ? conventional : null;
	}

	private static string BuildManagedConfiguration(ComfortSettings settings)
	{
		var state = settings.IsEnabled ? "ON" : "OFF";
		return string.Join(Environment.NewLine,
		[
			"# Managed by Unshrill. Use the app to change this file.",
			"Channel: all",
			$"Filter: {state} HSC Fc {Format(settings.FrequencyHz)} Hz Gain {Format(settings.GainDb)} dB Q {Format(settings.Q)}",
			string.Empty
		]);
	}

	private static string BuildRootConfiguration(string original, string endpointId)
	{
		var withoutManagedBlock = RemoveManagedBlock(original);
		var managedBlock = string.Join(Environment.NewLine,
		[
			BeginMarker,
			$"Device: {endpointId.Trim()}",
			"Stage: post-mix",
			$"Include: {ManagedFileName}",
			EndMarker
		]);

		if (string.IsNullOrEmpty(withoutManagedBlock))
			return $"{managedBlock}{Environment.NewLine}";

		var separator = withoutManagedBlock.EndsWith(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal)
			? string.Empty
			: withoutManagedBlock.EndsWith('\n') ? Environment.NewLine : Environment.NewLine + Environment.NewLine;
		return $"{withoutManagedBlock}{separator}{managedBlock}{Environment.NewLine}";
	}

	private static string GetDevicePattern(string endpointId)
	{
		var trimmed = endpointId.Trim();
		var guidStart = trimmed.LastIndexOf('{');
		return guidStart >= 0 && trimmed.EndsWith('}') ? trimmed[guidStart..] : trimmed;
	}

	private static string RemoveManagedBlock(string content)
	{
		var start = content.IndexOf(BeginMarker, StringComparison.Ordinal);
		if (start < 0)
			return content;

		var end = content.IndexOf(EndMarker, start, StringComparison.Ordinal);
		if (end < 0)
			throw new InvalidDataException("Equalizer APO's Unshrill block is incomplete; restore its backup before retrying.");

		end += EndMarker.Length;
		while (end < content.Length && content[end] is '\r' or '\n')
			end++;

		return content.Remove(start, end - start);
	}

	private static void WriteAtomically(string path, string content)
	{
		var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
		try
		{
			File.WriteAllText(temporaryPath, content);
			File.Move(temporaryPath, path, true);
		}
		finally
		{
			if (File.Exists(temporaryPath))
				File.Delete(temporaryPath);
		}
	}

	private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
