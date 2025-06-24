using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Experimental.Rendering;
public class TextureFormatChanger : EditorWindow
{
    private List<Texture2D> selectedTextures = new List<Texture2D>();
    private GraphicsFormat targetFormat = GraphicsFormat.R8G8B8A8_UNorm;
    private Vector2 scrollPosition;
    private Vector2 textureScrollPosition;
    private string searchFilter = "";
    private bool showOnlySupportedFormats = true;
    private bool showOnlyImporterFormats = false;
    
    // 常用格式及其描述
    private static readonly Dictionary<GraphicsFormat, string> commonFormats = new Dictionary<GraphicsFormat, string>
    {
        { GraphicsFormat.R8G8B8A8_UNorm, "RGBA 32位 - 标准格式" },
        { GraphicsFormat.R8G8B8A8_SRGB, "RGBA 32位 sRGB - 颜色纹理" },
        { GraphicsFormat.R8G8B8_UNorm, "RGB 24位 - 无透明通道" },
        { GraphicsFormat.R8G8B8_SRGB, "RGB 24位 sRGB - 无透明通道" },
        { GraphicsFormat.R16G16B16A16_SFloat, "RGBA 64位半精度 - HDR纹理" },
        { GraphicsFormat.R32G32B32A32_SFloat, "RGBA 128位全精度 - 高精度HDR" },
        { GraphicsFormat.R8_UNorm, "R 8位 - 单通道灰度" },
        { GraphicsFormat.R16_UNorm, "R 16位 - 高精度单通道" },
        { GraphicsFormat.R16_SNorm, "R 16位 - 高精度单通道" },
        { GraphicsFormat.R16_UInt, "R 16位 - 高精度单通道" },
        { GraphicsFormat.R16_SInt, "R 16位 - 高精度单通道" },
        { GraphicsFormat.R16_SFloat, "R 16位 - 高精度单通道" },
        { GraphicsFormat.R32_SFloat, "R 32位浮点 - 单通道数据" },
        { GraphicsFormat.R8G8_UNorm, "RG 16位 - 双通道" },
        { GraphicsFormat.R16G16_SFloat, "RG 32位半精度 - 双通道浮点" },
        { GraphicsFormat.RGBA_DXT1_UNorm, "DXT1 - 压缩RGB (4:1)" },
        { GraphicsFormat.RGBA_DXT1_SRGB, "DXT1 sRGB - 压缩RGB颜色 (4:1)" },
        { GraphicsFormat.RGBA_DXT5_UNorm, "DXT5 - 压缩RGBA (4:1)" },
        { GraphicsFormat.RGBA_DXT5_SRGB, "DXT5 sRGB - 压缩RGBA颜色 (4:1)" },
        { GraphicsFormat.RGB_BC6H_UFloat, "BC6H - HDR压缩 (6:1)" },
        { GraphicsFormat.RGBA_BC7_UNorm, "BC7 - 高质量压缩 (4:1)" },
        { GraphicsFormat.RGBA_BC7_SRGB, "BC7 sRGB - 高质量颜色压缩 (4:1)" },
        { GraphicsFormat.RGBA_ASTC4X4_UNorm, "ASTC 4x4 - 移动端高质量 (8:1)" },
        { GraphicsFormat.RGBA_ASTC4X4_SRGB, "ASTC 4x4 sRGB - 移动端颜色 (8:1)" },
        { GraphicsFormat.RGBA_ASTC6X6_UNorm, "ASTC 6x6 - 移动端标准 (5.56:1)" },
        { GraphicsFormat.RGBA_ASTC8X8_UNorm, "ASTC 8x8 - 移动端压缩 (4:1)" },
        
        // 更多高级格式
        { GraphicsFormat.R8G8B8A8_SNorm, "RGBA 32位 SNorm - 法线贴图" },
        { GraphicsFormat.B8G8R8A8_UNorm, "BGRA 32位 - 系统格式" },
        { GraphicsFormat.B8G8R8A8_SRGB, "BGRA 32位 sRGB - 系统颜色" },
        
        // ETC格式
        { GraphicsFormat.RGB_ETC_UNorm, "ETC RGB - Android压缩" },
        { GraphicsFormat.RGBA_ETC2_UNorm, "ETC2 RGBA - Android压缩" },
        
        // PVRTC格式
        { GraphicsFormat.RGB_PVRTC_4Bpp_UNorm, "PVRTC RGB 4bpp - iOS压缩" },
        { GraphicsFormat.RGBA_PVRTC_4Bpp_UNorm, "PVRTC RGBA 4bpp - iOS压缩" }
    };

