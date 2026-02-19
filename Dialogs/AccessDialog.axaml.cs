using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using MenuProUI.Models;
using MenuProUI.Services;

namespace MenuProUI.Dialogs;

public partial class AccessDialog : Window
{
    private readonly List<Client> _availableClients;
    public AccessEntry Result { get; private set; }

    public AccessDialog() : this(new AccessEntry(), null)
    {
    }

    public AccessDialog(AccessEntry initial, IEnumerable<Client>? clients)
    {
        InitializeComponent();
        _availableClients = (clients ?? Enumerable.Empty<Client>()).ToList();

        Result = new AccessEntry
        {
            Id = initial.Id,
            ClientId = initial.ClientId,
            Tipo = initial.Tipo,
            Apelido = initial.Apelido,
            Host = initial.Host,
            Porta = initial.Porta,
            Usuario = initial.Usuario,
            Dominio = initial.Dominio,
            RdpIgnoreCert = initial.RdpIgnoreCert,
            RdpFullScreen = initial.RdpFullScreen,
            RdpDynamicResolution = initial.RdpDynamicResolution,
            RdpWidth = initial.RdpWidth,
            RdpHeight = initial.RdpHeight,
            Url = initial.Url,
            Observacoes = initial.Observacoes,
            Tags = initial.Tags,
            IsFavorite = initial.IsFavorite,
            OpenCount = initial.OpenCount,
            LastOpenedAt = initial.LastOpenedAt,
            CriadoEm = initial.CriadoEm,
            AtualizadoEm = initial.AtualizadoEm
        };

        ClientBox.ItemsSource = _availableClients;
        var selectedClient = _availableClients.FirstOrDefault(c => c.Id == Result.ClientId) ?? _availableClients.FirstOrDefault();
        ClientBox.SelectedItem = selectedClient;
        ClientBox.IsEnabled = _availableClients.Count > 0;

        TypeBox.ItemsSource = Enum.GetValues<AccessType>();
        TypeBox.SelectedItem = Result.Tipo;

        AliasBox.Text = Result.Apelido;
        TagsBox.Text = Result.Tags ?? "";

        HostBox.Text = Result.Host ?? "";
        PortBox.Text = Result.Porta?.ToString() ?? "";
        UserBox.Text = Result.Usuario ?? "";

        DomainBox.Text = Result.Dominio ?? "";
        IgnoreCertBox.IsChecked = Result.RdpIgnoreCert;
        FullScreenBox.IsChecked = Result.RdpFullScreen;
        DynamicResBox.IsChecked = Result.RdpDynamicResolution;

        WidthBox.Text = Result.RdpWidth?.ToString() ?? "";
        HeightBox.Text = Result.RdpHeight?.ToString() ?? "";

        UrlBox.Text = Result.Url ?? "";
        NotesBox.Text = Result.Observacoes ?? "";
        FavoriteBox.IsChecked = Result.IsFavorite;

        ApplyPanels();
    }

    private void OnTypeChanged(object? sender, SelectionChangedEventArgs e) => ApplyPanels();

