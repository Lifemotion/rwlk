using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Win32;

var server = Environment.GetEnvironmentVariable("RWLK_SERVER") ?? "https://rwlk.net";

// --share is set by the file manager context menu; -x/--extract unpacks downloaded zips.
var shareMode = args.Any(a => a == "--share");
var extractMode = args.Any(a => a is "-x" or "--extract");
var cliArgs = args.Where(a => a is not "--share" and not "-x" and not "--extract").ToArray();

// Handle install/uninstall (no server connection needed)
if (cliArgs.Length == 1 && cliArgs[0] == "install")
{
    InstallContextMenu();
    return;
}
if (cliArgs.Length == 1 && cliArgs[0] == "uninstall")
{
    UninstallContextMenu();
    return;
}

using var http = new HttpClient { BaseAddress = new Uri(server) };
string? result = null;

if (cliArgs.Length == 0)
{
    PrintUsage();
    return;
}

if (cliArgs.Length == 1)
{
    var arg = cliArgs[0];

    if (arg is "-h" or "--help")
    {
        PrintUsage();
    }
    else if (arg.StartsWith("http://") || arg.StartsWith("https://"))
    {
        result = await Shorten(arg);
    }
    else if (Regex.IsMatch(arg, @"^\d{3,6}$"))
    {
        await OpenSlug(arg);
    }
    else if (File.Exists(arg))
    {
        result = await UploadAnonymous(arg);
    }
    else if (Directory.Exists(arg))
    {
        result = await UploadDirectory(UploadAnonymous, arg);
    }
    else if (IsFileKey(arg))
    {
        await DownloadByKey(arg);
    }
    else
    {
        Console.Error.WriteLine($"Not a URL, slug, existing file, or file key: {arg}");
    }
}
else if (cliArgs.Length == 2)
{
    var key = cliArgs[0];
    var filePath = cliArgs[1];

    if (!IsFileKey(key))
    {
        Console.Error.WriteLine($"Invalid file key: {key}");
    }
    else if (File.Exists(filePath))
    {
        result = await UploadByKey(key, filePath);
    }
    else if (Directory.Exists(filePath))
    {
        result = await UploadDirectory(p => UploadByKey(key, p), filePath);
    }
    else
    {
        Console.Error.WriteLine($"File or directory not found: {filePath}");
    }
}
else
{
    PrintUsage();
}

// Share mode: copy result to clipboard and notify
if (shareMode)
{
    if (result != null)
    {
        CopyToClipboard(result);

        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("Copied to clipboard.");
            Console.WriteLine("Press any key to close.");
            Console.ReadKey(true);
        }
        else if (OperatingSystem.IsLinux())
        {
            Notify("rwlk", $"Link copied: {result}");
        }
    }
    else if (OperatingSystem.IsWindows())
    {
        Console.WriteLine("Press any key to close.");
        Console.ReadKey(true);
    }
}

bool IsFileKey(string s) => Regex.IsMatch(s, @"^[a-z0-9]{12}$");

async Task<string?> Shorten(string url)
{
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent(url), "url");

    var resp = await http.PostAsync("/api/links", form);
    var body = await resp.Content.ReadAsStringAsync();

    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Error: {body}");
        return null;
    }
    Console.WriteLine(body);
    return body;
}

async Task OpenSlug(string slug)
{
    var resp = await http.GetAsync($"/{slug}");

    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Link not found: {slug}");
        return;
    }

    // If it was a redirect, print the target URL
    if (resp.RequestMessage?.RequestUri?.Host != http.BaseAddress?.Host)
    {
        Console.WriteLine(resp.RequestMessage?.RequestUri);
        return;
    }

    // If it's a file download
    if (resp.Content.Headers.ContentDisposition?.FileName != null)
    {
        var fileName = resp.Content.Headers.ContentDisposition.FileName.Trim('"');
        await using (var fs = File.Create(fileName))
        {
            await resp.Content.CopyToAsync(fs);
        }
        Console.WriteLine($"Downloaded: {fileName}");
        if (extractMode) ExtractIfZip(fileName);
        return;
    }

    // Otherwise print final URL
    Console.WriteLine(resp.RequestMessage?.RequestUri);
}

