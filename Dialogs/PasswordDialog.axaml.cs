using Avalonia.Controls;

namespace MenuProUI.Dialogs;

public enum PasswordDialogAction
{
    Cancel = 0,
    OpenTerminal = 1,
    Connect = 2
}

public partial class PasswordDialog : Window
{
    public string Password => PasswordBox.Text ?? "";

    public PasswordDialog(string title)
    {
        InitializeComponent();
        TitleText.Text = title;
        PasswordBox.Focus();
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close(PasswordDialogAction.Cancel);

    private void OnOpenTerminal(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close(PasswordDialogAction.OpenTerminal);

    private void OnConnect(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close(PasswordDialogAction.Connect);
}
