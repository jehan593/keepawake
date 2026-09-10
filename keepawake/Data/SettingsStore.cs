using System;
using System.IO;
using System.Text.Json;

namespace Keepawake.Data
{
    public sealed class SettingsStore
    {
        private static readonly string DirectoryPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "keepawake");

        private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

        public AppSettings Load()
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
