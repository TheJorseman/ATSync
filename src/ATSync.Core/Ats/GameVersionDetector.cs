using System.Text.RegularExpressions;
using ATSync.Core.Models;

namespace ATSync.Core.Ats;

/// <summary>
/// Detecta la versión del juego desde game.log.txt o, como fallback, desde version.scs.
/// </summary>
public sealed class GameVersionDetector
{
    private static readonly Regex RxVer = new(@"American Truck Simulator init ver\.(\d+\.\d+\.\d+\.\d+)([se])\s*\(rev\. (\w+)\)", RegexOptions.Compiled);

    public string? AtsHomeDir { get; }

    public GameVersionDetector(string? atsHomeDir = null)
    {
        AtsHomeDir = atsHomeDir ?? Core.Util.AppPaths.DefaultAtsHomeDir();
    }

    /// <summary>Lee el game.log.txt más reciente del usuario y extrae la versión.</summary>
    public GameVersion? DetectFromGameLog()
    {
        var log = Path.Combine(AtsHomeDir ?? "", "game.log.txt");
        if (!File.Exists(log)) return null;
        var line = ReadFirstMatchingLine(log, RxVer);
        return line is null ? null : ParseLine(line);
    }

    private static string? ReadFirstMatchingLine(string path, Regex rx)
    {
        // Sólo las primeras ~15 son suficientes — game.log empieza con init
        using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs);
        for (int i = 0; i < 30 && !sr.EndOfStream; i++)
        {
            var l = sr.ReadLine();
            if (l is not null && rx.IsMatch(l)) return l;
        }
        return null;
    }

    private static GameVersion? ParseLine(string line)
    {
        var m = RxVer.Match(line);
        if (!m.Success) return null;
        var v = GameVersion.Parse(m.Groups[1].Value + m.Groups[2].Value);
        return v;
    }
}