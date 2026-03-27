using System.Net.Http.Headers;
using System.Text.RegularExpressions;

var server = Environment.GetEnvironmentVariable("RWLK_SERVER") ?? "https://rwlk.net";
using var http = new HttpClient { BaseAddress = new Uri(server) };

if (args.Length == 0)
{
    PrintUsage();
    return;
}

if (args.Length == 1)
{
    var arg = args[0];

    if (arg is "-h" or "--help")
    {
        PrintUsage();
    }
    else if (arg.StartsWith("http://") || arg.StartsWith("https://"))
    {
        await Shorten(arg);
    }
    else if (Regex.IsMatch(arg, @"^\d{3,6}$"))
    {
        await OpenSlug(arg);
    }
    else if (File.Exists(arg))
    {
        await UploadAnonymous(arg);
    }
    else if (IsFileKey(arg))
    {
        await DownloadByKey(arg);
    }
    else
    {
        Console.Error.WriteLine($"Not a URL, slug, existing file, or file key: {arg}");
        return;
    }
}
else if (args.Length == 2)
{
    var key = args[0];
    var filePath = args[1];

    if (!IsFileKey(key))
    {
        Console.Error.WriteLine($"Invalid file key: {key}");
        return;
    }
    await UploadByKey(key, filePath);
}
else
{
    PrintUsage();
}

bool IsFileKey(string s) => Regex.IsMatch(s, @"^[a-z0-9]{12}$");

async Task Shorten(string url)
{
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent(url), "url");

    var resp = await http.PostAsync("/api/links", form);
    var body = await resp.Content.ReadAsStringAsync();

    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Error: {body}");
        return;
    }
    Console.WriteLine(body);
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
        await using var fs = File.Create(fileName);
        await resp.Content.CopyToAsync(fs);
        Console.WriteLine($"Downloaded: {fileName}");
        return;
    }

    // Otherwise print final URL
    Console.WriteLine(resp.RequestMessage?.RequestUri);
}

async Task UploadAnonymous(string filePath)
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
        return;
    }
    Console.WriteLine(body);
}

async Task UploadByKey(string key, string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File not found: {filePath}");
        return;
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
        return;
    }
    Console.WriteLine(body);
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
    await using var fs = File.Create(fileName);
    await resp.Content.CopyToAsync(fs);
    Console.WriteLine($"Downloaded: {fileName}");
}

void PrintUsage()
{
    Console.WriteLine("""
        rwlk - CLI for rwlk.net

        Usage:
          rwlk <url>                Shorten a URL
          rwlk <slug>              Open a link / download a file by slug
          rwlk <file>              Upload a file anonymously (max 100 KB, 30 days)
          rwlk <key> <file>        Upload a file using a file key
          rwlk <key>               Download a file using a file key

        Environment variables:
          RWLK_SERVER  Server URL (default: https://rwlk.net)
        """);
}
