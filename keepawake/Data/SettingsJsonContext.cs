using System.Text.Json.Serialization;

namespace Keepawake.Data;

/// <summary>Source-generated JSON metadata for settings.json — reflection-based serialization is
/// unavailable under Native AOT (see SettingsStore).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
