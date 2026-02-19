using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using MenuProUI.Models;

namespace MenuProUI.Services;

/// <summary>
/// Gerencia a persistência de dados em arquivos CSV.
/// Responsável por carregar, salvar e migrar dados de clientes e acessos.
/// </summary>
public sealed class CsvRepository
{
    private sealed class ClientMap : ClassMap<Client>
    {
        public ClientMap()
        {
            Map(x => x.Id).Name("Id");
            Map(x => x.Nome).Name("Nome");
            Map(x => x.Observacoes).Name("Observacoes");
            Map(x => x.CriadoEm).Name("CriadoEm");
            Map(x => x.AtualizadoEm).Name("AtualizadoEm");
        }
    }

    private sealed class AccessEntryMap : ClassMap<AccessEntry>
    {
        public AccessEntryMap()
        {
            Map(x => x.Id).Name("Id");
            Map(x => x.ClientId).Name("ClientId");
            Map(x => x.Tipo).Name("Tipo");
            Map(x => x.Apelido).Name("Apelido");
            Map(x => x.Host).Name("Host");
            Map(x => x.Porta).Name("Porta");
            Map(x => x.Usuario).Name("Usuario");
            Map(x => x.Dominio).Name("Dominio");
            Map(x => x.RdpIgnoreCert).Name("RdpIgnoreCert");
            Map(x => x.RdpFullScreen).Name("RdpFullScreen");
            Map(x => x.RdpDynamicResolution).Name("RdpDynamicResolution");
            Map(x => x.RdpWidth).Name("RdpWidth");
            Map(x => x.RdpHeight).Name("RdpHeight");
            Map(x => x.Url).Name("Url");
            Map(x => x.Observacoes).Name("Observacoes");
            Map(x => x.IsFavorite).Name("IsFavorite");
            Map(x => x.OpenCount).Name("OpenCount");
            Map(x => x.LastOpenedAt).Name("LastOpenedAt");
            Map(x => x.CriadoEm).Name("CriadoEm");
            Map(x => x.AtualizadoEm).Name("AtualizadoEm");
        }
    }

