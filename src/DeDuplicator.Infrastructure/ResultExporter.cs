using System.Text;
using System.Text.Json;
using DeDuplicator.Core;

namespace DeDuplicator.Infrastructure;

public static class ResultExporter
{
    public static string CsvCell(string value)
    {
        if (value.Length > 0 && ("=+-@\t\r\n".Contains(value[0]) || value.TrimStart().Length > 0 && "=+-@".Contains(value.TrimStart()[0]))) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public static async Task ExportAsync(ScanStore store, string scan, string path, bool json, CancellationToken token)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var db = store.Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT group_id,record,hash FROM files WHERE scan=$scan AND group_id IN (SELECT group_id FROM files WHERE scan=$scan AND group_id IS NOT NULL GROUP BY group_id HAVING count(*)>1) ORDER BY group_id,id";
        cmd.Parameters.AddWithValue("$scan", scan); using var reader = cmd.ExecuteReader();
        if (json)
        {
            using var writer = new Utf8JsonWriter(output, new() { Indented = true }); writer.WriteStartArray();
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                JsonSerializer.Serialize(writer, new { GroupId = reader.GetInt64(0), File = JsonSerializer.Deserialize<FileRecord>(reader.GetString(1)), Hash = reader.GetString(2), Verification = "Verified at scan time; revalidation required" });
                await writer.FlushAsync(token);
            }
            writer.WriteEndArray(); await writer.FlushAsync(token);
        }
        else
        {
            await using var writer = new StreamWriter(output, new UTF8Encoding(true)); await writer.WriteLineAsync("Group,Path,Length,ModifiedUtc,Identity,ReviewRequired,Verification");
            while (reader.Read())
            {
                var file = JsonSerializer.Deserialize<FileRecord>(reader.GetString(1))!;
                token.ThrowIfCancellationRequested(); await writer.WriteLineAsync(string.Join(',', CsvCell(reader.GetInt64(0).ToString()), CsvCell(file.Path), CsvCell(file.Length.ToString()), CsvCell(file.ModifiedUtc.ToString("O")), CsvCell(file.Identity), CsvCell(file.ReviewRequired.ToString()), CsvCell("Verified at scan time; revalidation required")));
            }
        }
    }
}
