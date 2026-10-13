using System.IO;
using System.Text;
namespace GuidedGrade.Services;
internal static class BoundedTextReader
{
    internal const int SourceBytes = 2 * 1024 * 1024;
    internal static string Read(string path, int maxBytes = SourceBytes) => ReadAsync(path, CancellationToken.None, maxBytes).GetAwaiter().GetResult();
    internal static async Task<string> ReadAsync(string path, CancellationToken token = default, int maxBytes = SourceBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192, FileOptions.Asynchronous);
        if (stream.Length > maxBytes) throw new IOException($"{Path.GetFileName(path)} exceeds the {maxBytes / 1024} KB read limit. No partial source was used.");
        using var buffer = new MemoryStream(); var bytes = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(bytes, token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > maxBytes) throw new IOException("File grew beyond the read limit. No partial source was used.");
            buffer.Write(bytes, 0, count);
        }
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, true);
        return await reader.ReadToEndAsync(token).ConfigureAwait(false);
    }
}
