using System;
using System.IO;

namespace Orbit;

static class Log
{
    static StreamWriter _w;
    static readonly object _lock = new();

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbit");

    public static void Init(string name)
    {
        Directory.CreateDirectory(Dir);
        string path = Path.Combine(Dir, name);
        try
        {
            // keep the previous run's log next to the new one
            if (File.Exists(path))
                File.Copy(path, Path.ChangeExtension(path, ".prev.log"), true);
            _w = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }
        catch { _w = null; }
    }

    static void Write(string level, string msg)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} {level} {msg}";
        lock (_lock)
        {
            try { _w?.WriteLine(line); } catch { }
        }
        Console.WriteLine(line);
    }

    public static void Info(string msg) => Write("INF", msg);
    public static void Warn(string msg) => Write("WRN", msg);
    public static void Error(string msg) => Write("ERR", msg);
}
