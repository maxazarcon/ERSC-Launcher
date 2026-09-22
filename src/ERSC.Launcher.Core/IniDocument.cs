using System.Text;
using System.Text.RegularExpressions;

namespace ERSC.Launcher.Core;

public sealed record IniEntry(string Section, string Key, string Value, int LineIndex, string? Description);

public sealed class IniDocument
{
    private readonly List<string> _lines;
    private readonly string _newline;
    private readonly bool _trailingNewline;
    private IniDocument(List<string> lines, string newline, bool trailingNewline) { _lines = lines; _newline = newline; _trailingNewline = trailingNewline; }

    public static IniDocument Parse(string text)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var trailing = text.EndsWith(newline, StringComparison.Ordinal);
        var lines = text.Length == 0 ? new List<string>() : Regex.Split(text, "\r\n|\n").ToList();
        if (trailing) lines.RemoveAt(lines.Count - 1);
        return new IniDocument(lines, newline, trailing);
    }

    public static IniDocument Load(string path) => Parse(File.ReadAllText(path));

    public IReadOnlyList<IniEntry> Entries
    {
        get
        {
            var result = new List<IniEntry>(); var section = "";
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i].Trim();
                if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1].Trim(); continue; }
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
                var eq = _lines[i].IndexOf('=');
                if (eq <= 0) continue;
                var key = _lines[i][..eq].Trim();
                if (key.Length > 0)
                {
                    var comments = new List<string>();
                    for (var c = i - 1; c >= 0; c--)
                    {
                        var prior = _lines[c].Trim();
                        if (prior.StartsWith(';') || prior.StartsWith('#')) comments.Insert(0, prior[1..].Trim());
                        else if (prior.Length != 0) break;
                        else if (comments.Count > 0) break;
                    }
                    result.Add(new IniEntry(section, key, _lines[i][(eq + 1)..].Trim(), i, comments.Count == 0 ? null : string.Join(" ", comments)));
                }
            }
            return result;
        }
    }

    public string? Get(string key) => Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

    public void Set(string key, string value)
    {
        var entry = Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException(key);
        SetAt(entry.LineIndex, value);
    }

    public void SetAt(int lineIndex, string value)
    {
        if (value.Contains('\r') || value.Contains('\n')) throw new ArgumentException("INI values must be one line.", nameof(value));
        var raw = _lines[lineIndex]; var eq = raw.IndexOf('=');
        if (eq < 0) throw new InvalidDataException("Setting line is no longer valid.");
        var after = raw[(eq + 1)..]; var spaces = after[..(after.Length - after.TrimStart().Length)];
        _lines[lineIndex] = raw[..(eq + 1)] + spaces + value;
    }

    public void MergeValuesFrom(IniDocument previous)
    {
        foreach (var entry in previous.Entries)
        {
            var current = Entries.FirstOrDefault(e => e.Key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase) && e.Section.Equals(entry.Section, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                SetAt(current.LineIndex, entry.Value);
                continue;
            }
            var siblings = Entries.Where(e => e.Section.Equals(entry.Section, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (siblings.Length > 0) _lines.Insert(siblings[^1].LineIndex + 1, entry.Key + " = " + entry.Value);
            else if (entry.Section.Length > 0)
            {
                _lines.Add("[" + entry.Section + "]");
                _lines.Add(entry.Key + " = " + entry.Value);
            }
            else _lines.Insert(0, entry.Key + " = " + entry.Value);
        }
    }

    public override string ToString() => string.Join(_newline, _lines) + (_trailingNewline ? _newline : "");

    public void Save(string path)
    {
        var tmp = path + ".ersc-" + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(tmp, ToString(), new UTF8Encoding(false));
        try { File.Move(tmp, path, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
