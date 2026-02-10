using System;

namespace MenuProUI.Models;

public class AccessEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }

    public AccessType Tipo { get; set; } = AccessType.URL;
    public string Apelido { get; set; } = "Novo Acesso";

    // SSH/RDP
    public string? Host { get; set; }
    public int? Porta { get; set; }
    public string? Usuario { get; set; }

    // RDP
    public string? Dominio { get; set; }
    public bool RdpIgnoreCert { get; set; } = true;          // default: sim, porque é comum em infra local
    public bool RdpFullScreen { get; set; } = false;
    public bool RdpDynamicResolution { get; set; } = true;   // default: sim (melhor UX)
    public int? RdpWidth { get; set; }
    public int? RdpHeight { get; set; }

    // URL
    public string? Url { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
