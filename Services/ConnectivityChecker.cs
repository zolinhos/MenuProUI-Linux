using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MenuProUI.Services;

/// <summary>
/// Serviço de checagem de conectividade TCP para acessos SSH/RDP/URL.
/// </summary>
public static class ConnectivityChecker
{
    /// <summary>
    /// Checa se um host:porta está acessível dentro do timeout informado.
    /// </summary>
    public static async Task<bool> CheckTcpAsync(string host, int port, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (port is < 1 or > 65535) return false;

        using var cts = new CancellationTokenSource(timeout);
        using var client = new TcpClient();

        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}