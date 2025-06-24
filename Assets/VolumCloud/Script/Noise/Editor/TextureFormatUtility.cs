using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.IO;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;
/// <summary>
/// 纹理格式转换工具类
/// 提供直接的API来创建和转换纹理格式
/// </summary>
public static class TextureFormatUtility
{
    // GraphicsFormat到TextureImporterFormat的映射表
    private static readonly Dictionary<GraphicsFormat, TextureImporterFormat> FormatMapping = 
        new Dictionary<GraphicsFormat, TextureImporterFormat>
    {
        // 单通道格式
        { GraphicsFormat.R8_UNorm, TextureImporterFormat.R8 },
        { GraphicsFormat.R16_UNorm, TextureImporterFormat.R16 },
        { GraphicsFormat.R16_SFloat, TextureImporterFormat.RHalf },
        { GraphicsFormat.R32_SFloat, TextureImporterFormat.RFloat },
        
        // 双通道格式
        { GraphicsFormat.R8G8_UNorm, TextureImporterFormat.RG16 },
        { GraphicsFormat.R16G16_SFloat, TextureImporterFormat.RGHalf },
        { GraphicsFormat.R32G32_SFloat, TextureImporterFormat.RGFloat },
        
        // RGB格式
        { GraphicsFormat.R8G8B8_UNorm, TextureImporterFormat.RGB24 },
        { GraphicsFormat.R8G8B8_SRGB, TextureImporterFormat.RGB24 },
        
        // RGBA格式
        { GraphicsFormat.R8G8B8A8_UNorm, TextureImporterFormat.RGBA32 },
        { GraphicsFormat.R8G8B8A8_SRGB, TextureImporterFormat.RGBA32 },
        { GraphicsFormat.R16G16B16A16_SFloat, TextureImporterFormat.RGBAHalf },
        { GraphicsFormat.R32G32B32A32_SFloat, TextureImporterFormat.RGBAFloat },
        
        // 压缩格式
        { GraphicsFormat.RGBA_DXT1_UNorm, TextureImporterFormat.DXT1 },
        { GraphicsFormat.RGBA_DXT1_SRGB, TextureImporterFormat.DXT1 },
        { GraphicsFormat.RGBA_DXT5_UNorm, TextureImporterFormat.DXT5 },
        { GraphicsFormat.RGBA_DXT5_SRGB, TextureImporterFormat.DXT5 },
        { GraphicsFormat.RGB_BC6H_UFloat, TextureImporterFormat.BC6H },
        { GraphicsFormat.RGBA_BC7_UNorm, TextureImporterFormat.BC7 },
        { GraphicsFormat.RGBA_BC7_SRGB, TextureImporterFormat.BC7 },
        
        // ASTC格式
        { GraphicsFormat.RGBA_ASTC4X4_UNorm, TextureImporterFormat.ASTC_4x4 },
        { GraphicsFormat.RGBA_ASTC4X4_SRGB, TextureImporterFormat.ASTC_4x4 },
        { GraphicsFormat.RGBA_ASTC6X6_UNorm, TextureImporterFormat.ASTC_6x6 },
        { GraphicsFormat.RGBA_ASTC8X8_UNorm, TextureImporterFormat.ASTC_8x8 },
        { GraphicsFormat.RGBA_ASTC10X10_UNorm, TextureImporterFormat.ASTC_10x10 },
        { GraphicsFormat.RGBA_ASTC12X12_UNorm, TextureImporterFormat.ASTC_12x12 },
        
        // ETC格式
        { GraphicsFormat.RGB_ETC_UNorm, TextureImporterFormat.ETC_RGB4 },
        { GraphicsFormat.RGBA_ETC2_UNorm, TextureImporterFormat.ETC2_RGBA8 },
        
        // PVRTC格式
        { GraphicsFormat.RGB_PVRTC_4Bpp_UNorm, TextureImporterFormat.PVRTC_RGB4 },
        { GraphicsFormat.RGBA_PVRTC_4Bpp_UNorm, TextureImporterFormat.PVRTC_RGBA4 }
    };

