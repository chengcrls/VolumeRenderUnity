using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Experimental.Rendering;

public class TextureFormatConverter : EditorWindow
{
    private Texture2D sourceTexture;
    private GraphicsFormat selectedFormat = GraphicsFormat.R8G8B8A8_UNorm;
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private bool showCompatibleFormatsOnly = true;
    
    private static readonly Dictionary<GraphicsFormat, string> formatDescriptions = new Dictionary<GraphicsFormat, string>
    {
        { GraphicsFormat.R8G8B8A8_UNorm, "标准RGBA 8位（推荐用于大多数纹理）" },
        { GraphicsFormat.R8G8B8A8_SRGB, "sRGB RGBA 8位（用于颜色纹理）" },
        { GraphicsFormat.R8G8B8_UNorm, "RGB 8位（无透明通道）" },
        { GraphicsFormat.R8G8B8_SRGB, "sRGB RGB 8位（无透明通道）" },
        { GraphicsFormat.R16G16B16A16_SFloat, "半精度浮点RGBA（HDR纹理）" },
        { GraphicsFormat.R32G32B32A32_SFloat, "全精度浮点RGBA（高精度HDR）" },
        { GraphicsFormat.R8_UNorm, "单通道8位（灰度图）" },
        { GraphicsFormat.R16_UNorm, "单通道16位（高精度高度图）" },
        { GraphicsFormat.R16_SNorm, "单通道16位（高精度单通道）" },
        { GraphicsFormat.R16_UInt, "单通道16位（高精度单通道）" },
        { GraphicsFormat.R16_SInt, "单通道16位（高精度单通道）" },
        { GraphicsFormat.R16_SFloat, "单通道16位（高精度单通道）" },
        { GraphicsFormat.R32_SFloat, "单通道32位浮点（高精度数据）" },
        { GraphicsFormat.RGBA_DXT1_UNorm, "DXT1压缩（无透明）" },
        { GraphicsFormat.RGBA_DXT1_SRGB, "DXT1压缩sRGB（无透明）" },
        { GraphicsFormat.RGBA_DXT5_UNorm, "DXT5压缩（带透明）" },
        { GraphicsFormat.RGBA_DXT5_SRGB, "DXT5压缩sRGB（带透明）" },
        { GraphicsFormat.RGB_BC6H_UFloat, "BC6H压缩（HDR）" },
        { GraphicsFormat.RGBA_BC7_UNorm, "BC7压缩（高质量）" },
        { GraphicsFormat.RGBA_BC7_SRGB, "BC7压缩sRGB（高质量颜色）" }
    };

    [MenuItem("工具/纹理格式转换器")]
    public static void ShowWindow()
    {
        GetWindow<TextureFormatConverter>("纹理格式转换器");
    }

