namespace ERSC.Launcher.Core;

public static class LauncherUpdateErrors
{
    private const string Name = "update-error.txt";

    public static void Write(string folder, string message)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Name), message);
    }

    public static string? Take(string folder)
    {
        var path = Path.Combine(folder, Name);
        if (!File.Exists(path)) return null;
        var message = File.ReadAllText(path);
        File.Delete(path);
        return message;
    }
}
