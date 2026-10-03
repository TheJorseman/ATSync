using ICSharpCode.SharpZipLib.Zip;

namespace ATSync.Core.Mods;

/// <summary>
/// Lectura de archivos .scs (formato zip sin compresión). Extrae entradas y las expone como stream.
/// </summary>
public sealed class ScsArchive : IDisposable
{
    private readonly ZipFile _zip;

    public string Path { get; }
    public IReadOnlyList<string> Entries { get; }

    public ScsArchive(string path)
    {
        Path = path;
        _zip = new ZipFile(path) { IsStreamOwner = true };
        var list = new List<string>();
        foreach (ZipEntry e in _zip)
            if (e.IsFile) list.Add(e.Name);
        Entries = list;
    }

    public Stream? OpenEntry(string name)
    {
        var ix = _zip.FindEntry(name, false);
        if (ix < 0) return null;
        return _zip.GetInputStream(ix);
    }

    public bool HasEntry(string name) => _zip.FindEntry(name, false) >= 0;

    public byte[]? ReadAllBytes(string name)
    {
        using var s = OpenEntry(name);
        if (s is null) return null;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public string? ReadAllText(string name)
    {
        var bytes = ReadAllBytes(name);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void Dispose() => _zip.Close();
}