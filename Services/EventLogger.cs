using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MenuProUI.Services;

public sealed class EventLogger
{
    private const long MaxEventsFileBytes = 5 * 1024 * 1024;
    private static readonly object Sync = new();

    public enum IntegrityStatus
    {
        Ok,
        Missing,
        Mismatch,
        Error
    }

    public void Log(string action, string entityType, string entityName, string details)
    {
        lock (Sync)
        {
            EnsureEventsFile();
            RotateIfNeeded();
            EnsureEventsFile();

            var line = string.Join(",",
                Csv(DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss")),
                Csv(action),
                Csv(entityType),
                Csv(entityName),
                Csv(details));

            File.AppendAllText(AppPaths.EventsPath, line + Environment.NewLine, Encoding.UTF8);
            UpdateChainAfterAppend(line);
        }
    }

    public static IntegrityStatus VerifyIntegrity()
    {
        try
        {
            if (!File.Exists(AppPaths.EventsPath) || !File.Exists(AppPaths.EventsChainPath))
                return IntegrityStatus.Missing;

            var chain = ReadChainState();
            if (chain is null) return IntegrityStatus.Error;

            var lines = File.ReadAllLines(AppPaths.EventsPath, Encoding.UTF8);
            var last = "GENESIS";
            var count = 0;

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                last = Sha256Hex(last + "\n" + line);
                count++;
            }

            return (count == chain.Value.count && last == chain.Value.lastHash)
                ? IntegrityStatus.Ok
                : IntegrityStatus.Mismatch;
        }
        catch
        {
            return IntegrityStatus.Error;
        }
    }

    private static void EnsureEventsFile()
    {
        if (File.Exists(AppPaths.EventsPath)) return;
        File.WriteAllText(AppPaths.EventsPath, "TimestampUtc,Action,EntityType,EntityName,Details\n", Encoding.UTF8);
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(AppPaths.EventsPath)) return;
        var info = new FileInfo(AppPaths.EventsPath);
        if (info.Length < MaxEventsFileBytes) return;

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var rotatedEvents = Path.Combine(AppPaths.AppDir, $"eventos_{stamp}.csv");
        File.Move(AppPaths.EventsPath, rotatedEvents, true);

        if (File.Exists(AppPaths.EventsChainPath))
        {
            var rotatedChain = Path.Combine(AppPaths.AppDir, $"eventos_{stamp}.chain");
            File.Move(AppPaths.EventsChainPath, rotatedChain, true);
        }
    }

    private static void UpdateChainAfterAppend(string line)
    {
        var state = ReadChainState() ?? (0, "GENESIS");
        var next = Sha256Hex(state.lastHash + "\n" + line);
        WriteChainState(state.count + 1, next);
    }

    private static (int count, string lastHash)? ReadChainState()
    {
        if (!File.Exists(AppPaths.EventsChainPath)) return null;
        var lines = File.ReadAllLines(AppPaths.EventsChainPath, Encoding.UTF8);
        var count = 0;
        var last = "GENESIS";
        foreach (var l in lines)
        {
            if (l.StartsWith("count=", StringComparison.OrdinalIgnoreCase))
                _ = int.TryParse(l["count=".Length..], out count);
            else if (l.StartsWith("last=", StringComparison.OrdinalIgnoreCase))
                last = l["last=".Length..];
        }
        return (count, string.IsNullOrWhiteSpace(last) ? "GENESIS" : last);
    }

    private static void WriteChainState(int count, string lastHash)
    {
        var content = $"count={Math.Max(0, count)}\nlast={lastHash}\n";
        File.WriteAllText(AppPaths.EventsChainPath, content, Encoding.UTF8);
    }

    private static string Csv(string value)
    {
        var escaped = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
