using Avalonia.Controls;
using MenuProUI.Models;

namespace MenuProUI.Dialogs;

public partial class ClientDialog : Window
{
    public Client Result { get; private set; }

    public ClientDialog(Client initial)
    {
        InitializeComponent();
        Result = new Client
        {
            Id = initial.Id,
            Nome = initial.Nome,
            Observacoes = initial.Observacoes,
            CriadoEm = initial.CriadoEm,
            AtualizadoEm = initial.AtualizadoEm
        };

        NameBox.Text = Result.Nome;
        NotesBox.Text = Result.Observacoes ?? "";
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "Sem Cliente";

        Result.Nome = name;
        Result.Observacoes = NotesBox.Text;
        Result.AtualizadoEm = System.DateTime.UtcNow;

        Close(true);
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
