using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MenuProUI.Models;

namespace MenuProUI.Services;

/// <summary>
/// Serviço de checagem de conectividade TCP para acessos SSH/RDP/URL.
/// </summary>
public static class ConnectivityChecker
{
    private static readonly object ToolLock = new();
    private static string? _nmapPath;
    private static string? _ncPath;
    private static bool _toolPathsReady;

    public enum ProbeMethod
    {
        Tcp,
        TcpFallback,
        Nmap,
        NmapFallback,
        Nc,
        NcFallback
    }

    public enum FailureKind
    {
        None,
        InvalidTarget,
        Dns,
        Timeout,
        Refused,
        Unreachable,
        Unknown
    }

    public sealed class CheckResult
    {
        public bool IsOnline { get; init; }
        public ProbeMethod Method { get; init; } = ProbeMethod.Tcp;
        public int EffectivePort { get; init; }
        public FailureKind FailureKind { get; init; } = FailureKind.None;
        public string ErrorDetail { get; init; } = "";
    }

    /// <summary>
    /// Checa se um host:porta está acessível dentro do timeout informado.
    /// </summary>
    public static async Task<bool> CheckTcpAsync(string host, int port, TimeSpan timeout)
    {
        var result = await CheckTcpDetailedAsync(host, port, timeout);
        return result.IsOnline;
    }

    public static async Task<CheckResult> CheckAccessDetailedAsync(
        AccessEntry entry,
        TimeSpan timeout,
        IReadOnlyCollection<int> urlFallbackPorts)
    {
        EnsureToolPaths();
        var hasNmap = !string.IsNullOrWhiteSpace(_nmapPath);
        var hasNc = !string.IsNullOrWhiteSpace(_ncPath);

        if (entry.Tipo != AccessType.URL)
        {
            var basePort = entry.Tipo == AccessType.SSH ? 22 : 3389;
            var port = entry.Porta is > 0 and <= 65535 ? entry.Porta.Value : basePort;
            if (hasNmap)
            {
                var nmap = await CheckWithNmapAsync(entry.Host ?? "", port, timeout, ProbeMethod.Nmap);
                if (nmap.IsOnline) return nmap;
            }

            var tcp = await CheckTcpDetailedAsync(entry.Host ?? "", port, timeout);
            if (tcp.IsOnline) return tcp;

            if (hasNc)
            {
                var nc = await CheckWithNcAsync(entry.Host ?? "", port, timeout, ProbeMethod.Nc);
                if (nc.IsOnline) return nc;
            }

            return tcp;
        }

        var endpoint = ResolveUrlHostPort(entry.Url);
        var fallbacks = (urlFallbackPorts ?? Array.Empty<int>())
            .Where(p => p is >= 1 and <= 65535)
            .Distinct()
            .Where(p => p != endpoint.port)
            .ToList();

        var candidates = new List<(int port, bool fallback)> { (endpoint.port, false) };
        candidates.AddRange(fallbacks.Select(p => (p, true)));

        CheckResult? lastFailure = null;
        foreach (var candidate in candidates)
        {
            var tcp = await CheckTcpDetailedAsync(endpoint.host, candidate.port, timeout);
            if (tcp.IsOnline)
            {
                return candidate.fallback
                    ? new CheckResult
                    {
                        IsOnline = true,
                        Method = ProbeMethod.TcpFallback,
                        EffectivePort = candidate.port
                    }
                    : tcp;
            }
            lastFailure = tcp;

            if (hasNmap)
            {
                var nmapMethod = candidate.fallback ? ProbeMethod.NmapFallback : ProbeMethod.Nmap;
                var nmap = await CheckWithNmapAsync(endpoint.host, candidate.port, timeout, nmapMethod);
                if (nmap.IsOnline) return nmap;
                lastFailure = nmap;
            }

            if (hasNc)
            {
                var ncMethod = candidate.fallback ? ProbeMethod.NcFallback : ProbeMethod.Nc;
                var nc = await CheckWithNcAsync(endpoint.host, candidate.port, timeout, ncMethod);
                if (nc.IsOnline) return nc;
                lastFailure = nc;
            }
        }

        return lastFailure ?? new CheckResult
        {
            IsOnline = false,
            EffectivePort = endpoint.port,
            FailureKind = FailureKind.Unknown,
            ErrorDetail = "Falha de rede"
        };
    }

    public static bool HasNmap
    {
        get { EnsureToolPaths(); return !string.IsNullOrWhiteSpace(_nmapPath); }
    }

    public static bool HasNc
    {
        get { EnsureToolPaths(); return !string.IsNullOrWhiteSpace(_ncPath); }
    }

    public static string NmapPathDescription
    {
        get { EnsureToolPaths(); return _nmapPath ?? "não encontrado"; }
    }

    public static string NcPathDescription
    {
        get { EnsureToolPaths(); return _ncPath ?? "não encontrado"; }
    }

