using System.IO;
using System.Text;
using UnityEditor;

public class Utf8ScriptPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedAssetFolders)
    {
        foreach (string assetPath in importedAssets)
        {
            if (assetPath.EndsWith(".cs") && File.Exists(assetPath))
            {
                string content = File.ReadAllText(assetPath);

                Encoding utf8WithBom = new UTF8Encoding(true);
                File.WriteAllText(assetPath, content, utf8WithBom);
            }
        }
    }
}