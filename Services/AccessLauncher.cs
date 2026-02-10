using System;
using System.Diagnostics;
using System.IO;
using MenuProUI.Models;

namespace MenuProUI.Services;

/// <summary>
/// Serviço responsável por abrir/conectar acessos de diferentes tipos.
/// Detecta tipo de acesso e executa comando apropriado:
/// - SSH: abre terminal com conexão SSH
/// - RDP: abre cliente RDP no terminal (com suporte a xfreerdp/xfreerdp3)
/// - URL: abre navegador padrão
/// </summary>
public static class AccessLauncher
{
    /// <summary>
    /// Abre um acesso baseado em seu tipo (SSH, RDP ou URL).
    /// Detecta automaticamente e executa a ação apropriada.
    /// </summary>
    /// <param name="e">Acesso a ser aberto</param>
    public static void Open(AccessEntry e)
    {
        switch (e.Tipo)
        {
            case AccessType.URL:
                OpenUrl(e.Url);
                break;
            case AccessType.SSH:
                OpenSsh(e);
                break;
            case AccessType.RDP:
                // Abre RDP em terminal (não congela a UI)
                OpenRdpInTerminal(e);
                break;
        }
    }

    /// <summary>
    /// Abre RDP com suporte a senha via stdin.
    /// Útil para automatizar conexões RDP com credenciais.
    /// Nota: requer xfreerdp ou xfreerdp3 instalados.
    /// </summary>
    /// <param name="e">Acesso RDP com configurações</param>
    /// <param name="password">Senha para autenticação RDP</param>
    public static void OpenRdpWithPassword(AccessEntry e, string password)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var client = FindRdpClient();
        if (client is null) return;

        // Usa porta padrão 3389 se não especificada
        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        // Configura processo para executar cliente RDP
        var psi = new ProcessStartInfo
        {
            FileName = client,
            UseShellExecute = false,
            RedirectStandardInput = true,  // Entrada padrão para senha
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Monta argumentos do xfreerdp
        psi.ArgumentList.Add($"/v:{e.Host}:{port}");

        if (!string.IsNullOrWhiteSpace(e.Usuario))
            psi.ArgumentList.Add($"/u:{e.Usuario}");

        if (!string.IsNullOrWhiteSpace(e.Dominio))
            psi.ArgumentList.Add($"/d:{e.Dominio}");

        if (e.RdpIgnoreCert)
            psi.ArgumentList.Add("/cert:ignore");

        // Force credentials from stdin (evita congelar)
        psi.ArgumentList.Add("/from-stdin:force");

        // Configura tela/resolução
        if (e.RdpFullScreen)
        {
            psi.ArgumentList.Add("/f");
        }
        else if (e.RdpDynamicResolution)
        {
            // xfreerdp3 usa sintaxe diferente de xfreerdp2
            psi.ArgumentList.Add(client.EndsWith("3", StringComparison.Ordinal) ? "+dynamic-resolution" : "/dynamic-resolution");
        }
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
        {
            psi.ArgumentList.Add($"/size:{e.RdpWidth}x{e.RdpHeight}");
        }

        var p = Process.Start(psi);
        if (p is null) return;

        // Drena saída padrão/erro assincronamente (evita deadlock)
        _ = p.StandardOutput.ReadToEndAsync();
        _ = p.StandardError.ReadToEndAsync();

        // Envia senha via stdin
        using (var sw = p.StandardInput)
        {
            sw.WriteLine(password);
            sw.Flush();
        }
    }

