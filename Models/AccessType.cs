namespace MenuProUI.Models;

/// <summary>
/// Enumeração que define os tipos de acesso disponíveis na aplicação.
/// </summary>
public enum AccessType
{
    /// <summary>Conexão SSH (Secure Shell) para servidores Linux/Unix</summary>
    SSH,
    
    /// <summary>Conexão RDP (Remote Desktop Protocol) para Windows</summary>
    RDP,
    
    /// <summary>Acesso a URL/Website no navegador</summary>
    URL
}
