using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MenuProUI.Dialogs;

public partial class AuditLogDialog : Window
{
    private readonly List<AuditEventRow> _events = new();
    private List<AuditEventRow> _filtered = new();

    public AuditLogDialog() : this("")
    {
    }

    public AuditLogDialog(string eventsPath)
    {
        InitializeComponent();
        LoadEvents(eventsPath);
        BuildFilters();
        ApplyFilter();
    }

    private void LoadEvents(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = SplitCsv(line);
            _events.Add(new AuditEventRow
            {
                TimestampUtc = Cell(cells, 0),
                Action = Cell(cells, 1),
                EntityType = Cell(cells, 2),
                EntityName = Cell(cells, 3),
                Details = Cell(cells, 4)
            });
        }
    }

    private void BuildFilters()
    {
        var actions = _events.Select(e => e.Action).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var entities = _events.Select(e => e.EntityType).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        ActionFilterBox.ItemsSource = new[] { "Todos" }.Concat(actions).ToList();
        EntityFilterBox.ItemsSource = new[] { "Todos" }.Concat(entities).ToList();
        SortByBox.ItemsSource = new[] { "Mais recente", "Mais antigo", "Ação (A-Z)", "Entidade (A-Z)", "Nome (A-Z)" };
        ActionFilterBox.SelectedIndex = 0;
        EntityFilterBox.SelectedIndex = 0;
        SortByBox.SelectedIndex = 0;
    }

    private void OnApplyFilter(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = (SearchBox.Text ?? "").Trim().ToLowerInvariant();
        var action = (ActionFilterBox.SelectedItem?.ToString() ?? "Todos").Trim();
        var entity = (EntityFilterBox.SelectedItem?.ToString() ?? "Todos").Trim();
        var sort = (SortByBox.SelectedItem?.ToString() ?? "Mais recente").Trim();

        var query = _events.Where(e =>
        {
            var matchesAction = action == "Todos" || string.Equals(e.Action, action, StringComparison.OrdinalIgnoreCase);
            var matchesEntity = entity == "Todos" || string.Equals(e.EntityType, entity, StringComparison.OrdinalIgnoreCase);
            var hay = $"{e.TimestampUtc} {e.Action} {e.EntityType} {e.EntityName} {e.Details}".ToLowerInvariant();
            var matchesTerm = string.IsNullOrWhiteSpace(term) || hay.Contains(term);
            return matchesAction && matchesEntity && matchesTerm;
        });

        query = sort switch
        {
            "Mais antigo" => query.OrderBy(e => e.TimestampUtc),
            "Ação (A-Z)" => query.OrderBy(e => e.Action).ThenBy(e => e.TimestampUtc),
            "Entidade (A-Z)" => query.OrderBy(e => e.EntityType).ThenBy(e => e.TimestampUtc),
            "Nome (A-Z)" => query.OrderBy(e => e.EntityName).ThenBy(e => e.TimestampUtc),
            _ => query.OrderByDescending(e => e.TimestampUtc)
        };

        _filtered = query.Take(2000).ToList();
        EventsListBox.ItemsSource = _filtered;
        Title = $"Auditoria ({_filtered.Count} eventos)";
    }

    private async void OnExportCsv(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar auditoria filtrada (CSV)",
            DefaultExtension = "csv",
            SuggestedFileName = $"auditoria_filtrada_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
            FileTypeChoices = new List<FilePickerFileType> { new("CSV") { Patterns = new[] { "*.csv" } } }
        });
        if (file is null) return;

        var sb = new StringBuilder();
        sb.AppendLine("TimestampUtc,Action,EntityType,EntityName,Details");
        foreach (var ev in _filtered)
        {
            sb.AppendLine($"{Csv(ev.TimestampUtc)},{Csv(ev.Action)},{Csv(ev.EntityType)},{Csv(ev.EntityName)},{Csv(ev.Details)}");
        }
        await File.WriteAllTextAsync(file.Path.LocalPath, sb.ToString());
    }

    private async void OnExportJson(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar auditoria filtrada (JSON)",
            DefaultExtension = "json",
            SuggestedFileName = $"auditoria_filtrada_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
            FileTypeChoices = new List<FilePickerFileType> { new("JSON") { Patterns = new[] { "*.json" } } }
        });
        if (file is null) return;

        var json = JsonSerializer.Serialize(_filtered, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(file.Path.LocalPath, json);
    }

    private static string Csv(string value)
    {
        var escaped = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string Cell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index] : "";

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }

    private sealed class AuditEventRow
    {
        public string TimestampUtc { get; set; } = "";
        public string Action { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string EntityName { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
