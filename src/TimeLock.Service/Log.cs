namespace TimeLock.Service;

public sealed class Log
{
    readonly string _path;
    public Log(string path) => _path = path;

    public void Write(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path) && new FileInfo(_path).Length > 512 * 1024) File.Delete(_path);
            File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
        }
        catch (Exception) { /* יומן אינו סיבה לתקלה */ }
    }
}

static class Run
{
    public static int Exe(string file, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(file)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        if (!p.WaitForExit(15000)) { try { p.Kill(); } catch (Exception) { } return -1; }
        return p.ExitCode;
    }
}
