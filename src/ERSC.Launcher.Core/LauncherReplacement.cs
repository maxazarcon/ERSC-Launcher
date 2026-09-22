namespace ERSC.Launcher.Core;

public static class LauncherReplacement
{
    public static void Replace(string target, string payload, string backup, Action<string> start)
    {
        target = Path.GetFullPath(target);
        if (!File.Exists(target) || !File.Exists(payload)) throw new FileNotFoundException("Launcher or downloaded update is missing.");
        var staged = target + ".update-" + Guid.NewGuid().ToString("N");
        var replaced = false;
        try
        {
            File.Copy(payload, staged);
            File.Copy(target, backup, true);
            File.Replace(staged, target, null);
            replaced = true;
            start(target);
        }
        catch
        {
            if (replaced)
            {
                File.Copy(backup, staged, true);
                File.Replace(staged, target, null);
            }
            throw;
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
}
