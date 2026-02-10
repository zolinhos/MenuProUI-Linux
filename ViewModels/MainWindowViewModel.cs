using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MenuProUI.Models;
using MenuProUI.Services;

namespace MenuProUI.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly CsvRepository _repo = new();

    public ObservableCollection<Client> Clients { get; } = new();
    public ObservableCollection<AccessEntry> Accesses { get; } = new();

    public AccessType[] Tipos { get; } = Enum.GetValues<AccessType>();

    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private AccessEntry? _selectedAccess;

    public string ClientsPath => AppPaths.ClientsPath;
    public string AccessesPath => AppPaths.AccessesPath;

    public MainWindowViewModel()
    {
        Reload();
    }

    public void Reload()
    {
        Clients.Clear();
        Accesses.Clear();

        var (clients, accesses) = _repo.Load();

        foreach (var c in clients.OrderBy(c => c.Nome))
            Clients.Add(c);

        SelectedClient = Clients.FirstOrDefault();

        RefreshAccesses(accesses);
    }

    public void RefreshAccesses(System.Collections.Generic.List<AccessEntry>? all = null)
    {
        var (_, loadedAccesses) = _repo.Load();
        var source = all ?? loadedAccesses;

        Accesses.Clear();

        if (SelectedClient is null)
        {
            foreach (var a in source.OrderBy(a => a.Tipo).ThenBy(a => a.Apelido))
                Accesses.Add(a);
            return;
        }

        foreach (var a in source.Where(a => a.ClientId == SelectedClient.Id)
                                .OrderBy(a => a.Tipo).ThenBy(a => a.Apelido))
            Accesses.Add(a);

        SelectedAccess = Accesses.FirstOrDefault();
    }

    public void SaveAll()
    {
        var (clients, accesses) = _repo.Load();

        // substitui pelo que está em memória (fonte de verdade)
        clients = Clients.ToList();
        // acessos precisam incluir os de outros clientes também (para não apagar)
        // então recarrega e aplica patch dos itens editados da tela atual
        var current = _repo.Load().accesses;

        // remove os do cliente selecionado e adiciona os da tela
        if (SelectedClient is not null)
        {
            current.RemoveAll(a => a.ClientId == SelectedClient.Id);
            current.AddRange(Accesses);
        }

        _repo.SaveAll(clients, current);
    }

    public void SetSelectedClient(Client? c)
    {
        SelectedClient = c;
        RefreshAccesses();
    }

    public Client EnsureClient(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "Sem Cliente";

        var existing = Clients.FirstOrDefault(x => string.Equals(x.Nome, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var c = new Client { Nome = name };
        Clients.Add(c);
        return c;
    }
}
