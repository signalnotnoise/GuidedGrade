using System.IO;
using System.Text;
namespace GuidedGrade.Services;
internal sealed record FsLogSnapshot(string Name, DateTimeOffset Time, string Content, int BuildNumber = 0);
internal sealed record FsLogDocument(float Version, IReadOnlyList<FsLogSnapshot> Snapshots)
{
    internal int BuildCount => Snapshots.Select(s => s.BuildNumber).Distinct().Count();
}
internal static class FsLogReader
{
    internal static FsLogDocument Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("Log exceeds the 32 MB viewer limit.");
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != 0x474C5346) throw new InvalidDataException("Not an FSLG log.");
        var version = reader.ReadSingle();
        if (version != 2f) throw new InvalidDataException($"Unsupported FSLG version: {version}.");
        var count = reader.ReadUInt32();
        if (count == 0 || count > 256) throw new InvalidDataException("Invalid file count.");
        byte[] Chunk(uint size)
        {
            if (size > stream.Length - stream.Position) throw new InvalidDataException("Truncated log chunk.");
            return reader.ReadBytes(checked((int)size));
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        var names = Enumerable.Range(0, (int)count).Select(_ => encoding.GetString(Chunk(reader.ReadUInt32()))).ToArray();
        var snapshots = new List<FsLogSnapshot>();
        var buildNumber = 0;
        while (stream.Position < stream.Length)
        {
            buildNumber++;
            var timestamp = reader.ReadUInt64();
            var time = DateTimeOffset.FromUnixTimeSeconds(checked((long)timestamp));
            foreach (var name in names)
            {
                if (snapshots.Count >= 10000) throw new InvalidDataException("Log exceeds the 10,000 snapshot viewer limit.");
                var bytes = Chunk(reader.ReadUInt32());
                for (var i = 0; i < bytes.Length; i++) bytes[i] = unchecked((byte)(bytes[i] - 128));
                snapshots.Add(new(name, time, encoding.GetString(bytes), buildNumber));
            }
        }
        return new(version, snapshots);
    }
}
