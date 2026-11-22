namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>
/// Keeps every file operation inside its root. Pages only ever send a root id and a path
/// relative to it; <see cref="Resolve"/> turns that into a full path, or null when the path
/// would leave the root (<c>..</c>, an absolute path, a drive, an NTFS stream, a device name
/// such as CON, or a symbolic link / junction that points outside).
/// </summary>
public static class FilePathGuard
{
    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Full path without a trailing separator (a drive root keeps its own, "C:\").</summary>
    public static string NormalizeRoot(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    public static bool IsInside(string root, string fullPath)
    {
        var parent = Path.TrimEndingDirectorySeparator(root);
        var child = Path.TrimEndingDirectorySeparator(fullPath);
        if (string.Equals(parent, child, Comparison))
        {
            return true;
        }

        var prefix = parent.EndsWith(Path.DirectorySeparatorChar) ? parent : parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, Comparison);
    }

    /// <summary>Whether one name (a single path segment) may be used for a file or folder.</summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or "..")
        {
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
        {
            return false;
        }

        // Windows drops trailing dots and spaces, which would make "a." the same file as "a".
        if (name.EndsWith('.') || name.EndsWith(' '))
        {
            return false;
        }

        var stem = name.Split('.')[0].TrimEnd();
        return !DeviceNames.Contains(stem);
    }

    /// <summary>
    /// The path with every symbolic link or junction along it followed, so a check sees where the
    /// path really leads. Parts that do not exist (yet) are kept as written.
    /// </summary>
    public static string RealPath(string fullPath)
    {
        var path = Path.GetFullPath(fullPath);
        var rootOfPath = Path.GetPathRoot(path) ?? string.Empty;
        var current = rootOfPath;
        var rest = path[rootOfPath.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (var hop = 0; hop < rest.Length; hop++)
        {
            current = Path.Combine(current, rest[hop]);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (!info.Exists || info.LinkTarget is null)
            {
                continue;
            }

            try
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    current = Path.GetFullPath(target.FullName);
                }
            }
            catch (IOException)
            {
                // A broken link: keep the written path; nothing exists behind it anyway.
            }
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    /// <summary>The full path of <paramref name="relative"/> inside <paramref name="root"/>, or null.</summary>
    public static string? Resolve(string root, string? relative)
    {
        var rootPath = NormalizeRoot(root);
        var text = (relative ?? string.Empty).Replace('\\', '/').Trim();
        if (text.Contains('\0') || text.Contains(':') || text.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => !IsValidName(part)))
        {
            return null;
        }

        var full = parts.Length == 0 ? rootPath : Path.GetFullPath(Path.Combine([rootPath, .. parts]));
        if (!IsInside(rootPath, full))
        {
            return null;
        }

        // A link anywhere below the root must not lead outside it (the root may itself be a link or junction).
        var rootReal = RealPath(rootPath);
        var current = rootPath;
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (!info.Exists || info.LinkTarget is null)
            {
                continue;
            }

            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            var targetPath = target is null ? null : Path.GetFullPath(target.FullName);
            if (targetPath is null || !(IsInside(rootPath, targetPath) || IsInside(rootReal, targetPath)))
            {
                return null;
            }
        }

        return full;
    }

    /// <summary>Path of <paramref name="fullPath"/> relative to the root, with "/" separators ("" for the root).</summary>
    public static string Relative(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(NormalizeRoot(root), fullPath);
        return relative == "." ? string.Empty : relative.Replace('\\', '/');
    }
}