async Task<string?> UploadAnonymous(string filePath)
{
    using var form = new MultipartFormDataContent();
    var fileBytes = await File.ReadAllBytesAsync(filePath);
    var fileContent = new ByteArrayContent(fileBytes);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    form.Add(fileContent, "file", Path.GetFileName(filePath));

    var resp = await http.PostAsync("/api/links", form);
    var body = await resp.Content.ReadAsStringAsync();

    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Error: {body}");
        return null;
    }
    Console.WriteLine(body);
    return body;
}

async Task<string?> UploadByKey(string key, string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File not found: {filePath}");
        return null;
    }

    using var form = new MultipartFormDataContent();
    var fileBytes = await File.ReadAllBytesAsync(filePath);
    var fileContent = new ByteArrayContent(fileBytes);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    form.Add(fileContent, "file", Path.GetFileName(filePath));

    var resp = await http.PostAsync($"/api/file/{key}", form);
    var body = await resp.Content.ReadAsStringAsync();

    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Error: {body}");
        return null;
    }
    Console.WriteLine(body);
    return body;
}

async Task<string?> UploadDirectory(Func<string, Task<string?>> upload, string dirPath)
{
    var dirName = new DirectoryInfo(dirPath).Name;
    var tmpDir = Path.Combine(Path.GetTempPath(), $"rwlk-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tmpDir);
    var zipPath = Path.Combine(tmpDir, $"{dirName}.zip");
    try
    {
        ZipFile.CreateFromDirectory(dirPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return await upload(zipPath);
    }
    finally
    {
        try { Directory.Delete(tmpDir, recursive: true); } catch { }
    }
}

void ExtractIfZip(string zipPath)
{
    if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        return;

    var extractDir = Path.GetFileNameWithoutExtension(zipPath);
    try
    {
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
        File.Delete(zipPath);
        Console.WriteLine($"Extracted to: {extractDir}/");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Extraction failed: {ex.Message}");
    }
}

async Task DownloadByKey(string key)
{
    var resp = await http.GetAsync($"/api/file/{key}");

    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Error: {await resp.Content.ReadAsStringAsync()}");
        return;
    }

    var fileName = resp.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? key;
    await using (var fs = File.Create(fileName))
    {
        await resp.Content.CopyToAsync(fs);
    }
    Console.WriteLine($"Downloaded: {fileName}");
    if (extractMode) ExtractIfZip(fileName);
}

void CopyToClipboard(string text)
{
    try
    {
        string cmd;
        string cmdArgs = "";

        if (OperatingSystem.IsWindows())
        {
            cmd = "clip";
        }
        else if (OperatingSystem.IsLinux())
        {
            if (HasCommand("wl-copy")) { cmd = "wl-copy"; }
            else if (HasCommand("xclip")) { cmd = "xclip"; cmdArgs = "-selection clipboard"; }
            else if (HasCommand("xsel")) { cmd = "xsel"; cmdArgs = "--clipboard --input"; }
            else return;
        }
        else return;

        using var process = Process.Start(new ProcessStartInfo(cmd, cmdArgs)
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (process != null)
        {
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit();
        }
    }
    catch { }
}

void Notify(string title, string message)
{
    try
    {
        Process.Start(new ProcessStartInfo("notify-send", $"\"{title}\" \"{message}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })?.WaitForExit();
    }
    catch { }
}

bool HasCommand(string command)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo("which", command)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
        return process?.ExitCode == 0;
    }
    catch { return false; }
}

void InstallContextMenu()
{
    var exePath = Environment.ProcessPath;
    if (string.IsNullOrEmpty(exePath))
    {
        Console.Error.WriteLine("Could not determine executable path.");
        return;
    }

    if (OperatingSystem.IsWindows())
        InstallWindows(exePath);
    else if (OperatingSystem.IsLinux())
        InstallLinux(exePath);
    else
        Console.Error.WriteLine("Unsupported platform.");
}

void UninstallContextMenu()
{
    if (OperatingSystem.IsWindows())
        UninstallWindows();
    else if (OperatingSystem.IsLinux())
        UninstallLinux();
    else
        Console.Error.WriteLine("Unsupported platform.");
}

void InstallWindows(string exePath)
{
    var icoPath = Path.Combine(Path.GetDirectoryName(exePath)!, "rwlk.ico");
    using var shellKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\rwlk");
    shellKey.SetValue("", "Share with rwlk");
    shellKey.SetValue("Icon", File.Exists(icoPath) ? icoPath : $"{exePath},0");

    using var commandKey = shellKey.CreateSubKey("command");
    commandKey.SetValue("", $"\"{exePath}\" --share \"%1\"");

    Console.WriteLine("Context menu installed.");
    Console.WriteLine($"Path: {exePath}");
}

void UninstallWindows()
{
    try
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\rwlk");
        Console.WriteLine("Context menu removed.");
    }
    catch (ArgumentException)
    {
        Console.Error.WriteLine("Context menu entry not found.");
    }
}

void InstallLinux(string exePath)
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var installed = new List<string>();

    // Nautilus (GNOME Files)
    var nautilusDir = Path.Combine(home, ".local/share/nautilus/scripts");
    if (Directory.Exists(Path.Combine(home, ".local/share/nautilus")) || HasCommand("nautilus"))
    {
        Directory.CreateDirectory(nautilusDir);
        var scriptPath = Path.Combine(nautilusDir, "Share with rwlk");
        File.WriteAllText(scriptPath,
            $"#!/bin/bash\nfile=\"$(echo \"$NAUTILUS_SCRIPT_SELECTED_FILE_PATHS\" | head -1)\"\n" +
            $"[ -n \"$file\" ] && exec \"{exePath}\" --share \"$file\"\n");
        Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit();
        installed.Add("Nautilus");
    }

    // Dolphin (KDE)
    var dolphinDir = Path.Combine(home, ".local/share/kio/servicemenus");
    if (Directory.Exists(Path.Combine(home, ".local/share/kio")) || HasCommand("dolphin"))
    {
        Directory.CreateDirectory(dolphinDir);
        File.WriteAllText(Path.Combine(dolphinDir, "rwlk.desktop"),
            "[Desktop Entry]\nType=Service\nMimeType=all/allfiles;\nActions=rwlk\n\n" +
            $"[Desktop Action rwlk]\nName=Share with rwlk\nIcon=document-send\nExec=\"{exePath}\" --share %f\n");
        installed.Add("Dolphin");
    }

    // Nemo (Cinnamon)
    var nemoDir = Path.Combine(home, ".local/share/nemo/actions");
    if (Directory.Exists(Path.Combine(home, ".local/share/nemo")) || HasCommand("nemo"))
    {
        Directory.CreateDirectory(nemoDir);
        File.WriteAllText(Path.Combine(nemoDir, "rwlk.nemo_action"),
            "[Nemo Action]\nName=Share with rwlk\nIcon-Name=document-send\n" +
            $"Exec=\"{exePath}\" --share %F\nSelection=s\nExtensions=any;\n");
        installed.Add("Nemo");
    }

    if (installed.Count > 0)
    {
        Console.WriteLine($"Context menu installed for: {string.Join(", ", installed)}");
        Console.WriteLine($"Path: {exePath}");
    }
    else
    {
        Console.Error.WriteLine("No supported file manager found (Nautilus, Dolphin, Nemo).");
    }
}

