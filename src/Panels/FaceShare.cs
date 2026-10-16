using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit;

// A watch page as a small JSON file (or text on the clipboard, for Discord): its name, its grid, its widgets with their looks,
// colours, designs and buttons - what the config keeps for a page, nothing else (no other page, no setting).
//   {"OrbitFace":1,"Name":"VRChat","Grid":"6x4","Widgets":[{"Type":"time","X":0,"Y":0,"W":3,"H":2}, ...]}
// Reading one back is careful: at most 64 KB, the marker and a known grid; every widget checked - a kind this version does not know
// left out (the editor could do nothing with it), a look / design / button it does not know dropped, and the page then put through
// WatchWidgets.Clean (inside the grid, no overlap, a size it offers). A file with nothing usable left is refused. An import always
// ADDS a page after the others (8 at most): the pages you have are never touched.
public static class FaceShare
{
    public const int Format = 1, MaxBytes = 64 * 1024, MaxWidgets = 64, MaxName = 40, MaxPages = 8;
    // the clipboard's text: the JSON when it fits a Discord message, else a short code - "orbitface:" and the JSON deflated, in base64
    public const string CodePrefix = "orbitface:";
    public const int DiscordMax = 1900;

    // where the in-VR export writes (and the in-VR import lists): Documents\OrbitOverlay\faces. Tests point it at a scratch folder.
    public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OrbitOverlay", "faces");

    // the clipboard (the main thread is STA); tests swap in their own so they never touch the real one
    public static Action<string> SetClipboard = t => System.Windows.Forms.Clipboard.SetText(t);
    public static Func<string> GetClipboard = () => System.Windows.Forms.Clipboard.ContainsText() ? System.Windows.Forms.Clipboard.GetText() : null;

    sealed class FaceFile
    {
        public int OrbitFace { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }
        public string Grid { get; set; }
        public List<WatchWidget> Widgets { get; set; }
    }

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    static readonly JsonSerializerOptions Compact = new();
    static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true, MaxDepth = 8, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // page i of the face as text (indented for a file, one line for the clipboard)
    public static string Export(WristConfig w, int i, bool compact = false)
    {
        var f = new FaceFile
        {
            OrbitFace = Format,
            Name = i <= 0 ? w.Name : w.Pages[i - 1].Name,
            Grid = WatchPages.GridId(w, i),
            Widgets = (WatchPages.Widgets(w, i) ?? new List<WatchWidget>()).Select(Copy).ToList(),
        };
        return JsonSerializer.Serialize(f, compact ? Compact : Indented);
    }

    // what Copy as text puts on the clipboard (see CodePrefix)
    public static string ClipboardText(WristConfig w, int i)
    {
        string json = Export(w, i, compact: true);
        if (json.Length <= DiscordMax) return json;
        using var ms = new MemoryStream();
        using (var z = new DeflateStream(ms, CompressionLevel.SmallestSize, true)) z.Write(Encoding.UTF8.GetBytes(json));
        return CodePrefix + Convert.ToBase64String(ms.ToArray());
    }

    // a code back to its JSON: never more than MaxBytes of it (a code made to blow up refuses)
    static string Decode(string text, int at, out string error)
    {
        error = null;
        int i = at + CodePrefix.Length, j = i;
        while (j < text.Length && (char.IsAsciiLetterOrDigit(text[j]) || text[j] is '+' or '/' or '=')) j++;
        byte[] packed;
        try { packed = Convert.FromBase64String(text[i..j]); }
        catch (FormatException) { error = "That code is broken (copy all of it)."; return null; }
        try
        {
            using var z = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
            var buf = new byte[MaxBytes + 1];
            int n = 0, r;
            while (n < buf.Length && (r = z.Read(buf, n, buf.Length - n)) > 0) n += r;
            if (n > MaxBytes) { error = "That is too big for a watch page."; return null; }
            return Encoding.UTF8.GetString(buf, 0, n);
        }
        catch (InvalidDataException) { error = "That code is broken (copy all of it)."; return null; }
    }

    static WatchWidget Copy(WatchWidget x) => new()
    {
        Type = x.Type, X = x.X, Y = x.Y, W = x.W, H = x.H, Style = x.Style, Color = x.Color, Color2 = x.Color2,
        Items = x.Items == null ? null : new List<string>(x.Items), Variant = x.Variant,
    };

    // A page from text (a file's, the clipboard's - the JSON may sit inside a Discord message or a code block). Null + why, else
    // the page and what was left out.
    public static WatchPage Parse(string text, out string error, out int dropped)
    {
        error = null;
        dropped = 0;
        if (string.IsNullOrWhiteSpace(text)) { error = "There is nothing to read."; return null; }
        if (text.Length > MaxBytes) { error = "That is too big for a watch page."; return null; }
        int code = text.IndexOf(CodePrefix, StringComparison.OrdinalIgnoreCase);
        if (code >= 0 && (text = Decode(text, code, out error)) == null) return null;
        int a = text.IndexOf('{'), b = text.LastIndexOf('}');
        if (a < 0 || b <= a) { error = "That is not a watch page."; return null; }
        FaceFile f;
        try { f = JsonSerializer.Deserialize<FaceFile>(text.AsSpan(a, b - a + 1), Read); }
        catch (Exception) { error = "That is not a watch page (it does not read as one)."; return null; }
        if (f == null || f.OrbitFace < 1) { error = "That is not a watch page."; return null; }
        if (f.OrbitFace > Format) { error = "That page comes from a newer OrbitOverlay: update first."; return null; }
        if (f.Grid is not ("6x4" or "4x3")) { error = "That page has a grid this version does not know."; return null; }
        if (f.Widgets == null || f.Widgets.Count == 0) { error = "That page has no widgets."; return null; }
        if (f.Widgets.Count > MaxWidgets) { error = "That page has too many widgets."; return null; }
        var list = new List<WatchWidget>();
        foreach (var x in f.Widgets)
        {
            if (x == null || string.IsNullOrEmpty(x.Type) || x.Type.Length > 32 || WatchWidgets.Find(x.Type) == null) { dropped++; continue; }
            list.Add(CleanLook(x));
        }
        var grid = GridSpec.Of(f.Grid);
        int known = list.Count;
        list = WatchWidgets.Clean(list, grid);
        dropped += known - list.Count;
        if (list.Count == 0) { error = "Nothing on that page fits this version's watch."; return null; }
        return new WatchPage { Name = CleanName(f.Name), Grid = f.Grid, Widgets = list };
    }

