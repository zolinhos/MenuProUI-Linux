using System;

namespace MenuProUI.Models;

/// <summary>
/// Representa um acesso individual (SSH, RDP ou URL) associado a um cliente.
/// Contém todas as configurações necessárias para conectar ou abrir um recurso.
/// </summary>
public class AccessEntry
{
    /// <summary>Identificador único do acesso (GUID)</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>ID do cliente ao qual este acesso está associado</summary>
    public Guid ClientId { get; set; }

    /// <summary>Tipo de acesso: SSH, RDP ou URL</summary>
    public AccessType Tipo { get; set; } = AccessType.URL;
    
    /// <summary>Nome/apelido do acesso para identificação rápida (ex: "Servidor Web Prod")</summary>
    public string Apelido { get; set; } = "Novo Acesso";

    // ============ CAMPOS COMUNS SSH/RDP ============
    /// <summary>Nome do host ou IP do servidor (ex: "192.168.1.100" ou "server.example.com")</summary>
    public string? Host { get; set; }
    
    /// <summary>Porta de conexão (padrão: 22 para SSH, 3389 para RDP)</summary>
    public int? Porta { get; set; }
    
    /// <summary>Nome de usuário para autenticação</summary>
    public string? Usuario { get; set; }

    // ============ CAMPOS ESPECÍFICOS RDP ============
    /// <summary>Domínio Windows para RDP (ex: "CORP" em "CORP\usuario")</summary>
    public string? Dominio { get; set; }
    
    /// <summary>Se true, ignora erros de certificado SSL no RDP (comum em infra local)</summary>
    public bool RdpIgnoreCert { get; set; } = true;
    
    /// <summary>Se true, abre RDP em tela cheia</summary>
    public bool RdpFullScreen { get; set; } = false;
    
    /// <summary>Se true, ajusta resolução dinamicamente (melhor UX)</summary>
    public bool RdpDynamicResolution { get; set; } = true;
    
    /// <summary>Largura da janela RDP em pixels (usado se RdpDynamicResolution for false)</summary>
    public int? RdpWidth { get; set; }
    
    /// <summary>Altura da janela RDP em pixels (usado se RdpDynamicResolution for false)</summary>
    public int? RdpHeight { get; set; }

    // ============ CAMPO ESPECÍFICO URL ============
    /// <summary>URL completa para abrir no navegador (ex: "https://example.com")</summary>
    public string? Url { get; set; }

    /// <summary>Observações adicionais sobre este acesso (opcional)</summary>
    public string? Observacoes { get; set; }

    /// <summary>Data e hora de criação deste acesso (UTC)</summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    
    /// <summary>Data e hora da última atualização (UTC)</summary>
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
