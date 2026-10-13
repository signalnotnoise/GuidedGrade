using Microsoft.Data.Sqlite;
namespace GuidedGrade.Services;
internal sealed class ReviewDraftStore : System.Collections.Generic.IReadOnlyDictionary<string,string>
{
    private readonly string _path;
    private readonly BoundedCache<string> _cache = new(64);
    internal ReviewDraftStore(string path)
    {
        _path = path;
        using var connection = Open(); using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS ReviewDrafts(Context TEXT PRIMARY KEY, Text TEXT NOT NULL, UpdatedUtc TEXT NOT NULL)"; cmd.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path }.ToString()); c.Open(); return c; }
    public string this[string key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set
        {
            if (value.Length > 1024 * 1024) throw new InvalidOperationException("Feedback draft exceeds the 1 MB text limit. Export it before adding more text.");
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO ReviewDrafts(Context,Text,UpdatedUtc) VALUES (@key,@text,@time) ON CONFLICT(Context) DO UPDATE SET Text=excluded.Text,UpdatedUtc=excluded.UpdatedUtc";
            cmd.Parameters.AddWithValue("@key",key); cmd.Parameters.AddWithValue("@text",value); cmd.Parameters.AddWithValue("@time",DateTime.UtcNow.ToString("O")); cmd.ExecuteNonQuery();
            _cache[key] = value;
        }
    }
    public IEnumerable<string> Keys
    {
        get
        {
            using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT Context FROM ReviewDrafts";
            using var reader=cmd.ExecuteReader(); var keys=new List<string>(); while(reader.Read()) keys.Add(reader.GetString(0)); return keys;
        }
    }
    public IEnumerable<string> Values => Keys.Select(key => this[key]);
    public int Count { get { using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT COUNT(*) FROM ReviewDrafts"; return Convert.ToInt32(cmd.ExecuteScalar()); } }
    public bool ContainsKey(string key) => TryGetValue(key,out _);
    public bool TryGetValue(string key,out string value)
    {
        if(_cache.TryGetValue(key,out value!)) return true;
        using var c=Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT Text FROM ReviewDrafts WHERE Context=@key";cmd.Parameters.AddWithValue("@key",key);
        var stored = cmd.ExecuteScalar() as string; value = stored ?? ""; if (stored == null) return false; _cache[key]=value; return true;
    }
    public IEnumerator<KeyValuePair<string,string>> GetEnumerator() => Keys.Select(key => new KeyValuePair<string,string>(key,this[key])).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
