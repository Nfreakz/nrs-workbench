namespace NRS.Workbench.App.Services;

public static class PortablePathCodec
{
    private const string Prefix = "@portable/";

    public static bool IsPortableToken(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Encode(string path, string portableRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return path;

        var root = NormalizeRoot(portableRoot);
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, full);

        if (relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
            return full;

        if (relative == ".") return Prefix;

        return Prefix + relative
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public static string Decode(string value, string portableRoot)
    {
        if (!IsPortableToken(value)) return value;

        var root = NormalizeRoot(portableRoot);
        var relative = value[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(root, relative));

        var check = Path.GetRelativePath(root, resolved);
        if (check == ".." ||
            check.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(check))
            throw new InvalidDataException("Portable path escapes the portable volume.");

        return resolved;
    }

    private static string NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Portable root is required.", nameof(root));

        return Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }
}