    private void OnGUI()
    {
        GUILayout.Label("纹理格式转换器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 源纹理选择
        EditorGUILayout.LabelField("源纹理", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("纹理", sourceTexture, typeof(Texture2D), false);
        
        if (sourceTexture == null)
        {
            EditorGUILayout.HelpBox("请选择一个Texture2D", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        // 显示当前纹理信息
        EditorGUILayout.LabelField("当前纹理信息", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"尺寸: {sourceTexture.width} x {sourceTexture.height}");
        EditorGUILayout.LabelField($"当前格式: {sourceTexture.graphicsFormat}");
        EditorGUILayout.LabelField($"文件大小: {GetTextureMemorySize(sourceTexture)} MB");

        EditorGUILayout.Space();

        // 格式选择区域
        EditorGUILayout.LabelField("选择新格式", EditorStyles.boldLabel);
        
        // 搜索过滤器
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        EditorGUILayout.EndHorizontal();

        // 仅显示兼容格式选项
        showCompatibleFormatsOnly = EditorGUILayout.Toggle("仅显示兼容格式", showCompatibleFormatsOnly);

        EditorGUILayout.Space();

        // 格式列表
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
        
        var availableFormats = GetAvailableFormats();
        
        foreach (var format in availableFormats)
        {
            if (!string.IsNullOrEmpty(searchFilter) && 
                !format.ToString().ToLower().Contains(searchFilter.ToLower()) &&
                (!formatDescriptions.ContainsKey(format) || 
                 !formatDescriptions[format].ToLower().Contains(searchFilter.ToLower())))
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal();
            
            bool isSelected = selectedFormat == format;
            bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            
            if (newSelection != isSelected)
            {
                selectedFormat = format;
            }

            EditorGUILayout.LabelField(format.ToString(), GUILayout.Width(200));
            
            if (formatDescriptions.ContainsKey(format))
            {
                EditorGUILayout.LabelField(formatDescriptions[format], EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 预估新文件大小
        if (selectedFormat != sourceTexture.graphicsFormat)
        {
            float estimatedSize = EstimateTextureSize(sourceTexture.width, sourceTexture.height, selectedFormat);
            EditorGUILayout.LabelField($"预估新文件大小: {estimatedSize:F2} MB");
        }

        EditorGUILayout.Space();

        // 转换按钮
        EditorGUI.BeginDisabledGroup(selectedFormat == sourceTexture.graphicsFormat);
        if (GUILayout.Button("转换纹理格式", GUILayout.Height(30)))
        {
            ConvertTextureFormat();
        }
        EditorGUI.EndDisabledGroup();

        if (selectedFormat == sourceTexture.graphicsFormat)
        {
            EditorGUILayout.HelpBox("选择的格式与当前格式相同", MessageType.Info);
        }
    }

    private List<GraphicsFormat> GetAvailableFormats()
    {
        var allFormats = System.Enum.GetValues(typeof(GraphicsFormat)).Cast<GraphicsFormat>().ToList();
        
        if (!showCompatibleFormatsOnly)
        {
            return allFormats.Where(f => f != GraphicsFormat.None).ToList();
        }

        // 过滤兼容格式
        var compatibleFormats = new List<GraphicsFormat>();
        
        foreach (var format in allFormats)
        {
            if (format == GraphicsFormat.None) continue;
            
            try
            {
                if (SystemInfo.IsFormatSupported(format, FormatUsage.Sample))
                {
                    compatibleFormats.Add(format);
                }
            }
            catch
            {
                // 某些格式可能会抛出异常，忽略它们
            }
        }
        
        return compatibleFormats;
    }

    private float GetTextureMemorySize(Texture2D texture)
    {
        if (texture == null) return 0f;
        
        int width = texture.width;
        int height = texture.height;
        GraphicsFormat format = texture.graphicsFormat;
        
        return EstimateTextureSize(width, height, format);
    }

    private float EstimateTextureSize(int width, int height, GraphicsFormat format)
    {
        int bytesPerPixel = GetBytesPerPixel(format);
        long totalBytes = (long)width * height * bytesPerPixel;
        return totalBytes / (1024f * 1024f); // 转换为MB
    }

    private int GetBytesPerPixel(GraphicsFormat format)
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
            case GraphicsFormat.R8G8_UInt:
            case GraphicsFormat.R8G8_SInt:
            case GraphicsFormat.R16_UNorm:
            case GraphicsFormat.R16_SNorm:
            case GraphicsFormat.R16_UInt:
            case GraphicsFormat.R16_SInt:
            case GraphicsFormat.R16_SFloat:
                return 2;
                
            case GraphicsFormat.R8G8B8_UNorm:
            case GraphicsFormat.R8G8B8_SNorm:
            case GraphicsFormat.R8G8B8_UInt:
            case GraphicsFormat.R8G8B8_SInt:
            case GraphicsFormat.R8G8B8_SRGB:
                return 3;
                
            case GraphicsFormat.R8G8B8A8_UNorm:
            case GraphicsFormat.R8G8B8A8_SNorm:
            case GraphicsFormat.R8G8B8A8_UInt:
            case GraphicsFormat.R8G8B8A8_SInt:
            case GraphicsFormat.R8G8B8A8_SRGB:
            case GraphicsFormat.R16G16_UNorm:
            case GraphicsFormat.R16G16_SNorm:
            case GraphicsFormat.R16G16_UInt:
            case GraphicsFormat.R16G16_SInt:
            case GraphicsFormat.R16G16_SFloat:
            case GraphicsFormat.R32_UInt:
            case GraphicsFormat.R32_SInt:
            case GraphicsFormat.R32_SFloat:
                return 4;
                
            case GraphicsFormat.R16G16B16A16_UNorm:
            case GraphicsFormat.R16G16B16A16_SNorm:
            case GraphicsFormat.R16G16B16A16_UInt:
            case GraphicsFormat.R16G16B16A16_SInt:
            case GraphicsFormat.R16G16B16A16_SFloat:
            case GraphicsFormat.R32G32_UInt:
            case GraphicsFormat.R32G32_SInt:
            case GraphicsFormat.R32G32_SFloat:
                return 8;
                
            case GraphicsFormat.R32G32B32A32_UInt:
            case GraphicsFormat.R32G32B32A32_SInt:
            case GraphicsFormat.R32G32B32A32_SFloat:
                return 16;
                
            // 压缩格式的估算（这些是近似值）
            case GraphicsFormat.RGBA_DXT1_UNorm:
            case GraphicsFormat.RGBA_DXT1_SRGB:
                return 1; // DXT1 约为0.5字节每像素
                
            case GraphicsFormat.RGBA_DXT5_UNorm:
            case GraphicsFormat.RGBA_DXT5_SRGB:
                return 1; // DXT5 约为1字节每像素
                
            default:
                return 4; // 默认假设4字节每像素
        }
    }

    private void ConvertTextureFormat()
    {
        if (sourceTexture == null)
        {
            EditorUtility.DisplayDialog("错误", "请选择源纹理", "确定");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(sourceTexture);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("错误", "无法获取纹理资源路径", "确定");
            return;
        }

        // 获取TextureImporter
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("错误", "无法获取TextureImporter", "确定");
            return;
        }

        try
        {
            // 设置纹理为可读
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(assetPath);
            }

            // 创建新纹理
            Texture2D newTexture = new Texture2D(sourceTexture.width, sourceTexture.height, selectedFormat, TextureCreationFlags.None);
            
            // 复制像素数据
            Color[] pixels = sourceTexture.GetPixels();
            newTexture.SetPixels(pixels);
            newTexture.Apply();

            // 保存新纹理
            string newPath = assetPath.Replace(".png", "_converted.png").Replace(".jpg", "_converted.png").Replace(".tga", "_converted.png");
            byte[] pngData = newTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(newPath, pngData);

            // 清理
            DestroyImmediate(newTexture);

            // 恢复原始可读设置
            if (!wasReadable)
            {
                importer.isReadable = wasReadable;
                AssetDatabase.ImportAsset(assetPath);
            }

            // 刷新资源数据库
            AssetDatabase.Refresh();

            // 设置新纹理的导入设置
            TextureImporter newImporter = AssetImporter.GetAtPath(newPath) as TextureImporter;
            if (newImporter != null)
            {
                // 这里我们无法直接设置GraphicsFormat，但可以设置相关的导入参数
                SetImporterFormatSettings(newImporter, selectedFormat);
                AssetDatabase.ImportAsset(newPath);
            }

            EditorUtility.DisplayDialog("成功", $"纹理已转换并保存到: {newPath}", "确定");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"转换失败: {e.Message}", "确定");
        }
    }

    private void SetImporterFormatSettings(TextureImporter importer, GraphicsFormat targetFormat)
    {
        // 根据目标格式设置TextureImporter的相关参数
        switch (targetFormat)
        {
            case GraphicsFormat.R8G8B8A8_SRGB:
            case GraphicsFormat.R8G8B8_SRGB:
            case GraphicsFormat.RGBA_DXT1_SRGB:
            case GraphicsFormat.RGBA_DXT5_SRGB:
            case GraphicsFormat.RGBA_BC7_SRGB:
                importer.sRGBTexture = true;
                break;
            default:
                importer.sRGBTexture = false;
                break;
        }

        // 设置压缩格式
        TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
        
        if (targetFormat.ToString().Contains("DXT1"))
        {
            settings.format = TextureImporterFormat.DXT1;
        }
        else if (targetFormat.ToString().Contains("DXT5"))
        {
            settings.format = TextureImporterFormat.DXT5;
        }
        else if (targetFormat.ToString().Contains("BC7"))
        {
            settings.format = TextureImporterFormat.BC7;
        }
        else if (targetFormat.ToString().Contains("BC6H"))
        {
            settings.format = TextureImporterFormat.BC6H;
        }
        else if (targetFormat.ToString().Contains("R8_"))
        {
            settings.format = TextureImporterFormat.R8;
        }
        else if (targetFormat.ToString().Contains("R16_"))
        {
            settings.format = TextureImporterFormat.R16;
        }
        else
        {
            settings.format = TextureImporterFormat.RGBA32;
        }

        importer.SetPlatformTextureSettings(settings);
    }
} 