using System.Diagnostics;
using System.Text;

namespace ERSC.Launcher.Core;

// One node of Steam's binary KeyValues format. Maps hold children; other types keep their value.
public sealed class VdfNode(byte type, string name, object value)
{
    public const byte Map = 0, String = 1, Int32 = 2, Float = 3, Pointer = 4, WideString = 5, Color = 6, UInt64 = 7, End = 8;
    public byte Type { get; set; } = type;
    public string Name { get; set; } = name;
    public object Value { get; set; } = value;
    public List<VdfNode> Children => (List<VdfNode>)Value;

    public static VdfNode NewMap(string name) => new(Map, name, new List<VdfNode>());
    public VdfNode? Find(string name) => Children.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    public string? GetString(string name) => Find(name) is { Type: String } node ? (string)node.Value : null;

    public void Set(string name, string value) => Upsert(name, String, value);
    public void Set(string name, int value) => Upsert(name, Int32, value);
    private void Upsert(string name, byte type, object value)
    {
        var node = Find(name);
        if (node is null) Children.Add(new VdfNode(type, name, value));
        else { node.Type = type; node.Value = value; }
    }
}

public static class BinaryVdf
{
    public static VdfNode Read(byte[] data)
    {
        var root = VdfNode.NewMap("");
        var position = 0;
        ReadChildren(data, ref position, root.Children);
        return root;
    }

    private static void ReadChildren(byte[] data, ref int position, List<VdfNode> children)
    {
        while (true)
        {
            if (position >= data.Length) return; // Tolerate a missing trailing end marker.
            var type = data[position++];
            if (type == VdfNode.End) return;
            var name = ReadString(data, ref position);
            object value = type switch
            {
                VdfNode.Map => ReadMap(data, ref position),
                VdfNode.String => ReadString(data, ref position),
                VdfNode.Int32 => BitConverter.ToInt32(Take(data, ref position, 4)),
                VdfNode.Float or VdfNode.Pointer or VdfNode.Color => Take(data, ref position, 4),
                VdfNode.UInt64 => Take(data, ref position, 8),
                VdfNode.WideString => ReadWide(data, ref position),
                _ => throw new InvalidDataException($"Unknown Steam shortcut value type {type}.")
            };
            children.Add(new VdfNode(type, name, value));
        }
    }

    private static List<VdfNode> ReadMap(byte[] data, ref int position)
    {
        var children = new List<VdfNode>();
        ReadChildren(data, ref position, children);
        return children;
    }

    private static byte[] Take(byte[] data, ref int position, int count)
    {
        if (position + count > data.Length) throw new InvalidDataException("Steam shortcut file is truncated.");
        var bytes = data[position..(position + count)];
        position += count;
        return bytes;
    }

    private static string ReadString(byte[] data, ref int position)
    {
        var end = Array.IndexOf(data, (byte)0, position);
        if (end < 0) throw new InvalidDataException("Steam shortcut file is truncated.");
        var text = Encoding.UTF8.GetString(data, position, end - position);
        position = end + 1;
        return text;
    }

    private static byte[] ReadWide(byte[] data, ref int position)
    {
        var start = position;
        while (position + 1 < data.Length && (data[position] != 0 || data[position + 1] != 0)) position += 2;
        if (position + 1 >= data.Length) throw new InvalidDataException("Steam shortcut file is truncated.");
        position += 2;
        return data[start..position];
    }

    public static byte[] Write(VdfNode root)
    {
        using var stream = new MemoryStream();
        WriteChildren(stream, root.Children);
        stream.WriteByte(VdfNode.End);
        return stream.ToArray();
    }

    private static void WriteChildren(Stream stream, List<VdfNode> children)
    {
        foreach (var node in children)
        {
            stream.WriteByte(node.Type);
            WriteString(stream, node.Name);
            switch (node.Type)
            {
                case VdfNode.Map: WriteChildren(stream, node.Children); stream.WriteByte(VdfNode.End); break;
                case VdfNode.String: WriteString(stream, (string)node.Value); break;
                case VdfNode.Int32: stream.Write(BitConverter.GetBytes((int)node.Value)); break;
                default: stream.Write((byte[])node.Value); break;
            }
        }
    }

    private static void WriteString(Stream stream, string text)
    {
        stream.Write(Encoding.UTF8.GetBytes(text));
        stream.WriteByte(0);
    }
}

