namespace XIVHelper;

/// <summary>The notes file the Mac-side watchers write (one "source: note" line each); the plugin leaves its own notes the same way.</summary>
public static class AttentionFile
{
    public static string Lines(string existing, string source, string? note)
    {
        var kept = existing.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0 && !l.StartsWith(source + ":")).ToList();
        if (!string.IsNullOrEmpty(note))
            kept.Add($"{source}: {note}");
        return string.Concat(kept.Select(l => l + "\n"));
    }

    public static void Set(string path, string source, string? note)
    {
        try
        {
            var existing = File.Exists(path) ? File.ReadAllText(path) : "";
            var text = Lines(existing, source, note);
            if (text.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
