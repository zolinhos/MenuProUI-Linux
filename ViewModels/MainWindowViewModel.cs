using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MenuProUI.Models;
using MenuProUI.Services;

namespace MenuProUI.ViewModels;

/// <summary>
/// ViewModel principal da janela, responsável por gerenciar clientes, acessos e filtros.
/// Implementa MVVM com PropertyChanged notifications sincronizadas com a UI.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Repositório para persistência de dados em CSV</summary>
    private readonly CsvRepository _repo = new();

    /// <summary>Coleção de todos os clientes carregados</summary>
    public ObservableCollection<Client> Clients { get; } = new();
    
    /// <summary>Coleção de todos os acessos carregados</summary>
    public ObservableCollection<AccessEntry> Accesses { get; } = new();
    
    /// <summary>Coleção de clientes filtrados pela busca (exibidos na UI)</summary>
    public ObservableCollection<Client> ClientsFiltered { get; } = new();
    
    /// <summary>Coleção de acessos filtrados pela busca (exibidos na UI)</summary>
    public ObservableCollection<AccessEntry> AccessesFiltered { get; } = new();

    /// <summary>Array com todos os tipos de acesso disponíveis (SSH, RDP, URL)</summary>
    public AccessType[] Tipos { get; } = Enum.GetValues<AccessType>();

    /// <summary>Cliente atualmente selecionado na lista. Observável para mudanças na UI</summary>
    [ObservableProperty] private Client? _selectedClient;
    
    /// <summary>Acesso atualmente selecionado na lista. Observável para mudanças na UI</summary>
    [ObservableProperty] private AccessEntry? _selectedAccess;
    
    /// <summary>Texto de busca para filtrar clientes em tempo real</summary>
    [ObservableProperty] private string _clientsSearchText = "";
    
    /// <summary>Texto de busca para filtrar acessos em tempo real</summary>
    [ObservableProperty] private string _accessesSearchText = "";

    /// <summary>Caminho do arquivo CSV de clientes</summary>
    public string ClientsPath => AppPaths.ClientsPath;
    
    /// <summary>Caminho do arquivo CSV de acessos</summary>
    public string AccessesPath => AppPaths.AccessesPath;
    
    /// <summary>Quantidade total de clientes carregados</summary>
    public int ClientsCount => Clients.Count;
    
    /// <summary>Quantidade de clientes após aplicar filtro</summary>
    public int ClientsFilteredCount => ClientsFiltered.Count;
    
    /// <summary>Quantidade total de acessos carregados</summary>
    public int AccessesCount => Accesses.Count;
    
    /// <summary>Quantidade de acessos após aplicar filtro</summary>
    public int AccessesFilteredCount => AccessesFiltered.Count;

    /// <summary>
    /// Texto formatado mostrando contagem de acessos filtrados vs total.
    /// Exemplo: "5 de 10" ou "10" se sem filtro.
    /// Útil para saber se há filtro ativo.
    /// </summary>
    public string AccessesCountDisplay
    {
        get
        {
            var filtered = AccessesFilteredCount;
            var total = AccessesCount;
            
            // Se tem filtro ativo (menos filtrados que total), mostra "X de Y"
            if (filtered < total && !string.IsNullOrWhiteSpace(AccessesSearchText))
                return $"{filtered} de {total}";
            
            // Caso contrário, mostra apenas o total
            return total.ToString();
        }
    }

    /// <summary>Inicializa o ViewModel e carrega os dados</summary>
    public MainWindowViewModel()
    {
        Reload();
    }

    /// <summary>
    /// Recarrega todos os dados do armazenamento e aplica filtros.
    /// Limpa seleções anteriores e reinicializa tudo.
    /// </summary>
    public void Reload()
    {
        Clients.Clear();
        Accesses.Clear();
        ClientsFiltered.Clear();
        AccessesFiltered.Clear();

        // Carrega dados do repositório
        var (clients, accesses) = _repo.Load();

        // Popula coleção de clientes (ordenados por nome)
        foreach (var c in clients.OrderBy(c => c.Nome))
            Clients.Add(c);

        // Seleciona o primeiro cliente como padrão
        SelectedClient = Clients.FirstOrDefault();

        // Atualiza a lista de acessos e aplica filtros
        RefreshAccesses(accesses);
        ApplyClientFilter();
        ApplyAccessesFilter();
    }

    /// <summary>
    /// Atualiza a coleção de acessos com base no cliente selecionado.
    /// </summary>
    /// <param name="all">Lista de acessos para usar. Se null, recarrega do repositório</param>
    public void RefreshAccesses(System.Collections.Generic.List<AccessEntry>? all = null)
    {
        // Recarrega acessos se não fornecidos (para sincronizar com disco)
        var (_, loadedAccesses) = _repo.Load();
        var source = all ?? loadedAccesses;

        Accesses.Clear();

        // Se nenhum cliente selecionado, exibe todos os acessos
        if (SelectedClient is null)
        {
            foreach (var a in source.OrderBy(a => a.Tipo).ThenBy(a => a.Apelido))
                Accesses.Add(a);
            ApplyAccessesFilter();
            return;
        }

        // Filtra acessos do cliente selecionado
        foreach (var a in source.Where(a => a.ClientId == SelectedClient.Id)
                                .OrderBy(a => a.Tipo).ThenBy(a => a.Apelido))
            Accesses.Add(a);

        // Seleciona o primeiro acesso como padrão
        SelectedAccess = Accesses.FirstOrDefault();
        ApplyAccessesFilter();
    }

    /// <summary>
    /// Salva todas as mudanças no repositório de permanência.
    /// Mantém integridade: clientes da tela substituem disco, acessos fazem merge.
    /// </summary>
    public void SaveAll()
    {
        var (clients, accesses) = _repo.Load();

        // Substitui clientes pelo que está em memória (fonte de verdade)
        clients = Clients.ToList();
        
        // Acessos precisam fazer merge: mantém acessos de outros clientes
        // Remove apenas os do cliente atual e adiciona os editados
        var current = _repo.Load().accesses;

        // Remove acessos do cliente selecionado e adiciona os da tela
        if (SelectedClient is not null)
        {
            current.RemoveAll(a => a.ClientId == SelectedClient.Id);
            current.AddRange(Accesses);
        }

        _repo.SaveAll(clients, current);
    }

    /// <summary>
    /// Define o cliente selecionado e atualiza os acessos correspondentes.
    /// Limpa o filtro de busca de acessos para mostrar contagem correta.
    /// </summary>
    /// <param name="c">Cliente a ser selecionado (pode ser null)</param>
    public void SetSelectedClient(Client? c)
    {
        SelectedClient = c;
        // Limpa o filtro de busca de acessos ao trocar de cliente
        AccessesSearchText = "";
        RefreshAccesses();
    }

    /// <summary>
    /// Busca ou cria um cliente por nome.
    /// Útil para vincular acessos importados a clientes existentes ou novos.
    /// </summary>
    /// <param name="name">Nome do cliente a buscar/criar</param>
    /// <returns>Cliente encontrado ou recém-criado</returns>
    public Client EnsureClient(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "Sem Cliente";

        // Busca cliente existente (case-insensitive)
        var existing = Clients.FirstOrDefault(x => string.Equals(x.Nome, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        // Cria novo cliente se não existir
        var c = new Client { Nome = name };
        Clients.Add(c);
        return c;
    }

    /// <summary>
    /// Aplica filtro de busca na coleção de clientes.
    /// Busca por nome ou observações (case-insensitive, busca parcial).
    /// </summary>
    public void ApplyClientFilter()
    {
        ClientsFiltered.Clear();

        var search = (ClientsSearchText ?? "").Trim().ToLower();

        // Se busca vazia, exibe todos os clientes
        if (string.IsNullOrWhiteSpace(search))
        {
            foreach (var c in Clients)
                ClientsFiltered.Add(c);
        }
        else
        {
            // Filtra por nome ou observações
            foreach (var c in Clients.Where(c => 
                c.Nome.ToLower().Contains(search) ||
                (c.Observacoes ?? "").ToLower().Contains(search)))
                ClientsFiltered.Add(c);
        }
    }

    /// <summary>
    /// Aplica filtro de busca na coleção de acessos.
    /// Busca em múltiplos campos: apelido, host, usuário, URL, domínio.
    /// </summary>
    public void ApplyAccessesFilter()
    {
        AccessesFiltered.Clear();

        var search = (AccessesSearchText ?? "").Trim().ToLower();

        // Se busca vazia, exibe todos os acessos
        if (string.IsNullOrWhiteSpace(search))
        {
            foreach (var a in Accesses)
                AccessesFiltered.Add(a);
        }
        else
        {
            // Filtra por múltiplos campos
            foreach (var a in Accesses.Where(x =>
                x.Apelido.ToLower().Contains(search) ||
                (x.Host ?? "").ToLower().Contains(search) ||
                (x.Usuario ?? "").ToLower().Contains(search) ||
                (x.Url ?? "").ToLower().Contains(search) ||
                (x.Dominio ?? "").ToLower().Contains(search)))
                AccessesFiltered.Add(a);
        }
        
        // Notifica que a contagem mudou
        OnPropertyChanged(nameof(AccessesCountDisplay));
    }

    /// <summary>Evento disparado quando ClientsSearchText muda - aplica filtro automaticamente</summary>
    partial void OnClientsSearchTextChanged(string value)
    {
        ApplyClientFilter();
    }

    /// <summary>Evento disparado quando AccessesSearchText muda - aplica filtro automaticamente</summary>
    partial void OnAccessesSearchTextChanged(string value)
    {
        ApplyAccessesFilter();
        // Notifica que AccessesCountDisplay mudou
        OnPropertyChanged(nameof(AccessesCountDisplay));
    }
}
