using System.Text.RegularExpressions;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

// Load .env file
var root = Directory.GetCurrentDirectory();
var dotenv = Path.Combine(root, ".env");
DotEnv.Load(dotenv);
var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

var pathToPaks = config["PATH_TO_PAKS"] ?? "";
var outputDir = config["OUTPUT_DIR"] ?? ""; // Creates a folder "Content" at the specified location
var aesKey = config["AES_KEY"] ?? "";

var provider = new DefaultFileProvider(pathToPaks, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_UE4_27));
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey(aesKey));

// provider.MountedVfs contains paths to .pak files; provider.Files contains individual file paths
// Also consider: provider.Files.Keys, provider.Files.Values, (file).Key, (file).Value

// Create a list of regexes based on ExportList.txt
var targetFilePaths = new List<Regex>();
foreach (var regex in File.ReadAllLines("ExportList.txt"))
    targetFilePaths.Add(new Regex("^" + regex + "$", RegexOptions.IgnoreCase));

double start = now();
var totalFiles = 0;

// Loop through all files and export the ones that match any of the regexes in ExportList.txt
foreach (var file in provider.Files)
{
    if (targetFilePaths.Any(regex => regex.IsMatch(file.Value.Path)))
    {
        var filePath = (Path.GetFullPath(outputDir) + Path.GetDirectoryName(file.Value.Path)).Replace("BLUEPROTOCOL", "");
        var fileName = Path.GetFileNameWithoutExtension(file.Value.Path) + ".json";
        Directory.CreateDirectory(filePath);
        Console.WriteLine("=> " + filePath + Path.DirectorySeparatorChar + fileName);

        // Load all objects in the .uasset/.umap file, serialize to JSON, then write to file
        var allObjects = provider.LoadAllObjects(file.Value.Path);
        var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
        File.WriteAllText(filePath + Path.DirectorySeparatorChar + fileName, json);
        
        totalFiles++;
    }
}

Console.WriteLine("Exported " + totalFiles + " files in " + elapsed(start, now(), 1000) + "seconds");

static double now()
{
    return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
}

static string elapsed(double start, double end, int factor = 1)
{
    return ((end - start) / factor).ToString("0.00");
}