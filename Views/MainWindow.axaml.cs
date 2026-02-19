using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MenuProUI.Dialogs;
using MenuProUI.Models;
using MenuProUI.Services;
using MenuProUI.ViewModels;

namespace MenuProUI.Views;

/// <summary>
/// Janela principal da aplicação MenuProUI.
/// Gerencia a interface de usuário, eventos e fluxo de interação com clientes e acessos.
/// Implementa padrão MVVM com ViewModel binding automático.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Atalho para acessar o ViewModel (DataContext da janela)</summary>
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;
    private readonly CsvRepository _repo = new();
    private readonly EventLogger _eventLogger = new();
    private readonly SettingsRepository _settingsRepo = new();
    private readonly Dictionary<Guid, ConnectivityState> _connectivityStatusByAccessId = new();
    private readonly Dictionary<Guid, ConnectivityState> _connectivityStatusByClientId = new();
    private AppSettings _currentSettings = new();
    private CancellationTokenSource? _autoCheckCts;

    /// <summary>
    /// Inicializa a janela principal.
    /// Configura o ViewModel como DataContext e conecta handlers de eventos.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        _currentSettings = _settingsRepo.Load();

        // Conecta botões de menu aos handlers de toggle
        var clientsMenuBtn = this.FindControl<Button>("ClientsMenuBtn");
        if (clientsMenuBtn != null)
            clientsMenuBtn.Click += (s, e) => ToggleMenu("ClientsMenu");

        var accessesMenuBtn = this.FindControl<Button>("AccessesMenuBtn");
        if (accessesMenuBtn != null)
            accessesMenuBtn.Click += (s, e) => ToggleMenu("AccessesMenu");

        // Configura handler para tecla F1 (Help)
        this.KeyDown += MainWindow_KeyDown;
        UpdateAuditIntegrityStatus();
        UpdateAppVersionText();
        UpdateConnectivityStatusText("Aguardando checagem");
    }

    private AppSettings Settings => _currentSettings;

    /// <summary>Handler para teclas pressionadas - detecta atalhos de teclado</summary>
    private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // Modifiers: Ctrl, Alt, Shift, Meta
        var hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        try
        {
            // F1 - Ajuda
            if (e.Key == Key.F1)
            {
                e.Handled = true;
                await ShowHelp();
                return;
            }

            // Escape - Fechar menus
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseMenus();
                return;
            }

            // Setas (sem modificadores): navegação confiável na lista de clientes.
            if (!hasCtrl && !hasShift && (e.Key == Key.Up || e.Key == Key.Down))
            {
                var focusedControl = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (focusedControl is not TextBox)
                {
                    e.Handled = true;
                    MoveClientSelection(e.Key == Key.Down ? 1 : -1);
                    return;
                }
            }

            // Ctrl+Q - Sair (Close)
            if (hasCtrl && e.Key == Key.Q)
            {
                e.Handled = true;
                this.Close();
                return;
            }

            // Ctrl+R - Recarregar
            if (hasCtrl && e.Key == Key.R)
            {
                e.Handled = true;
                OnReload(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+F - Focar Busca Clientes
            if (hasCtrl && !hasShift && e.Key == Key.F)
            {
                e.Handled = true;
                var clientsSearchBox = this.FindControl<TextBox>("ClientsSearchBox");
                if (clientsSearchBox != null)
                {
                    clientsSearchBox.Focus();
                    clientsSearchBox.SelectAll();
                }
                return;
            }

            // Ctrl+Shift+F - Focar Busca Acessos
            if (hasCtrl && hasShift && e.Key == Key.F)
            {
                e.Handled = true;
                var accessesSearchBox = this.FindControl<TextBox>("AccessesSearchBox");
                if (accessesSearchBox != null)
                {
                    accessesSearchBox.Focus();
                    accessesSearchBox.SelectAll();
                }
                return;
            }

            // Ctrl+L - Limpar Buscas
            if (hasCtrl && e.Key == Key.L)
            {
                e.Handled = true;
                VM.ClientsSearchText = "";
                VM.AccessesSearchText = "";
                return;
            }

            // Ctrl+N - Novo Cliente
            if (hasCtrl && !hasShift && e.Key == Key.N)
            {
                e.Handled = true;
                OnNewClient(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+N - Novo Acesso
            if (hasCtrl && hasShift && e.Key == Key.N)
            {
                e.Handled = true;
                OnNewAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+K - Checar conectividade
            if (hasCtrl && hasShift && e.Key == Key.K)
            {
                e.Handled = true;
                await OpenConnectivityScopeChooser();
                return;
            }

            // Ctrl+Shift+B - Exportar CSVs
            if (hasCtrl && hasShift && e.Key == Key.B)
            {
                e.Handled = true;
                await OnExportData();
                return;
            }

            // Ctrl+Shift+I - Importar CSVs (de ~/.config/MenuProUI/imports)
            if (hasCtrl && hasShift && e.Key == Key.I)
            {
                e.Handled = true;
                await OnImportData();
                return;
            }

            // Ctrl+Shift+J - Ver auditoria
            if (hasCtrl && hasShift && e.Key == Key.J)
            {
                e.Handled = true;
                await OnShowAuditLog();
                return;
            }

            if (hasCtrl && hasShift && e.Key == Key.S)
            {
                e.Handled = true;
                await OnOpenSettings();
                return;
            }

            // Ctrl+Shift+D - Clonar acesso
            if (hasCtrl && hasShift && e.Key == Key.D)
            {
                e.Handled = true;
                OnCloneAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+E - Editar Cliente
            if (hasCtrl && !hasShift && e.Key == Key.E)
            {
                e.Handled = true;
                OnEditClient(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+E - Editar Acesso
            if (hasCtrl && hasShift && e.Key == Key.E)
            {
                e.Handled = true;
                OnEditAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Delete - Excluir Cliente
            if (hasCtrl && e.Key == Key.Delete)
            {
                e.Handled = true;
                OnDeleteClient(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+Delete - Excluir Acesso
            if (hasCtrl && hasShift && e.Key == Key.Delete)
            {
                e.Handled = true;
                OnDeleteAccess(null, new RoutedEventArgs());
                return;
            }

            // Enter - Abrir Acesso
            if (e.Key == Key.Return)
            {
                // Verifica se está em um TextBox (não quer executar em campos de texto)
                var focusedControl = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (focusedControl is TextBox)
                    return;

                e.Handled = true;
                OnOpenAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+. - Favoritar/desfavoritar acesso
            if (hasCtrl && e.Key == Key.OemPeriod)
            {
                e.Handled = true;
                ToggleFavoriteSelectedAccess();
                return;
            }
        }
        catch
        {
            // Falha silenciosa em caso de erro no atalho
        }
    }

    /// <summary>
    /// Exibe o diálogo de ajuda/help com instruções sobre a aplicação.
    /// Acessível via tecla F1 ou menu Help.
    /// </summary>
    private async Task ShowHelp()
    {
        var helpText = @"MENU PRO UI - Ajuda Completa (F1)
════════════════════════════════════════════

FUNCIONALIDADES PRINCIPAIS:

👥 CLIENTES
  • Novo: Cria um novo cliente (organização/projeto)
  • Editar: Modifica nome e observações do cliente
  • Excluir: Remove cliente e todos seus acessos
  • Buscar: Filtra por nome ou observações em tempo real

🔓 ACESSOS
  • Novo: Cria acesso (SSH, RDP ou URL) para cliente
  • Editar: Modifica configurações do acesso
  • Excluir: Remove o acesso
  • Abrir: Abre/conecta ao acesso
  • Buscar: Filtra por apelido, host, usuário ou URL

⌨️ ATALHOS DE TECLADO:

Navegação Geral:
  F1                    Abre esta ajuda
  Escape                Fecha menus abertos
  Ctrl+R                Recarrega dados do disco
  Ctrl+Q                Sair da aplicação

Clientes:
  Ctrl+N                Novo cliente
  Ctrl+E                Editar cliente selecionado
  Ctrl+Delete           Excluir cliente selecionado
  Ctrl+F                Focar campo de busca de clientes

Acessos:
  Ctrl+Shift+N          Novo acesso
    Ctrl+Shift+D          Clonar acesso selecionado
  Ctrl+Shift+E          Editar acesso selecionado
  Ctrl+Shift+Delete     Excluir acesso selecionado
    Ctrl+Shift+K          Checar conectividade (cliente/todos)
  Enter                 Abre/conecta ao acesso selecionado
  Ctrl+Shift+F          Focar campo de busca de acessos

Busca:
  Ctrl+L                Limpa todos os campos de busca
  (Digite para filtrar em tempo real)

📁 ARMAZENAMENTO:
  Linux:   ~/.config/MenuProUI/
  Windows: %APPDATA%\MenuProUI\
  
  Arquivos:
  • clientes.csv - Lista de clientes
  • acessos.csv - Lista de acessos

🔧 TIPOS DE ACESSO:
  • SSH: Conexão segura para Linux/Unix (porta 22)
  • RDP: Área de trabalho remota Windows (porta 3389)
  • URL: Abrir página web no navegador padrão

💡 DICAS ÚTEIS:
  • Use Ctrl+F para encontrar rapidamente um cliente
  • Use Ctrl+Shift+F para procurar um acesso específico
  • Duplo-clique em um acesso também o abre
  • Acessos sem cliente são agrupados em 'Sem Cliente'
  • Dados são salvos automaticamente nas mudanças
  • Faça backup dos arquivos CSV manualmente se necesário

📋 CAMPOS POR TIPO DE ACESSO:

SSH: Host, Porta (padrão 22), Usuário
RDP: Host, Porta (padrão 3389), Usuário, Domínio
     Opções: Tela Cheia, Resolução Dinâmica, Ignorar Certificado
URL: Link completo (https://...)
Todos: Apelido, Observações

════════════════════════════════════════════

📚 DÚVIDAS OU SUGESTÕES?
GitHub: https://github.com/zolinhos/MenuProUI-Linux
Issues: https://github.com/zolinhos/MenuProUI-Linux/issues
Discussions: https://github.com/zolinhos/MenuProUI-Linux/discussions

Versão 1.8.0 - MenuProUI";

        var dlg = new HelpDialog(helpText);
        await dlg.ShowDialog<bool>(this);
        _eventLogger.Log("help_opened", "ui", "help_dialog", "Ajuda exibida");
    }

    /// <summary>
    /// Alterna visibilidade de um menu popup.
    /// Fecha outros menus se necessário (para manter apenas um aberto).
    /// </summary>
    /// <param name="popupName">Nome do popup a alternar (ClientsMenu ou AccessesMenu)</param>
    private void ToggleMenu(string popupName)
    {
        var popup = this.FindControl<Popup>(popupName);
        if (popup != null)
            popup.IsOpen = !popup.IsOpen;
    }

    /// <summary>Fecha todos os menus popup abertos</summary>
    private void CloseMenus()
    {
        var clientsMenu = this.FindControl<Popup>("ClientsMenu");
        var accessesMenu = this.FindControl<Popup>("AccessesMenu");
        if (clientsMenu != null) clientsMenu.IsOpen = false;
        if (accessesMenu != null) accessesMenu.IsOpen = false;
    }

    /// <summary>
    /// Handler para mudança de seleção na lista de clientes.
    /// Atualiza acessos exibidos quando um cliente é selecionado.
    /// </summary>
    private void OnClientSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        VM.SetSelectedClient(VM.SelectedClient);
        // Evita mutar coleções vinculadas durante o evento de seleção do ListBox.
        Dispatcher.UIThread.Post(() => ApplyConnectivityStatusesToCurrentAccesses(refreshClientList: false), DispatcherPriority.Background);
    }

    private async void OnAccessSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _autoCheckCts?.Cancel();
        _autoCheckCts?.Dispose();
        _autoCheckCts = null;

        if (!Settings.ConnectivityAutoCheckOnSelect) return;
        if (VM.SelectedAccess is null) return;
        if (VM.SelectedAccess.ConnectivityState == ConnectivityState.Checking) return;

        var cts = new CancellationTokenSource();
        _autoCheckCts = cts;
        try
        {
            var delay = Math.Clamp(Settings.ConnectivityAutoCheckDebounceMs, 0, 10000);
            if (delay > 0) await Task.Delay(delay, cts.Token);
            if (cts.IsCancellationRequested) return;

            var entry = VM.SelectedAccess;
            entry.ConnectivityState = ConnectivityState.Checking;
            _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Checking;
            VM.ApplyAccessesFilter();

            var timeout = TimeSpan.FromSeconds(Settings.ConnectivityTimeoutSeconds);
            var result = await ConnectivityChecker.CheckAccessDetailedAsync(entry, timeout, ParseFallbackPorts(Settings.ConnectivityUrlFallbackPortsCsv));
            entry.ConnectivityState = result.IsOnline ? ConnectivityState.Online : ConnectivityState.Offline;
            _connectivityStatusByAccessId[entry.Id] = entry.ConnectivityState;
            VM.ApplyAccessesFilter();
        }
        catch (OperationCanceledException)
        {
            // Seleção mudou durante debounce/checagem.
        }
    }

    /// <summary>
    /// Handler para botão Recarregar.
    /// Recarrega todos os dados do disco e reaplica filtros.
    /// </summary>
    private void OnReload(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        VM.Reload();
        ApplyConnectivityStatusesToCurrentAccesses();
        _currentSettings = _settingsRepo.Load();
        UpdateAuditIntegrityStatus();
    }

    private async void OnCheckConnectivity(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        await OpenConnectivityScopeChooser();
    }

    private async Task OpenConnectivityScopeChooser()
    {
        var dlg = new ConnectivityScopeDialog();
        var scope = await dlg.ShowDialog<ConnectivityScope>(this);
        if (scope == ConnectivityScope.Cancel) return;

        if (scope == ConnectivityScope.AllClients)
        {
            await CheckAllClientsConnectivity();
        }
        else
        {
            await CheckSelectedClientConnectivity();
        }
    }

    private async Task CheckSelectedClientConnectivity()
    {
        if (VM.SelectedClient is null)
        {
            await new ConfirmDialog("Selecione um cliente para checar conectividade.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        var rows = VM.Accesses.ToList();
        if (rows.Count == 0)
        {
            await new ConfirmDialog("Este cliente não possui acessos para checar.", "Conectividade")
                .ShowDialog<bool>(this);
            return;
        }

        var online = 0;
        var offline = 0;
        UpdateConnectivityStatusText($"Checando cliente '{VM.SelectedClient.Nome}'...");

        foreach (var entry in rows)
        {
            entry.ConnectivityState = ConnectivityState.Checking;
            _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Checking;
        }
        VM.ApplyAccessesFilter();

        foreach (var entry in rows)
        {
            var timeout = TimeSpan.FromSeconds(Settings.ConnectivityTimeoutSeconds);
            var detailed = await ConnectivityChecker.CheckAccessDetailedAsync(entry, timeout, ParseFallbackPorts(Settings.ConnectivityUrlFallbackPortsCsv));
            if (detailed.IsOnline)
            {
                online++;
                entry.ConnectivityState = ConnectivityState.Online;
                _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Online;
            }
            else
            {
                offline++;
                entry.ConnectivityState = ConnectivityState.Offline;
                _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Offline;
            }
        }

        VM.ApplyAccessesFilter();
        UpdateConnectivityStatusText($"Escopo cliente: total {rows.Count} | online {online} | offline {offline}");
        _eventLogger.Log("check_connectivity", "access", VM.SelectedClient.Nome, $"Scope=selected_client; Total={rows.Count}; Online={online}; Offline={offline}");
    }

    private async Task CheckAllClientsConnectivity()
    {
        var repo = new CsvRepository();
        var (_, allAccesses) = repo.Load();
        if (allAccesses.Count == 0)
        {
            await new ConfirmDialog("Não há acessos cadastrados para checar conectividade.", "Conectividade")
                .ShowDialog<bool>(this);
            return;
        }

        foreach (var entry in allAccesses)
            _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Checking;

        ApplyConnectivityStatusesToCurrentAccesses();
        UpdateConnectivityStatusText("Checando todos os clientes...");

        var online = 0;
        var offline = 0;

        var timeout = TimeSpan.FromSeconds(Settings.ConnectivityTimeoutSeconds);
        var maxConcurrency = Math.Max(1, Settings.ConnectivityMaxConcurrency);
        var fallbackPorts = ParseFallbackPorts(Settings.ConnectivityUrlFallbackPortsCsv);
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = allAccesses.Select(async entry =>
        {
            await semaphore.WaitAsync();
            try
            {
                var detailed = await ConnectivityChecker.CheckAccessDetailedAsync(entry, timeout, fallbackPorts);
                lock (_connectivityStatusByAccessId)
                {
                    if (detailed.IsOnline)
                    {
                        online++;
                        _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Online;
                    }
                    else
                    {
                        offline++;
                        _connectivityStatusByAccessId[entry.Id] = ConnectivityState.Offline;
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        ApplyConnectivityStatusesToCurrentAccesses();
        UpdateConnectivityStatusText($"Escopo todos: total {allAccesses.Count} | online {online} | offline {offline}");
        _eventLogger.Log("check_connectivity", "access", "all_clients", $"Total={allAccesses.Count}; Online={online}; Offline={offline}");
    }

    private (string host, int port) ResolveProbeTarget(AccessEntry entry)
    {
        if (entry.Tipo == AccessType.URL)
            return ResolveUrlHostPort(entry.Url);

        var host = entry.Host ?? "";
        var port = entry.Tipo switch
        {
            AccessType.SSH => (entry.Porta is > 0 and <= 65535) ? entry.Porta!.Value : 22,
            AccessType.RDP => (entry.Porta is > 0 and <= 65535) ? entry.Porta!.Value : 3389,
            _ => 443
        };

        return (host, port);
    }

    private static (string host, int port) ResolveUrlHostPort(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return ("", 443);

        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return (parsed.Host, parsed.Port > 0 ? parsed.Port : 443);

        if (Uri.TryCreate("https://" + url, UriKind.Absolute, out parsed))
            return (parsed.Host, parsed.Port > 0 ? parsed.Port : 443);

        return ("", 443);
    }

    private static int[] ParseFallbackPorts(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new[] { 443, 80, 8443, 8080, 9443 };
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var p) ? p : -1)
            .Where(p => p is >= 1 and <= 65535)
            .Distinct()
            .DefaultIfEmpty(443)
            .ToArray();
    }

    private void ApplyConnectivityStatusesToCurrentAccesses(bool refreshClientList = true)
    {
        foreach (var entry in VM.Accesses)
        {
            entry.ConnectivityState = _connectivityStatusByAccessId.TryGetValue(entry.Id, out var status)
                ? status
                : ConnectivityState.Unknown;
        }

        VM.ApplyAccessesFilter();
        ApplyClientConnectivityStatuses(refreshClientList);
    }

    private void ApplyClientConnectivityStatuses(bool refreshClientList = true)
    {
        var repo = new CsvRepository();
        var (_, allAccesses) = repo.Load();
        var accessesByClient = allAccesses
            .GroupBy(a => a.ClientId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var client in VM.Clients)
        {
            var aggregated = ResolveClientState(client.Id, accessesByClient);
            client.ConnectivityState = aggregated;
            _connectivityStatusByClientId[client.Id] = aggregated;
        }

        if (refreshClientList)
            VM.ApplyClientFilter();
    }

    private void MoveClientSelection(int delta)
    {
        if (VM.ClientsFiltered.Count == 0) return;

        var currentIndex = VM.SelectedClient is null
            ? -1
            : VM.ClientsFiltered.IndexOf(VM.SelectedClient);

        var nextIndex = currentIndex + delta;
        if (nextIndex < 0) nextIndex = 0;
        if (nextIndex >= VM.ClientsFiltered.Count) nextIndex = VM.ClientsFiltered.Count - 1;

        VM.SelectedClient = VM.ClientsFiltered[nextIndex];

        var clientsList = this.FindControl<ListBox>("ClientsList");
        clientsList?.Focus();
    }

    private ConnectivityState ResolveClientState(Guid clientId, Dictionary<Guid, List<AccessEntry>> accessesByClient)
    {
        if (!accessesByClient.TryGetValue(clientId, out var accesses) || accesses.Count == 0)
            return ConnectivityState.Unknown;

        var statuses = accesses
            .Select(a => _connectivityStatusByAccessId.TryGetValue(a.Id, out var status)
                ? status
                : ConnectivityState.Unknown)
            .ToList();

        if (statuses.Any(s => s == ConnectivityState.Checking))
            return ConnectivityState.Checking;

        if (statuses.Any(s => s == ConnectivityState.Offline))
            return ConnectivityState.Offline;

        if (statuses.Any(s => s == ConnectivityState.Online))
            return ConnectivityState.Online;

        return ConnectivityState.Unknown;
    }

    // ============== HANDLERS DE CLIENTES ==============

    /// <summary>
    /// Handler para criar novo cliente.
    /// Exibe diálogo para entrada de nome e observações.
    /// </summary>
    private async void OnNewClient(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
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
        _eventLogger.Log("create", "client", created.Nome, "Cliente criado");
    }

    /// <summary>
    /// Handler para editar cliente selecionado.
    /// Valida unicidade de nome antes de salvar.
    /// </summary>
    private async void OnEditClient(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedClient is null) return;

        var dlg = new ClientDialog(VM.SelectedClient);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;

        // Valida se outro cliente já tem esse nome
        var sameNameOther = VM.Clients.Any(x =>
            x.Id != edited.Id &&
            string.Equals(x.Nome, edited.Nome, StringComparison.OrdinalIgnoreCase));

        if (sameNameOther)
        {
            await new ConfirmDialog("Já existe um cliente com esse nome. Use um nome único.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        // Atualiza dados do cliente selecionado
        VM.SelectedClient.Nome = edited.Nome;
        VM.SelectedClient.Observacoes = edited.Observacoes;
        VM.SelectedClient.AtualizadoEm = DateTime.UtcNow;

        VM.SaveAll();
        VM.Reload();
        _eventLogger.Log("edit", "client", edited.Nome, "Cliente editado");
    }

    /// <summary>
    /// Handler para excluir cliente selecionado.
    /// Exibe confirmação pois remove também todos os acessos do cliente.
    /// </summary>
    private async void OnDeleteClient(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedClient is null) return;

        var client = VM.SelectedClient;

        // Pede confirmação (operação pode perder dados)
        var confirm = new ConfirmDialog(
            $"Excluir o cliente '{client.Nome}'?\n\nIsso também removerá TODOS os acessos desse cliente.",
            "Excluir Cliente");

        var ok = await confirm.ShowDialog<bool>(this);
        if (!ok) return;

        VM.Clients.Remove(client);
        VM.Accesses.Clear();

        VM.SaveAll();
        VM.Reload();
        _eventLogger.Log("delete", "client", client.Nome, "Cliente removido em cascata");
    }

    // ============== HANDLERS DE ACESSOS ==============

    /// <summary>
    /// Handler para criar novo acesso.
    /// Requer cliente selecionado.
    /// </summary>
    private async void OnNewAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedClient is null)
        {
            await new ConfirmDialog("Selecione um cliente antes de criar um acesso.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        // Cria acesso padrão (URL vazio por padrão)
        var a = new AccessEntry
        {
            ClientId = VM.SelectedClient.Id,
            Tipo = AccessType.URL,
            Apelido = "Novo Acesso",
            Url = "https://",
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        var dlg = new AccessDialog(a, VM.Clients);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var created = dlg.Result;
        created.Id = Guid.NewGuid();
        created.CriadoEm = DateTime.UtcNow;
        created.AtualizadoEm = DateTime.UtcNow;
        var createdId = created.Id;
        var targetClientId = created.ClientId;

        VM.Accesses.Add(created);
        VM.SaveAll();
        SelectClientAndRefresh(targetClientId);
        VM.SelectedAccess = VM.Accesses.FirstOrDefault(a => a.Id == createdId) ?? VM.SelectedAccess;
        _eventLogger.Log("create", "access", created.Apelido, $"Tipo={created.Tipo}");
    }

    /// <summary>
    /// Handler para editar acesso selecionado.
    /// Permite modificar todos os campos de configuração.
    /// </summary>
    private async void OnEditAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var dlg = new AccessDialog(VM.SelectedAccess, VM.Clients);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;
        var editedId = VM.SelectedAccess.Id;
        var targetClientId = edited.ClientId;

        // Atualiza todos os campos do acesso
        VM.SelectedAccess.Tipo = edited.Tipo;
        VM.SelectedAccess.ClientId = edited.ClientId;
        VM.SelectedAccess.Apelido = edited.Apelido;
        VM.SelectedAccess.Host = edited.Host;
        VM.SelectedAccess.Porta = edited.Porta;
        VM.SelectedAccess.Usuario = edited.Usuario;
        VM.SelectedAccess.Dominio = edited.Dominio;
        VM.SelectedAccess.Url = edited.Url;
        VM.SelectedAccess.Tags = edited.Tags;
        VM.SelectedAccess.IsFavorite = edited.IsFavorite;
        VM.SelectedAccess.OpenCount = edited.OpenCount;
        VM.SelectedAccess.LastOpenedAt = edited.LastOpenedAt;
        VM.SelectedAccess.Observacoes = edited.Observacoes;
        VM.SelectedAccess.AtualizadoEm = DateTime.UtcNow;

        VM.SaveAll();
        SelectClientAndRefresh(targetClientId);
        VM.SelectedAccess = VM.Accesses.FirstOrDefault(a => a.Id == editedId) ?? VM.SelectedAccess;
        _eventLogger.Log("edit", "access", edited.Apelido, $"Tipo={edited.Tipo}");
    }

    private async void OnCloneAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var source = VM.SelectedAccess;
        var clone = new AccessEntry
        {
            Id = Guid.NewGuid(),
            ClientId = source.ClientId,
            Tipo = source.Tipo,
            Apelido = BuildCloneAlias(source.Apelido),
            Host = source.Host,
            Porta = source.Porta,
            Usuario = source.Usuario,
            Dominio = source.Dominio,
            RdpIgnoreCert = source.RdpIgnoreCert,
            RdpFullScreen = source.RdpFullScreen,
            RdpDynamicResolution = source.RdpDynamicResolution,
            RdpWidth = source.RdpWidth,
            RdpHeight = source.RdpHeight,
            Url = source.Url,
            Tags = source.Tags,
            IsFavorite = source.IsFavorite,
            OpenCount = 0,
            LastOpenedAt = null,
            Observacoes = source.Observacoes,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        var dlg = new AccessDialog(clone, VM.Clients);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var created = dlg.Result;
        created.Id = Guid.NewGuid();
        created.CriadoEm = DateTime.UtcNow;
        created.AtualizadoEm = DateTime.UtcNow;
        var createdId = created.Id;
        var targetClientId = created.ClientId;

        VM.Accesses.Add(created);
        VM.SaveAll();
        SelectClientAndRefresh(targetClientId);
        VM.SelectedAccess = VM.Accesses.FirstOrDefault(a => a.Id == createdId) ?? VM.Accesses.LastOrDefault();
        _eventLogger.Log("clone", "access", created.Apelido, $"Clonado de={source.Apelido}; Tipo={created.Tipo}");
    }

    private string BuildCloneAlias(string alias)
    {
        var baseAlias = string.IsNullOrWhiteSpace(alias) ? "Acesso" : alias.Trim();
        var candidate = baseAlias + " (cópia)";
        var i = 2;

        while (VM.Accesses.Any(a => string.Equals(a.Apelido, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseAlias} (cópia {i})";
            i++;
        }

        return candidate;
    }

    /// <summary>
    /// Handler para excluir acesso selecionado.
    /// Exibe confirmação antes de remover.
    /// </summary>
    private async void OnDeleteAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var a = VM.SelectedAccess;
        var ok = await new ConfirmDialog($"Excluir o acesso '{a.Apelido}'?", "Excluir Acesso")
            .ShowDialog<bool>(this);

        if (!ok) return;

        VM.Accesses.Remove(a);
        VM.SaveAll();
        VM.RefreshAccesses();
        _eventLogger.Log("delete", "access", a.Apelido, $"Tipo={a.Tipo}");
    }

    /// <summary>
    /// Handler para abrir/conectar ao acesso selecionado.
    /// Detecta tipo (SSH, RDP, URL) e executa aktion apropriada.
    /// Fecha menus depois de executar.
    /// </summary>
    private void OnOpenAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        try
        {
            // Abre/conecta ao acesso usando o serviço de launcher
            AccessLauncher.Open(VM.SelectedAccess);
            VM.SelectedAccess.OpenCount = Math.Max(0, VM.SelectedAccess.OpenCount) + 1;
            VM.SelectedAccess.LastOpenedAt = DateTime.UtcNow.ToString("o");
            VM.SelectedAccess.AtualizadoEm = DateTime.UtcNow;
            VM.SaveAll();
            _eventLogger.Log("open", "access", VM.SelectedAccess.Apelido, $"Tipo={VM.SelectedAccess.Tipo}; OpenCount={VM.SelectedAccess.OpenCount}");
        }
        catch (Exception ex)
        {
            // Exibe erro se falhar
            _ = new ConfirmDialog($"Falha ao abrir:\n{ex.Message}", "Erro").ShowDialog<bool>(this);
        }
    }

    private void ToggleFavoriteSelectedAccess()
    {
        if (VM.SelectedAccess is null) return;
        var selectedId = VM.SelectedAccess.Id;
        var alias = VM.SelectedAccess.Apelido;
        var clientId = VM.SelectedAccess.ClientId;
        var toggled = !VM.SelectedAccess.IsFavorite;

        VM.SelectedAccess.IsFavorite = toggled;
        VM.SelectedAccess.AtualizadoEm = DateTime.UtcNow;
        VM.SaveAll();
        SelectClientAndRefresh(clientId);
        VM.SelectedAccess = VM.Accesses.FirstOrDefault(a => a.Id == selectedId) ?? VM.SelectedAccess;
        _eventLogger.Log("favorite", "access", alias, $"IsFavorite={toggled}");
    }

    private void SelectClientAndRefresh(Guid clientId)
    {
        var targetClient = VM.Clients.FirstOrDefault(c => c.Id == clientId);
        if (targetClient is null)
        {
            VM.RefreshAccesses();
            return;
        }

        VM.SetSelectedClient(targetClient);
        ApplyConnectivityStatusesToCurrentAccesses(refreshClientList: false);
    }

    private async Task OnExportData()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = await top.StorageProvider.TryGetFolderFromPathAsync(AppPaths.ExportDir),
            Title = "Escolha a pasta para exportar CSVs"
        });
        var selected = folders.FirstOrDefault();
        if (selected is null) return;
        var targetDir = selected.Path.LocalPath;
        var exported = _repo.ExportCsvSnapshot(Settings.ExportFormulaProtection);
        foreach (var file in Directory.GetFiles(exported))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        _eventLogger.Log("export", "data", "csv", $"Dir={targetDir}");
        await new ConfirmDialog($"Exportacao concluida em:\n{targetDir}", "Exportar CSV").ShowDialog<bool>(this);
    }

    private async void OnExportDataClick(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        await OnExportData();
    }

    private async Task OnImportData()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Selecione clientes.csv e acessos.csv (eventos.csv opcional)",
            SuggestedStartLocation = await top.StorageProvider.TryGetFolderFromPathAsync(AppPaths.ImportDir),
            FileTypeFilter = new List<FilePickerFileType> { new("CSV") { Patterns = new[] { "*.csv" } } }
        });
        if (files.Count == 0) return;
        var byName = files.ToDictionary(f => Path.GetFileName(f.Path.LocalPath).ToLowerInvariant(), f => f.Path.LocalPath);
        if (!byName.TryGetValue("clientes.csv", out var clientsImport) || !byName.TryGetValue("acessos.csv", out var accessesImport))
        {
            await new ConfirmDialog("Selecione ao menos clientes.csv e acessos.csv.", "Importar CSV").ShowDialog<bool>(this);
            return;
        }
        byName.TryGetValue("eventos.csv", out var eventsImport);

        var preview = _repo.ValidateImportPreview(clientsImport, accessesImport);
        if (preview.hasErrors)
        {
            await new HelpDialog(preview.report) { Title = "Falha na previa de importacao" }.ShowDialog<bool>(this);
            return;
        }

        var confirm = await new ConfirmDialog(
            $"{preview.report}\n\nDeseja importar de:\n{AppPaths.ImportDir}?",
            "Importar CSV").ShowDialog<bool>(this);
        if (!confirm) return;

        _repo.ImportCsvFiles(clientsImport, accessesImport, !string.IsNullOrWhiteSpace(eventsImport) && File.Exists(eventsImport) ? eventsImport : null);
        VM.Reload();
        ApplyConnectivityStatusesToCurrentAccesses();
        UpdateAuditIntegrityStatus();
        _eventLogger.Log("import", "data", "csv", $"Dir={AppPaths.ImportDir}");
        await new ConfirmDialog("Importacao concluida com backup automatico.", "Importar CSV").ShowDialog<bool>(this);
    }

    private async void OnImportDataClick(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        await OnImportData();
    }

    private async Task OnShowAuditLog()
    {
        UpdateAuditIntegrityStatus();
        if (!File.Exists(AppPaths.EventsPath))
        {
            await new ConfirmDialog("Arquivo eventos.csv ainda nao existe.", "Auditoria").ShowDialog<bool>(this);
            return;
        }

        await new AuditLogDialog(AppPaths.EventsPath).ShowDialog<bool>(this);
    }

    private async Task OnOpenSettings()
    {
        var dlg = new SettingsDialog(Settings);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        if (dlg.RequestRestoreLatestBackup)
        {
            await OnRestoreLatestBackup();
            return;
        }

        _settingsRepo.Save(dlg.Result);
        _currentSettings = _settingsRepo.Load();
        _eventLogger.Log("edit", "settings", "connectivity", "Configuracoes atualizadas");
        await new ConfirmDialog("Configurações salvas.", "Configurações").ShowDialog<bool>(this);
    }

    private async Task OnRestoreLatestBackup()
    {
        var latest = _repo.GetLatestBackupSnapshot();
        if (string.IsNullOrWhiteSpace(latest))
        {
            await new ConfirmDialog("Nenhum backup encontrado.", "Backups").ShowDialog<bool>(this);
            return;
        }

        var name = Path.GetFileName(latest);
        var confirm = await new ConfirmDialog(
            $"Restaurar backup '{name}'?\n\nIsso vai substituir clientes.csv, acessos.csv e eventos.csv atuais.",
            "Restaurar Backup").ShowDialog<bool>(this);
        if (!confirm) return;

        try
        {
            _repo.RestoreBackupSnapshot(latest);
            VM.Reload();
            ApplyConnectivityStatusesToCurrentAccesses();
            UpdateAuditIntegrityStatus();
            _eventLogger.Log("restore_backup", "backup", name, "Backup restaurado via configurações");
            await new ConfirmDialog($"Backup restaurado: {name}", "Backups").ShowDialog<bool>(this);
        }
        catch (Exception ex)
        {
            await new ConfirmDialog($"Falha ao restaurar backup:\n{ex.Message}", "Backups").ShowDialog<bool>(this);
        }
    }

    private async void OnShowAuditClick(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        await OnShowAuditLog();
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        await OnOpenSettings();
    }

    private void OnToggleFavoriteClick(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        ToggleFavoriteSelectedAccess();
    }

    private void UpdateAuditIntegrityStatus()
    {
        var text = this.FindControl<TextBlock>("AuditIntegrityText");
        if (text is null) return;

        var status = EventLogger.VerifyIntegrity();
        text.Text = status switch
        {
            EventLogger.IntegrityStatus.Ok => "Auditoria: OK",
            EventLogger.IntegrityStatus.Mismatch => "Auditoria: Divergente",
            EventLogger.IntegrityStatus.Missing => "Auditoria: Ausente",
            _ => "Auditoria: Erro"
        };
    }

    private void UpdateAppVersionText()
    {
        var text = this.FindControl<TextBlock>("AppVersionText");
        if (text is null) return;
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        text.Text = $"Versão: {v?.Major}.{v?.Minor}.{v?.Build}";
    }

    private void UpdateConnectivityStatusText(string message)
    {
        var text = this.FindControl<TextBlock>("ConnectivityStatusText");
        if (text is null) return;
        text.Text = $"Conectividade: {message}";
    }
}
