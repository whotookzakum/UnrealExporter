using Newtonsoft.Json;
using System.Globalization;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using CUE4Parse.UE4.Localization;
using System.Collections.Concurrent;
using CUE4Parse.MappingsProvider;

namespace UnrealExporter;

public class UnrealExporter
{
    private static int totalFiles = 0;
    private static int totalRegexMatches = 0;
    private static int totalExportedFiles = 0;
    private static bool useCheckpoint = false;

    public static List<ConfigObj> LoadConfigFile(string path)
    {
        try
        {
            string jsonString = File.ReadAllText(path);
            List<ConfigObj> configObjs = JsonConvert.DeserializeObject<List<ConfigObj>>(jsonString) ?? [];
            return configObjs;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"ERROR: {path} not found.");
        }
        catch (JsonException)
        {
            Console.WriteLine($"ERROR: {path} is not a valid JSON format.");
        }
        return [];
    }

    public static List<ConfigObj> LoadAllConfigs(string[] args)
    {
        List<ConfigObj> allConfigObjs = [];

        if (args.Length > 0)
        {
            int totalConfigFiles = 0;

            // Load all files
            if (args[0].ToLower().Equals("all"))
            {
                string[] filePaths = Directory.GetFiles($"{Directory.GetCurrentDirectory()}\\configs");
                foreach (var filePath in filePaths)
                {
                    List<ConfigObj> configObjsInFile = LoadConfigFile(filePath);
                    foreach (ConfigObj configObj in configObjsInFile)
                    {
                        allConfigObjs.Add(configObj);
                    }
                    if (configObjsInFile.Count > 0)
                    {
                        totalConfigFiles++;
                        Console.WriteLine(filePath.Split(Path.DirectorySeparatorChar).Last());
                    }
                }
            }
            // Load specified files
            else
            {
                foreach (var arg in args)
                {
                    List<ConfigObj> configObjsInFile = LoadConfigFile($"{Directory.GetCurrentDirectory()}\\configs\\{arg}.json");
                    foreach (ConfigObj configObj in configObjsInFile)
                    {
                        allConfigObjs.Add(configObj);
                    }
                    if (configObjsInFile.Count > 0)
                    {
                        totalConfigFiles++;
                        Console.WriteLine($"{arg}.json");
                    }
                }
            }

            Console.WriteLine($"Loaded {totalConfigFiles} config files ({allConfigObjs.Count} total objects)");
        }
        // Fallback to default config.json
        else
        {
            Console.WriteLine("No config files specified. Defaulting to config.json..");
            List<ConfigObj> configObjsInFile = LoadConfigFile($"{Directory.GetCurrentDirectory()}\\configs\\config.json");
            foreach (ConfigObj configObj in configObjsInFile)
            {
                allConfigObjs.Add(configObj);
            }
            if (configObjsInFile.Count > 0)
            {
                Console.WriteLine($"Loaded config.json ({allConfigObjs.Count} total objects)");
            }
        }
        Console.WriteLine();

        return allConfigObjs;
    }

    public static void Main(string[] args)
    {
        double trueStart = Now();

        List<ConfigObj> configs = LoadAllConfigs(args);

        foreach (ConfigObj config in configs)
        {
            double start = Now();
            totalFiles = 0;
            totalRegexMatches = 0;
            totalExportedFiles = 0;

            EGame selectedVersion = GetGameVersion(config.Version);
            Console.WriteLine($"Game: {config.GameTitle}");
            Console.WriteLine($"Version: {selectedVersion}");
            Console.WriteLine($"Locale: {config.Lang}");
            Console.WriteLine($"Paks: {config.PaksDir}");
            Console.WriteLine($"Output: {config.OutputDir}");
            Console.WriteLine($"AES key: {config.Aes}");
            Console.WriteLine($"Log output files: {config.LogOutputs}");
            Console.WriteLine($"Keep directory structure: {config.KeepDirectoryStructure}");
            Console.WriteLine($"Include JSONs in PNG paths: {config.IncludeJsonsInPngPaths}");
            Console.WriteLine($"Create new checkpoint: {config.CreateNewCheckpoint}");

            // Load CUE4Parse and export files
            AbstractFileProvider provider = CreateProvider(config, selectedVersion);
            Export(provider, config, start);
        }

        Console.WriteLine($"Finished in {Elapsed(trueStart, Now(), 1000)} seconds");
    }

    public static EGame GetGameVersion(string versionString)
    {
        string version;

        // "4.27"
        if (versionString.Contains('.'))
        {
            version = $"UE{versionString.Replace('.', '_')}";
        }
        // "tower of fantasy"
        else if (versionString.Split(" ").Length > 1)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            version = textInfo.ToTitleCase(versionString).Replace(" ", "");
        }
        // "TowerOfFantasy"
        else
        {
            version = versionString;
        }

        EGame selectedVersion = (EGame)Enum.Parse(typeof(EGame), $"GAME_{version}");

        return selectedVersion;
    }

    public static AbstractFileProvider CreateProvider(ConfigObj config, EGame selectedVersion)
    {
        // Load CUE4Parse
        var provider = new DefaultFileProvider(config.PaksDir, SearchOption.TopDirectoryOnly, true, new VersionContainer(selectedVersion));
        provider.Initialize();

        // Decrypt
        string aes = config.Aes.Length > 0 ? config.Aes : "0x0000000000000000000000000000000000000000000000000000000000000000";
        provider.SubmitKey(new FGuid(), new FAesKey(aes));

        // Set locale if provided, otherwise English
        if (config.Lang?.Length > 0)
        {
            ELanguage selectedLang = (ELanguage)Enum.Parse(typeof(ELanguage), config.Lang);
            provider.LoadLocalization(selectedLang);
        }
        else
        {
            provider.LoadLocalization(ELanguage.English);
        }

        // Load files into PatchFileProvider so the patch uassets override original uassets
        var patchProvider = new PatchFileProvider();
        patchProvider.Load(provider);

        // Add mapping file based on GameTitle if provided
        string pathToMappingFile = $"{Directory.GetCurrentDirectory()}\\mappings\\{config.GameTitle}.usmap";
        if (File.Exists(pathToMappingFile))
        {
            Console.WriteLine($"Using mapping file: {pathToMappingFile}");
            patchProvider.MappingsContainer = new FileUsmapTypeMappingsProvider(pathToMappingFile);
        }

        return patchProvider;
    }

    public static void Export(AbstractFileProvider provider, ConfigObj config, double start)
    {
        // Load checkpoint if provided
        useCheckpoint = false;
        Dictionary<string, long> loadedCheckpoint = LoadCheckpoint(config);
        ConcurrentDictionary<string, long> newCheckpointDict = [];

        Console.WriteLine($"Scanning {provider.Files.Count} files...{Environment.NewLine}");

        // Loop through all files and export the ones that match any of the ExportJsonPaths (converted to regex)
        Parallel.ForEach(provider.Files, file =>
        {
            // "Hotta/Content/Resources/UI/Activity/Activity/DT_Activityquest_Balance.uasset"
            // file.Value.Path

            // "Hotta\Content\Resources\UI\Activity\Activity"
            var internalFilePath = Path.GetDirectoryName(file.Value.Path);

            // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity"
            var outputDir = config.KeepDirectoryStructure ?
                Path.GetFullPath(config.OutputDir) + Path.DirectorySeparatorChar + internalFilePath
                : Path.GetFullPath(config.OutputDir);

            // "DT_Activityquest_Balance"
            var fileName = Path.GetFileNameWithoutExtension(file.Value.Path);

            // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity\DT_Activityquest_Balance"
            var outputPath = outputDir + Path.DirectorySeparatorChar + fileName;

            // ".uasset"
            var fileExtension = file.Value.Path.SubstringAfterLast('.').ToLower();

            bool isJsonExport = config.ExportJsonPaths.Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
            bool isPngExport = config.ExportPngPaths.Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
            bool isExcludedPath = config.ExcludedPaths.Any(path => new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
            bool exportThisFile = true;

            // If checkpoint is specified, skip unchanged files (same file size as in the checkpoint)
            if (useCheckpoint && loadedCheckpoint.TryGetValue(file.Value.Path, out long fileSize))
            {
                exportThisFile = fileSize != file.Value.Size;
            }

            if (config.CreateNewCheckpoint)
            {
                newCheckpointDict.TryAdd(file.Value.Path, file.Value.Size);
            }

            if (!isExcludedPath && (isJsonExport || isPngExport) && exportThisFile)
            {
                try
                {
                    switch (fileExtension)
                    {
                        case "uasset":
                        case "umap":
                            {
                                var allObjects = provider.LoadAllObjects(file.Value.Path);

                                if (isPngExport)
                                {
                                    foreach (var obj in allObjects)
                                    {
                                        // Only exports the first object that is a valid bitmap
                                        if (obj is UTexture2D texture)
                                        {
                                            var bitmap = texture.Decode(ETexturePlatform.DesktopMobile);

                                            if (bitmap != null)
                                            {
                                                if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".png");
                                                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                                                // Save the bitmap to a file
                                                using (SKImage image = SKImage.FromBitmap(bitmap))
                                                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                                                using (Stream stream = File.OpenWrite(outputPath + ".png"))
                                                {
                                                    data.SaveTo(stream);
                                                }
                                                Interlocked.Increment(ref totalExportedFiles);

                                                break;
                                            }
                                            else
                                            {
                                                Console.WriteLine($"WARNING: The following texture is not a valid image bitmap: {file.Value.Path}");

                                                if (config.IncludeJsonsInPngPaths)
                                                {
                                                    // Serialize to JSON, then write to file
                                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".json");
                                                    var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                                    File.WriteAllText(outputPath + ".json", json);
                                                    Interlocked.Increment(ref totalExportedFiles);
                                                }
                                            }
                                        }
                                        else if (config.IncludeJsonsInPngPaths)
                                        {
                                            // Serialize to JSON, then write to file
                                            if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".json");
                                            var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                                            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                            File.WriteAllText(outputPath + ".json", json);
                                            Interlocked.Increment(ref totalExportedFiles);
                                        }
                                    }
                                }

                                else if (isJsonExport)
                                {
                                    // Serialize to JSON, then write to file
                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".json");
                                    var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                    File.WriteAllText(outputPath + ".json", json);
                                    Interlocked.Increment(ref totalExportedFiles);
                                }

                                break;
                            }
                        case "locres":
                            {
                                if (isJsonExport && provider.TryCreateReader(file.Value.Path, out var archive))
                                {
                                    if (config.LogOutputs) Console.WriteLine("=> " + outputPath + ".json");
                                    var locres = new FTextLocalizationResource(archive);
                                    var json = JsonConvert.SerializeObject(locres, Formatting.Indented);
                                    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                    File.WriteAllText(outputPath + ".json", json);
                                    Interlocked.Increment(ref totalExportedFiles);
                                }
                                break;
                            }
                    }
                }
                catch (AggregateException)
                {
                    Console.WriteLine($"ERROR: File cannot be opened: {file.Value.Path}. Possible issues include incorrect UE version in config.json, missing mapping file, or this file type is not supported.");
                }

                Interlocked.Increment(ref totalRegexMatches);
            }

            if (exportThisFile) Interlocked.Increment(ref totalFiles);
        });

        // Create checkpoint
        if (config.CreateNewCheckpoint)
        {
            CreateCheckpoint(newCheckpointDict, config);
        }

        // Log results
        if (config.LogOutputs && totalExportedFiles > 0 && !config.CreateNewCheckpoint) Console.WriteLine();
        if (useCheckpoint)
        {
            Console.WriteLine($"Regex matched {totalRegexMatches} out of {totalFiles} changed files ({provider.Files.Count - totalFiles} unchanged)");
        }
        else
        {
            Console.WriteLine($"Regex matched {totalRegexMatches} out of {totalFiles} total files");
        }
        Console.WriteLine($"Exported {totalExportedFiles} files in {Elapsed(start, Now(), 1000)} seconds");
        Console.WriteLine();
    }

    public static Dictionary<string, long> LoadCheckpoint(ConfigObj config)
    {
        if (config?.UseCheckpointFile?.Length > 0)
        {
            string checkpointPath = $"{Directory.GetCurrentDirectory()}\\{config.UseCheckpointFile}";
            if (File.Exists(checkpointPath))
            {
                useCheckpoint = true;
                Console.WriteLine($"Using checkpoint: {config.UseCheckpointFile}");
                var fromFile = File.ReadAllText(checkpointPath);
                var loadedCheckpoint = JsonConvert.DeserializeObject<Dictionary<string, long>>(fromFile);
                return loadedCheckpoint ?? [];
            }
            else
            {
                Console.WriteLine($"ERROR: Checkpoint file at location \"{config.UseCheckpointFile}\" does not exist. Ignoring...");
                return [];
            }
        }
        else
        {
            Console.WriteLine($"No checkpoint file selected. Skipping...");
            return [];
        }
    }

    public static void CreateCheckpoint(ConcurrentDictionary<string, long> newCheckpointDict, ConfigObj config)
    {
        Console.WriteLine();
        var newCheckpointJson = JsonConvert.SerializeObject(newCheckpointDict, Formatting.Indented);
        var dateStamp = DateTime.Now.ToString("MM-dd-yyyy HH-mm");
        File.WriteAllText($"./checkpoints/{config.GameTitle} {dateStamp}.ckpt", newCheckpointJson);
        Console.WriteLine($"Created checkpoint file: ./checkpoints/{config.GameTitle} {dateStamp}.ckpt");
    }

    public static double Now()
    {
        return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    public static string Elapsed(double start, double end, int factor = 1)
    {
        return ((end - start) / factor).ToString("0.00");
    }
}

public class ConfigObj
{
    public string? GameTitle { get; set; }
    public required string Version { get; set; }
    public required string PaksDir { get; set; }
    public required string OutputDir { get; set; }
    public required string Aes { get; set; }
    public bool LogOutputs { get; set; }
    public required bool KeepDirectoryStructure { get; set; }
    public string? Lang { get; set; }
    public required bool IncludeJsonsInPngPaths { get; set; }
    public bool CreateNewCheckpoint { get; set; }
    public string? UseCheckpointFile { get; set; }
    public required List<string> ExportJsonPaths { get; set; }
    public required List<string> ExportPngPaths { get; set; }
    public required List<string> ExcludedPaths { get; set; }
}