using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class NoiseGenerator
{
    [MenuItem("Tools/Generate Texture3D From Folder")]
    static void GenerateTexture3D()
    {
        string folderPath = "Assets/SkyCloud/Resource";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        var textures = guids
            .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)))
            .OrderBy(tex => tex.name)
            .ToArray();

        if (textures.Length == 0)
        {
            Debug.LogError("未找到任何 Texture2D！");
            return;
        }

        int width = textures[0].width;
        int height = textures[0].height;
        int depth = textures.Length;

        // 检查尺寸一致性
        foreach (var tex in textures)
        {
            if (!tex.isReadable)
            {
                Debug.LogError($"纹理 {tex.name} 未开启 Read/Write！");
                return;
            }

            if (tex.width != width || tex.height != height)
            {
                Debug.LogError($"纹理 {tex.name} 尺寸不一致！");
                return;
            }
        }

        // 构建 Texture3D
        Texture3D texture3D = new Texture3D(width, height, depth, TextureFormat.RGBA32, false);
        texture3D.wrapMode = TextureWrapMode.Clamp;
        texture3D.filterMode = FilterMode.Bilinear;

        Color[] colors = new Color[width * height * depth];

        for (int z = 0; z < depth; z++)
        {
            Color[] layerColors = textures[z].GetPixels();
            layerColors.CopyTo(colors, z * width * height);
        }

        texture3D.SetPixels(colors);
        texture3D.Apply();

        // 保存为 asset
        string savePath = "Assets/SkyCloud/Textures/CloudSDF.asset";
        AssetDatabase.CreateAsset(texture3D, savePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"✅ Texture3D 已生成并保存到：{savePath}");
    }
}