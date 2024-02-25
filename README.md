# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Currently only exports .umap and .uasset files as .json files.  

Feel free to customize the code however you want. The repo will be published for reference, since CUE4Parse documentation is incomplete.  

## Features
- [x] Multiple game support
- [x] Regex paths for bulk exporting
- [x] Path exclusions to avoid crashing
- [x] Patch .pak reconciliation (courtesy of [MCMrARM](https://github.com/MCMrARM))
- [ ] CLI args support
- [ ] Log file
- [ ] Automatic AES key finding

### Supported file types
- [x] uasset (to JSON or PNG)
- [x] umap (to JSON; needs testing!)
- [x] locres (to JSON)
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
| includeJsonsInPngPaths | `bool`          | If set to `true`, `exportPngPaths` will include objects that cannot be converted into images as JSON, such as DataTables and invalid bitmaps. If set to `false`, `exportPngPaths` will skip objects that cannot be converted to images. Useful for debugging image exports. |
| exportJsonPaths        | `Array(string)` | A list of files to export as JSON. Supports regex. |
| exportPngPaths        | `Array(string)` | A list of files to export as PNG. Supports regex. |
| excludedFilePaths      | `Array(string)` | A list of files to skip exporting. Supports regex. Useful for avoiding files that crash CUE4Parse. |

> Note: file paths reside **inside the game files**—use [FModel](https://github.com/4sval/FModel) to verify the paths you need. For example, Tower of Fantasy starts at `Hotta/Content/...`

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

## Example configs
Below are examples of config regexes on a per-game basis. The excluded paths are a non-exhaustive list of files that are known to crash CUE4Parse.

### Tower of Fantasy
```json
[
    {
        "version": "TowerOfFantasy",
        "lang": "English",
        "paksDir": "C:/Program Files (x86)/Steam/steamapps/common/Tower of Fantasy/Tower of Fantasy/Hotta/Content/Paks",
        "outputDir": "./output",
        "aes": "0x6E6B325B02B821BD46AF6B62B1E929DC89957DC6F8AA78210D5316798B7508F8",
        "keepDirectoryStructure": true,
        "includeJsonsInPngPaths": false,
        "exportJsonPaths": [
            "Hotta/Content/Resources/CoreBlueprints/DataTable.*\\.uasset",
            "Hotta/Content/Resources/Dialogues/Quests/.*\\.uasset",
            "Hotta/Content/Resources/Text/.*\\.uasset",
            "Hotta/Content/Localization/Game/.*\\.uasset",
            "Hotta/Content/Resources/Abilities/Buff/Wormhole/NewBuff/.*\\.uasset"
        ],
        "exportPngPaths": [
            "Hotta/Content/L10N/.*\\.uasset",
            "Hotta/Content/ResourcesOverSea/.*\\.uasset",
            "Hotta/Content/Resources/UI/.*\\.uasset",
            "Hotta/Content/Resources/Icon/.*\\.uasset"
        ],
        "excludedPaths": [
            "Hotta/Content/Resources/CoreBlueprints/DataTable/ImitationElementEffectConfigDataTable.uasset",
            "Hotta/Content/Resources/CoreBlueprints/DataTable/ImitationChatNodeDataTable.uasset",
            "Hotta/Content/Resources/CoreBlueprints/DataTable/DamageFloatiesTemplatePathData.uasset",
            "Hotta/Content/Resources/CoreBlueprints/DataTable/PlayerAnimSequenceDataTable.uasset",
            "Hotta/Content/Resources/CoreBlueprints/DataTable/TamingMonster/DA_TamingMonster.uasset",
            "Hotta/Content/Resources/CoreBlueprints/DataTable/PlayerCommonMontageDataTable.uasset"
        ]
    }
]
```