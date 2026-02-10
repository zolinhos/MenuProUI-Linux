using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MenuProUI.Dialogs;

/// <summary>
/// Diálogo de ajuda completo com ScrollViewer para exibir textos longos.
/// Mostra documentação, atalhos de teclado e links de suporte.
/// </summary>
public partial class HelpDialog : Window
{
    /// <summary>Inicializa o diálogo de ajuda com o conteúdo fornecido</summary>
    /// <param name="helpText">Texto de ajuda a ser exibido</param>
    public HelpDialog(string helpText)
    {
        InitializeComponent();
        var helpContent = this.FindControl<TextBlock>("HelpContent");
        if (helpContent != null)
        {
            helpContent.Text = helpText;
        }
    }

    /// <summary>Handler para o botão Fechar</summary>
    private void OnClose(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
