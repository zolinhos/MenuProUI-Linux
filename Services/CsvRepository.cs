using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using MenuProUI.Models;

namespace MenuProUI.Services;

public sealed class CsvRepository
{
    private static CsvConfiguration Cfg => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        HeaderValidated = null
    };

    public (List<Client> clients, List<AccessEntry> accesses) Load()
    {
        Directory.CreateDirectory(AppPaths.AppDir);

        // Se ainda não existe clientes.csv, tenta migrar do modelo antigo
        if (!File.Exists(AppPaths.ClientsPath))
        {
            TryMigrateLegacySingleCsv();
        }

        var clients = File.Exists(AppPaths.ClientsPath) ? LoadCsv<Client>(AppPaths.ClientsPath) : new List<Client>();
        var accesses = File.Exists(AppPaths.AccessesPath) ? LoadCsv<AccessEntry>(AppPaths.AccessesPath) : new List<AccessEntry>();

        if (clients.Count == 0)
        {
            clients.Add(new Client { Nome = "Sem Cliente" });
            SaveClients(clients);
        }

        // saneamento
        foreach (var c in clients)
        {
            if (c.Id == Guid.Empty) c.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(c.Nome)) c.Nome = "Sem Cliente";
        }

        foreach (var a in accesses)
        {
            if (a.Id == Guid.Empty) a.Id = Guid.NewGuid();
            if (a.ClientId == Guid.Empty)
            {
                // joga no "Sem Cliente"
                var sem = clients.First();
                a.ClientId = sem.Id;
            }
        }

        return (clients, accesses);
    }

    public void SaveAll(List<Client> clients, List<AccessEntry> accesses)
    {
        SaveClients(clients);
        SaveAccesses(accesses);
    }

    public void SaveClients(IEnumerable<Client> clients)
        => SaveCsvAtomic(AppPaths.ClientsPath, clients.OrderBy(c => c.Nome));

    public void SaveAccesses(IEnumerable<AccessEntry> accesses)
        => SaveCsvAtomic(AppPaths.AccessesPath, accesses.OrderBy(a => a.Tipo).ThenBy(a => a.Apelido));

    private static List<T> LoadCsv<T>(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, Cfg);
        return csv.GetRecords<T>().ToList();
    }

    private static void SaveCsvAtomic<T>(string path, IEnumerable<T> records)
    {
        var tmp = path + ".tmp";
        using (var writer = new StreamWriter(tmp))
        using (var csv = new CsvWriter(writer, Cfg))
        {
            csv.WriteRecords(records);
        }
        File.Move(tmp, path, true);
    }

    // ---------------- MIGRAÇÃO ----------------
    // VERSÃO ANTIGA: um acessos.csv com coluna "Cliente" string.
    private sealed class LegacyAccess
    {
        public Guid Id { get; set; }
        public string Cliente { get; set; } = "Sem Cliente";
        public AccessType Tipo { get; set; }
        public string Apelido { get; set; } = "";
        public string? Host { get; set; }
        public int? Porta { get; set; }
        public string? Usuario { get; set; }
        public string? Url { get; set; }
        public string? Observacoes { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }

    private void TryMigrateLegacySingleCsv()
    {
        if (!File.Exists(AppPaths.AccessesPath)) return;

        var header = File.ReadLines(AppPaths.AccessesPath).FirstOrDefault() ?? "";
        var looksLegacy = header.Contains("cliente", StringComparison.OrdinalIgnoreCase)
                          && !header.Contains("clientid", StringComparison.OrdinalIgnoreCase);

        if (!looksLegacy) return;

        // carrega legado
        List<LegacyAccess> legacy;
        try
        {
            legacy = LoadCsv<LegacyAccess>(AppPaths.AccessesPath);
        }
        catch
        {
            // se o arquivo estiver muito estranho, não mexe
            return;
        }

        // cria clientes
        var clients = legacy.Select(l => (l.Cliente ?? "Sem Cliente").Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(x => x)
                            .Select(nome => new Client { Nome = string.IsNullOrWhiteSpace(nome) ? "Sem Cliente" : nome })
                            .ToList();

        if (clients.Count == 0) clients.Add(new Client { Nome = "Sem Cliente" });

        var map = clients.ToDictionary(c => c.Nome, c => c.Id, StringComparer.OrdinalIgnoreCase);

        // cria novos acessos
        var accesses = legacy.Select(l => new AccessEntry
        {
            Id = l.Id == Guid.Empty ? Guid.NewGuid() : l.Id,
            ClientId = map.TryGetValue(l.Cliente ?? "Sem Cliente", out var id) ? id : clients[0].Id,
            Tipo = l.Tipo,
            Apelido = l.Apelido ?? "",
            Host = l.Host,
            Porta = l.Porta,
            Usuario = l.Usuario,
            Url = l.Url,
            Observacoes = l.Observacoes,
            CriadoEm = l.CriadoEm == default ? DateTime.UtcNow : l.CriadoEm,
            AtualizadoEm = l.AtualizadoEm == default ? DateTime.UtcNow : l.AtualizadoEm
        }).ToList();

        // backup do legado
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backup = Path.Combine(AppPaths.AppDir, $"acessos_legacy_backup_{stamp}.csv");
        File.Copy(AppPaths.AccessesPath, backup, true);

        // grava no novo formato
        SaveClients(clients);
        SaveAccesses(accesses);
    }
}
