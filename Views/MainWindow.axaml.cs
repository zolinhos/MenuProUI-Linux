using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    /// <summary>
    /// Inicializa a janela principal.
    /// Configura o ViewModel como DataContext e conecta handlers de eventos.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        // Conecta botões de menu aos handlers de toggle
        var clientsMenuBtn = this.FindControl<Button>("ClientsMenuBtn");
        if (clientsMenuBtn != null)
            clientsMenuBtn.Click += (s, e) => ToggleMenu("ClientsMenu");

        var accessesMenuBtn = this.FindControl<Button>("AccessesMenuBtn");
        if (accessesMenuBtn != null)
            accessesMenuBtn.Click += (s, e) => ToggleMenu("AccessesMenu");

        // Configura handler para tecla F1 (Help)
        this.KeyDown += MainWindow_KeyDown;
    }

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
  Ctrl+Shift+E          Editar acesso selecionado
  Ctrl+Shift+Delete     Excluir acesso selecionado
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
GitHub: https://github.com/adrianotech/MenuProUI-Linux
Issues: https://github.com/adrianotech/MenuProUI-Linux/issues
Discussions: https://github.com/adrianotech/MenuProUI-Linux/discussions

Versão 1.0.4 - MenuProUI";

        var dlg = new HelpDialog(helpText);
        await dlg.ShowDialog<bool>(this);
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
    }

    /// <summary>
    /// Handler para botão Recarregar.
    /// Recarrega todos os dados do disco e reaplica filtros.
    /// </summary>
    private void OnReload(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        VM.Reload();
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

    /// <summary>
    /// Handler para editar acesso selecionado.
    /// Permite modificar todos os campos de configuração.
    /// </summary>
    private async void OnEditAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var dlg = new AccessDialog(VM.SelectedAccess);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;

        // Atualiza todos os campos do acesso
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
        }
        catch (Exception ex)
        {
            // Exibe erro se falhar
            _ = new ConfirmDialog($"Falha ao abrir:\n{ex.Message}", "Erro").ShowDialog<bool>(this);
        }
    }
}