    /// <summary>Configuração do CsvHelper para leitura/escrita consistente</summary>
    private static CsvConfiguration Cfg => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        HeaderValidated = null
    };

    /// <summary>
    /// Carrega todos os clientes e acessos do armazenamento CSV.
    /// Realiza saneamento automático de dados e migração de versões antigas.
    /// </summary>
    /// <returns>Tupla contendo listas de clientes e acessos carregados</returns>
    public (List<Client> clients, List<AccessEntry> accesses) Load()
    {
        Directory.CreateDirectory(AppPaths.AppDir);

        // Tenta migrar dados do modelo antigo se necessário
        if (!File.Exists(AppPaths.ClientsPath))
        {
            TryMigrateLegacySingleCsv();
        }

        // Carrega clientes e acessos dos arquivos CSV
        var clients = File.Exists(AppPaths.ClientsPath) ? LoadCsv<Client>(AppPaths.ClientsPath) : new List<Client>();
        var accesses = File.Exists(AppPaths.AccessesPath) ? LoadCsv<AccessEntry>(AppPaths.AccessesPath) : new List<AccessEntry>();
        EnsureEventsFile();

        // Garante sempre ter pelo menos um cliente padrão
        if (clients.Count == 0)
        {
            clients.Add(new Client { Nome = "Sem Cliente" });
            SaveClients(clients);
        }

        // Saneamento de clientes: garante IDs válidos e nomes não vazios
        foreach (var c in clients)
        {
            if (c.Id == Guid.Empty) c.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(c.Nome)) c.Nome = "Sem Cliente";
        }

        // Saneamento de acessos: garante IDs válidos e vinculação de cliente
        foreach (var a in accesses)
        {
            if (a.Id == Guid.Empty) a.Id = Guid.NewGuid();
            if (a.ClientId == Guid.Empty)
            {
                // Associa a "Sem Cliente" se não tiver cliente vinculado
                var sem = clients.First();
                a.ClientId = sem.Id;
            }
        }

        return (clients, accesses);
    }

    public string ExportCsvSnapshot(bool formulaProtection = false)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var outDir = Path.Combine(AppPaths.ExportDir, $"export_{stamp}");
        Directory.CreateDirectory(outDir);

        var clientsOut = Path.Combine(outDir, "clientes.csv");
        var accessesOut = Path.Combine(outDir, "acessos.csv");
        File.Copy(AppPaths.ClientsPath, clientsOut, true);
        File.Copy(AppPaths.AccessesPath, accessesOut, true);
        if (File.Exists(AppPaths.EventsPath))
        {
            var eventsOut = Path.Combine(outDir, "eventos.csv");
            File.Copy(AppPaths.EventsPath, eventsOut, true);
            if (formulaProtection) ProtectCsvAgainstFormulaInjection(eventsOut);
        }

        if (formulaProtection)
        {
            ProtectCsvAgainstFormulaInjection(clientsOut);
            ProtectCsvAgainstFormulaInjection(accessesOut);
        }

        return outDir;
    }

    public string CreateBackupSnapshot()
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(AppPaths.BackupsDir, $"backup_{stamp}");
        Directory.CreateDirectory(backupDir);

        if (File.Exists(AppPaths.ClientsPath))
            File.Copy(AppPaths.ClientsPath, Path.Combine(backupDir, "clientes.csv"), true);
        if (File.Exists(AppPaths.AccessesPath))
            File.Copy(AppPaths.AccessesPath, Path.Combine(backupDir, "acessos.csv"), true);
        if (File.Exists(AppPaths.EventsPath))
            File.Copy(AppPaths.EventsPath, Path.Combine(backupDir, "eventos.csv"), true);
        if (File.Exists(AppPaths.EventsChainPath))
            File.Copy(AppPaths.EventsChainPath, Path.Combine(backupDir, "eventos.chain"), true);

        return backupDir;
    }

    public void RestoreBackupSnapshot(string backupDir)
    {
        File.Copy(Path.Combine(backupDir, "clientes.csv"), AppPaths.ClientsPath, true);
        File.Copy(Path.Combine(backupDir, "acessos.csv"), AppPaths.AccessesPath, true);

        var eventsBackup = Path.Combine(backupDir, "eventos.csv");
        if (File.Exists(eventsBackup))
            File.Copy(eventsBackup, AppPaths.EventsPath, true);
    }

    public string? GetLatestBackupSnapshot()
    {
        if (!Directory.Exists(AppPaths.BackupsDir)) return null;
        var latest = Directory.GetDirectories(AppPaths.BackupsDir, "backup_*")
            .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return latest;
    }

    public (bool hasErrors, string report) ValidateImportPreview(string clientsPath, string accessesPath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateHeader(clientsPath, new[] { "id", "nome" }, errors, "clientes.csv");
        ValidateHeader(accessesPath, new[] { "id", "clientid", "tipo" }, errors, "acessos.csv");

        if (!errors.Any())
        {
            var clientsCount = Math.Max(0, File.ReadLines(clientsPath).Count() - 1);
            var accessesCount = Math.Max(0, File.ReadLines(accessesPath).Count() - 1);
            if (clientsCount == 0) warnings.Add("clientes.csv sem registros.");
            if (accessesCount == 0) warnings.Add("acessos.csv sem registros.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("PREVIA DE IMPORTACAO");
        sb.AppendLine("====================");
        if (errors.Count > 0)
        {
            sb.AppendLine("ERROS:");
            foreach (var e in errors) sb.AppendLine("- " + e);
        }

        if (warnings.Count > 0)
        {
            sb.AppendLine("AVISOS:");
            foreach (var w in warnings) sb.AppendLine("- " + w);
        }

        if (errors.Count == 0) sb.AppendLine("Status: importacao permitida.");
        return (errors.Count > 0, sb.ToString());
    }

    public void ImportCsvFiles(string clientsImportPath, string accessesImportPath, string? eventsImportPath = null)
    {
        var backupDir = CreateBackupSnapshot();
        try
        {
            File.Copy(clientsImportPath, AppPaths.ClientsPath, true);
            File.Copy(accessesImportPath, AppPaths.AccessesPath, true);
            if (!string.IsNullOrWhiteSpace(eventsImportPath) && File.Exists(eventsImportPath))
                File.Copy(eventsImportPath, AppPaths.EventsPath, true);
        }
        catch
        {
            RestoreBackupSnapshot(backupDir);
            throw;
        }
    }

    /// <summary>Salva todos os clientes e acessos no armazenamento CSV</summary>
    /// <param name="clients">Lista de clientes a ser salva</param>
    /// <param name="accesses">Lista de acessos a ser salva</param>
    public void SaveAll(List<Client> clients, List<AccessEntry> accesses)
    {
        SaveClients(clients);
        SaveAccesses(accesses);
    }

    /// <summary>Salva apenas os clientes no arquivo CSV</summary>
    /// <param name="clients">Clientes a serem salvos (ordenados por nome)</param>
    public void SaveClients(IEnumerable<Client> clients)
        => SaveCsvAtomic(AppPaths.ClientsPath, clients.OrderBy(c => c.Nome));

    /// <summary>Salva apenas os acessos no arquivo CSV</summary>
    /// <param name="accesses">Acessos a serem salvos (ordenados por tipo e apelido)</param>
    public void SaveAccesses(IEnumerable<AccessEntry> accesses)
        => SaveCsvAtomic(AppPaths.AccessesPath, accesses.OrderBy(a => a.Tipo).ThenBy(a => a.Apelido));

    /// <summary>
    /// Carrega objetos genéricos de um arquivo CSV.
    /// </summary>
    /// <typeparam name="T">Tipo de objeto a carregar</typeparam>
    /// <param name="path">Caminho do arquivo CSV</param>
    /// <returns>Lista de objetos carregados</returns>
    private static List<T> LoadCsv<T>(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, Cfg);
        csv.Context.RegisterClassMap<ClientMap>();
        csv.Context.RegisterClassMap<AccessEntryMap>();
        return csv.GetRecords<T>().ToList();
    }

    /// <summary>
    /// Salva objetos em arquivo CSV de forma atômica (usa arquivo temporário).
    /// Isso evita corrupção de dados se houver erro durante a escrita.
    /// </summary>
    /// <typeparam name="T">Tipo de objeto a salvar</typeparam>
    /// <param name="path">Caminho do arquivo CSV destino</param>
    /// <param name="records">Registros a serem salvos</param>
    private static void SaveCsvAtomic<T>(string path, IEnumerable<T> records)
    {
        // Escreve em arquivo temporário primeiro
        var tmp = path + ".tmp";
        using (var writer = new StreamWriter(tmp))
        using (var csv = new CsvWriter(writer, Cfg))
        {
            csv.Context.RegisterClassMap<ClientMap>();
            csv.Context.RegisterClassMap<AccessEntryMap>();
            csv.WriteRecords(records);
        }
        
        // Move arquivo temporário para sobrescrever o original (atômico)
        File.Move(tmp, path, true);
    }

    private static void EnsureEventsFile()
    {
        if (File.Exists(AppPaths.EventsPath)) return;
        File.WriteAllText(AppPaths.EventsPath, "TimestampUtc,Action,EntityType,EntityName,Details\n");
    }

    private static void ProtectCsvAgainstFormulaInjection(string path)
    {
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return;

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = SplitCsvLine(lines[i]);
            for (var c = 0; c < cells.Count; c++)
                cells[c] = ProtectCell(cells[c]);
            lines[i] = ToCsvLine(cells);
        }

        File.WriteAllLines(path, lines);
    }

    private static string ProtectCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var trimmed = value.TrimStart();
        if (trimmed.Length == 0) return value;
        var first = trimmed[0];
        if (first is '=' or '+' or '-' or '@')
            return "'" + value;
        return value;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        result.Add(sb.ToString());
        return result;
    }

    private static string ToCsvLine(IEnumerable<string> cells)
    {
        return string.Join(",", cells.Select(EscapeCsv));
    }

    private static string EscapeCsv(string value)
    {
        var v = value ?? string.Empty;
        if (v.Contains('"')) v = v.Replace("\"", "\"\"");
        return (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            ? $"\"{v}\""
            : v;
    }

    private static void ValidateHeader(string path, string[] requiredColumns, List<string> errors, string label)
    {
        if (!File.Exists(path))
        {
            errors.Add($"{label} nao encontrado.");
            return;
        }

        var header = File.ReadLines(path).FirstOrDefault() ?? "";
        var cols = header.Split(',').Select(x => x.Trim().Trim('"').ToLowerInvariant()).ToHashSet();
        foreach (var req in requiredColumns)
        {
            if (!cols.Contains(req.ToLowerInvariant()))
                errors.Add($"{label} sem coluna obrigatoria: {req}.");
        }
    }

    // ==================== MIGRAÇÃO DE DADOS ====================
    /// <summary>
    /// Classe auxiliar para ler dados da versão legada do arquivo CSV.
    /// A versão antiga tinha uma coluna "Cliente" string em vez de "ClientId".
    /// </summary>
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

    /// <summary>
    /// Tenta migrar dados da estrutura antiga (um arquivo CSV com coluna Cliente string)
    /// para a nova estrutura (dois arquivos CSV com ClientId GUID).
    /// Cria backup automático do arquivo legado antes de converter.
    /// </summary>
    private void TryMigrateLegacySingleCsv()
    {
        if (!File.Exists(AppPaths.AccessesPath)) return;

        // Verifica se é tipo legado lendo o header
        var header = File.ReadLines(AppPaths.AccessesPath).FirstOrDefault() ?? "";
        var looksLegacy = header.Contains("cliente", StringComparison.OrdinalIgnoreCase)
                          && !header.Contains("clientid", StringComparison.OrdinalIgnoreCase);

        if (!looksLegacy) return;

        // Carrega dados legados
        List<LegacyAccess> legacy;
        try
        {
            legacy = LoadCsv<LegacyAccess>(AppPaths.AccessesPath);
        }
        catch
        {
            // Se o arquivo estiver muito estranho, não prossegue
            return;
        }

        // Cria clientes únicos a partir dos nomes na coluna "Cliente"
        var clients = legacy.Select(l => (l.Cliente ?? "Sem Cliente").Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(x => x)
                            .Select(nome => new Client { Nome = string.IsNullOrWhiteSpace(nome) ? "Sem Cliente" : nome })
                            .ToList();

        if (clients.Count == 0) clients.Add(new Client { Nome = "Sem Cliente" });

        // Cria mapa de nomes para IDs para vincular acessos aos clientes corretos
        var map = clients.ToDictionary(c => c.Nome, c => c.Id, StringComparer.OrdinalIgnoreCase);

        // Converte acessos legados para novo formato
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

        // Cria backup do arquivo legado com timestamp
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backup = Path.Combine(AppPaths.AppDir, $"acessos_legacy_backup_{stamp}.csv");
        File.Copy(AppPaths.AccessesPath, backup, true);

        // Salva dados migrados no novo formato
        SaveClients(clients);
        SaveAccesses(accesses);
    }
}
