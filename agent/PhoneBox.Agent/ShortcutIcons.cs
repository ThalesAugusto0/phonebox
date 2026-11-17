#if WINDOWS
using System.Drawing.Imaging;
#endif

namespace PhoneBox.Agent;

/// <summary>
/// Real app icons for shortcuts whose command is an actual program on disk (an .exe, an .app bundle, a Linux binary).
/// Shell addresses (shell:AppsFolder\…), protocol handlers (ms-settings:, ms-screenclip:), URLs, builtin: commands,
/// and Windows' App Execution Alias stubs for packaged apps (wt.exe, ms-teams.exe: a reparse point with no icon of
/// its own) have no real icon to read, so this returns null for them and the phone keeps showing the chosen emoji.
/// </summary>
internal static class ShortcutIcons
{
    /// <summary>Cheap check for whether <see cref="Icon"/> is worth asking for, without extracting one.</summary>
    public static bool HasIcon(Shortcut sc) => ResolvePath(sc.Cmd) is not null;

    public static byte[]? Icon(Shortcut sc)
    {
        var path = ResolvePath(sc.Cmd);
        if (path is null) return null;
#if WINDOWS
        try
        {
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (ico is null) return null;
            using var bmp = ico.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch { return null; }
#else
        var name = Path.GetFileNameWithoutExtension(path);
        // UnixProc.Icon only recognises a macOS bundle when the path contains ".app/"; a bare "X.app" has nothing after it.
        var probe = path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? path + "/" : path;
        return UnixProc.Icon(name, probe);
#endif
    }

    /// <summary>
    /// True for an actual file path ("C:\…\app.exe", "/usr/bin/x", "code", "~/bin/x"); false for a builtin: command,
    /// a URL, a protocol handler (ms-settings:) or a shell address (shell:AppsFolder\…) — none of which name a file.
    /// </summary>
    internal static bool LooksLikeFilePath(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return false;
        cmd = cmd.Trim().Trim('"');
        if (cmd.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase)) return false;
        // A drive letter ("C:\…") is a path; any other "word:" is a shell address or protocol handler (ms-settings:, shell:AppsFolder\…, https://…).
        var colon = cmd.IndexOf(':');
        return colon < 0 || (colon == 1 && char.IsLetter(cmd[0]));
    }

    /// <summary>
    /// The real file behind a shortcut's command, or null when it is not a program with its own icon (a shell
    /// address, a protocol handler, a URL, a builtin: command, or an App Execution Alias stub).
    /// </summary>
    private static string? ResolvePath(string cmd)
    {
        if (!LooksLikeFilePath(cmd)) return null;
        cmd = cmd.Trim().Trim('"');
        if (cmd.StartsWith("~")) cmd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + cmd[1..];
#if WINDOWS
        if (Path.IsPathRooted(cmd)) return IsRealFile(cmd) ? cmd : null;
        var name = cmd.Contains('.') ? cmd : cmd + ".exe";
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Append(Environment.SystemDirectory)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64"));
        foreach (var dir in dirs)
        {
            string full;
            try { full = Path.Combine(dir, name); } catch { continue; }
            if (IsRealFile(full)) return full;
        }
        return null;
#else
        if (cmd.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) return Directory.Exists(cmd) ? cmd : null;
        if (cmd.Contains('/')) return File.Exists(cmd) ? cmd : null;
        return Sh.Which(cmd); // a bare command name: resolve it through PATH, like the shell would
#endif
    }

#if WINDOWS
    /// <summary>
    /// A real executable with its own icon resource. An App Execution Alias (the stub Windows Apps installs in
    /// WindowsApps\ for a packaged app, e.g. wt.exe, ms-teams.exe) is a reparse point, not a real PE file: Windows
    /// hands back a generic "unknown program" icon for it instead of the app's real one, so this rejects those.
    /// </summary>
    private static bool IsRealFile(string path)
    {
        try { return File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch { return false; }
    }
#endif
}
