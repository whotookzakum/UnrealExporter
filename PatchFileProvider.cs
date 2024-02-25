using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.VirtualFileSystem;

namespace SBDumper;

public class PatchFileProvider : AbstractFileProvider
{
    private readonly FileProviderDictionary _files = new(true);

    public override IReadOnlyDictionary<string, GameFile> Files => _files;
    public override IReadOnlyDictionary<FPackageId, GameFile> FilesById => _files.byId;

    public void Load(AbstractVfsFileProvider provider)
    {
        //var vfsList = new List<IAesVfsReader>();
        var vfsList = new Dictionary<int, List<IAesVfsReader>>();
        vfsList[-1] = new List<IAesVfsReader>();
        foreach (var vfs in provider.MountedVfs)
        {
            if (vfs.Name.EndsWith("_P.pak"))
            {
                var name = vfs.Name.Substring(0, vfs.Name.Length - 6);
                var iof = name.LastIndexOf('_');
                var patchNo = int.Parse(name.Substring(iof + 1));
                if (!vfsList.ContainsKey(patchNo))
                    vfsList[patchNo] = new List<IAesVfsReader>();
                vfsList[patchNo].Add(vfs);
            }
            else
            {
                vfsList[-1].Add(vfs);
            }
        }

        var files = new Dictionary<string, GameFile>();
        int[] patchNos = vfsList.Keys.ToArray();
        Array.Sort(patchNos);
        foreach (int patchNo in patchNos)
        {
            foreach (var vfs in vfsList[patchNo])
            {
                foreach (var file in vfs.Files)
                    files[file.Key] = file.Value;
            }
        }

        _files.AddFiles(files);
    }
}