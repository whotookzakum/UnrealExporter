# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Great for automating UE datamining, just specify the files you want and don't want, and speed up your exports by only exporting what's changed.

This project can be used as-is or as a reference, since CUE4Parse documentation is incomplete. Heavily references the source code of [FModel](https://github.com/4sval/FModel) for CUE4Parse usage.

## Features
- [x] Multiple game support
- [x] Regex paths for bulk exporting
- [x] Path exclusions to avoid crashing
- [x] Patch .pak reconciliation (courtesy of [MCMrARM](https://github.com/MCMrARM))
- [x] Checkpoint support (only export new/changed files!)
- [x] Parallel-processing files
- [ ] Specify mapping file (such as naming it the same as gameTitle in `/mappings` folder)
- [ ] CLI args support (pass individual key/value or point to a specific config file)
- [ ] Log file (errors, which files were skipped, which files were outputted, config settings, etc.)
- [ ] Automatic AES key finding
- [ ] Automatic binary releases (GitHub actions)

### Supported file types
- [x] uasset (to JSON or PNG)
- [x] umap (to JSON)
- [x] locres (to JSON)
- [ ] everything else

## Usage
> [!TIP]
> If you know how to use git and the terminal, I recommend cloning the repo instead of downloading a release so that you can quickly get the latest updates with `git pull`.

### Easy setup (download and run)
1. Download latest [release](https://github.com/whotookzakum/UnrealExporter/releases)
2. Create and configure `config.json` (read Config Options below)
3. Run `UnrealExporter.exe`

### Manual setup (recommended)
1. Download and install .NET SDK 8.0
2. Clone the repo, i.e. `git clone https://github.com/whotookzakum/UnrealExporter`
3. Create and configure `config.json` (read Config Options below)
4. Open terminal and execute `dotnet run`

If you wish to build the project as a binary (.exe), use the following command:

```sh
dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

> [!WARNING]
> **Don't forget to include config.json and any desired checkpoints in their respective directories.**

## Config Options
Create a `config.json` file in the root directory and configure it based on the game(s) you wish to export from. By adding multiple objects, you can point to different games at the same time.

Example config files can be found in `/examples`. The excluded paths in the example configs are a non-exhaustive list of game files that are known to crash CUE4Parse.

| Key | Type | Description |
|-----|-----------|-----------|
| gameTitle              | `string`        | Title of the UE game. Can be any arbitrary string that will work as a file name (not allowed: `\ / : * ? " \| < >`). Used for checkpoint naming, matching mapping files, and for your own recordkeeping. |
| version                | `string`        | Unreal Engine version. [UE versions supported by CUE4Parse](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs). Accepts strings separated by one period, like `"4.27"`, and supported game titles formatted camel-case or separated by spaces, such as `"TowerOfFantasy"` or `"tower of fantasy"`. Can be found by right clicking the game's .exe > Properties tab > Details (if removed, check the [FModel discord](https://discord.com/channels/637265123144237061/1090586945412931734)). |
| paksDir                | `string`        | An __absolute path__ to the game directory containing the .pak file(s). |
| outputDir              | `string`        | A __relative path__ to where you want to place the exported files. |
| aes                    | `string`        | The AES-256 decryption key to access game files. [Guide on how to obtain](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide). Leave blank if not needed. |
| logOutputs             | `bool`          | If set to `true`, every exported file's path will be logged. If set to `false`, these logs are skipped. Note: The logging occurs **before** attempting to export the file, so if the program crashes, check the last few logged files (it will not always be the last file as the program has multiple threads running--I may add a feature to disable MT for debugging purposes in the future). | 
| keepDirectoryStructure | `bool`          | If set to `true`, folders will be made matching those found in the .paks. If set to `false`, all files will be output at the root level of the `outputDir`.     |
| lang                   | `string`        | Language that strings should be output in. [Supported languages](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/ELanguage.cs). Useful for specifying the target language for localized resources. Will only work if the game supports the specified localization. Defaults to `English`. |
| includeJsonsInPngPaths | `bool`          | If set to `true`, `exportPngPaths` will include objects that cannot be converted into images as JSON, such as DataTables and invalid bitmaps. If set to `false`, `exportPngPaths` will skip objects that cannot be converted to images. Useful for debugging image exports. |
| createNewCheckpoint       | `bool`          | If set to `true`, will output a new checkpoint file in the root directory. If set to `false`, will not create a checkpoint file. More details about checkpoints below. |
| useCheckpointFile         | `string`        | A __relative path__ to the checkpoint file to use, i.e. `/checkpoints/Tower of Fantasy 02-26-2024 06-08.ckpt`. More details about checkpoints below. |
| exportJsonPaths        | `Array(string)` | A list of files to export as JSON. Supports regex. |
| exportPngPaths         | `Array(string)` | A list of files to export as PNG. Supports regex. |
| excludedFilePaths      | `Array(string)` | A list of files to skip exporting. Supports regex. Useful for avoiding files that crash CUE4Parse. Note: the program will try to automatically skip files that cannot be parsed by CUE4Parse, however files causing issues such as segmentation faults and heap corruption will not be skipped as they are not technically a failed parse, so they will need to be added to the excluded paths. |

> [!NOTE]
> File paths for `exportJsonPaths`, `exportPngPaths`, and `excludedFilePaths` reside **inside the game files** (virtual file system). Use [FModel](https://github.com/4sval/FModel) to verify the paths that you wish to export. For example, Tower of Fantasy starts at `Hotta/Content/...`

I recommend specifying file extensions to avoid getting useless/unexportable files in your output. For example, you may only need a `.uasset` to be exported as JSON, but if you don't specify the file extension, it can export other files such as `.umap`, `.ubulk`, etc. which may be undesired.

<!-- ### Multiple Configs
While you can always export from multiple games in one config, you may want to target only one game without having to modify the config file every time. Multiple configs makes this easy.

1. Create a folder in the root called `configs`
2. Place config files in the folder, naming them something easy for you to remember **without spaces**, i.e. `blue-protocol.json` and `tower-of-fantasy-global.json`
3. Run the program

You can append the file name to the run command, i.e. `dotnet run blue-protocol`. If no file name is specified, you will be prompted to select a game.

If the `configs` directory does not exist or does not contain a config JSON, the `config.json` in the root folder will be used as a fallback. -->

## Checkpoints
Similar to FModel's `.fbkp` system, checkpoints allow you to export only new/modified files and skip unchanged files, reducing the amount of time needed to export. 

A `.ckpt` file is a JSON that maps each file's path to its size, i.e. `"Hotta/Content/Resources/FB/FB_Gulan/Warning.uexp": 3513`. 

If a valid checkpoint is provided, the program will only export files that have different file sizes than the one in the checkpoint (modified files), or do not have an entry in the checkpoint (new files).

**Checkpoint files will always cover all the files in all paks, regardless of what your export/regexes.** That way, you can export a new checkpoint while using an older checkpoint, effectively only exporting the changed files in every game update.

> [!TIP]
> If you want to create a checkpoint but don't want to re-export existing files, set `"createNewCheckpoint": true"` and change the export paths to empty arrays.

## Supported Games
By default, all games supported by FModel should technically be supported by UnrealExporter, as both use CUE4Parse under the hood. You can find working configs for games that have been tested and confirmed to be working in the `/examples` folder. Mileage may vary depending on the files you wish to export, so check for any error messages and exclude paths accordingly.

### How to fix no files loading due to missing mapping file
If you are loading a game like Palworld, you will need the correct `.usmap` file so that the game files will correctly load into CUE4Parse. If your game's mapping file is not already provided in the `mappings` folder, follow the instructions below to obtain the file.

The following instructions are a trimmed version of [this message](https://discord.com/channels/505994577942151180/1196354583040118824/1198468327308271698) by [rin](https://github.com/rinjugatla).

1. Install [Unreal Engine 4/5 Scripting System](https://github.com/UE4SS-RE/RE-UE4SS)
2. Modify the configuration file. `ConsoleEnabled` can be 0 or 1.
```
[Debug]
ConsoleEnabled = 0
GuiConsoleEnabled = 1
GuiConsoleVisible = 1
```
3. Launch the game
4. Output mapping file from the UE4SS Debugging Tool > Dumper tab. The file will be located alongside the game files in `.../Binaries/Win64/Mappings.usmap`.  
Continue reading the [original post](https://discord.com/channels/505994577942151180/1196354583040118824/1198468327308271698) for instructions (with images!) on how to load the `.usmap` file in FModel.
5. Copy or move the `.usmap` file to UnrealExporter's `mappings` folder
6. Rename the file to match the `gameTitle` inside of the config file. For example, if `"gameTitle": "MyFunGame"`, name the file `MyFunGame.usmap`.

The exporter should now be able to detect the game files.

### How to fix no files loading due to incorrect AES key
If you have the wrong AES key, refer to [this guide](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide) or [this tool (untested)](https://github.com/mmozeiko/aes-finder) to get the correct key.