    [MenuItem("工具/纹理格式修改器")]
    public static void ShowWindow()
    {
        GetWindow<TextureFormatChanger>("纹理格式修改器");
    }

    [MenuItem("Assets/修改纹理格式", true)]
    public static bool ValidateChangeTextureFormat()
    {
        return Selection.objects.Any(obj => obj is Texture2D);
    }

    [MenuItem("Assets/修改纹理格式")]
    public static void ChangeTextureFormatFromAssets()
    {
        var window = GetWindow<TextureFormatChanger>("纹理格式修改器");
        window.LoadSelectedTextures();
    }

    private void LoadSelectedTextures()
    {
        selectedTextures.Clear();
        foreach (var obj in Selection.objects)
        {
            if (obj is Texture2D texture)
            {
                selectedTextures.Add(texture);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("纹理格式修改器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawTextureSelection();
        EditorGUILayout.Space();
        
        if (selectedTextures.Count > 0)
        {
            DrawCurrentTextureInfo();
            EditorGUILayout.Space();
            DrawFormatSelection();
            EditorGUILayout.Space();
            DrawActionButtons();
        }
        else
        {
            EditorGUILayout.HelpBox("请添加要修改的纹理", MessageType.Info);
        }
    }

    private void DrawTextureSelection()
    {
        EditorGUILayout.LabelField("选择纹理", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加选中的纹理"))
        {
            LoadSelectedTextures();
        }
        if (GUILayout.Button("清空列表"))
        {
            selectedTextures.Clear();
        }
        EditorGUILayout.EndHorizontal();

        if (selectedTextures.Count > 0)
        {
            EditorGUILayout.LabelField($"已选择 {selectedTextures.Count} 个纹理:");
            
            textureScrollPosition = EditorGUILayout.BeginScrollView(textureScrollPosition, GUILayout.Height(100));
            for (int i = selectedTextures.Count - 1; i >= 0; i--)
            {
                if (selectedTextures[i] == null)
                {
                    selectedTextures.RemoveAt(i);
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(selectedTextures[i], typeof(Texture2D), false);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    selectedTextures.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // 拖拽区域
        var dragArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dragArea, "拖拽纹理到这里");
        
        HandleDragAndDrop(dragArea);
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        
        if (evt.type == EventType.DragUpdated && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.AcceptDrag();
            
            foreach (var draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is Texture2D texture && !selectedTextures.Contains(texture))
                {
                    selectedTextures.Add(texture);
                }
            }
            evt.Use();
        }
    }

    private void DrawCurrentTextureInfo()
    {
        EditorGUILayout.LabelField("当前纹理信息", EditorStyles.boldLabel);
        
        if (selectedTextures.Count == 1)
        {
            var texture = selectedTextures[0];
            EditorGUILayout.LabelField($"尺寸: {texture.width} x {texture.height}");
            EditorGUILayout.LabelField($"当前格式: {texture.graphicsFormat}");
            EditorGUILayout.LabelField($"内存占用: {CalculateTextureSize(texture):F2} MB");
        }
        else
        {
            EditorGUILayout.LabelField($"已选择 {selectedTextures.Count} 个纹理");
            float totalSize = selectedTextures.Sum(CalculateTextureSize);
            EditorGUILayout.LabelField($"总内存占用: {totalSize:F2} MB");
        }
    }

    private void DrawFormatSelection()
    {
        EditorGUILayout.LabelField("选择目标格式", EditorStyles.boldLabel);
        
        // 搜索框
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        EditorGUILayout.EndHorizontal();
        
        // 格式过滤选项
        EditorGUILayout.BeginHorizontal();
        showOnlySupportedFormats = EditorGUILayout.Toggle("仅显示支持的格式", showOnlySupportedFormats);
        showOnlyImporterFormats = EditorGUILayout.Toggle("仅显示导入器格式", showOnlyImporterFormats);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        
        var formatsToShow = GetFormatsToShow();
        
        foreach (var kvp in formatsToShow)
        {
            var format = kvp.Key;
            var description = kvp.Value;
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                if (!format.ToString().ToLower().Contains(searchFilter.ToLower()) &&
                    !description.ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            
            bool isSelected = targetFormat == format;
            bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            
            if (newSelection && !isSelected)
            {
                targetFormat = format;
            }
            
            // 格式名称
            EditorGUILayout.LabelField(format.ToString(), GUILayout.Width(200));
            
            // 状态标识
            string status = "";
            if (TextureFormatUtility.IsImporterFormatSupported(format))
            {
                status = "[导入器]";
            }
            else if (TextureFormatUtility.IsFormatSupported(format))
            {
                status = "[运行时]";
            }
            else
            {
                status = "[不支持]";
            }
            
            EditorGUILayout.LabelField(status, GUILayout.Width(60));
            
            // 描述
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        
        // 显示选中格式的信息
        if (selectedTextures.Count > 0)
        {
            float estimatedSize = EstimateSizeWithFormat(targetFormat);
            EditorGUILayout.LabelField($"预估转换后大小: {estimatedSize:F2} MB");
            
            // 显示转换方式
            if (TextureFormatUtility.IsImporterFormatSupported(targetFormat))
            {
                EditorGUILayout.HelpBox("此格式支持通过TextureImporter设置，将直接修改导入设置。", MessageType.Info);
            }
            else if (TextureFormatUtility.IsFormatSupported(targetFormat))
            {
                EditorGUILayout.HelpBox("此格式仅支持运行时转换，将创建新的纹理文件。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("此格式在当前平台不受支持。", MessageType.Error);
            }
        }
    }

    private Dictionary<GraphicsFormat, string> GetFormatsToShow()
    {
        var formatsToShow = new Dictionary<GraphicsFormat, string>();
        
        foreach (var kvp in commonFormats)
        {
            var format = kvp.Key;
            
            // 根据过滤器决定是否显示
            if (showOnlySupportedFormats && !TextureFormatUtility.IsFormatSupported(format))
                continue;
                
            if (showOnlyImporterFormats && !TextureFormatUtility.IsImporterFormatSupported(format))
                continue;
            
            formatsToShow.Add(format, kvp.Value);
        }
        
        return formatsToShow;
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();
        
        bool isFormatSupported = TextureFormatUtility.IsFormatSupported(targetFormat);
        bool isImporterSupported = TextureFormatUtility.IsImporterFormatSupported(targetFormat);
        
        EditorGUI.BeginDisabledGroup(!isFormatSupported);
        
        if (isImporterSupported)
        {
            if (GUILayout.Button("修改导入格式", GUILayout.Height(30)))
            {
                ApplyImporterFormatChanges();
            }
        }
        else
        {
            if (GUILayout.Button("创建运行时格式副本", GUILayout.Height(30)))
            {
                CreateRuntimeFormatCopies();
            }
        }
        
        EditorGUI.EndDisabledGroup();
        
        if (GUILayout.Button("重置为自动格式", GUILayout.Height(30)))
        {
            ResetToAutoFormat();
        }
        
        EditorGUILayout.EndHorizontal();
    }

    private void ApplyImporterFormatChanges()
    {
        if (selectedTextures.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请选择要修改的纹理", "确定");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "确认修改", 
            $"确定要将 {selectedTextures.Count} 个纹理的导入格式修改为 {targetFormat}？", 
            "确定", "取消");

        if (!proceed) return;

        int successCount = 0;
        int totalCount = selectedTextures.Count;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < totalCount; i++)
            {
                var texture = selectedTextures[i];
                if (texture == null) continue;

                EditorUtility.DisplayProgressBar("修改纹理格式", 
                    $"处理 {texture.name} ({i + 1}/{totalCount})", 
                    (float)i / totalCount);

                if (TextureFormatUtility.ChangeTextureImportFormat(texture, targetFormat))
                {
                    successCount++;
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

        EditorUtility.DisplayDialog("完成", 
            $"成功修改了 {successCount}/{totalCount} 个纹理的格式", "确定");
    }

    private void CreateRuntimeFormatCopies()
    {
        if (selectedTextures.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请选择要转换的纹理", "确定");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "确认创建", 
            $"确定要为 {selectedTextures.Count} 个纹理创建 {targetFormat} 格式的副本？\n\n新文件将保存在原文件旁边。", 
            "确定", "取消");

        if (!proceed) return;

        int successCount = 0;
        int totalCount = selectedTextures.Count;

        try
        {
            for (int i = 0; i < totalCount; i++)
            {
                var texture = selectedTextures[i];
                if (texture == null) continue;

                EditorUtility.DisplayProgressBar("创建运行时格式副本", 
                    $"处理 {texture.name} ({i + 1}/{totalCount})", 
                    (float)i / totalCount);

                string assetPath = AssetDatabase.GetAssetPath(texture);
                string directory = System.IO.Path.GetDirectoryName(assetPath);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                string newPath = System.IO.Path.Combine(directory, $"{fileName}_Runtime_{targetFormat}.png");

                if (TextureFormatUtility.CreateNewTextureAsset(texture, targetFormat, newPath) != null)
                {
                    successCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("完成", 
            $"成功创建了 {successCount}/{totalCount} 个运行时格式的纹理副本", "确定");
    }

    private void ResetToAutoFormat()
    {
        if (selectedTextures.Count == 0) return;

        bool proceed = EditorUtility.DisplayDialog(
            "确认重置", 
            $"确定要将 {selectedTextures.Count} 个纹理重置为自动格式？", 
            "确定", "取消");

        if (!proceed) return;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var texture in selectedTextures)
            {
                if (texture == null) continue;

                string assetPath = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                
                if (importer != null)
                {
                    var settings = importer.GetDefaultPlatformTextureSettings();
                    settings.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(settings);
                    AssetDatabase.ImportAsset(assetPath);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("完成", "已重置为自动格式", "确定");
    }

    private float CalculateTextureSize(Texture2D texture)
    {
        if (texture == null) return 0f;
        
        // 使用Unity的内建方法获取纹理大小
        long size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
        return size / (1024f * 1024f); // 转换为MB
    }

    private float EstimateSizeWithFormat(GraphicsFormat format)
    {
        float totalSize = 0f;
        
        foreach (var texture in selectedTextures)
        {
            if (texture == null) continue;
            
            int bytesPerPixel = GetBytesPerPixel(format);
            long pixelCount = (long)texture.width * texture.height;
            totalSize += (pixelCount * bytesPerPixel) / (1024f * 1024f);
        }
        
        return totalSize;
    }

    private int GetBytesPerPixel(GraphicsFormat format)
    {
        switch (format)
        {
            case GraphicsFormat.R8_UNorm:
                return 1;
            case GraphicsFormat.R8G8_UNorm:
            case GraphicsFormat.R16_UNorm:
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
            // 压缩格式的近似值
            case GraphicsFormat.RGBA_DXT1_UNorm:
            case GraphicsFormat.RGBA_DXT1_SRGB:
                return 1; // 约0.5字节每像素
            case GraphicsFormat.RGBA_DXT5_UNorm:
            case GraphicsFormat.RGBA_DXT5_SRGB:
            case GraphicsFormat.RGBA_BC7_UNorm:
            case GraphicsFormat.RGBA_BC7_SRGB:
                return 1; // 约1字节每像素
            case GraphicsFormat.RGBA_ASTC4X4_UNorm:
            case GraphicsFormat.RGBA_ASTC4X4_SRGB:
                return 1; // 约1字节每像素
            case GraphicsFormat.RGBA_ASTC6X6_UNorm:
                return 1; // 约0.89字节每像素
            case GraphicsFormat.RGBA_ASTC8X8_UNorm:
                return 1; // 约0.5字节每像素
            default:
                return 4;
        }
    }
}
