using System;
using System.IO;
using System.Text.Json;
using MenuProUI.Models;

namespace MenuProUI.Services;

public sealed class SettingsRepository
{
    private static string SettingsPath => Path.Combine(AppPaths.AppDir, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            var cfg = JsonSerializer.Deserialize<AppSettings>(json);
            return cfg ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var sane = new AppSettings
        {
            ConnectivityTimeoutSeconds = Math.Clamp(settings.ConnectivityTimeoutSeconds, 0.5, 60.0),
            ConnectivityMaxConcurrency = Math.Clamp(settings.ConnectivityMaxConcurrency, 1, 128),
            ConnectivityCacheTTLSeconds = Math.Clamp(settings.ConnectivityCacheTTLSeconds, 0.0, 3600.0),
            ConnectivityUrlFallbackPortsCsv = string.IsNullOrWhiteSpace(settings.ConnectivityUrlFallbackPortsCsv)
                ? "443,80,8443,8080,9443"
                : settings.ConnectivityUrlFallbackPortsCsv.Trim(),
            ConnectivityAutoCheckOnSelect = settings.ConnectivityAutoCheckOnSelect,
            ConnectivityAutoCheckDebounceMs = Math.Clamp(settings.ConnectivityAutoCheckDebounceMs, 0, 10000),
            ExportFormulaProtection = settings.ExportFormulaProtection
        };

        var json = JsonSerializer.Serialize(sane, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
