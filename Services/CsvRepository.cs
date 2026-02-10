using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            csv.WriteRecords(records);
        }
        
        // Move arquivo temporário para sobrescrever o original (atômico)
        File.Move(tmp, path, true);
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
