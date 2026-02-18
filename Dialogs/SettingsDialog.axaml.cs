using System;
using System.Globalization;
using Avalonia.Controls;
using MenuProUI.Models;
using MenuProUI.Services;

namespace MenuProUI.Dialogs;

public partial class SettingsDialog : Window
{
    public AppSettings Result { get; private set; }
    public bool RequestRestoreLatestBackup { get; private set; }

    public SettingsDialog() : this(new AppSettings())
    {
    }

    public SettingsDialog(AppSettings initial)
    {
        InitializeComponent();
        Result = initial;
        TimeoutBox.Text = initial.ConnectivityTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        ConcurrencyBox.Text = initial.ConnectivityMaxConcurrency.ToString(CultureInfo.InvariantCulture);
        CacheTtlBox.Text = initial.ConnectivityCacheTTLSeconds.ToString(CultureInfo.InvariantCulture);
        FallbackPortsBox.Text = initial.ConnectivityUrlFallbackPortsCsv;
        AutoCheckOnSelectBox.IsChecked = initial.ConnectivityAutoCheckOnSelect;
        AutoCheckDebounceBox.Text = initial.ConnectivityAutoCheckDebounceMs.ToString(CultureInfo.InvariantCulture);
        ExportFormulaProtectionBox.IsChecked = initial.ExportFormulaProtection;
        RefreshToolStatus();

        var latestBackup = new CsvRepository().GetLatestBackupSnapshot();
        LatestBackupText.Text = $"Último backup: {(latestBackup is null ? "(nenhum)" : System.IO.Path.GetFileName(latestBackup))}";
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!double.TryParse((TimeoutBox.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var timeout))
            timeout = 3.0;
        if (!int.TryParse((ConcurrencyBox.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var concurrency))
            concurrency = 12;
        if (!double.TryParse((CacheTtlBox.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cache))
            cache = 10.0;
        if (!int.TryParse((AutoCheckDebounceBox.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var debounceMs))
            debounceMs = 800;

        Result = new AppSettings
        {
            ConnectivityTimeoutSeconds = Math.Clamp(timeout, 0.5, 60.0),
            ConnectivityMaxConcurrency = Math.Clamp(concurrency, 1, 128),
            ConnectivityCacheTTLSeconds = Math.Clamp(cache, 0, 3600),
            ConnectivityUrlFallbackPortsCsv = string.IsNullOrWhiteSpace(FallbackPortsBox.Text)
                ? "443,80,8443,8080,9443"
                : FallbackPortsBox.Text.Trim(),
            ConnectivityAutoCheckOnSelect = AutoCheckOnSelectBox.IsChecked == true,
            ConnectivityAutoCheckDebounceMs = Math.Clamp(debounceMs, 0, 10000),
            ExportFormulaProtection = ExportFormulaProtectionBox.IsChecked == true
        };

        Close(true);
    }

    private void RefreshToolStatus()
    {
        NmapStatusText.Text = $"nmap: {(ConnectivityChecker.HasNmap ? ConnectivityChecker.NmapPathDescription : "não encontrado")}";
        NcStatusText.Text = $"nc: {(ConnectivityChecker.HasNc ? ConnectivityChecker.NcPathDescription : "não encontrado")}";
    }

    private void OnRefreshTools(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConnectivityChecker.RevalidateToolPaths();
        NmapTestResultText.Text = "";
        RefreshToolStatus();
    }

    private async void OnTestNmap(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NmapTestResultText.Text = "Testando nmap...";
        var result = await ConnectivityChecker.TestNmapNowAsync(TimeSpan.FromSeconds(2));
        NmapTestResultText.Text = result.Message;
    }

    private void OnRestoreLatestBackup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RequestRestoreLatestBackup = true;
        Close(true);
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
