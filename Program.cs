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

    public static void Main(string[] args)
    {
        double trueStart = Now();

        try
        {
            List<ConfigObj> configs = LoadAllConfigs(args);

            foreach (ConfigObj config in configs)
            {
                double start = Now();
                totalFiles = 0;
                totalRegexMatches = 0;
                totalExportedFiles = 0;

                EGame selectedVersion = GetGameVersion(config.Version);
                Console.WriteLine($"Config: {config.ConfigFileName} (object #{config.ConfigObjectIndex + 1})");
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
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nExiting UnrealExporter.");
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"ERROR: no config files found.");
        }
    }

    public static List<ConfigObj>? LoadConfigFile(string path)
    {
        try
        {
            string jsonString = File.ReadAllText(path);
            List<ConfigObj> configObjs = JsonConvert.DeserializeObject<List<ConfigObj>>(jsonString) ?? [];
            int index = 0;
            foreach (ConfigObj obj in configObjs)
            {
                obj.ConfigFileName = path.Split(Path.DirectorySeparatorChar).Last();
                obj.ConfigObjectIndex = index;
                index++;
            }
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
        return null;
    }

    public static List<ConfigObj> LoadConfigsFromSelector(string[] args, string[] allConfigFilePaths)
    {
        if (allConfigFilePaths.Length < 1)
        {
            throw new FileNotFoundException();
        }
        bool[] selectedOptions = new bool[allConfigFilePaths.Length + 1];
        int currentOption = 0;
        List<string> gameTitles = [];
        int longestFileName = 0;

        // Get longest file name for padding purposes
        // Also check items that were passed in args
        for (int i = 0; i < allConfigFilePaths.Length; i++)
        {
            string fileName = allConfigFilePaths[i].Split(Path.DirectorySeparatorChar).Last();
            if (fileName.Length > longestFileName)
            {
                longestFileName = fileName.Length;
            }

            // If "bp" was passed as an arg, set "bp.json" to checked by default
            if (args.Any(arg => arg.Equals(fileName.Split(".").First())))
            {
                selectedOptions[i + 1] = true;
            }
        }

        List<string> paddedFileNames = [];
        foreach (string filePath in allConfigFilePaths)
        {
            string fileName = filePath.Split(Path.DirectorySeparatorChar).Last();
            paddedFileNames.Add(fileName.PadRight(longestFileName + 1, ' '));
        }

        for (int i = 0; i < allConfigFilePaths.Length; i++)
        {
            List<ConfigObj>? configObjsInFile = LoadConfigFile(allConfigFilePaths[i]);

            if (configObjsInFile != null)
            {
                List<string> gameTitlesInFile = [];
                foreach (ConfigObj configObj in configObjsInFile)
                {
                    gameTitlesInFile.Add(configObj.GameTitle);
                }
                gameTitles.Add($"({string.Join(", ", [.. gameTitlesInFile])})");
            }
            else
            {
                gameTitles.Add("");
            }
        }

        while (true)
        {
            Console.Clear(); // Clear the console screen before re-printing options
            Console.WriteLine($"{(allConfigFilePaths.Length > 1 ? "Multiple config files detected. Select the ones" : "Select the config files")} you wish to execute with arrows keys, space to select, enter to confirm, or escape to exit.");

            for (int i = 0; i < selectedOptions.Length; i++)
            {
                Console.Write(currentOption == i ? "> " : "  ");

                if (i > 0)
                {
                    Console.Write(selectedOptions[i] ? "[x] " : "[ ] ");
                    Console.WriteLine($"{paddedFileNames[i - 1]} {gameTitles[i - 1]}");
                }
                else if (i == 0 && selectedOptions[0])
                {
                    Console.WriteLine("Unselect all");
                }
                else
                {
                    Console.WriteLine("Select all");
                }
            }

            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    currentOption = (currentOption - 1 + selectedOptions.Length) % selectedOptions.Length;
                    break;
                case ConsoleKey.DownArrow:
                    currentOption = (currentOption + 1) % selectedOptions.Length;
                    break;
                case ConsoleKey.Spacebar:
                    if (currentOption == 0)
                    {
                        for (int i = 0; i < selectedOptions.Length; i++)
                        {
                            selectedOptions[i] = !selectedOptions[i];
                        }
                    }
                    else
                    {
                        selectedOptions[currentOption] = !selectedOptions[currentOption];
                    }
                    break;
                case ConsoleKey.Enter:
                    List<string> result = [];
                    for (int i = 1; i < selectedOptions.Length; i++)
                    {
                        if (selectedOptions[i])
                        {
                            result.Add(allConfigFilePaths[i - 1].Split(Path.DirectorySeparatorChar).Last().Split(".")[0]);
                        }
                    }
                    Console.WriteLine();
                    return LoadAllConfigs([.. result]);
                case ConsoleKey.Escape:
                    throw new OperationCanceledException();
            }
        }
    }

    public static List<ConfigObj> LoadAllConfigs(string[] args)
    {
        List<ConfigObj> allConfigObjs = [];
        string[] allConfigFilePaths = Directory.GetFiles($"{Directory.GetCurrentDirectory()}\\configs");

        bool isReleaseMode = false;

#if !DEBUG
            isReleaseMode = true;
#endif

        if (args.Length > 0 || isReleaseMode)
        {
            // Show list of config files
            if (args.Any(arg => arg.Equals("--list")) || (isReleaseMode && args.Length < 1))
            {
                args = args.Where(arg => arg != "--list").ToArray();
                return LoadConfigsFromSelector(args, allConfigFilePaths);
            }

            int totalConfigFiles = 0;

            // Load all files
            if (args.Any(arg => arg.Equals("all")))
            {
                foreach (var filePath in allConfigFilePaths)
                {
                    List<ConfigObj>? configObjsInFile = LoadConfigFile(filePath);

                    if (configObjsInFile != null)
                    {
                        foreach (ConfigObj configObj in configObjsInFile)
                        {
                            allConfigObjs.Add(configObj);
                        }
                        totalConfigFiles++;
                        Console.WriteLine($"{filePath.Split(Path.DirectorySeparatorChar).Last()} ({configObjsInFile.Count} object{(configObjsInFile.Count > 1 ? "s" : "")})");
                    }
                }
            }
            // Load specified files
            else
            {
                foreach (var arg in args)
                {
                    List<ConfigObj>? configObjsInFile = LoadConfigFile($"{Directory.GetCurrentDirectory()}\\configs\\{arg}.json");

                    if (configObjsInFile != null)
                    {
                        foreach (ConfigObj configObj in configObjsInFile)
                        {
                            allConfigObjs.Add(configObj);
                        }
                        totalConfigFiles++;
                        Console.WriteLine($"{arg}.json ({configObjsInFile.Count} object{(configObjsInFile.Count > 1 ? "s" : "")})");
                    }
                }
            }

            Console.WriteLine($"Loaded {totalConfigFiles} config file(s) ({allConfigObjs.Count} total object{(allConfigObjs.Count > 1 ? "s" : "")})");
        }
        // Fallback to default config.json
        else
        {
            Console.WriteLine("No config file(s) specified. Defaulting to config.json...");
            List<ConfigObj>? configObjsInFile = LoadConfigFile($"{Directory.GetCurrentDirectory()}\\configs\\config.json");

            if (configObjsInFile != null)
            {
                foreach (ConfigObj configObj in configObjsInFile)
                {
                    allConfigObjs.Add(configObj);
                }
                Console.WriteLine($"Loaded config.json ({allConfigObjs.Count} object{(allConfigObjs.Count > 1 ? "s" : "")})");
            }
        }
        Console.WriteLine();

        return allConfigObjs;
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
        var provider = new DefaultFileProvider(config.PaksDir, SearchOption.AllDirectories, true, new VersionContainer(selectedVersion));
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
            if (config.UseCheckpointFile.Equals("latest"))
            {
                string[] allCheckpointPaths = Directory.GetFiles($"{Directory.GetCurrentDirectory()}\\checkpoints");
                var pathsForGameTitle = allCheckpointPaths.Where(path => path.Contains(config.GameTitle));

                if (!pathsForGameTitle.Any())
                {
                    Console.WriteLine($"ERROR: could not find any checkpoints for \"{config.GameTitle}\". Ignoring...");
                    return [];
                }

                var sortedPaths = pathsForGameTitle.OrderBy(path =>
                {
                    string dateTimeFromFileName = path.Split(Path.DirectorySeparatorChar).Last().Split(".").First().SubstringAfter(config.GameTitle)[1..];
                    string date = dateTimeFromFileName.Split(" ")[0];
                    string time = dateTimeFromFileName.Split(" ")[1].Replace("-", ":");
                    double unixTime = DateTime.Parse($"{date} {time}").Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                    return unixTime;
                });

                var latestCheckpointPath = sortedPaths.Last();

                if (File.Exists(latestCheckpointPath))
                {
                    useCheckpoint = true;
                    Console.WriteLine($"Using checkpoint: latest ({latestCheckpointPath.Split(Path.DirectorySeparatorChar).Last()})");
                    var fromFile = File.ReadAllText(latestCheckpointPath);
                    var loadedCheckpoint = JsonConvert.DeserializeObject<Dictionary<string, long>>(fromFile);
                    return loadedCheckpoint ?? [];
                }

                return [];
            }
            else if (File.Exists(checkpointPath))
            {
                useCheckpoint = true;
                Console.WriteLine($"Using checkpoint: {config.UseCheckpointFile}");
                var fromFile = File.ReadAllText(checkpointPath);
                var loadedCheckpoint = JsonConvert.DeserializeObject<Dictionary<string, long>>(fromFile);
                return loadedCheckpoint ?? [];
            }
            else
            {
                Console.WriteLine($"ERROR: checkpoint file at location \"{config.UseCheckpointFile}\" does not exist. Ignoring...");
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
        string checkpointsDirPath = $"{Directory.GetCurrentDirectory()}\\checkpoints";
        if (!Directory.Exists(checkpointsDirPath))
        {
            Directory.CreateDirectory(checkpointsDirPath);
        }
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
    public required string ConfigFileName { get; set; }
    public required int ConfigObjectIndex { get; set; }
    public required string GameTitle { get; set; }
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