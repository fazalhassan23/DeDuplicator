using System.Text.Json;
using DeDuplicator.Core;
using Microsoft.Data.Sqlite;

namespace DeDuplicator.Infrastructure;

public sealed class ScanStore
{
    private readonly string connectionString;
    public ScanStore(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=FULL;
            CREATE TABLE IF NOT EXISTS scans(id TEXT PRIMARY KEY, started TEXT, state TEXT, profile TEXT);
            CREATE TABLE IF NOT EXISTS files(id INTEGER PRIMARY KEY, scan TEXT, path TEXT, identity TEXT, length INTEGER, record TEXT, hash TEXT, sample TEXT, group_id INTEGER);
            CREATE UNIQUE INDEX IF NOT EXISTS physical ON files(scan,identity);
            CREATE INDEX IF NOT EXISTS sizes ON files(scan,length);
            CREATE INDEX IF NOT EXISTS hashes ON files(scan,length,hash);
            CREATE INDEX IF NOT EXISTS groups ON files(scan,group_id);
            CREATE TABLE IF NOT EXISTS issues(id INTEGER PRIMARY KEY, scan TEXT, path TEXT, reason TEXT);
            CREATE TABLE IF NOT EXISTS journal(id TEXT PRIMARY KEY, plan TEXT, source TEXT, destination TEXT, action TEXT, state TEXT, detail TEXT, record TEXT, created TEXT, hash TEXT);
            """;
        cmd.ExecuteNonQuery();
    }
    public SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public void Execute(string sql, params (string, object?)[] parameters)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var (key, value) in parameters) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
    public void Start(string id, ScanProfile profile) => Execute("INSERT INTO scans VALUES($id,$time,'Running',$profile)", ("$id", id), ("$time", DateTime.UtcNow.ToString("O")), ("$profile", JsonSerializer.Serialize(profile)));
    public void Issue(string scan, string path, string reason) => Execute("INSERT INTO issues(scan,path,reason) VALUES($scan,$path,$reason)", ("$scan", scan), ("$path", path), ("$reason", reason));
    public void Add(string scan, FileRecord file) => Execute("INSERT OR IGNORE INTO files(scan,path,identity,length,record) VALUES($scan,$path,$identity,$length,$record)", ("$scan", scan), ("$path", file.Path), ("$identity", file.Identity), ("$length", file.Length), ("$record", JsonSerializer.Serialize(file)));
    public IEnumerable<long> CandidateSizes(string scan)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT length FROM files WHERE scan=$scan GROUP BY length HAVING count(*)>1 ORDER BY length"; cmd.Parameters.AddWithValue("$scan", scan);
        using var reader = cmd.ExecuteReader(); while (reader.Read()) yield return reader.GetInt64(0);
    }
    public IEnumerable<(long Id, FileRecord File, string? Hash)> Files(string scan, long length)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT id,record,hash FROM files WHERE scan=$scan AND length=$length ORDER BY id"; cmd.Parameters.AddWithValue("$scan", scan); cmd.Parameters.AddWithValue("$length", length);
        using var reader = cmd.ExecuteReader(); while (reader.Read()) yield return (reader.GetInt64(0), JsonSerializer.Deserialize<FileRecord>(reader.GetString(1))!, reader.IsDBNull(2) ? null : reader.GetString(2));
    }
    public IReadOnlyList<DuplicateGroup> Groups(string scan, int offset = 0, int limit = 100, string search = "", string sort = "space")
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        var order = sort switch { "count" => "count(*) DESC", "type" => "min(path)", "location" => "min(path)", "size" => "max(length) DESC", _ => "max(length)*(count(*)-1) DESC" };
        cmd.CommandText = $"SELECT group_id FROM files WHERE scan=$scan AND group_id IS NOT NULL GROUP BY group_id HAVING count(*)>1 AND max(CASE WHEN instr(lower(path),lower($search))>0 THEN 1 ELSE 0 END)=1 ORDER BY {order},group_id LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$scan", scan); cmd.Parameters.AddWithValue("$search", search); cmd.Parameters.AddWithValue("$limit", Math.Min(limit, 25)); cmd.Parameters.AddWithValue("$offset", offset);
        var ids = new List<long>(); using (var reader = cmd.ExecuteReader()) while (reader.Read()) ids.Add(reader.GetInt64(0));
        var result = new List<DuplicateGroup>();
        foreach (long id in ids)
        {
            using var members = db.CreateCommand(); members.CommandText = "SELECT record,hash FROM files WHERE scan=$scan AND group_id=$id ORDER BY path LIMIT 501"; members.Parameters.AddWithValue("$scan", scan); members.Parameters.AddWithValue("$id", id);
            using var reader = members.ExecuteReader(); var files = new List<FileRecord>(); string hash = "";
            while (reader.Read()) { files.Add(JsonSerializer.Deserialize<FileRecord>(reader.GetString(0))!); hash = reader.GetString(1); }
            bool partial = files.Count > 500; if (partial) files.RemoveAt(500);
            result.Add(new(id, files, hash, partial));
        }
        return result;
    }
    public DuplicateGroup GroupMembers(string scan, long group, int offset)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT record,hash FROM files WHERE scan=$scan AND group_id=$group ORDER BY path LIMIT 500 OFFSET $offset";
        cmd.Parameters.AddWithValue("$scan", scan); cmd.Parameters.AddWithValue("$group", group); cmd.Parameters.AddWithValue("$offset", offset);
        using var reader = cmd.ExecuteReader(); var files = new List<FileRecord>(); string hash = "";
        while (reader.Read()) { files.Add(JsonSerializer.Deserialize<FileRecord>(reader.GetString(0))!); hash = reader.GetString(1); }
        return new(group, files, hash, true);
    }
    public IReadOnlyList<(string Id, string Started, string State)> History()
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT id,started,state FROM scans ORDER BY started DESC LIMIT 100";
        using var reader = cmd.ExecuteReader(); var result = new List<(string, string, string)>(); while (reader.Read()) result.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2))); return result;
    }
    public string Diagnostics(string scan)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT path,reason FROM issues WHERE scan=$scan ORDER BY id LIMIT 500"; cmd.Parameters.AddWithValue("$scan", scan);
        using var reader = cmd.ExecuteReader(); var lines = new List<string>(); while (reader.Read()) lines.Add(reader.GetString(0) + ": " + reader.GetString(1)); return string.Join(Environment.NewLine, lines);
    }
    public void Recover() { Execute("UPDATE scans SET state='Interrupted; rescan required' WHERE state='Running'"); Execute("UPDATE journal SET state='Interrupted; inspect destination before retry' WHERE state IN ('Started','Copied','Restoring')"); }
}