// Adds the launcher to Steam as a non-Steam game by editing userdata/<user>/config/shortcuts.vdf,
// and gives it library artwork in userdata/<user>/config/grid.
// Steam rewrites that file when it exits, so callers must make sure Steam is closed first.
public static class SteamShortcuts
{
    public const string AppName = "Seamless Co-Op Launcher (Unofficial)";
    // Earlier names. Steam keys artwork by an ID made from the name, so a rename has to carry the art across.
    private static readonly string[] LegacyAppNames = ["Seamless Co-Op Launcher"];
    private const string BackupSuffix = ".ersc-backup";

    public static uint AppId(string quotedExe, string appName)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in Encoding.UTF8.GetBytes(quotedExe + appName))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc | 0x80000000u;
    }

    public static IReadOnlyList<string> ShortcutFiles(string steamRoot)
    {
        var userdata = Path.Combine(steamRoot, "userdata");
        if (!Directory.Exists(userdata)) return [];
        return Directory.EnumerateDirectories(userdata)
            .Where(d => ulong.TryParse(Path.GetFileName(d), out var id) && id != 0)
            .Select(d => Path.Combine(d, "config", "shortcuts.vdf"))
            .ToArray();
    }

    public static bool Contains(string steamRoot, string exe) => ShortcutFiles(steamRoot).Any(file =>
    {
        try { return File.Exists(file) && Shortcuts(Load(file)).Children.Any(s => Matches(s, exe)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    });

    /// <summary>True when the launcher's shortcut still has an earlier name, so adding it again would rename it.</summary>
    public static bool HasLegacyName(string steamRoot, string exe) => ShortcutFiles(steamRoot).Any(file =>
    {
        try { return File.Exists(file) && Shortcuts(Load(file)).Children.Any(s => Matches(s, exe) && s.GetString("AppName") is { } name && LegacyAppNames.Contains(name)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    });

    /// <summary>Adds or refreshes the launcher entry for every Steam user. Returns the number of users updated.</summary>
    public static int Add(string steamRoot, string exe)
    {
        exe = Path.GetFullPath(exe);
        var count = 0;
        foreach (var file in ShortcutFiles(steamRoot))
        {
            var root = File.Exists(file) ? Load(file) : VdfNode.NewMap("");
            var shortcuts = Shortcuts(root);
            var entry = shortcuts.Children.FirstOrDefault(s => Matches(s, exe));
            if (entry is null)
            {
                entry = VdfNode.NewMap(shortcuts.Children.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                shortcuts.Children.Add(entry);
            }
            Fill(entry, exe);
            Save(file, root);
            MoveLegacyArtwork(file, exe);
            AddArtwork(file, exe);
            count++;
        }
        return count;
    }

    /// <summary>Removes the launcher entry for every Steam user. Returns the number of users changed.</summary>
    public static int Remove(string steamRoot, string exe)
    {
        exe = Path.GetFullPath(exe);
        var count = 0;
        foreach (var file in ShortcutFiles(steamRoot).Where(File.Exists))
        {
            var root = Load(file);
            var shortcuts = Shortcuts(root);
            if (shortcuts.Children.RemoveAll(s => Matches(s, exe)) == 0) continue;
            for (var i = 0; i < shortcuts.Children.Count; i++) shortcuts.Children[i].Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Save(file, root);
            RemoveArtwork(file, exe);
            count++;
        }
        return count;
    }

    // Library artwork, by the suffix Steam expects after the app ID in userdata/<user>/config/grid.
    private static readonly (string Suffix, string Resource)[] Artwork =
        [("p", "portrait.png"), ("", "wide.png"), ("_hero", "hero.png"), ("_logo", "logo.png")];
    private static readonly string[] ArtworkExtensions = [".png", ".jpg", ".jpeg"];

    public static string ArtworkPath(string shortcutFile, string exe, string suffix, string appName = AppName) =>
        Path.Combine(Path.GetDirectoryName(shortcutFile)!, "grid", $"{AppId(Quote(Path.GetFullPath(exe)), appName)}{suffix}.png");

    // After a rename, art the player chose moves to the new ID; the launcher's own old images are deleted and redrawn by AddArtwork.
    private static void MoveLegacyArtwork(string shortcutFile, string exe)
    {
        foreach (var legacy in LegacyAppNames)
            foreach (var (suffix, resource) in Artwork)
                foreach (var ext in ArtworkExtensions)
                {
                    try
                    {
                        var old = Path.ChangeExtension(ArtworkPath(shortcutFile, exe, suffix, legacy), ext);
                        if (!File.Exists(old)) continue;
                        if (File.ReadAllBytes(old).AsSpan().SequenceEqual(ArtworkBytes(resource))) { File.Delete(old); continue; }
                        var target = ArtworkPath(shortcutFile, exe, suffix);
                        if (!ArtworkExtensions.Any(e => File.Exists(Path.ChangeExtension(target, e)))) File.Move(old, Path.ChangeExtension(target, ext));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
    }

    // Artwork is cosmetic, so a failure here never undoes the shortcut itself.
    private static void AddArtwork(string shortcutFile, string exe)
    {
        foreach (var (suffix, resource) in Artwork)
        {
            try
            {
                var target = ArtworkPath(shortcutFile, exe, suffix);
                // Leave art the player picked in Steam, which it may have saved as a JPEG.
                if (ArtworkExtensions.Any(ext => File.Exists(Path.ChangeExtension(target, ext)))) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, ArtworkBytes(resource));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    // Deletes only the launcher's own images, so custom art survives a remove and re-add.
    private static void RemoveArtwork(string shortcutFile, string exe)
    {
        foreach (var name in LegacyAppNames.Prepend(AppName))
        foreach (var (suffix, resource) in Artwork)
        {
            try
            {
                var target = ArtworkPath(shortcutFile, exe, suffix, name);
                if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(ArtworkBytes(resource))) File.Delete(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public static byte[] ArtworkBytes(string resource)
    {
        using var stream = typeof(SteamShortcuts).Assembly.GetManifestResourceStream("SteamArt." + resource)
            ?? throw new InvalidOperationException($"Missing embedded Steam artwork {resource}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static void Fill(VdfNode entry, string exe)
    {
        var quoted = Quote(exe);
        entry.Set("appid", unchecked((int)AppId(quoted, AppName)));
        entry.Set("AppName", AppName);
        entry.Set("Exe", quoted);
        entry.Set("StartDir", Quote(Path.GetDirectoryName(exe)!));
        entry.Set("icon", exe);
        // Keep anything the player changed in Steam's properties, such as their own launch options.
        foreach (var (key, value) in new[] { ("LaunchOptions", ""), ("ShortcutPath", "") })
            if (entry.Find(key) is null) entry.Set(key, value);
        foreach (var (key, value) in new[] { ("IsHidden", 0), ("AllowDesktopConfig", 1), ("AllowOverlay", 1), ("OpenVR", 0), ("LastPlayTime", 0) })
            if (entry.Find(key) is null) entry.Set(key, value);
        if (entry.Find("tags") is not { Type: VdfNode.Map } tags)
        {
            entry.Children.RemoveAll(c => c.Name.Equals("tags", StringComparison.OrdinalIgnoreCase));
            tags = VdfNode.NewMap("tags");
            entry.Children.Add(tags);
        }
        if (!tags.Children.Any(t => t.Value is string s && s == "Elden Ring")) tags.Set(tags.Children.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), "Elden Ring");
    }

    private static bool Matches(VdfNode shortcut, string exe)
    {
        var value = shortcut.Type == VdfNode.Map ? shortcut.GetString("Exe") : null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return Path.GetFullPath(value.Trim().Trim('"')).Equals(Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static VdfNode Shortcuts(VdfNode root)
    {
        if (root.Find("shortcuts") is { Type: VdfNode.Map } existing) return existing;
        var created = VdfNode.NewMap("shortcuts");
        root.Children.Add(created);
        return created;
    }

    private static string Quote(string path) => "\"" + path + "\"";
    private static VdfNode Load(string file) => BinaryVdf.Read(File.ReadAllBytes(file));

    private static void Save(string file, VdfNode root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file)) File.Copy(file, file + BackupSuffix, true);
        var temp = file + ".tmp";
        File.WriteAllBytes(temp, BinaryVdf.Write(root));
        File.Move(temp, file, true);
    }
}

public static class SteamClient
{
    public static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("steam");
        foreach (var process in processes) process.Dispose();
        return processes.Length > 0;
    }

    /// <summary>Asks Steam to exit and waits for it. Returns false if Steam is still running after the timeout.</summary>
    public static async Task<bool> ShutdownAsync(string steamRoot, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsRunning()) return true;
        var exe = Path.Combine(steamRoot, "steam.exe");
        if (!File.Exists(exe)) return false;
        using (Process.Start(new ProcessStartInfo(exe, "-shutdown") { UseShellExecute = false })) { }
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(500, cancellationToken);
            if (!IsRunning())
            {
                await Task.Delay(1000, cancellationToken); // Give Steam a moment to finish writing its own files.
                return true;
            }
        }
        return false;
    }

    public static void Start(string steamRoot)
    {
        var exe = Path.Combine(steamRoot, "steam.exe");
        if (File.Exists(exe)) using (Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })) { }
    }
}
