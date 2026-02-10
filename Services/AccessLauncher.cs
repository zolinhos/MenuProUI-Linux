using System;
using System.Diagnostics;
using System.IO;
using MenuProUI.Models;

namespace MenuProUI.Services;

public static class AccessLauncher
{
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
                // fallback seguro: abre no terminal (não congela)
                OpenRdpInTerminal(e);
                break;
        }
    }

    public static void OpenRdpWithPassword(AccessEntry e, string password)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var client = FindRdpClient();
        if (client is null) return;

        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        var psi = new ProcessStartInfo
        {
            FileName = client,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add($"/v:{e.Host}:{port}");

        if (!string.IsNullOrWhiteSpace(e.Usuario))
            psi.ArgumentList.Add($"/u:{e.Usuario}");

        if (!string.IsNullOrWhiteSpace(e.Dominio))
            psi.ArgumentList.Add($"/d:{e.Dominio}");

        if (e.RdpIgnoreCert)
            psi.ArgumentList.Add("/cert:ignore");

        // Força leitura de credenciais via stdin (evita congelar no GUI)
        psi.ArgumentList.Add("/from-stdin:force"); // freerdp2/3 doc :contentReference[oaicite:2]{index=2}

        // tela/resolução (freerdp2: /dynamic-resolution, freerdp3 costuma usar +dynamic-resolution) :contentReference[oaicite:3]{index=3}
        if (e.RdpFullScreen)
        {
            psi.ArgumentList.Add("/f");
        }
        else if (e.RdpDynamicResolution)
        {
            psi.ArgumentList.Add(client.EndsWith("3", StringComparison.Ordinal) ? "+dynamic-resolution" : "/dynamic-resolution");
        }
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
        {
            psi.ArgumentList.Add($"/size:{e.RdpWidth}x{e.RdpHeight}");
        }

        var p = Process.Start(psi);
        if (p is null) return;

        // drenar stdout/stderr (evita travar se houver muita saída)
        _ = p.StandardOutput.ReadToEndAsync();
        _ = p.StandardError.ReadToEndAsync();

        // envia senha e fecha stdin
        using (var sw = p.StandardInput)
        {
            sw.WriteLine(password);
            sw.Flush();
        }
    }

    public static void OpenRdpInTerminal(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var client = FindRdpClient() ?? "xfreerdp";
        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        // Sem /p -> o próprio xfreerdp pede senha no terminal.
        // Isso evita “congelar” quando o app foi aberto pelo menu (sem TTY). :contentReference[oaicite:4]{index=4}
        var cmd = $"{client} /v:{e.Host}:{port}";

        if (!string.IsNullOrWhiteSpace(e.Usuario))
            cmd += $" /u:{EscapeShell(e.Usuario)}";

        if (!string.IsNullOrWhiteSpace(e.Dominio))
            cmd += $" /d:{EscapeShell(e.Dominio)}";

        if (e.RdpIgnoreCert)
            cmd += " /cert:ignore";

        if (e.RdpFullScreen)
            cmd += " /f";
        else if (e.RdpDynamicResolution)
            cmd += (client.EndsWith("3", StringComparison.Ordinal) ? " +dynamic-resolution" : " /dynamic-resolution");
        else if (e.RdpWidth is > 0 && e.RdpHeight is > 0)
            cmd += $" /size:{e.RdpWidth}x{e.RdpHeight}";

        OpenTerminal(cmd);
    }

    private static string? FindRdpClient()
    {
        // tenta xfreerdp e xfreerdp3
        if (IsOnPath("xfreerdp")) return "xfreerdp";
        if (IsOnPath("xfreerdp3")) return "xfreerdp3";
        return null;
    }

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

    private static void OpenSsh(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var port = (e.Porta is > 0) ? e.Porta.Value : 22;
        var userAt = string.IsNullOrWhiteSpace(e.Usuario) ? e.Host : $"{e.Usuario}@{e.Host}";
        var cmd = $"ssh -p {port} {EscapeShell(userAt)}";

        OpenTerminal(cmd);
    }

    private static void OpenTerminal(string command)
    {
        if (TryTerminal("x-terminal-emulator", "-e", "bash", "-lc", command)) return;
        if (TryTerminal("gnome-terminal", "--", "bash", "-lc", command)) return;
        if (TryTerminal("konsole", "-e", "bash", "-lc", command)) return;

        TryTerminal("xterm", "-e", "bash", "-lc", command);
    }

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

    private static string EscapeShell(string s)
    {
        // simples e efetivo para bash -lc
        return s.Replace("'", "'\"'\"'");
    }
}
