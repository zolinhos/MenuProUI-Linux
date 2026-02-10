using System;
using Avalonia.Controls;
using MenuProUI.Models;

namespace MenuProUI.Dialogs;

public partial class AccessDialog : Window
{
    public AccessEntry Result { get; private set; }

    public AccessDialog(AccessEntry initial)
    {
        InitializeComponent();

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
            CriadoEm = initial.CriadoEm,
            AtualizadoEm = initial.AtualizadoEm
        };

        TypeBox.ItemsSource = Enum.GetValues<AccessType>();
        TypeBox.SelectedItem = Result.Tipo;

        AliasBox.Text = Result.Apelido;

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
        Result.Observacoes = NotesBox.Text;

        if (tipo == AccessType.URL)
        {
            var url = (UrlBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            Result.Url = url;

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

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
