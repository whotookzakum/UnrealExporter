# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Great for automating UE datamining, just specify the files you want and don't want, and speed up your exports by only exporting what's changed.

This project can be used as-is or as a reference, since CUE4Parse documentation is incomplete. Heavily references the source code of [FModel](https://github.com/4sval/FModel) for CUE4Parse usage.

## Features
- [x] Multiple game support
- [x] Regex paths for bulk exporting
- [x] Path exclusions to avoid crashing
- [x] Patch .pak reconciliation (courtesy of [MCMrARM](https://github.com/MCMrARM))
- [x] Checkpoint support to only export new/changed files
- [ ] CLI args support
- [ ] Log file
- [ ] Automatic AES key finding
- [ ] Automatic releases (GitHub actions)

### Supported file types
- [x] uasset (to JSON or PNG)
- [x] umap (to JSON; needs testing!)
- [x] locres (to JSON)
- [ ] everything else


## Usage
It's recommended that you clone the repo instead of downloading a release, so that you can quickly get the latest updates with `git pull`.

### Download Release (NOT recommended)
Simpler, but **not recommended** if you know how to use git.  
1. Download latest [release](https://github.com/whotookzakum/UnrealExporter/releases)
2. Create and configure `config.json` (read Config Options below)
3. Run `UnrealExporter.exe`

### Manual Setup (recommended)
More complex, but is the preferred method as you can quickly receive updates with `git pull`.  

1. Download and install .NET SDK 8.0
2. Clone the repo, i.e. `git clone https://github.com/whotookzakum/UnrealExporter`
3. Create and configure `config.json` (read Config Options below)
4. Open terminal and execute `dotnet run`

#### Building
If you wish to build the project yourself, follow the same steps as above (no need to run), and publish as a binary (.exe) with the following command:

```sh
dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

**Don't forget to paste config.json and any desired checkpoints into the folder that contains the binary.**

### Config Options
Create a `config.json` file in the root directory and configure it based on the game(s) you wish to export from. By adding multiple objects, you can point to different games at the same time.

Example config files can be found in `/examples`. The excluded paths in the example configs are a non-exhaustive list of game files that are known to crash CUE4Parse.

| Key | Type | Description |
|-----|-----------|-----------|
| gameTitle              | `string`        | Title of the UE game. Can be any value, only used for recordkeeping and file generation purposes. |
| version                | `string`        | Unreal Engine version. [Supported versions](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs). Accepts strings separated by one period, like `"4.27"`, and supported game titles formatted camel-case or separated by spaces, such as `"TowerOfFantasy"` or `"tower of fantasy"`. |
| paksDir                | `string`        | An __absolute path__ to the game directory containing the .pak file(s). |
| outputDir              | `string`        | A __relative path__ to where you want to place the exported files. |
| aes                    | `string`        | The AES-256 decryption key to access game files. [Guide on how to obtain](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide). Leave blank if not needed. |
| keepDirectoryStructure | `bool`          | If set to `true`, folders will be made matching those found in the .paks. If set to `false`, all files will be output at the root level of the `outputDir`.     |
| lang                   | `string`        | Language that strings should be output in. [Supported languages](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/ELanguage.cs). Useful for specifying the target language for localized resources. Will only work if the game supports the specified localization. Defaults to `English`. |
| includeJsonsInPngPaths | `bool`          | If set to `true`, `exportPngPaths` will include objects that cannot be converted into images as JSON, such as DataTables and invalid bitmaps. If set to `false`, `exportPngPaths` will skip objects that cannot be converted to images. Useful for debugging image exports. |
| createCheckpoint       | `bool`          | If set to `true`, will output a new checkpoint file in the root directory. If set to `false`, will not create a checkpoint file. |
| checkpointFile         | `string`        | A __relative path__ to the checkpoint file to use. More details about checkpoints below. |
| exportJsonPaths        | `Array(string)` | A list of files to export as JSON. Supports regex. |
| exportPngPaths         | `Array(string)` | A list of files to export as PNG. Supports regex. |
| excludedFilePaths      | `Array(string)` | A list of files to skip exporting. Supports regex. Useful for avoiding files that crash CUE4Parse. |

> Note: file paths for `exportJsonPaths`, `exportPngPaths`, and `excludedFilePaths` reside **inside the game files** (virtual file system). Use [FModel](https://github.com/4sval/FModel) to verify the paths that you wish to export. For example, Tower of Fantasy starts at `Hotta/Content/...`

### Checkpoints
Similar to FModel's `.fbkp` system, checkpoints allow you to export only new/modified files and skip unchanged files, reducing the amount of time needed to export. 

A `.ckpt` file is a JSON that maps each file's path to its size, i.e. `"Hotta/Content/Resources/FB/FB_Gulan/Warning.uexp": 3513`. 

If a valid checkpoint is provided, the program will only export files that have different file sizes than the one in the checkpoint (modified files), or do not have an entry in the checkpoint (new files).

**Checkpoint files for all paks will be generated regardless of which files are exported.** In other words, you can export 0 files and still generate a full checkpoint. This can be useful if you want a checkpoint but don't want to re-export existing files.