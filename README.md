# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Currently only exports .umap and .uasset files as .json files.  

Feel free to customize the code however you want. The repo will be published for reference, since CUE4Parse documentation is incomplete.  

## Features
- [x] Multiple game support
- [x] Regex paths for bulk exporting
- [x] Path exclusions to avoid crashing
- [ ] CLI args support
- [ ] Patch .pak reconciliation

### Supported file types
- [x] uasset
- [x] umap
- [x] locres
- [ ] everything else

## How to use
1. Download latest [release](https://github.com/whotookzakum/UnrealExporter/releases)
2. Configure `config.json` based on the game. By adding multiple objects, you can point to different games at the same time.

| Key | Type | Description |
|-----|-----------|-----------|
| version                | `string`        | Unreal Engine version. [Supported versions](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs). Accepts strings separated by one period, like `"4.27"`, and supported game titles formatted camel-case or separated by spaces, such as `"TowerOfFantasy"` or `"tower of fantasy"`. |
| paksDir                | `string`        | An __absolute path__ to the game directory containing the .pak file(s). |
| outputDir              | `string`        | A __relative path__ to where you want to place the exported files. |
| aes                    | `string`        | The AES-256 decryption key to access game files. [Guide on how to obtain](https://github.com/Cracko298/UE4-AES-Key-Extracting-Guide). Leave blank if not needed. |
| keepDirectoryStructure | `bool`          | If set to `true`, folders will be made matching those found in the .paks. If set to `false`, all files will be output at the root level of the `outputDir`.     |
| lang                   | `string`        | Language that strings should be output in. [Supported languages](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/ELanguage.cs). Useful for specifying the target language for localized resources. Will only work if the game supports the specified localization. Defaults to `English`. |
| targetFilePaths        | `Array(string)` | A list of files that should be exported. Supports regex. Note: the path's root resides **inside the game files**—use [FModel](https://github.com/4sval/FModel) or [UE Viewer](https://github.com/gildor2/UEViewer) to verify the paths you need—i.e. `Hotta/Content/...` for Tower of Fantasy. |
| excludedFilePaths      | `Array(string)` | A list of files that should be skipped, useful for avoiding files that crash CUE4Parse. Supports regex. Note: the path's root resides **inside the game files**—use [FModel](https://github.com/4sval/FModel) or [UE Viewer](https://github.com/gildor2/UEViewer) to verify the paths you need—i.e. `Hotta/Content/...` for Tower of Fantasy.  |

### Building
If you wish to build the project yourself, download .NET SDK 8.0, clone the repo, and configure `config.json` in the root directory.

To run:

```sh
dotnet run
``` 

To publish as a binary:

```sh
dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

**Don't forget to paste config.json into the folder that contains the binary.**

## Example file paths and exclusions lists
Below are examples of config regexes on a per-game basis. The excluded paths are a non-exhaustive list of files that are known to crash CUE4Parse.

### Tower of Fantasy
```json
"targetFilePaths": [
    "Hotta/Content/Resources/CoreBlueprints/DataTable.*\\.uasset",
    "Hotta/Content/Resources/Dialogues/Quests/.*\\.uasset",
    "Hotta/Content/Resources/Text/.*\\.uasset",
    "Hotta/Content/Localization/Game/.*\\.uasset",
    "Hotta/Content/Resources/Abilities/Buff/Wormhole/NewBuff/.*\\.uasset"
],
"excludedPaths": [
    "Hotta/Content/Resources/CoreBlueprints/DataTable/ImitationElementEffectConfigDataTable.uasset",
    "Hotta/Content/Resources/CoreBlueprints/DataTable/ImitationChatNodeDataTable.uasset",
    "Hotta/Content/Resources/CoreBlueprints/DataTable/DamageFloatiesTemplatePathData.uasset",
    "Hotta/Content/Resources/CoreBlueprints/DataTable/PlayerAnimSequenceDataTable.uasset",
    "Hotta/Content/Resources/CoreBlueprints/DataTable/TamingMonster/DA_TamingMonster.uasset",
    "Hotta/Content/Resources/CoreBlueprints/DataTable/PlayerCommonMontageDataTable.uasset"
]
```