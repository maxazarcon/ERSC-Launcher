using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using ERSC.Launcher.Core;

var tests = new (string Name, Action Run)[]
{
    ("INI preserves unknown settings and comments", TestIni),
    ("Game folder accepts root or Game", TestGamePath),
    ("Steam library discovery finds custom library", TestSteam),
    ("Release selection includes beta and rejects ambiguous ZIPs", TestRelease),
    ("Archive validates layout and blocks traversal", TestArchive),
    ("Update preserves settings and can roll back", TestInstall),
    ("Manual binary comparison recognizes current install", TestFingerprint)
    ,("Merging settings retains new defaults and old unknown keys in their sections", TestMerge)
    ,("Interrupted and corrupt downloads leave no ZIP", TestDownloads)
    ,("Offline release checks fail without affecting installed files", TestOffline)
    ,("Locked launcher leaves prior installation intact", TestLockedLauncher)
    ,("Current upstream ZIP extracts", TestRealArchive)
    ,("Live GitHub release downloads and validates", TestLiveRelease)
    ,("Known settings choose controls without changing raw values", TestSettingControls)
    ,("Launcher updates select stable newer versions and a verified asset", TestLauncherRelease)
    ,("Launcher downloads reject corrupt and incomplete executables", TestLauncherDownload)
    ,("Launcher replacement preserves its path and restores on failure", TestLauncherReplacement)
    ,("Failed launcher update reports the error on the next launch", TestLauncherError)
    ,("Published launcher updates an older portable file", TestLiveLauncherUpdate)
};
var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {test.Name}: {ex}"); }
}
return failed == 0 ? 0 : 1;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}");
}
static void True(bool value) { if (!value) throw new Exception("Expected true"); }
static string Temp() { var p = Path.Combine(Path.GetTempPath(), "ersc-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
static void TestIni()
{
    var ini = IniDocument.Parse("; heading\r\n[password]\r\ncooppassword = old\r\n[other]\r\nnew_setting = 7\r\n");
    Equal("old", ini.Get("cooppassword"));
    ini.Set("cooppassword", "new secret");
    True(ini.ToString().Contains("; heading\r\n"));
    True(ini.ToString().Contains("new_setting = 7"));
    True(ini.ToString().Contains("cooppassword = new secret"));
    Equal(2, ini.Entries.Count);
}
static void TestGamePath()
{
    var root = Temp(); var game = Path.Combine(root, "Game"); Directory.CreateDirectory(game); File.WriteAllText(Path.Combine(game, "eldenring.exe"), "x");
    Equal(Path.GetFullPath(game), GameLocator.Validate(root));
    Equal(Path.GetFullPath(game), GameLocator.Validate(game));
}
static void TestSteam()
{
    var root = Temp(); var library = Path.Combine(root, "other library");
    Directory.CreateDirectory(Path.Combine(root, "steamapps"));
    Directory.CreateDirectory(Path.Combine(library, "steamapps", "common", "ELDEN RING", "Game"));
    File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"), $"\"libraryfolders\"\n{{\n \"1\" {{ \"path\" \"{library.Replace("\\", "\\\\")}\" }}\n}}");
    File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_1245620.acf"), "\"AppState\" { \"installdir\" \"ELDEN RING\" }");
    File.WriteAllText(Path.Combine(library, "steamapps", "common", "ELDEN RING", "Game", "eldenring.exe"), "x");
    True(GameLocator.FindInSteam(root).Any(x => x.EndsWith(Path.Combine("ELDEN RING", "Game"))));
}
static void TestRelease()
{
    const string baseUrl = "https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases/download/";
    var older = new ModRelease("v1.9.8", false, false, DateTimeOffset.Parse("2026-04-01"), [new ModAsset("mod.zip", baseUrl + "v1.9.8/mod.zip", 1, null)]);
    var beta = new ModRelease("v2.0.1", true, false, DateTimeOffset.Parse("2026-09-01"), [new ModAsset("Seamless.Co-op.zip", baseUrl + "v2.0.1/Seamless.Co-op.zip", 1, null)]);
    Equal("v2.0.1", ReleaseCatalog.SelectNewest([older, beta]).Tag);
    Equal("Seamless.Co-op.zip", ReleaseCatalog.SelectZip(beta).Name);
    try { ReleaseCatalog.SelectZip(beta with { Assets = [beta.Assets[0], new ModAsset("other.zip", baseUrl + "v2.0.1/other.zip", 1, null)] }); throw new Exception("accepted ambiguous archives"); }
    catch (InvalidDataException) { }
}
static void TestArchive()
{
    var root = Temp(); var zip = Path.Combine(root, "mod.zip");
    using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
        Add(z, "bundle/ersc_launcher.exe", "launcher"); Add(z, "bundle/SeamlessCoop/ersc.dll", "dll"); Add(z, "bundle/SeamlessCoop/ersc_settings.ini", "cooppassword = \n");
    }
    var stage = Path.Combine(root, "stage"); ModPackage.Extract(zip, stage); True(File.Exists(Path.Combine(stage, "ersc_launcher.exe")));
    var bad = Path.Combine(root, "bad.zip"); using (var z = ZipFile.Open(bad, ZipArchiveMode.Create)) { Add(z, "../bad", "oops"); }
    try { ModPackage.Extract(bad, Path.Combine(root, "badstage")); throw new Exception("accepted traversal"); } catch (InvalidDataException) { }
}
static void TestInstall()
{
    var root = Temp(); var game = Path.Combine(root, "Game"); var staged = Path.Combine(root, "stage"); var backups = Path.Combine(root, "backups"); Directory.CreateDirectory(game); Directory.CreateDirectory(staged);
    File.WriteAllText(Path.Combine(game, "eldenring.exe"), "game"); File.WriteAllText(Path.Combine(game, "ersc_launcher.exe"), "old"); Directory.CreateDirectory(Path.Combine(game, "SeamlessCoop"));
    File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc.dll"), "old"); File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"), "cooppassword = secret\ncustom = yes\n");
    File.WriteAllText(Path.Combine(staged, "ersc_launcher.exe"), "new"); Directory.CreateDirectory(Path.Combine(staged, "SeamlessCoop"));
    File.WriteAllText(Path.Combine(staged, "SeamlessCoop", "ersc.dll"), "new"); File.WriteAllText(Path.Combine(staged, "SeamlessCoop", "ersc_settings.ini"), "cooppassword = \nnew = 1\n");
    var backup = ModInstaller.Install(game, staged, backups);
    True(File.ReadAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini")).Contains("cooppassword = secret"));
    True(File.Exists(Path.Combine(backup, "SeamlessCoop", "ersc.dll")));
    ModInstaller.Restore(game, backup);
    Equal("old", File.ReadAllText(Path.Combine(game, "ersc_launcher.exe")));
}
static void TestFingerprint()
{
    var root = Temp(); var game = Path.Combine(root, "Game"); var staged = Path.Combine(root, "stage"); Directory.CreateDirectory(game); Directory.CreateDirectory(staged);
    foreach (var dir in new[] { game, staged }) { File.WriteAllText(Path.Combine(dir, "ersc_launcher.exe"), "x"); Directory.CreateDirectory(Path.Combine(dir, "SeamlessCoop")); File.WriteAllText(Path.Combine(dir, "SeamlessCoop", "ersc.dll"), "y"); File.WriteAllText(Path.Combine(dir, "SeamlessCoop", "ersc_settings.ini"), "cooppassword = x"); }
    True(ModFingerprint.Matches(game, staged));
    foreach (var dir in new[] { game, staged }) File.WriteAllText(Path.Combine(dir, "SeamlessCoop", "locale.json"), "same");
    var record = StateStore.Record("v2.0.1", game, staged);
    True(StateStore.Matches(game, record));
    File.WriteAllText(Path.Combine(game, "SeamlessCoop", "locale.json"), "stale");
    True(!ModFingerprint.Matches(game, staged));
    True(!StateStore.Matches(game, record));
    File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc.dll"), "changed");
    True(!ModFingerprint.Matches(game, staged));
}
static void TestMerge()
{
    var current = IniDocument.Parse("[GAMEPLAY]\nallow_invaders = 1\n[PASSWORD]\ncooppassword = \nnew_setting = 3\n");
    current.MergeValuesFrom(IniDocument.Parse("[GAMEPLAY]\nallow_invaders = 0\ncustom = yes\n[PASSWORD]\ncooppassword = secret\n"));
    var text = current.ToString();
    True(text.Contains("allow_invaders = 0\ncustom = yes\n[PASSWORD]"));
    True(text.Contains("new_setting = 3"));
    True(text.Contains("cooppassword = secret"));
}
static void TestDownloads()
{
    var root = Temp(); var dest = Path.Combine(root, "release.zip");
    var client = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2]) }));
    var asset = new ModAsset("mod.zip", "https://github.com/yuiamoroll/EldenRingSeamlessCoopRelease/releases/download/v1/mod.zip", 5, null);
    try { ModPackage.DownloadAsync(client, asset, dest).GetAwaiter().GetResult(); throw new Exception("accepted short download"); } catch (InvalidDataException) { }
    True(!File.Exists(dest) && !File.Exists(dest + ".partial"));
    try { ModPackage.DownloadAsync(client, asset with { Size = 2, Digest = "sha256:0000" }, dest).GetAwaiter().GetResult(); throw new Exception("accepted bad digest"); } catch (InvalidDataException) { }
    True(!File.Exists(dest));
}
static void TestOffline()
{
    var client = new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline")));
    try { new GitHubReleases(client).GetNewestAsync().GetAwaiter().GetResult(); throw new Exception("offline check succeeded"); } catch (HttpRequestException) { }
}
static void TestLockedLauncher()
{
    var root = Temp(); var game = Path.Combine(root, "Game"); var staged = Path.Combine(root, "stage"); Directory.CreateDirectory(game); Directory.CreateDirectory(staged);
    File.WriteAllText(Path.Combine(game, "eldenring.exe"), "game");
    foreach (var dir in new[] { game, staged }) { File.WriteAllText(Path.Combine(dir, "ersc_launcher.exe"), dir == game ? "old" : "new"); Directory.CreateDirectory(Path.Combine(dir, "SeamlessCoop")); File.WriteAllText(Path.Combine(dir, "SeamlessCoop", "ersc.dll"), dir == game ? "old" : "new"); File.WriteAllText(Path.Combine(dir, "SeamlessCoop", "ersc_settings.ini"), "cooppassword = secret"); }
    using (var locked = new FileStream(Path.Combine(game, "ersc_launcher.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
    {
        try { ModInstaller.Install(game, staged, Path.Combine(root, "backups")); throw new Exception("installed over locked launcher"); } catch (IOException) { }
    }
    Equal("old", File.ReadAllText(Path.Combine(game, "ersc_launcher.exe")));
    Equal("old", File.ReadAllText(Path.Combine(game, "SeamlessCoop", "ersc.dll")));
}
static void TestRealArchive()
{
    var path = Environment.GetEnvironmentVariable("ERSC_SAMPLE_ZIP");
    if (string.IsNullOrWhiteSpace(path)) { Console.WriteLine("  SKIP: set ERSC_SAMPLE_ZIP for live archive check"); return; }
    var extracted = Path.Combine(Temp(), "extracted");
    ModPackage.Extract(path, extracted);
    True(ModFingerprint.IsInstalled(extracted));
    True(IniDocument.Load(Path.Combine(extracted, "SeamlessCoop", "ersc_settings.ini")).Entries.Count > 10);
}
static void TestLiveRelease()
{
    if (Environment.GetEnvironmentVariable("ERSC_LIVE_RELEASE") != "1") { Console.WriteLine("  SKIP: set ERSC_LIVE_RELEASE=1 for network integration"); return; }
    using var client = new HttpClient();
    var release = new GitHubReleases(client).GetNewestAsync().GetAwaiter().GetResult();
    var asset = ReleaseCatalog.SelectZip(release);
    var root = Temp(); var zip = Path.Combine(root, "release.zip");
    ModPackage.DownloadAsync(client, asset, zip).GetAwaiter().GetResult();
    var stage = Path.Combine(root, "stage"); ModPackage.Extract(zip, stage);
    True(ModFingerprint.IsInstalled(stage));
    var game = Path.Combine(root, "Game"); Directory.CreateDirectory(game);
    File.WriteAllText(Path.Combine(game, "eldenring.exe"), "test game placeholder");
    ModInstaller.Install(game, stage, Path.Combine(root, "backups"));
    True(ModFingerprint.Matches(game, stage));
    True(StateStore.Matches(game, StateStore.Record(release.Tag, game, stage)));
    Console.WriteLine("  Verified " + release.Tag + " / " + asset.Name);
}
static void TestSettingControls()
{
    var ini = IniDocument.Parse("[GAMEPLAY]\nallow_invaders = 1\nallow_summons = 0\noverhead_player_display = 5\ndefault_boot_master_volume = 7\n[SCALING]\nenemy_health_scaling = 350\n[PASSWORD]\ncooppassword = secret\n[OTHER]\nfuture_setting = custom\n");
    var entries = ini.Entries.ToDictionary(e => e.Key);
    Equal(SettingKind.Toggle, SettingPresentation.For(entries["allow_invaders"]).Kind);
    Equal(SettingKind.Toggle, SettingPresentation.For(entries["allow_summons"]).Kind);
    var overhead = SettingPresentation.For(entries["overhead_player_display"]);
    Equal(SettingKind.Choice, overhead.Kind);
    Equal(6, overhead.Options.Count);
    Equal("Soul level and ping", overhead.Options.Single(o => o.Value == "5").Label);
    var volume = SettingPresentation.For(entries["default_boot_master_volume"]);
    Equal(SettingKind.Slider, volume.Kind);
    Equal(0, volume.Minimum); Equal(10, volume.Maximum);
    var scaling = SettingPresentation.For(entries["enemy_health_scaling"]);
    Equal(SettingKind.Slider, scaling.Kind);
    True(scaling.Maximum >= 350);
    Equal(SettingKind.Password, SettingPresentation.For(entries["cooppassword"]).Kind);
    Equal(SettingKind.Text, SettingPresentation.For(entries["future_setting"]).Kind);
    Equal(SettingKind.Text, SettingPresentation.For(entries["allow_invaders"] with { Value = "2" }).Kind);
    Equal(SettingKind.Text, SettingPresentation.For(entries["overhead_player_display"] with { Value = "99" }).Kind);
    Equal(SettingKind.Text, SettingPresentation.For(entries["default_boot_master_volume"] with { Value = "15" }).Kind);
}
static void TestLauncherRelease()
{
    const string repo = "friend/ERSC-Launcher";
    var asset = new LauncherAsset("ERSCLauncher-1.2.0-win-x64.exe", "https://github.com/friend/ERSC-Launcher/releases/download/v1.2.0/ERSCLauncher-1.2.0-win-x64.exe", 3, "sha256:" + new string('A', 64));
    var stable = new LauncherRelease("v1.2.0", false, false, [asset]);
    var beta = new LauncherRelease("v9.0.0", true, false, [asset]);
    Equal("v1.2.0", LauncherUpdates.SelectNewer([beta, stable], new Version(1, 1, 1))!.Tag);
    True(LauncherUpdates.SelectNewer([stable], new Version(1, 2, 0)) is null);
    Equal(asset, LauncherUpdates.SelectAsset(stable, repo));
    Equal("v1.2.0", LauncherUpdates.SelectNewerUsable([new LauncherRelease("v1.3.0", false, false, []), stable], new Version(1, 1, 1), repo)!.Tag);
    try { LauncherUpdates.SelectAsset(stable with { Assets = [asset with { Digest = null }] }, repo); throw new Exception("accepted missing digest"); } catch (InvalidDataException) { }
    try { LauncherUpdates.SelectAsset(stable with { Assets = [asset with { Url = "https://evil.test/ERSCLauncher.exe" }] }, repo); throw new Exception("accepted foreign URL"); } catch (InvalidDataException) { }
}
static void TestLauncherDownload()
{
    var root = Temp(); var destination = Path.Combine(root, "candidate.exe");
    var bytes = new byte[] { 1, 2, 3 }; var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes));
    var asset = new LauncherAsset("candidate.exe", "https://github.com/friend/ERSC-Launcher/releases/download/v1.2.0/candidate.exe", 3, digest);
    using var client = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
    LauncherUpdates.DownloadAsync(client, asset, destination).GetAwaiter().GetResult();
    True(File.ReadAllBytes(destination).SequenceEqual(bytes));
    try { LauncherUpdates.DownloadAsync(client, asset with { Size = 4 }, destination).GetAwaiter().GetResult(); throw new Exception("accepted short file"); } catch (InvalidDataException) { }
    try { LauncherUpdates.DownloadAsync(client, asset with { Digest = "sha256:" + new string('0', 64) }, destination).GetAwaiter().GetResult(); throw new Exception("accepted wrong hash"); } catch (InvalidDataException) { }
    True(File.ReadAllBytes(destination).SequenceEqual(bytes) && !File.Exists(destination + ".partial"));
}
static void TestLauncherReplacement()
{
    var root = Temp(); var app = Path.Combine(root, "My Launcher.exe"); var payload = Path.Combine(root, "new.exe"); var backup = Path.Combine(root, "old.exe");
    File.WriteAllText(app, "old"); File.WriteAllText(payload, "new");
    LauncherReplacement.Replace(app, payload, backup, _ => { });
    Equal("new", File.ReadAllText(app)); Equal("old", File.ReadAllText(backup));
    File.WriteAllText(payload, "next");
    try { LauncherReplacement.Replace(app, payload, backup, _ => throw new IOException("launch failed")); throw new Exception("accepted failed launch"); } catch (IOException ex) when (ex.Message == "launch failed") { }
    Equal("new", File.ReadAllText(app));
    Equal("My Launcher.exe", Path.GetFileName(app));
    using (var locked = new FileStream(app, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        try { LauncherReplacement.Replace(app, payload, backup, _ => { }); throw new Exception("accepted locked target"); } catch (IOException) { }
    }
    Equal("new", File.ReadAllText(app));
}
static void TestLauncherError()
{
    var folder = Temp();
    LauncherUpdateErrors.Write(folder, "Access denied");
    Equal("Access denied", LauncherUpdateErrors.Take(folder));
    True(LauncherUpdateErrors.Take(folder) is null);
}
static void TestLiveLauncherUpdate()
{
    if (Environment.GetEnvironmentVariable("ERSC_LIVE_LAUNCHER_UPDATE") != "1") { Console.WriteLine("  SKIP: set ERSC_LIVE_LAUNCHER_UPDATE=1 for public release integration"); return; }
    using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    var release = new GitHubLauncherReleases(client, "maxazarcon/ERSC-Launcher").GetNewerAsync(new Version(1, 1, 1)).GetAwaiter().GetResult();
    True(release is not null && LauncherUpdates.ParseVersion(release.Tag)! > new Version(1, 1, 1));
    var asset = LauncherUpdates.SelectAsset(release!, "maxazarcon/ERSC-Launcher");
    var root = Temp(); var downloaded = Path.Combine(root, asset.Name);
    LauncherUpdates.DownloadAsync(client, asset, downloaded).GetAwaiter().GetResult();
    var app = Path.Combine(root, "Friends Launcher.exe"); var backup = Path.Combine(root, "previous.exe");
    File.WriteAllText(app, "older portable build");
    string? started = null;
    LauncherReplacement.Replace(app, downloaded, backup, path => started = path);
    Equal(app, started);
    Equal("older portable build", File.ReadAllText(backup));
    using var updated = File.OpenRead(app);
    Equal(asset.Digest![7..].ToUpperInvariant(), Convert.ToHexString(SHA256.HashData(updated)));
}
static void Add(ZipArchive z, string name, string content) { using var w = new StreamWriter(z.CreateEntry(name).Open()); w.Write(content); }
sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
}