    public sealed class NmapTestResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
    }

    public static void RevalidateToolPaths()
    {
        lock (ToolLock)
        {
            _toolPathsReady = false;
        }
        EnsureToolPaths();
    }

    public static async Task<NmapTestResult> TestNmapNowAsync(TimeSpan timeout)
    {
        EnsureToolPaths();
        if (string.IsNullOrWhiteSpace(_nmapPath))
        {
            return new NmapTestResult { Ok = false, Message = "nmap não encontrado no PATH." };
        }

        var result = await CheckWithProcessAsync(
            _nmapPath,
            "--version",
            timeout,
            ProbeMethod.Nmap,
            0,
            output => output.Contains("Nmap", StringComparison.OrdinalIgnoreCase),
            "nmap");

        return new NmapTestResult
        {
            Ok = result.IsOnline,
            Message = result.IsOnline
                ? $"nmap OK: {_nmapPath}"
                : $"Falha no teste do nmap: {result.ErrorDetail}"
        };
    }

    public static async Task<CheckResult> CheckWithNmapAsync(string host, int port, TimeSpan timeout, ProbeMethod method)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.InvalidTarget,
                ErrorDetail = "Destino inválido"
            };
        }

        EnsureToolPaths();
        if (string.IsNullOrWhiteSpace(_nmapPath))
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.Unknown,
                ErrorDetail = "nmap indisponível"
            };
        }

        var timeoutMs = Math.Max(500, (int)Math.Ceiling(timeout.TotalMilliseconds));
        var args = $"-Pn -p {port} --open --host-timeout {timeoutMs}ms {host}";
        return await CheckWithProcessAsync(
            _nmapPath,
            args,
            timeout,
            method,
            port,
            output => output.Contains($"{port}/tcp open", StringComparison.OrdinalIgnoreCase),
            "nmap");
    }

    public static async Task<CheckResult> CheckWithNcAsync(string host, int port, TimeSpan timeout, ProbeMethod method)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.InvalidTarget,
                ErrorDetail = "Destino inválido"
            };
        }

        EnsureToolPaths();
        if (string.IsNullOrWhiteSpace(_ncPath))
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.Unknown,
                ErrorDetail = "nc indisponível"
            };
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
        var args = $"-z -w {seconds} {host} {port}";
        return await CheckWithProcessAsync(
            _ncPath,
            args,
            timeout,
            method,
            port,
            _ => true,
            "nc");
    }

    private static async Task<CheckResult> CheckWithProcessAsync(
        string? fileName,
        string arguments,
        TimeSpan timeout,
        ProbeMethod method,
        int port,
        Func<string, bool> outputSuccessPredicate,
        string label)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.Unknown,
                ErrorDetail = $"{label} indisponível"
            };
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            using var cts = new CancellationTokenSource(timeout);
            var waitTask = process.WaitForExitAsync(cts.Token);
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await waitTask;
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;
            var fullOutput = $"{stdOut}\n{stdErr}";
            var ok = process.ExitCode == 0 && outputSuccessPredicate(fullOutput);

            return new CheckResult
            {
                IsOnline = ok,
                Method = method,
                EffectivePort = port,
                FailureKind = ok ? FailureKind.None : FailureKind.Refused,
                ErrorDetail = ok ? "" : string.IsNullOrWhiteSpace(fullOutput) ? $"{label} sem resposta positiva" : fullOutput.Trim()
            };
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.Timeout,
                ErrorDetail = "Timeout"
            };
        }
        catch
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = method,
                EffectivePort = port,
                FailureKind = FailureKind.Unknown,
                ErrorDetail = $"Falha ao executar {label}"
            };
        }
    }

    private static string? FindExecutable(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var fullPath = Path.Combine(dir.Trim(), executableName);
                if (File.Exists(fullPath)) return fullPath;
            }
            catch
            {
                // Ignora entradas inválidas do PATH.
            }
        }

        return null;
    }

    private static void EnsureToolPaths()
    {
        lock (ToolLock)
        {
            if (_toolPathsReady) return;
            _nmapPath = FindExecutable("nmap");
            _ncPath = FindExecutable("nc");
            _toolPathsReady = true;
        }
    }

    public static async Task<CheckResult> CheckTcpDetailedAsync(string host, int port, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.InvalidTarget,
                ErrorDetail = "Destino inválido"
            };
        }
        if (port is < 1 or > 65535)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.InvalidTarget,
                ErrorDetail = "Destino inválido"
            };
        }

        using var cts = new CancellationTokenSource(timeout);
        using var client = new TcpClient();

        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return new CheckResult
            {
                IsOnline = true,
                Method = ProbeMethod.Tcp,
                EffectivePort = port
            };
        }
        catch (OperationCanceledException)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.Timeout,
                ErrorDetail = "Timeout"
            };
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound || ex.SocketErrorCode == SocketError.NoData)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.Dns,
                ErrorDetail = "Erro DNS"
            };
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.Refused,
                ErrorDetail = "Conexão recusada"
            };
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.NetworkUnreachable || ex.SocketErrorCode == SocketError.HostUnreachable)
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.Unreachable,
                ErrorDetail = "Host indisponível"
            };
        }
        catch
        {
            return new CheckResult
            {
                IsOnline = false,
                Method = ProbeMethod.Tcp,
                EffectivePort = port,
                FailureKind = FailureKind.Unknown,
                ErrorDetail = "Falha de rede"
            };
        }
    }

    private static (string host, int port) ResolveUrlHostPort(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return ("", 443);

        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            var port = parsed.IsDefaultPort ? DefaultPortForScheme(parsed.Scheme) : parsed.Port;
            return (parsed.Host, port > 0 ? port : 443);
        }

        if (Uri.TryCreate("https://" + url, UriKind.Absolute, out parsed))
        {
            var port = parsed.IsDefaultPort ? DefaultPortForScheme(parsed.Scheme) : parsed.Port;
            return (parsed.Host, port > 0 ? port : 443);
        }

        return ("", 443);
    }

    private static int DefaultPortForScheme(string? scheme)
    {
        return (scheme ?? "").ToLowerInvariant() switch
        {
            "http" => 80,
            "https" => 443,
            "ftp" => 21,
            _ => 443
        };
    }
}
