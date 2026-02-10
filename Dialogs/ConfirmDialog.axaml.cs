using Avalonia.Controls;

namespace MenuProUI.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string message, string title = "Confirmar")
    {
        InitializeComponent();
        Title = title;
        Msg.Text = message;
    }

    private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
