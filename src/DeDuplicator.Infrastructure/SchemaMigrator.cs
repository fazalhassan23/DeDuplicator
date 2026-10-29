using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace DeDuplicator.Infrastructure;

public static class SchemaMigrator
{
    public const int CurrentVersion = 2;

    public static void Upgrade(SqliteConnection db)
    {
        using var versionCommand = db.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version";
        int version = Convert.ToInt32(versionCommand.ExecuteScalar());
        if (version > CurrentVersion) throw new InvalidDataException($"Database schema {version} is newer than this application's schema {CurrentVersion}. Use the matching application; the database was not changed.");
        var assembly = typeof(SchemaMigrator).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Select(name => (Name: name, Match: Regex.Match(name, @"\.Migrations\.(\d{3})_[^.]+\.sql$")))
            .Where(x => x.Match.Success).Select(x => (x.Name, Version: int.Parse(x.Match.Groups[1].Value))).OrderBy(x => x.Version).ToArray();
        if (!migrations.Select(x => x.Version).SequenceEqual(Enumerable.Range(1, CurrentVersion))) throw new InvalidDataException("Embedded schema migrations are missing or out of sequence.");
        string Read(string name) { using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new StreamReader(stream); return reader.ReadToEnd(); }
        if (version == 0) ValidateLegacy(db, Read(migrations[0].Name));
        using var transaction = db.BeginTransaction();
        foreach (var migration in migrations.Where(x => x.Version > version))
        {
            using var command = db.CreateCommand(); command.Transaction = transaction;
            command.CommandText = Read(migration.Name); command.ExecuteNonQuery();
            command.CommandText = $"PRAGMA user_version={migration.Version}"; command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static void ValidateLegacy(SqliteConnection actual, string baseline)
    {
        static string[] Objects(SqliteConnection db)
        {
            using var command = db.CreateCommand();
            command.CommandText = "SELECT type || ':' || name || ':' || replace(replace(sql,char(10),''),char(13),'') FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name";
            using var reader = command.ExecuteReader(); var result = new List<string>();
            while (reader.Read()) result.Add(Regex.Replace(reader.GetString(0), @"\s+", " ").Trim());
            return result.ToArray();
        }
        var objects = Objects(actual);
        if (objects.Length == 0) return;
        using var expected = new SqliteConnection("Data Source=:memory:"); expected.Open();
        using (var command = expected.CreateCommand()) { command.CommandText = baseline; command.ExecuteNonQuery(); }
        if (!objects.SequenceEqual(Objects(expected))) throw new InvalidDataException("Unrecognized unversioned database. Preserve the journal and use a matching build or a reviewed migration; no schema change was committed.");
    }
}
