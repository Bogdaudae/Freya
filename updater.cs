using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class FreyaUpdater
{
    private static readonly string versionFile = "version.txt";
    private static readonly string updatesUrl = "https://raw.githubusercontent.com/Bogdaudae/Freya/main/update.json";
    private static readonly HttpClient http = new HttpClient();

    public static async Task Main()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("=== Freya Updater Started ===\n");

        if (!File.Exists(versionFile))
        {
            Console.WriteLine("version.txt not found. Exiting...");
            return;
        }

        string currentVersion = File.ReadAllText(versionFile).Trim();
        Console.WriteLine($"Current version: {currentVersion}");

        Console.WriteLine("Fetching update info...");
        string json = await http.GetStringAsync(updatesUrl);
        var data = JsonSerializer.Deserialize<UpdateData>(json);

        if (data?.updates == null || data.updates.Count == 0)
        {
            Console.WriteLine("No updates found in JSON.");
            return;
        }

        // Find list of updates that are newer
        var updateList = new List<UpdateItem>();
        bool startCollecting = false;
        foreach (var item in data.updates)
        {
            if (item.version == currentVersion)
            {
                startCollecting = true;
                continue;
            }

            if (startCollecting)
                updateList.Add(item);
        }

        if (updateList.Count == 0)
        {
            Console.WriteLine("Already up to date!");
            return;
        }

        Console.WriteLine($"Found {updateList.Count} updates to apply.");

        foreach (var update in updateList)
        {
            Console.WriteLine($"\nUpdating to version {update.version} ...");
            await ApplyUpdate(update);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll updates applied successfully!");
        Console.ResetColor();
    }

    private static async Task ApplyUpdate(UpdateItem update)
    {
        string tempZip = Path.Combine(Path.GetTempPath(), $"FreyaUpdate_{update.version}.zip");

        Console.WriteLine($"Downloading: {update.download_url}");
        using (var stream = await http.GetStreamAsync(update.download_url))
        using (var fs = new FileStream(tempZip, FileMode.Create))
        {
            await stream.CopyToAsync(fs);
        }

        Console.WriteLine("Extracting files...");
        ZipFile.ExtractToDirectory(tempZip, Directory.GetCurrentDirectory(), true);
        File.Delete(tempZip);

        string toDeletePath = Path.Combine(Directory.GetCurrentDirectory(), "todelete.txt");
        if (File.Exists(toDeletePath))
        {
            Console.WriteLine("Processing todelete.txt...");
            await HandleDeletions(toDeletePath);
            File.Delete(toDeletePath);
        }

        Console.WriteLine($"Version {update.version} applied.\n");
    }

    private static async Task HandleDeletions(string filePath)
    {
        string baseDir = Directory.GetCurrentDirectory();
        string[] lines = await File.ReadAllLinesAsync(filePath);
        string currentPath = "";

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.EndsWith("\\"))
            {
                currentPath = Path.Combine(baseDir, line.TrimEnd('\\'));
                continue;
            }

            string fullPath = Path.Combine(currentPath == "" ? baseDir : currentPath, line);

            try
            {
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    Console.WriteLine($"Deleted folder: {fullPath}");
                }
                else if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    Console.WriteLine($"Deleted file: {fullPath}");
                }
                else
                {
                    Console.WriteLine($"Not found: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting {fullPath}: {ex.Message}");
            }
        }
    }

    private class UpdateData
    {
        public List<UpdateItem> updates { get; set; }
    }

    private class UpdateItem
    {
        public string version { get; set; }
        public string download_url { get; set; }
    }
}

