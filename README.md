# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Currently only exports .umap and .uasset files as .json files.  

Feel free to customize the code however you want. The repo will be published for reference, since CUE4Parse documentation is incomplete.  

**For customization**, you might want to add args for the game client path and export output path for use as a CLI. Or maybe add timers for each every iteration of the loop to see how long each file takes. Or you might want to add some custom logic to differentiate and include patch .pak files (ends in _P.pak). Or you might want to add support for exporting other file types supported by CUE4Parse.

**Any UE files that would crash FModel (which also uses CUE4Parse) will also crash this program.**  
Maybe implement an `ExclusionsList.txt`?

## How to use
1. Download .NET SDK 8.0
2. Clone the repo
3. Change variables in `Program.cs`
4. Add paths (supports regex) that you want files from in `ExportList.txt` (example below)
5. Create a `.env` at the root directory (example below)
6. Run `dotnet run`

Optional: you could also try publishing as an .exe `dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=true` but will need some extra [configuration](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish), setting up args, etc.

### Example ExportList.txt
```
BLUEPROTOCOL/Content/Text/.*\.uasset
BLUEPROTOCOL/Content/Blueprints/Manager/DT_.*\.uasset
BLUEPROTOCOL/Content/Blueprints/Manager/EnemySet/.*\.uasset
BLUEPROTOCOL/Content/Blueprints/UI/Icon/DT_.*\.uasset
BLUEPROTOCOL/Content/Blueprints/UI/LoadingScreen/LoadingTips.*\.uasset
BLUEPROTOCOL/Content/Blueprints/UI/Map/DT_.*\.uasset
BLUEPROTOCOL/Content/Blueprints/UI/Map/CDT_.*\.uasset
BLUEPROTOCOL/Content/Maps/.*_EN\.umap
BLUEPROTOCOL/Content/Maps/.*_PU\.umap
BLUEPROTOCOL/Content/Maps/.*_SC\.umap
BLUEPROTOCOL/Content/Maps/.*_Nappo\.umap
BLUEPROTOCOL/Content/Maps/DT_.*\.uasset
BLUEPROTOCOL/Content/Blueprints/Dungeon/Develop/.*\.uasset
```

### Example .env
```
PATH_TO_PAKS=C:/BandaiNamcoLauncherGames/BLUEPROTOCOL/BLUEPROTOCOL/Content/Paks
OUTPUT_DIR=.
AES_KEY=
```
> Note: PATH_TO_PAKS is an absolute path while OUTPUT_DIR is a relative path. In this example, OUTPUT_DIR will create a new folder named "Content" in the specified directory.