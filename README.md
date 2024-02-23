# UnrealExporter
A batch file exporter for Unreal Engine games using [CUE4Parse](https://github.com/FabianFG/CUE4Parse). Currently only exports .umap and .uasset files as .json files.  

Feel free to customize the code however you want. The repo will be published for reference, since CUE4Parse documentation is incomplete.  

## Features
- [x] Customizable
- [x] Multiple game support
- [ ] CLI args support
- [ ] Patch .pak reconciliation
- [ ] Files other than .uasset

## How to use
1. Download latest [release](https://github.com/whotookzakum/UnrealExporter/releases)
2. Configure `config.json` based on the game. By adding multiple objects, you can point to different games at the same time.

| Key | Type | Description |
|-----|-----------|-----------|
| version                | `string`        | Unreal Engine version. [Supported versions](https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Versions/EGame.cs). Accepts strings separated by one period, like "4.27", and game titles such as "TowerOfFantasy" or "tower of fantasy". |
| paksDir                | `string`        | An __absolute path__ to the game directory containing the .pak file(s). |
| outputDir              | `string`        | A __relative path__ to where you want to place the exported files. |
| aes                    | `string`        | The AES-256 decryption key to access game files. Leave blank if not needed. |
| keepDirectoryStructure | `bool`          | If set to `true`, folders will be made matching those found in the .paks. If set to `false`, all files will be output at the root level of the `outputDir`.     |
| targetFilePaths        | `Array(string)` | A list of files that should be exported. Supports regex. |
| excludedFilePaths      | `Array(string)` | A list of files that should be skipped, useful for avoiding files that crash CUE4Parse. Supports regex. |

### Building
If you wish to build the project yourself clone the repo and follow the steps below.
1. Download .NET SDK 8.0
2. Configure `config.json`
3. Run `dotnet run` to test, or `dotnet publish -c Release --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false` to publish to a binary.