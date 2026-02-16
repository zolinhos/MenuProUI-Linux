using Avalonia.Controls;

namespace MenuProUI.Dialogs;

public enum ConnectivityScope
{
    Cancel = 0,
    SelectedClient = 1,
    AllClients = 2
}

public partial class ConnectivityScopeDialog : Window
{
    public ConnectivityScopeDialog()
    {
        InitializeComponent();
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(ConnectivityScope.Cancel);

    private void OnSelectedClient(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(ConnectivityScope.SelectedClient);

    private void OnAllClients(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(ConnectivityScope.AllClients);
}
