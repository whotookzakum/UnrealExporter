using System.Text.RegularExpressions;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
// using Newtonsoft.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Globalization;



// Load .env file
// var root = Directory.GetCurrentDirectory();
// var dotenv = Path.Combine(root, ".env");
// DotEnv.Load(dotenv);
// var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();


try
{
    // Read the JSON file into a string
    string jsonString = File.ReadAllText("config.json");

    // Deserialize the JSON array into a list of objects
    List<ConfigObj> configs = JsonSerializer.Deserialize<List<ConfigObj>>(jsonString);

    // Loop through each person and print their details
    foreach (ConfigObj config in configs)
    {
        var provider = new DefaultFileProvider(config.paksDir, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_UE4_27));
        // Console.WriteLine(provider);
        // Console.WriteLine(EGame.GAME_UE4_27);

        string version = "";

        if (config.version.Contains("."))
        {
            version = $"UE{config.version.Replace('.', '_')}";
        }
        else if (config.version.Split(" ").Length > 1)
        {
            // Creates a TextInfo based on the "en-US" culture.
            // https://stackoverflow.com/questions/913090/how-to-capitalize-the-first-character-of-each-word-or-the-first-character-of-a
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            version = textInfo.ToTitleCase(config.version).Replace(" ", "");
        }
        else {
            version = config.version;
        }

        // Parse the user input into the EGame enum
        EGame selectedVersion = (EGame)Enum.Parse(typeof(EGame), $"GAME_{version}");

        // Output the selected version
        Console.WriteLine($"Version: {selectedVersion}");
        Console.WriteLine($"Paks: {config.paksDir}");
        Console.WriteLine($"Output: {config.outputDir}");
        Console.WriteLine($"AES key: {config.aes}");
        Console.WriteLine($"Keep directory structure: {config.keepDirectoryStructure}");
        Console.WriteLine();


        foreach (string path in config.targetFilePaths)
        {
            // new Regex("^" + regex + "$", RegexOptions.IgnoreCase)
            // Console.WriteLine(path);
        }
    }
}
catch (FileNotFoundException)
{
    Console.WriteLine("File not found.");
}
catch (JsonException)
{
    Console.WriteLine("Invalid JSON format.");
}

class ConfigObj
{
    public string paksDir { get; set; }
    public string outputDir { get; set; }
    public string aes { get; set; }
    public bool keepDirectoryStructure { get; set; }
    public List<string> targetFilePaths { get; set; }
    public string version { get; set; }

}



// var paksDir = config["PATH_TO_PAKS"] ?? "";
// var outputDir = config["OUTPUT_DIR"] ?? ""; // Creates a folder "Content" at the specified location
// var aesKey = config["AES_KEY"] ?? "";

// var provider = new DefaultFileProvider(paksDir, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_UE4_27));
// provider.Initialize();
// provider.SubmitKey(new FGuid(), new FAesKey(aesKey));

// // provider.MountedVfs contains paths to .pak files; provider.Files contains individual file paths
// // Also consider: provider.Files.Keys, provider.Files.Values, (file).Key, (file).Value

// // Create a list of regexes based on ExportList.txt
// var targetFilePaths = new List<Regex>();
// foreach (var regex in File.ReadAllLines("ExportList.txt"))
// {
//     targetFilePaths.Add();
//     Console.WriteLine(regex);
// }


// double start = now();
// var totalFiles = 0;

// // Loop through all files and export the ones that match any of the regexes in ExportList.txt
// foreach (var file in provider.Files)
// {
//     if (targetFilePaths.Any(regex => regex.IsMatch(file.Value.Path)))
//     {
//         var filePath = (Path.GetFullPath(outputDir) + Path.GetDirectoryName(file.Value.Path)).Replace("BLUEPROTOCOL", "");
//         var fileName = Path.GetFileNameWithoutExtension(file.Value.Path) + ".json";
//         Directory.CreateDirectory(filePath);
//         Console.WriteLine("=> " + filePath + Path.DirectorySeparatorChar + fileName);

//         // Load all objects in the .uasset/.umap file, serialize to JSON, then write to file
//         var allObjects = provider.LoadAllObjects(file.Value.Path);
//         var json = JsonConvert.SerializeObject(allObjects, Formatting.Indented);
//         File.WriteAllText(filePath + Path.DirectorySeparatorChar + fileName, json);

//         totalFiles++;
//     }
// }

// Console.WriteLine("Exported " + totalFiles + " files in " + elapsed(start, now(), 1000) + "seconds");

// static double now()
// {
//     return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
// }

// static string elapsed(double start, double end, int factor = 1)
// {
//     return ((end - start) / factor).ToString("0.00");
// }