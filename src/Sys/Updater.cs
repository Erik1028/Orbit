using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Orbit;

// "Check for updates": asks GitHub for the latest release of Erik1028/Orbit, and the same button then
// updates in place: OrbitSetup.exe is downloaded, checked (size and SHA-256 as GitHub lists them, file
// version) and started with --update for the folder Orbit runs from, which closes Orbit, replaces its
// files (settings, shortcuts and the Apps entry stay as they are) and starts it again.
public sealed class Updater
{
    public const string Repo = "Erik1028/Orbit";
    const string AssetName = "OrbitSetup.exe";

    public enum State { Idle, Checking, UpToDate, Available, Downloading, Starting, Failed }

    readonly object _lock = new();
    State _state;
    string _message = "";
    float _progress;
    Version _latest;
    string _latestName, _assetUrl, _sha256, _pageUrl;
    long _assetSize;

    public static Version Current => typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(1, 0, 0);

    public (State state, string message, float progress, string latest) Snapshot()
    {
        lock (_lock) return (_state, _message, _progress, _latest?.ToString(3));
    }

    void Set(State s, string msg, float progress = 0)
    {
        lock (_lock) { _state = s; _message = msg; _progress = progress; }
    }

    public bool Busy { get { lock (_lock) return _state is State.Checking or State.Downloading or State.Starting; } }

    static HttpClient Client()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"Orbit/{Current.ToString(3)} (+https://github.com/{Repo})");
        return c;
    }

    public void Check()
    {
        if (Busy) return;
        Set(State.Checking, "checking GitHub...");
        Task.Run(async () =>
        {
            try
            {
                using var http = Client();
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                using var resp = await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) { Set(State.UpToDate, "no release published yet"); return; }
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                string tag = root.GetProperty("tag_name").GetString() ?? "";
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) { Set(State.Failed, $"unexpected release tag '{tag}'"); return; }
                v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
                string name = root.TryGetProperty("name", out var n) ? n.GetString() : tag;
                string page = root.TryGetProperty("html_url", out var h) ? h.GetString() : $"https://github.com/{Repo}/releases/latest";
                string url = null, sha = null;
                long size = 0;
                foreach (var a in root.GetProperty("assets").EnumerateArray())
                {
                    if (!string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                    url = a.GetProperty("browser_download_url").GetString();
                    size = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    if (a.TryGetProperty("digest", out var dg) && dg.GetString() is { } d && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        sha = d[7..].ToLowerInvariant();
                }
                lock (_lock) { _latest = v; _latestName = name; _assetUrl = url; _sha256 = sha; _assetSize = size; _pageUrl = page; }
                if (v > Current) Set(State.Available, $"{name} is out");
                else Set(State.UpToDate, $"up to date ({Current.ToString(3)} is the latest)");
                Log.Info($"update check: latest {tag}, running {Current.ToString(3)}, asset {(url != null ? "found" : "missing")}, digest {(sha != null ? "yes" : "no")}");
            }
            catch (Exception e)
            {
                Log.Warn("update check: " + e.Message);
                Set(State.Failed, "could not reach GitHub");
            }
        });
    }

    // the "Update" button: download, verify, install over the running copy (the page only if the release has no installer)
    public void Apply()
    {
        if (Busy) return;
        string url, sha, page;
        Version v;
        long size;
        lock (_lock) { url = _assetUrl; sha = _sha256; page = _pageUrl; v = _latest; size = _assetSize; }
        if (v == null) return;
        string dir = Installer.InstalledCopyDir() ?? Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\');
        if (url == null)
        {
            OpenPage(page ?? $"https://github.com/{Repo}/releases/latest");
            Set(State.Available, "this release has no installer - opened its page");
            return;
        }
        Set(State.Downloading, "downloading...", 0);
        Task.Run(async () =>
        {
            try
            {
                string file = await DownloadVerified(url, sha, size, v);
                Set(State.Starting, "installing - Orbit restarts in a moment");
                Log.Info($"update: starting {file} --update --install-dir \"{dir}\"");
                Process.Start(new ProcessStartInfo(file)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(file),
                    ArgumentList = { "--update", "--install-dir", dir },
                });
            }
            catch (Exception e)
            {
                Log.Warn("update: " + e.Message);
                Set(State.Failed, "update failed: " + e.Message);
            }
        });
    }

    // downloads the release's OrbitSetup.exe and proves it is that file: size, SHA-256 (as GitHub lists
    // it), and the file's own product name / version
    async Task<string> DownloadVerified(string url, string sha, long size, Version v)
    {
        string folder = Path.Combine(Log.Dir, "updates");
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder, "OrbitSetup-*.exe")) { try { File.Delete(old); } catch { } }
        string file = Path.Combine(folder, $"OrbitSetup-{v.ToString(3)}.exe");
        try
        {
            using (var http = Client())
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? size;
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(file);
                var buf = new byte[81920];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, read));
                    done += read;
                    if (total > 0) Set(State.Downloading, $"downloading... {done * 100 / total}%", done / (float)total);
                }
            }
            // the file must be exactly what GitHub lists for this release
            long length = new FileInfo(file).Length;
            if (size > 0 && length != size) throw new InvalidDataException("size does not match the release");
            if (sha != null)
            {
                await using var fs = File.OpenRead(file);
                string hash = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
                if (hash != sha) throw new InvalidDataException("SHA-256 does not match the release");
            }
            var fv = FileVersionInfo.GetVersionInfo(file);
            if (fv.ProductName != "Orbit" || !(fv.ProductVersion ?? "").StartsWith(v.ToString(3)))
                throw new InvalidDataException($"the download is '{fv.ProductName} {fv.ProductVersion}', expected Orbit {v.ToString(3)}");
            Log.Info($"update: {file} verified ({length} bytes, sha256 {(sha != null ? "matches" : "not listed")}, version {fv.ProductVersion})");
            return file;
        }
        catch
        {
            try { File.Delete(file); } catch { }      // never leave an unverified installer behind
            throw;
        }
    }

    // dev (--updatetest): check, then download + verify the latest release's installer without running it
    public static int Test()
    {
        var u = new Updater();
        u.Check();
        while (u.Busy) Thread.Sleep(100);
        var (st, msg, _, latest) = u.Snapshot();
        Log.Info($"updatetest: {st} - {msg} (latest {latest ?? "-"}, running {Current.ToString(3)}, installed copy: {Installer.InstalledCopyDir() ?? "no"})");
        if (latest == null || u._assetUrl == null) return st == State.Failed ? 1 : 0;
        try
        {
            string f = u.DownloadVerified(u._assetUrl, u._sha256, u._assetSize, u._latest).GetAwaiter().GetResult();
            Log.Info("updatetest: download ok: " + f);
            return 0;
        }
        catch (Exception e) { Log.Error("updatetest: " + e.Message); return 2; }
    }

    // through Explorer, so the browser does not start with Orbit's administrator rights
    public static void OpenPage(string url)
    {
        if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + url + "\"") { UseShellExecute = false }); }
        catch (Exception e) { Log.Warn("open page: " + e.Message); }
    }
}
