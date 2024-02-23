using System.Text.RegularExpressions;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
// using System.Text.Json;
using System.Globalization;
using CUE4Parse.UE4.Localization;
using CUE4Parse.Utils;

try
{
    double start = now();
    var totalFiles = 0;

    string jsonString = File.ReadAllText("config.json");
    List<ConfigObj> configs = JsonConvert.DeserializeObject<List<ConfigObj>>(jsonString);

    foreach (ConfigObj config in configs)
    {

        // Get game version from config
        string version = "";

        // "4.27"
        if (config.version.Contains("."))
        {
            version = $"UE{config.version.Replace('.', '_')}";
        }
        // "tower of fantasy"
        else if (config.version.Split(" ").Length > 1)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            version = textInfo.ToTitleCase(config.version).Replace(" ", "");
        }
        // "TowerOfFantasy"
        else
        {
            version = config.version;
        }

        EGame selectedVersion = (EGame)Enum.Parse(typeof(EGame), $"GAME_{version}");

        Console.WriteLine($"Version: {selectedVersion}");
        Console.WriteLine($"Paks: {config.paksDir}");
        Console.WriteLine($"Output: {config.outputDir}");
        Console.WriteLine($"AES key: {config.aes}");
        Console.WriteLine($"Keep directory structure: {config.keepDirectoryStructure}");

        // Load CUE4Parse
        var provider = new DefaultFileProvider(config.paksDir, SearchOption.TopDirectoryOnly, true, new VersionContainer(selectedVersion));
        provider.Initialize();

        // Decrypt if AES key is provided
        if (config.aes.Length > 0)
        {
            provider.SubmitKey(new FGuid(), new FAesKey(config.aes));
        }

        // Loop through all files and export the ones that match any of the targetFilePaths (converted to regex)
        foreach (var file in provider.Files)
        {
            bool isTargetPath = config.targetFilePaths.Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
            bool isExcludedPath = config.excludedPaths.Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));

            if (isTargetPath && !isExcludedPath)
            {
                var outputDir = Path.GetFullPath(config.outputDir);

                if (config.keepDirectoryStructure == true)
                {
                    outputDir =
                        Path.GetFullPath(config.outputDir)
                        + Path.DirectorySeparatorChar
                        + Path.GetDirectoryName(file.Value.Path);
                }

                var fileName =
                    Path.GetFileNameWithoutExtension(file.Value.Path)
                    + ".json";
                var fullFilePath =
                    outputDir
                    + Path.DirectorySeparatorChar
                    + fileName;

                try
                {
                    var fileExtension = file.Value.Path.SubstringAfterLast('.').ToLower();
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine("=> " + fullFilePath);

                    switch (fileExtension)
                    {
                        case "locres":
                            {
                                if (provider.TryCreateReader(file.Value.Path, out var archive))
                                {

                                    var locres = new FTextLocalizationResource(archive);
                                    var json = JsonConvert.SerializeObject(locres, Formatting.Indented);
                                    File.WriteAllText(fullFilePath, json);
                                }
                                break;
                            }
                        case "uasset":
                        case "umap":
                            {
                                // Load all objects in the .uasset/.umap file, serialize to JSON, then write to file
                                var allObjects = provider.LoadAllObjects(file.Value.Path);
                                var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                                File.WriteAllText(fullFilePath, json);
                                break;
                            }
                    }
                }
                catch (AggregateException)
                {
                    var filePathAndName = Path.GetDirectoryName(file.Value.Path) + Path.DirectorySeparatorChar + fileName;
                    Console.WriteLine($"File cannot be opened: {filePathAndName}. Possible issues include incorrect UE version in config.json, or this file type is not supported.");
                }

                totalFiles++;
            }
        }

        Console.WriteLine();
    }

    Console.WriteLine("Exported " + totalFiles + " files in " + elapsed(start, now(), 1000) + "seconds");
}
catch (FileNotFoundException)
{
    Console.WriteLine("config.json not found.");
}
catch (JsonException)
{
    Console.WriteLine("config.json is not a valid JSON format.");
}

static double now()
{
    return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
}

static string elapsed(double start, double end, int factor = 1)
{
    return ((end - start) / factor).ToString("0.00");
}

public class ConfigObj
{
    public string version { get; set; }
    public string paksDir { get; set; }
    public string outputDir { get; set; }
    public string aes { get; set; }
    public bool keepDirectoryStructure { get; set; }
    public List<string> targetFilePaths { get; set; }
    public List<string> excludedPaths { get; set; }
}