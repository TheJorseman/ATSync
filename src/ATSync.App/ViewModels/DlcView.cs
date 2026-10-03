using ATSync.Core.Models;

namespace ATSync.App.ViewModels;

/// <summary>Wrapper de DlcInfo para la UI.</summary>
public sealed class DlcView
{
    public DlcInfo Info { get; }
    public string Id => Info.Id;
    public string Name => Info.Name;
    public string Category => Info.Category;
    public bool Owned => Info.Owned;
    public string Glyph => Owned ? "✓" : "·";
    public string StateColor => Owned ? "ok" : "muted";

    public DlcView(DlcInfo info) { Info = info; }
}