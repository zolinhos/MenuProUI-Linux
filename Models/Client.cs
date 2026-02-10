using System;

namespace MenuProUI.Models;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = "Sem Cliente";
    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public override string ToString() => Nome;
}
