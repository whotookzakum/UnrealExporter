using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.Localization;
using CUE4Parse.Utils;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using CUE4Parse.UE4.Assets.Exports.Material;
using SBDumper;
using System.Text;

public class UnrealExporter
{
    public static void Main(String[] args)
    {
        try
        {
            double start = now();
            var totalFiles = 0;
            var totalRegexMatches = 0;
            var totalExportedFiles = 0;

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
                Console.WriteLine($"Locale: {config.lang}");
                Console.WriteLine($"Paks: {config.paksDir}");
                Console.WriteLine($"Output: {config.outputDir}");
                Console.WriteLine($"AES key: {config.aes}");
                Console.WriteLine($"Keep directory structure: {config.keepDirectoryStructure}");
                Console.WriteLine($"Include JSONs in PNG paths: {config.includeJsonsInPngPaths}");
                Console.WriteLine($"Create checkpoint file: {config.createCheckpoint}");

                // Load CUE4Parse
                var provider = new DefaultFileProvider(config.paksDir, SearchOption.TopDirectoryOnly, true,
                    new VersionContainer(selectedVersion));
                provider.Initialize();

                // Decrypt if AES key is provided
                if (config.aes?.Length > 0)
                {
                    provider.SubmitKey(new FGuid(), new FAesKey(config.aes));
                }

                // Set locale if provided, otherwise English
                if (config.lang?.Length > 0)
                {
                    ELanguage selectedLang = (ELanguage)Enum.Parse(typeof(ELanguage), config.lang);
                    provider.LoadLocalization(selectedLang);
                }
                else
                {
                    provider.LoadLocalization(ELanguage.English);
                }

                // Sort files to PatchFileProvider to sort by patch number, so the patch uassets override original uassets
                var patchProvider = new PatchFileProvider();
                patchProvider.Load(provider);

                // Checkpoint setup
                bool useCheckpoint = false;
                Dictionary<string, long> checkpointFileDict =  []
                ;
                Dictionary<string, long> newCheckpointDict =  []
                ;

                if (config.checkpointFile.Length > 0)
                {
                    if (File.Exists(config.checkpointFile))
                    {
                        useCheckpoint = true;
                        Console.WriteLine($"Using checkpoint: {config.checkpointFile}");
                        var fromFile = File.ReadAllText(config.checkpointFile);
                        checkpointFileDict = JsonConvert.DeserializeObject<Dictionary<string, long>>(fromFile);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"ERROR: Checkpoint file at location \"${config.checkpointFile}\" does not exist. Ignoring...");
                    }
                }
                else
                {
                    Console.WriteLine($"No checkpoint file selected. Skipping...");
                }

                Console.WriteLine($"Scanning {patchProvider.Files.Count} files...");
                Console.WriteLine();

                // Loop through all files and export the ones that match any of the exportJsonPaths (converted to regex)
                foreach (var file in patchProvider.Files)
                {
                    // define the thread and wrap the code for each file in it
                    Thread thread = new Thread(new ThreadStart(() =>
                    {
                        // "Hotta/Content/Resources/UI/Activity/Activity/DT_Activityquest_Balance.uasset"
                        // file.Value.Path

                        // "Hotta\Content\Resources\UI\Activity\Activity"
                        var internalFilePath = Path.GetDirectoryName(file.Value.Path);

                        // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity"
                        var outputDir = config.keepDirectoryStructure
                            ? Path.GetFullPath(config.outputDir) + Path.DirectorySeparatorChar + internalFilePath
                            : Path.GetFullPath(config.outputDir);

                        // "DT_Activityquest_Balance"
                        var fileName = Path.GetFileNameWithoutExtension(file.Value.Path);

                        // "D:\UnrealExporter\output\Hotta\Content\Resources\UI\Activity\Activity\DT_Activityquest_Balance"
                        var outputPath = outputDir + Path.DirectorySeparatorChar + fileName;

                        // ".uasset"
                        var fileExtension = file.Value.Path.SubstringAfterLast('.').ToLower();

                        bool isJsonExport = config.exportJsonPaths.Any(path =>
                            new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
                        bool isPngExport = config.exportPngPaths.Any(path =>
                            new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
                        bool isExcludedPath = config.excludedPaths.Any(path =>
                            new Regex("^" + path + "$", RegexOptions.IgnoreCase).IsMatch(file.Value.Path));
                        bool exportThisFile = true;

                        // If checkpoint is specified, skip unchanged files (same file size as in the checkpoint)
                        if (useCheckpoint && checkpointFileDict.TryGetValue(file.Value.Path, out long fileSize))
                        {
                            exportThisFile = fileSize != file.Value.Size;
                        }

                        if (config.createCheckpoint)
                        {
                            newCheckpointDict[file.Value.Path] = file.Value.Size;
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
                                                        Console.WriteLine("=> " + outputPath + ".png");
                                                        if (!Directory.Exists(outputDir))
                                                            Directory.CreateDirectory(outputDir);

                                                        // Save the bitmap to a file
                                                        using (SKImage image = SKImage.FromBitmap(bitmap))
                                                        using (SKData data = image.Encode(SKEncodedImageFormat.Png,
                                                                   100))
                                                        using (Stream stream = File.OpenWrite(outputPath + ".png"))
                                                        {
                                                            data.SaveTo(stream);
                                                        }

                                                        totalExportedFiles++;

                                                        break;
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine(
                                                            $"WARNING: The following texture is not a valid image bitmap: {file.Value.Path}");

                                                        if (config.includeJsonsInPngPaths)
                                                        {
                                                            // Serialize to JSON, then write to file
                                                            Console.WriteLine("=> " + outputPath + ".json");
                                                            var json = JsonConvert.SerializeObject(allObjects,
                                                                Formatting.Indented);
                                                            if (!Directory.Exists(outputDir))
                                                                Directory.CreateDirectory(outputDir);
                                                            File.WriteAllText(outputPath + ".json", json);
                                                            totalExportedFiles++;
                                                        }
                                                    }
                                                }
                                                else if (config.includeJsonsInPngPaths)
                                                {
                                                    // Serialize to JSON, then write to file
                                                    Console.WriteLine("=> " + outputPath + ".json");
                                                    var json = JsonConvert.SerializeObject(allObjects,
                                                        Formatting.Indented);
                                                    if (!Directory.Exists(outputDir))
                                                        Directory.CreateDirectory(outputDir);
                                                    File.WriteAllText(outputPath + ".json", json);
                                                    totalExportedFiles++;
                                                }
                                            }
                                        }

                                        else if (isJsonExport)
                                        {
                                            // Serialize to JSON, then write to file
                                            Console.WriteLine("=> " + outputPath + ".json");
                                            var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
                                            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                            File.WriteAllText(outputPath + ".json", json);
                                            totalExportedFiles++;
                                        }

                                        break;
                                    }
                                    case "locres":
                                    {
                                        if (isJsonExport && provider.TryCreateReader(file.Value.Path, out var archive))
                                        {
                                            Console.WriteLine("=> " + outputPath + ".json");
                                            var locres = new FTextLocalizationResource(archive);
                                            var json = JsonConvert.SerializeObject(locres, Formatting.Indented);
                                            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                                            File.WriteAllText(outputPath + ".json", json);
                                            totalExportedFiles++;
                                        }

                                        break;
                                    }
                                }
                            }
                            catch (AggregateException)
                            {
                                Console.WriteLine(
                                    $"ERROR: File cannot be opened: {file.Value.Path}. Possible issues include incorrect UE version in config.json, or this file type is not supported.");
                            }

                            totalRegexMatches++;
                        }

