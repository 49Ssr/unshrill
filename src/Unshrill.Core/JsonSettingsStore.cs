using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unshrill.Core;

public sealed class JsonSettingsStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public JsonSettingsStore(string? path = null)
	{
		Path = path ?? System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Unshrill",
			"settings.json");
	}

	public string Path { get; }

	public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
	{
		if (!File.Exists(Path))
			return new SettingsLoadResult(UnshrillSettings.Default, Path);

		try
		{
			await using var stream = File.OpenRead(Path);
			var settings = await JsonSerializer.DeserializeAsync<UnshrillSettings>(
				stream,
				SerializerOptions,
				cancellationToken);
			if (settings is null)
				throw new JsonException("The settings file was empty.");

			settings.Validate();
			return new SettingsLoadResult(settings, Path);
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException)
		{
			var recoveryPath = $"{Path}.invalid-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
			File.Copy(Path, recoveryPath, false);
			return new SettingsLoadResult(
				UnshrillSettings.Default,
				Path,
				$"Settings were invalid and were not applied: {exception.Message}",
				recoveryPath);
		}
	}

	public async Task SaveAsync(UnshrillSettings settings, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(settings);
		settings.Validate();

		var directory = System.IO.Path.GetDirectoryName(Path)
			?? throw new InvalidOperationException("The settings path has no parent directory.");
		Directory.CreateDirectory(directory);

		var temporaryPath = $"{Path}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var stream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				4096,
				FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
				await stream.FlushAsync(cancellationToken);
			}

			File.Move(temporaryPath, Path, true);
		}
		finally
		{
			if (File.Exists(temporaryPath))
				File.Delete(temporaryPath);
		}
	}
}
