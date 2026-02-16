using System;
using CsvHelper.Configuration.Attributes;

namespace MenuProUI.Models;

/// <summary>
/// Representa um cliente (organização, projeto ou agregador de acessos).
/// Um cliente pode ter múltiplos acessos (SSH, RDP, URLs) associados.
/// </summary>
public class Client
{
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
    public ConnectivityState ConnectivityState { get; set; } = ConnectivityState.Unknown;

    /// <summary>Badge visual simples para exibição do status agregado.</summary>
    [Ignore]
    public string ConnectivityBadge => ConnectivityState switch
    {
        ConnectivityState.Online => "🟢",
        ConnectivityState.Offline => "🔴",
        ConnectivityState.Checking => "🟡",
        _ => "⚪"
    };

    /// <summary>Retorna o nome do cliente como representação em string</summary>
    public override string ToString() => Nome;
}