void UninstallLinux()
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var removed = new List<string>();

    var nautilusScript = Path.Combine(home, ".local/share/nautilus/scripts/Share with rwlk");
    if (File.Exists(nautilusScript)) { File.Delete(nautilusScript); removed.Add("Nautilus"); }

    var dolphinDesktop = Path.Combine(home, ".local/share/kio/servicemenus/rwlk.desktop");
    if (File.Exists(dolphinDesktop)) { File.Delete(dolphinDesktop); removed.Add("Dolphin"); }

    var nemoAction = Path.Combine(home, ".local/share/nemo/actions/rwlk.nemo_action");
    if (File.Exists(nemoAction)) { File.Delete(nemoAction); removed.Add("Nemo"); }

    if (removed.Count > 0)
        Console.WriteLine($"Context menu removed for: {string.Join(", ", removed)}");
    else
        Console.Error.WriteLine("No context menu entries found.");
}

void PrintUsage()
{
    Console.WriteLine("""
        rwlk - CLI for rwlk.net

        Usage:
          rwlk <url>                Shorten a URL
          rwlk <slug>               Open a link / download a file by slug
          rwlk <file>               Upload a file anonymously (max 100 KB, 30 days)
          rwlk <folder>             Zip and upload a folder anonymously
          rwlk <key> <file>         Upload a file using a file key
          rwlk <key> <folder>       Zip and upload a folder using a file key
          rwlk <key>                Download a file using a file key
          rwlk install              Add to file manager context menu
          rwlk uninstall            Remove from file manager context menu

        Flags:
          -x, --extract             Extract downloaded .zip archives

        Environment variables:
          RWLK_SERVER  Server URL (default: https://rwlk.net)
        """);
}