    // a widget's own look, its design and its buttons: only what this version knows (the rest: as if never set)
    static WatchWidget CleanLook(WatchWidget x)
    {
        var w = Copy(x);
        if (w.Style != null && Array.IndexOf(Bento.Styles, w.Style) <= 0) w.Style = null;
        if (w.Variant != null && !Array.Exists(WatchWidgets.VariantsOf(w.Type) ?? Array.Empty<WatchWidgets.Variant>(), v => v.Id == w.Variant)) w.Variant = null;
        if (w.Items != null)
        {
            if (w.Type != "buttons") w.Items = null;
            else
            {
                var ok = new List<string>();
                foreach (var id in w.Items)
                    if (id != null && Array.IndexOf(WatchWidgets.GroupKinds, id) >= 0 && !ok.Contains(id) && ok.Count < 6) ok.Add(id);
                w.Items = ok.Count >= 2 ? ok : null;
            }
        }
        return w;
    }

    static string CleanName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var sb = new StringBuilder();
        foreach (char c in s.Trim()) if (!char.IsControl(c)) sb.Append(c);
        string n = sb.ToString().Trim();
        if (n.Length > MaxName) n = n[..MaxName].TrimEnd();
        if (n.Length > 0 && char.IsHighSurrogate(n[^1])) n = n[..^1];
        return n.Length == 0 ? null : n;
    }

    // adds the page after the others; false (and why) when the face has 8 already. Page 1 is set up first if it never was.
    public static bool Add(WristConfig w, WatchPage page, out string error)
    {
        error = null;
        if (WatchPages.Count(w) >= MaxPages) { error = Lang.F("The watch has {0} pages already: remove one first.", MaxPages); return false; }
        w.Widgets ??= WatchPages.Grid(w, 0).Is43 ? WatchWidgets.PagePreset("everyday", GridSpec.G43) : WatchWidgets.Seed(w, "full");
        (w.Pages ??= new List<WatchPage>()).Add(page);
        return true;
    }

    // ---- files ----

    // a file name from the page's name: letters, digits, spaces, - and _ (else "watch-page")
    public static string FileName(string pageName, DateTime? stamp = null)
    {
        var sb = new StringBuilder();
        foreach (char c in pageName ?? "")
            if (char.IsLetterOrDigit(c) || c is ' ' or '-' or '_') sb.Append(c);
        string n = sb.ToString().Trim();
        if (n.Length == 0) n = "watch-page";
        if (n.Length > 40) n = n[..40].TrimEnd();
        return stamp is { } t ? $"{n}-{t:yyyyMMdd-HHmmss}.json" : n + ".json";
    }

    // the in-VR export: into Folder with the page's name and the time (never over another file); the path written
    public static string SaveToFolder(WristConfig w, int i, DateTime now)
    {
        Directory.CreateDirectory(Folder);
        string path = Path.Combine(Folder, FileName(WatchPages.Name(w, i), now));
        for (int k = 2; File.Exists(path); k++) path = Path.Combine(Folder, Path.GetFileNameWithoutExtension(FileName(WatchPages.Name(w, i), now)) + $"-{k}.json");
        File.WriteAllText(path, Export(w, i), new UTF8Encoding(false));
        return path;
    }

    public static void SaveTo(string path, WristConfig w, int i) => File.WriteAllText(path, Export(w, i), new UTF8Encoding(false));

    // a file's text, refusing anything over MaxBytes before reading it
    public static string ReadFile(string path, out string error)
    {
        error = null;
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) { error = "That file is not there any more."; return null; }
            if (fi.Length > MaxBytes) { error = "That file is too big for a watch page."; return null; }
            return File.ReadAllText(path);
        }
        catch (Exception e) { error = Lang.F("That file could not be read: {0}", e.Message); return null; }
    }

    // the files the in-VR import offers: Folder's .json files, newest first
    public static List<FileInfo> List(int max = 6)
    {
        try
        {
            var d = new DirectoryInfo(Folder);
            if (!d.Exists) return new List<FileInfo>();
            return d.GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc).Take(max).ToList();
        }
        catch { return new List<FileInfo>(); }
    }

    // a short form of a path for a card or a message: Documents\OrbitOverlay\faces\x.json
    public static string Short(string path)
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return !string.IsNullOrEmpty(docs) && path.StartsWith(docs, StringComparison.OrdinalIgnoreCase) ? "Documents" + path[docs.Length..] : path;
    }
}
