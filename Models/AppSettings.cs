namespace MenuProUI.Models;

public sealed class AppSettings
{
    public double ConnectivityTimeoutSeconds { get; set; } = 3.0;
    public int ConnectivityMaxConcurrency { get; set; } = 12;
    public double ConnectivityCacheTTLSeconds { get; set; } = 10.0;
    public string ConnectivityUrlFallbackPortsCsv { get; set; } = "443,80,8443,8080,9443";
    public bool ConnectivityAutoCheckOnSelect { get; set; } = false;
    public int ConnectivityAutoCheckDebounceMs { get; set; } = 800;
    public bool ExportFormulaProtection { get; set; } = false;
}