    /// <summary>
    /// 创建指定格式的纹理（运行时）
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="format">GraphicsFormat格式</param>
    /// <param name="name">纹理名称</param>
    /// <returns>新创建的纹理</returns>
    public static Texture2D CreateTextureWithFormat(int width, int height, GraphicsFormat format, string name = "NewTexture")
    {
        try
        {
            var texture = new Texture2D(width, height, format, TextureCreationFlags.None);
            texture.name = name;
            return texture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"无法创建格式为 {format} 的纹理: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 复制纹理并转换格式（运行时转换）
    /// </summary>
    /// <param name="sourceTexture">源纹理</param>
    /// <param name="targetFormat">目标格式</param>
    /// <param name="name">新纹理名称</param>
    /// <returns>转换后的新纹理</returns>
    public static Texture2D ConvertTextureFormat(Texture2D sourceTexture, GraphicsFormat targetFormat, string name = null)
    {
        if (sourceTexture == null)
        {
            Debug.LogError("源纹理为空");
            return null;
        }

        if (name == null)
            name = sourceTexture.name + "_" + targetFormat.ToString();

        try
        {
            // 创建目标格式的新纹理
            var newTexture = CreateTextureWithFormat(sourceTexture.width, sourceTexture.height, targetFormat, name);
            if (newTexture == null) return null;

            // 使用Graphics.ConvertTexture进行格式转换（GPU加速）
            if (SystemInfo.copyTextureSupport != CopyTextureSupport.None)
            {
                Graphics.ConvertTexture(sourceTexture, newTexture);
            }
            else if (sourceTexture.isReadable)
            {
                // 回退到CPU复制
                Color[] pixels = sourceTexture.GetPixels();
                newTexture.SetPixels(pixels);
                newTexture.Apply();
            }
            else
            {
                Debug.LogWarning($"源纹理 {sourceTexture.name} 不可读且不支持GPU转换，无法复制像素数据");
            }

            return newTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"转换纹理格式失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 保存运行时纹理到文件并设置导入器格式
    /// </summary>
    /// <param name="texture">要保存的纹理</param>
    /// <param name="format">目标格式</param>
    /// <param name="path">保存路径</param>
    /// <returns>是否成功</returns>
    public static bool SaveRuntimeTextureWithFormat(Texture2D texture, GraphicsFormat format, string path)
    {
        if (texture == null)
        {
            Debug.LogError("纹理为空");
            return false;
        }

        try
        {
            // 确保纹理可读
            if (!texture.isReadable)
            {
                Debug.LogError("纹理不可读，无法保存");
                return false;
            }

            // 保存为PNG
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);

            // 刷新资源数据库
            AssetDatabase.Refresh();

            // 设置导入器格式
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                SetImporterFormat(importer, format);
                AssetDatabase.ImportAsset(path);
                return true;
            }
            else
            {
                Debug.LogError("无法获取TextureImporter");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存纹理失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 修改现有纹理的导入格式（仅支持映射表中的格式）
    /// </summary>
    /// <param name="texture">要修改的纹理</param>
    /// <param name="format">目标格式</param>
    /// <returns>是否成功</returns>
    public static bool ChangeTextureImportFormat(Texture2D texture, GraphicsFormat format)
    {
        if (texture == null)
        {
            Debug.LogError("纹理为空");
            return false;
        }

        // 检查格式是否受支持
        if (!FormatMapping.ContainsKey(format))
        {
            Debug.LogWarning($"格式 {format} 不支持通过TextureImporter设置，请使用运行时转换方法");
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("无法获取纹理资源路径");
            return false;
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("无法获取TextureImporter");
            return false;
        }

        try
        {
            SetImporterFormat(importer, format);
            AssetDatabase.ImportAsset(assetPath);
            Debug.Log($"成功将纹理 {texture.name} 的格式修改为 {format}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"修改纹理格式失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 创建新纹理文件并设置指定格式
    /// </summary>
    /// <param name="sourceTexture">源纹理</param>
    /// <param name="targetFormat">目标格式</param>
    /// <param name="outputPath">输出路径</param>
    /// <returns>新创建的纹理资源</returns>
    public static Texture2D CreateNewTextureAsset(Texture2D sourceTexture, GraphicsFormat targetFormat, string outputPath)
    {
        if (sourceTexture == null)
        {
            Debug.LogError("源纹理为空");
            return null;
        }

        try
        {
            // 先转换为运行时格式
            var runtimeTexture = ConvertTextureFormat(sourceTexture, targetFormat);
            if (runtimeTexture == null) return null;

            // 保存到文件
            if (SaveRuntimeTextureWithFormat(runtimeTexture, targetFormat, outputPath))
            {
                // 加载保存的资源
                var newAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                
                // 清理运行时纹理
                if (Application.isPlaying)
                    Object.Destroy(runtimeTexture);
                else
                    Object.DestroyImmediate(runtimeTexture);
                
                return newAsset;
            }
            
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建新纹理资源失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 批量修改纹理格式
    /// </summary>
    /// <param name="textures">纹理数组</param>
    /// <param name="format">目标格式</param>
    /// <returns>成功修改的数量</returns>
    public static int BatchChangeTextureFormat(Texture2D[] textures, GraphicsFormat format)
    {
        if (textures == null || textures.Length == 0)
        {
            Debug.LogWarning("纹理数组为空");
            return 0;
        }

        int successCount = 0;
        bool isImporterSupported = FormatMapping.ContainsKey(format);
        
        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) continue;

                EditorUtility.DisplayProgressBar("批量修改纹理格式", 
                    $"处理 {textures[i].name} ({i + 1}/{textures.Length})", 
                    (float)i / textures.Length);

                if (isImporterSupported)
                {
                    // 使用导入器设置
                    if (ChangeTextureImportFormat(textures[i], format))
                    {
                        successCount++;
                    }
                }
                else
                {
                    // 创建新的运行时纹理文件
                    string assetPath = AssetDatabase.GetAssetPath(textures[i]);
                    string directory = System.IO.Path.GetDirectoryName(assetPath);
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    string newPath = System.IO.Path.Combine(directory, $"{fileName}_Runtime_{format}.png");
                    
                    if (CreateNewTextureAsset(textures[i], format, newPath) != null)
                    {
                        successCount++;
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        if (!isImporterSupported)
        {
            Debug.Log($"格式 {format} 不被TextureImporter支持，已创建新的纹理文件");
        }

        Debug.Log($"批量修改完成: {successCount}/{textures.Length} 个纹理成功处理");
        return successCount;
    }

    private static void SetImporterFormat(TextureImporter importer, GraphicsFormat format)
    {
        // 设置sRGB
        bool isSRGB = format.ToString().Contains("SRGB");
        importer.sRGBTexture = isSRGB;

        // 设置平台格式
        var platformSettings = importer.GetDefaultPlatformTextureSettings();
        
        if (FormatMapping.ContainsKey(format))
        {
            platformSettings.format = FormatMapping[format];
        }
        else
        {
            Debug.LogWarning($"格式 {format} 不在映射表中，使用 Automatic");
            platformSettings.format = TextureImporterFormat.Automatic;
        }
        
        importer.SetPlatformTextureSettings(platformSettings);
    }

    /// <summary>
    /// 检查格式是否被当前平台支持
    /// </summary>
    /// <param name="format">要检查的格式</param>
    /// <returns>是否支持</returns>
    public static bool IsFormatSupported(GraphicsFormat format)
    {
        try
        {
            return SystemInfo.IsFormatSupported(format, FormatUsage.Sample);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查格式是否可以通过TextureImporter设置
    /// </summary>
    /// <param name="format">要检查的格式</param>
    /// <returns>是否可以通过导入器设置</returns>
    public static bool IsImporterFormatSupported(GraphicsFormat format)
    {
        return FormatMapping.ContainsKey(format);
    }

    /// <summary>
    /// 获取所有支持的GraphicsFormat列表
    /// </summary>
    /// <returns>支持的格式列表</returns>
    public static List<GraphicsFormat> GetSupportedFormats()
    {
        var supportedFormats = new List<GraphicsFormat>();
        var allFormats = System.Enum.GetValues(typeof(GraphicsFormat));
        
        foreach (GraphicsFormat format in allFormats)
        {
            if (format != GraphicsFormat.None && IsFormatSupported(format))
            {
                supportedFormats.Add(format);
            }
        }
        
        return supportedFormats;
    }

    /// <summary>
    /// 获取可以通过TextureImporter设置的格式列表
    /// </summary>
    /// <returns>导入器支持的格式列表</returns>
    public static List<GraphicsFormat> GetImporterSupportedFormats()
    {
        var importerFormats = new List<GraphicsFormat>();
        
        foreach (var kvp in FormatMapping)
        {
            if (IsFormatSupported(kvp.Key))
            {
                importerFormats.Add(kvp.Key);
            }
        }
        
        return importerFormats;
    }

    /// <summary>
    /// 获取格式的估算字节数
    /// </summary>
    /// <param name="format">图形格式</param>
    /// <returns>每像素字节数</returns>
    public static int GetBytesPerPixel(GraphicsFormat format)
    {
        switch (format)
        {
            case GraphicsFormat.R8_UNorm:
            case GraphicsFormat.R8_SNorm:
            case GraphicsFormat.R8_UInt:
            case GraphicsFormat.R8_SInt:
                return 1;
                
            case GraphicsFormat.R8G8_UNorm:
            case GraphicsFormat.R8G8_SNorm:
            case GraphicsFormat.R16_UNorm:
            case GraphicsFormat.R16_SNorm:
            case GraphicsFormat.R16_SFloat:
                return 2;
                
            case GraphicsFormat.R8G8B8_UNorm:
            case GraphicsFormat.R8G8B8_SRGB:
                return 3;
                
            case GraphicsFormat.R8G8B8A8_UNorm:
            case GraphicsFormat.R8G8B8A8_SRGB:
            case GraphicsFormat.R16G16_SFloat:
            case GraphicsFormat.R32_SFloat:
                return 4;
                
            case GraphicsFormat.R16G16B16A16_SFloat:
                return 8;
                
            case GraphicsFormat.R32G32B32A32_SFloat:
                return 16;
                
            // 压缩格式（近似值）
            case GraphicsFormat.RGBA_DXT1_UNorm:
            case GraphicsFormat.RGBA_DXT1_SRGB:
                return 1; // 约0.5字节每像素
                
            case GraphicsFormat.RGBA_DXT5_UNorm:
            case GraphicsFormat.RGBA_DXT5_SRGB:
            case GraphicsFormat.RGBA_BC7_UNorm:
            case GraphicsFormat.RGBA_BC7_SRGB:
                return 1; // 约1字节每像素
                
            default:
                return 4; // 默认RGBA
        }
    }

    /// <summary>
    /// 计算纹理的估算大小
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="format">格式</param>
    /// <returns>大小（MB）</returns>
    public static float CalculateTextureSize(int width, int height, GraphicsFormat format)
    {
        int bytesPerPixel = GetBytesPerPixel(format);
        long totalBytes = (long)width * height * bytesPerPixel;
        return totalBytes / (1024f * 1024f);
    }
}

/// <summary>
/// 菜单项扩展 - 提供快速访问的菜单项
/// </summary>
public static class TextureFormatMenuItems
{
    [MenuItem("工具/纹理格式工具/转换为DXT1")]
    public static void ConvertToDXT1()
    {
        ConvertSelectedTextures(GraphicsFormat.RGBA_DXT1_UNorm);
    }

    [MenuItem("工具/纹理格式工具/转换为DXT5")]
    public static void ConvertToDXT5()
    {
        ConvertSelectedTextures(GraphicsFormat.RGBA_DXT5_UNorm);
    }

    [MenuItem("工具/纹理格式工具/转换为BC7")]
    public static void ConvertToBC7()
    {
        ConvertSelectedTextures(GraphicsFormat.RGBA_BC7_UNorm);
    }

    [MenuItem("工具/纹理格式工具/转换为RGBA32")]
    public static void ConvertToRGBA32()
    {
        ConvertSelectedTextures(GraphicsFormat.R8G8B8A8_UNorm);
    }

    [MenuItem("工具/纹理格式工具/转换为单通道R8")]
    public static void ConvertToR8()
    {
        ConvertSelectedTextures(GraphicsFormat.R8_UNorm);
    }

    [MenuItem("工具/纹理格式工具/转换为HDR格式")]
    public static void ConvertToHDR()
    {
        ConvertSelectedTextures(GraphicsFormat.R16G16B16A16_SFloat);
    }

    [MenuItem("工具/纹理格式工具/创建运行时转换副本")]
    public static void CreateRuntimeConvertedCopy()
    {
        var selectedTextures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        
        if (selectedTextures.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先选择要转换的纹理", "确定");
            return;
        }

        // 让用户选择格式
        var supportedFormats = TextureFormatUtility.GetSupportedFormats();
        var formatNames = new string[supportedFormats.Count];
        for (int i = 0; i < supportedFormats.Count; i++)
        {
            formatNames[i] = supportedFormats[i].ToString();
        }

        int selectedIndex = EditorUtility.DisplayDialogComplex("选择格式", 
            "选择要转换到的GraphicsFormat", "R16G16B16A16_SFloat (HDR)", "R8G8B8A8_UNorm (标准)", "取消");

        if (selectedIndex == 2) return; // 取消

        GraphicsFormat targetFormat = selectedIndex == 0 ? 
            GraphicsFormat.R16G16B16A16_SFloat : 
            GraphicsFormat.R8G8B8A8_UNorm;

        int successCount = 0;
        foreach (var texture in selectedTextures)
        {
            string assetPath = AssetDatabase.GetAssetPath(texture);
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            string newPath = System.IO.Path.Combine(directory, $"{fileName}_Runtime_{targetFormat}.png");
            
            if (TextureFormatUtility.CreateNewTextureAsset(texture, targetFormat, newPath) != null)
            {
                successCount++;
            }
        }

        EditorUtility.DisplayDialog("完成", 
            $"成功创建了 {successCount}/{selectedTextures.Length} 个运行时格式的纹理副本", "确定");
    }

    private static void ConvertSelectedTextures(GraphicsFormat format)
    {
        var selectedTextures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        
        if (selectedTextures.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先选择要转换的纹理", "确定");
            return;
        }

        string message = $"确定要将 {selectedTextures.Length} 个纹理转换为 {format}？";
        if (!TextureFormatUtility.IsImporterFormatSupported(format))
        {
            message += "\n\n注意：此格式不被TextureImporter支持，将创建新的纹理文件。";
        }

        bool proceed = EditorUtility.DisplayDialog("确认转换", message, "确定", "取消");

        if (proceed)
        {
            int success = TextureFormatUtility.BatchChangeTextureFormat(selectedTextures, format);
            EditorUtility.DisplayDialog("完成", 
                $"成功转换了 {success}/{selectedTextures.Length} 个纹理", "确定");
        }
    }

    // 验证菜单项是否应该启用
    [MenuItem("工具/纹理格式工具/转换为DXT1", true)]
    [MenuItem("工具/纹理格式工具/转换为DXT5", true)]
    [MenuItem("工具/纹理格式工具/转换为BC7", true)]
    [MenuItem("工具/纹理格式工具/转换为RGBA32", true)]
    [MenuItem("工具/纹理格式工具/转换为单通道R8", true)]
    [MenuItem("工具/纹理格式工具/转换为HDR格式", true)]
    [MenuItem("工具/纹理格式工具/创建运行时转换副本", true)]
    public static bool ValidateConvertTextures()
    {
        return Selection.GetFiltered<Texture2D>(SelectionMode.Assets).Length > 0;
    }
} 