    /// <summary>
    /// Abre conexão RDP em terminal.
    /// O cliente RDP (xfreerdp) pede senha interativamente no terminal.
    /// Evita congelar a UI pois executa em processo separado.
    /// </summary>
    /// <param name="e">Acesso RDP com configurações</param>
    public static void OpenRdpInTerminal(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var client = FindRdpClient() ?? "xfreerdp";
        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        // Monta comando RDP (sem /p = cliente pede senha no terminal)
        var cmd = $"{client} /v:{e.Host}:{port}";

        if (!string.IsNullOrWhiteSpace(e.Usuario))
            cmd += $" /u:{EscapeShell(e.Usuario)}";

        if (!string.IsNullOrWhiteSpace(e.Dominio))
            cmd += $" /d:{EscapeShell(e.Dominio)}";

        if (e.RdpIgnoreCert)
            cmd += " /cert:ignore";

        // Configurações de tela
        if (e.RdpFullScreen)
            cmd += " /f";
        else if (e.RdpDynamicResolution)
            cmd += (client.EndsWith("3", StringComparison.Ordinal) ? " +dynamic-resolution" : " /dynamic-resolution");
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
            cmd += $" /size:{e.RdpWidth}x{e.RdpHeight}";

        // Abre em terminal
        OpenTerminal(cmd);
    }

    /// <summary>
    /// Localiza cliente RDP instalado no sistema.
    /// Tenta encontrar xfreerdp ou xfreerdp3.
    /// </summary>
    /// <returns>Caminho do cliente RDP ou null se não encontrado</returns>
    private static string? FindRdpClient()
    {
        if (IsOnPath("xfreerdp")) return "xfreerdp";
        if (IsOnPath("xfreerdp3")) return "xfreerdp3";
        return null;
    }

    /// <summary>
    /// Verifica se um executável está disponível no PATH.
    /// Usa comando 'command -v' do bash para buscar.
    /// </summary>
    /// <param name="exe">Nome do executável a buscar</param>
    /// <returns>true se encontrado, false caso contrário</returns>
    private static bool IsOnPath(string exe)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "bash",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            psi.ArgumentList.Add("-lc");
            psi.ArgumentList.Add($"command -v {exe}");
            var p = Process.Start(psi);
            if (p is null) return false;
            var output = p.StandardOutput.ReadToEnd().Trim();
            return !string.IsNullOrWhiteSpace(output);
        }
        catch { return false; }
    }

    /// <summary>
    /// Abre uma URL no navegador padrão usando xdg-open.
    /// </summary>
    /// <param name="url">URL a abrir</param>
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var psi = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false
        };
        psi.ArgumentList.Add(url);
        Process.Start(psi);
    }

    /// <summary>
    /// Abre conexão SSH em terminal.
    /// Usa porta 22 como padrão se não especificada.
    /// </summary>
    /// <param name="e">Acesso SSH com host, usuário e porta</param>
    private static void OpenSsh(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var port = (e.Porta is > 0) ? e.Porta.Value : 22;
        var userAt = string.IsNullOrWhiteSpace(e.Usuario) ? e.Host : $"{e.Usuario}@{e.Host}";
        var cmd = $"ssh -p {port} {EscapeShell(userAt)}";

        OpenTerminal(cmd);
    }

    /// <summary>
    /// Abre um terminal com um comando a executar.
    /// Tenta vários emuladores de terminal comuns até conseguir abrir um.
    /// </summary>
    /// <param name="command">Comando a executar no terminal</param>
    private static void OpenTerminal(string command)
    {
        // Tenta emuladores em ordem de preferência
        if (TryTerminal("x-terminal-emulator", "-e", "bash", "-lc", command)) return;
        if (TryTerminal("gnome-terminal", "--", "bash", "-lc", command)) return;
        if (TryTerminal("konsole", "-e", "bash", "-lc", command)) return;
        // Fallback para xterm
        TryTerminal("xterm", "-e", "bash", "-lc", command);
    }

    /// <summary>
    /// Tenta abrir um terminal com argumentos.
    /// Retorna sucesso se conseguir iniciar o processo.
    /// </summary>
    /// <param name="file">Nome/caminho do emulador de terminal</param>
    /// <param name="args">Argumentos para passar ao terminal</param>
    /// <returns>true se conseguiu iniciar, false caso contrário</returns>
    private static bool TryTerminal(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = false
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Escapa uma string para uso seguro em bash -lc.
    /// Substitui aspas simples por sequência de escape.
    /// </summary>
    /// <param name="s">String a escapar</param>
    /// <returns>String escapada para bash</returns>
    private static string EscapeShell(string s)
    {
        // Escapa aspas simples: ' -> '\"'\"'
        return s.Replace("'", "'\"'\"'");
    }
}
