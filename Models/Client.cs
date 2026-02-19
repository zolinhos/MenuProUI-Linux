using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CsvHelper.Configuration.Attributes;

namespace MenuProUI.Models;

/// <summary>
/// Representa um cliente (organização, projeto ou agregador de acessos).
/// Um cliente pode ter múltiplos acessos (SSH, RDP, URLs) associados.
/// </summary>
public class Client : INotifyPropertyChanged
{
    private ConnectivityState _connectivityState = ConnectivityState.Unknown;
    /// <summary>Identificador único do cliente (GUID)</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>Nome do cliente (ex: "Acme Corp", "Servidor Prod")</summary>
    public string Nome { get; set; } = "Sem Cliente";
    
    /// <summary>Observações adicionais sobre o cliente (opcional)</summary>
    public string? Observacoes { get; set; }

    /// <summary>Data e hora de criação do cliente (UTC)</summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    
    /// <summary>Data e hora da última atualização (UTC)</summary>
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Status de conectividade agregado do cliente (não persistido em CSV).</summary>
    [Ignore]
    public ConnectivityState ConnectivityState
    {
        get => _connectivityState;
        set
        {
            if (_connectivityState == value) return;
            _connectivityState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectivityBadge));
            OnPropertyChanged(nameof(ConnectivityBadgeColor));
        }
    }

    /// <summary>Badge visual simples para exibição do status agregado.</summary>
    [Ignore]
    public string ConnectivityBadge => ConnectivityState switch
    {
        ConnectivityState.Online => "●",
        ConnectivityState.Offline => "●",
        ConnectivityState.Checking => "●",
        _ => "●"
    };

    /// <summary>Cor fixa do badge para não depender de tema.</summary>
    [Ignore]
    public string ConnectivityBadgeColor => ConnectivityState switch
    {
        ConnectivityState.Online => "#16A34A",  // verde
        ConnectivityState.Offline => "#DC2626", // vermelho
        ConnectivityState.Checking => "#CA8A04", // amarelo
        _ => "#6B7280" // cinza
    };

    /// <summary>Retorna o nome do cliente como representação em string</summary>
    public override string ToString() => Nome;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