                        if (exportThisFile) totalFiles++;
                    }));

                    // lower thread priority for safety and cpu usage
                    thread.Priority = ThreadPriority.BelowNormal;
                    
                    thread.Start();
                }

                Console.WriteLine();

                if (config.createCheckpoint)
                {
                    // Export new checkpoint file
                    var newCheckpointJson = JsonConvert.SerializeObject(newCheckpointDict, Formatting.Indented);
                    var dateStamp = DateTime.Now.ToString("MM-dd-yyyy HH-mm");
                    File.WriteAllText($"./{config.gameTitle} {dateStamp}.ckpt", newCheckpointJson);
                    Console.WriteLine($"Created checkpoint file: ./{config.gameTitle} {dateStamp}.ckpt");
                }

                Console.WriteLine(
                    $"Regex matched {totalRegexMatches} out of {totalFiles} {(useCheckpoint ? "changed files" : "total files")}.");
                Console.WriteLine($"Exported {totalExportedFiles} files in {elapsed(start, now(), 1000)} seconds");
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("ERROR: config.json not found.");
        }
        catch (JsonException)
        {
            Console.WriteLine("ERROR: config.json is not a valid JSON format.");
        }

        static double now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        static string elapsed(double start, double end, int factor = 1)
        {
            return ((end - start) / factor).ToString("0.00");
        }
    }
}

public class ConfigObj
{
    public string gameTitle { get; set; }
    public string version { get; set; }
    public string paksDir { get; set; }
    public string outputDir { get; set; }
    public string aes { get; set; }
    public bool keepDirectoryStructure { get; set; }
    public string lang { get; set; }
    public bool includeJsonsInPngPaths { get; set; }
    public bool createCheckpoint { get; set; }
    public string checkpointFile { get; set; }
    public List<string> exportJsonPaths { get; set; }
    public List<string> exportPngPaths { get; set; }
    public List<string> excludedPaths { get; set; }
}