using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MenuProUI.Dialogs;
using MenuProUI.Models;
using MenuProUI.Services;
using MenuProUI.ViewModels;

namespace MenuProUI.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void OnClientSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        VM.SetSelectedClient(VM.SelectedClient);
    }

    // ---------- CLIENTES ----------
    private async void OnNewClient(object? sender, RoutedEventArgs e)
    {
        var c = new Client { Nome = "Novo Cliente" };
        var dlg = new ClientDialog(c);

        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var created = dlg.Result;
        created.Id = Guid.NewGuid();
        created.CriadoEm = DateTime.UtcNow;
        created.AtualizadoEm = DateTime.UtcNow;

        VM.Clients.Add(created);
        VM.SaveAll();
        VM.SelectedClient = created;
        VM.RefreshAccesses();
    }

    private async void OnEditClient(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedClient is null) return;

        var dlg = new ClientDialog(VM.SelectedClient);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;

        var sameNameOther = VM.Clients.Any(x =>
            x.Id != edited.Id &&
            string.Equals(x.Nome, edited.Nome, StringComparison.OrdinalIgnoreCase));

        if (sameNameOther)
        {
            await new ConfirmDialog("Já existe um cliente com esse nome. Use um nome único.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        VM.SelectedClient.Nome = edited.Nome;
        VM.SelectedClient.Observacoes = edited.Observacoes;
        VM.SelectedClient.AtualizadoEm = DateTime.UtcNow;

        VM.SaveAll();
        VM.Reload();
    }

    private async void OnDeleteClient(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedClient is null) return;

        var client = VM.SelectedClient;

        var confirm = new ConfirmDialog(
            $"Excluir o cliente '{client.Nome}'?\n\nIsso também removerá TODOS os acessos desse cliente.",
            "Excluir Cliente");

        var ok = await confirm.ShowDialog<bool>(this);
        if (!ok) return;

        VM.Clients.Remove(client);
        VM.Accesses.Clear(); // remove acessos do cliente atual

        VM.SaveAll();
        VM.Reload();
    }

    // ---------- ACESSOS ----------
    private async void OnNewAccess(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedClient is null)
        {
            await new ConfirmDialog("Selecione um cliente antes de criar um acesso.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        var a = new AccessEntry
        {
            ClientId = VM.SelectedClient.Id,
            Tipo = AccessType.URL,
            Apelido = "Novo Acesso",
            Url = "https://",
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        var dlg = new AccessDialog(a);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var created = dlg.Result;
        created.Id = Guid.NewGuid();
        created.ClientId = VM.SelectedClient.Id;
        created.CriadoEm = DateTime.UtcNow;
        created.AtualizadoEm = DateTime.UtcNow;

        VM.Accesses.Add(created);
        VM.SaveAll();
        VM.RefreshAccesses();
        VM.SelectedAccess = created;
    }

    private async void OnEditAccess(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedAccess is null) return;

        var dlg = new AccessDialog(VM.SelectedAccess);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;

        VM.SelectedAccess.Tipo = edited.Tipo;
        VM.SelectedAccess.Apelido = edited.Apelido;
        VM.SelectedAccess.Host = edited.Host;
        VM.SelectedAccess.Porta = edited.Porta;
        VM.SelectedAccess.Usuario = edited.Usuario;
        VM.SelectedAccess.Dominio = edited.Dominio;
        VM.SelectedAccess.Url = edited.Url;
        VM.SelectedAccess.Observacoes = edited.Observacoes;
        VM.SelectedAccess.AtualizadoEm = DateTime.UtcNow;

        VM.SaveAll();
        VM.RefreshAccesses();
    }

    private async void OnDeleteAccess(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedAccess is null) return;

        var a = VM.SelectedAccess;
        var ok = await new ConfirmDialog($"Excluir o acesso '{a.Apelido}'?", "Excluir Acesso")
            .ShowDialog<bool>(this);

        if (!ok) return;

        VM.Accesses.Remove(a);
        VM.SaveAll();
        VM.RefreshAccesses();
    }

    private void OnOpenAccess(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedAccess is null) return;

        try
        {
            AccessLauncher.Open(VM.SelectedAccess);
        }
        catch (Exception ex)
        {
            _ = new ConfirmDialog($"Falha ao abrir:\n{ex.Message}", "Erro").ShowDialog<bool>(this);
        }
    }
}
