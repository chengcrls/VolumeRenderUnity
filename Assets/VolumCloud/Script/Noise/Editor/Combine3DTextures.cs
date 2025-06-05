using UnityEditor;
using UnityEngine;

public class Combine3DTextures
{
    [MenuItem("Tools/Combine 3D Textures")]
    static void CombineTextures()
    {
        var texR=AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/VolumCloud/Texture/Generated/WrolyNoise1.asset");
        var texG=AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/VolumCloud/Texture/Generated/WrolyNoise2.asset");
        var texB=AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/VolumCloud/Texture/Generated/WrolyNoise3.asset");

        if (texR == null || texG == null || texB == null)
        {
            Debug.LogError("No 3D Textures found");
            return;
        }
        int width = texR.width;
        int height = texR.height;
        int depth = texR.depth;

        if (texG.width != width || texG.height != height || texG.depth != depth ||
            texB.width != width || texB.height != height || texB.depth != depth)
        {
            Debug.LogError("Textures are not the same size");
        }
        Color[] colorsR=texR.GetPixels();
        Color[] colorsG=texG.GetPixels();
        Color[] colorsB=texB.GetPixels();
        
        Color[] colorMerge=new Color[colorsR.Length];
        for (int i = 0; i < colorsR.Length; i++)
        {
            colorMerge[i].r=colorsR[i].r;
            colorMerge[i].g=colorsG[i].r;
            colorMerge[i].b=colorsB[i].r;
            colorMerge[i].a=1;
        }
        
        Texture3D result = new Texture3D(width, height, depth, TextureFormat.ARGB32, false);
        result.wrapMode = TextureWrapMode.Repeat;
        result.filterMode = FilterMode.Trilinear;
        result.SetPixels(colorMerge);
        result.Apply();

        string path = "Assets/VolumCloud/Texture/Generated/Worly.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.Refresh();
        
        AssetDatabase.CreateAsset(result, path);
        AssetDatabase.Refresh();
    }
}