    private void ApplyPanels()
    {
        var tipo = (AccessType)(TypeBox.SelectedItem ?? AccessType.URL);

        PanelUrl.IsVisible = tipo == AccessType.URL;
        PanelSshRdp.IsVisible = tipo is AccessType.SSH or AccessType.RDP;
        PanelRdp.IsVisible = tipo == AccessType.RDP;

        if (tipo == AccessType.SSH && string.IsNullOrWhiteSpace(PortBox.Text))
            PortBox.Text = "22";

        if (tipo == AccessType.RDP && string.IsNullOrWhiteSpace(PortBox.Text))
            PortBox.Text = "3389";
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tipo = (AccessType)(TypeBox.SelectedItem ?? AccessType.URL);

        var alias = (AliasBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(alias)) alias = "Acesso";

        Result.Tipo = tipo;
        Result.Apelido = alias;
        Result.Tags = (TagsBox.Text ?? "").Trim();
        Result.Observacoes = NotesBox.Text;
        Result.IsFavorite = FavoriteBox.IsChecked == true;
        if (ClientBox.SelectedItem is Client selectedClient)
            Result.ClientId = selectedClient.Id;

        if (tipo == AccessType.URL)
        {
            var url = (UrlBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            Result.Url = NormalizeUrl(url);

            Result.Host = null;
            Result.Usuario = null;
            Result.Porta = null;

            Result.Dominio = null;
            Result.RdpIgnoreCert = true;
            Result.RdpFullScreen = false;
            Result.RdpDynamicResolution = true;
            Result.RdpWidth = null;
            Result.RdpHeight = null;
        }
        else
        {
            var host = (HostBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(host)) return;

            var user = (UserBox.Text ?? "").Trim();
            var portText = (PortBox.Text ?? "").Trim();

            int? port = null;
            if (int.TryParse(portText, out var p) && p > 0 && p <= 65535) port = p;

            Result.Host = host;
            Result.Usuario = string.IsNullOrWhiteSpace(user) ? null : user;
            Result.Porta = port;

            Result.Url = null;

            if (tipo == AccessType.RDP)
            {
                var dom = (DomainBox.Text ?? "").Trim();
                Result.Dominio = string.IsNullOrWhiteSpace(dom) ? null : dom;

                Result.RdpIgnoreCert = IgnoreCertBox.IsChecked == true;
                Result.RdpFullScreen = FullScreenBox.IsChecked == true;
                Result.RdpDynamicResolution = DynamicResBox.IsChecked == true;

                Result.RdpWidth = ParseInt(WidthBox.Text);
                Result.RdpHeight = ParseInt(HeightBox.Text);
            }
            else
            {
                Result.Dominio = null;
                Result.RdpIgnoreCert = true;
                Result.RdpFullScreen = false;
                Result.RdpDynamicResolution = true;
                Result.RdpWidth = null;
                Result.RdpHeight = null;
            }
        }

        Result.AtualizadoEm = DateTime.UtcNow;
        Close(true);
    }

    private static int? ParseInt(string? s)
    {
        s = (s ?? "").Trim();
        if (int.TryParse(s, out var v) && v > 0) return v;
        return null;
    }

    private static string NormalizeUrl(string input)
    {
        var value = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value)) return "https://";

        if (!value.Contains("://", StringComparison.Ordinal))
            value = "https://" + value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return value;

        var builder = new UriBuilder(uri)
        {
            Scheme = string.IsNullOrWhiteSpace(uri.Scheme) ? "https" : uri.Scheme,
            Port = uri.IsDefaultPort ? 443 : uri.Port,
            Path = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath
        };

        return builder.Uri.ToString();
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private async void OnTestUrl(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var raw = (UrlBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            UrlTestResultText.Text = "Informe uma URL para testar.";
            return;
        }

        var normalized = NormalizeUrl(raw);
        UrlBox.Text = normalized;
        UrlTestResultText.Text = "Testando...";
        TestUrlButton.IsEnabled = false;

        try
        {
            var probeEntry = new AccessEntry
            {
                Tipo = AccessType.URL,
                Url = normalized
            };

            var fallbackPorts = ParseFallbackPorts("443,80,8443,8080,9443");
            var result = await ConnectivityChecker.CheckAccessDetailedAsync(
                probeEntry,
                TimeSpan.FromSeconds(3),
                fallbackPorts);

            UrlTestResultText.Text = result.IsOnline
                ? $"Online ({result.Method} porta {result.EffectivePort})"
                : $"Offline ({result.ErrorDetail})";
        }
        finally
        {
            TestUrlButton.IsEnabled = true;
        }
    }

    private static int[] ParseFallbackPorts(string csv)
    {
        return (csv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var p) ? p : 0)
            .Where(p => p is >= 1 and <= 65535)
            .Distinct()
            .ToArray();
    }
}
