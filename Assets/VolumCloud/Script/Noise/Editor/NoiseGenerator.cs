using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class NoiseGeneratorWindow : EditorWindow
{
    private string inputFolderPath = "Assets/";
    private string outputFolderPath = "Assets/";
    private string outputFileName = "Texture3D";
    private bool generateMipmaps = false;
    private TextureWrapMode wrapMode = TextureWrapMode.Clamp;
    private FilterMode filterMode = FilterMode.Bilinear;

    [MenuItem("Tools/Texture3D Generator")]
    public static void ShowWindow()
    {
        GetWindow<NoiseGeneratorWindow>("Texture3D Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Texture3D 生成器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 输入文件夹选择
        EditorGUILayout.LabelField("输入设置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("输入文件夹:", GUILayout.Width(100));
        inputFolderPath = EditorGUILayout.TextField(inputFolderPath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择包含Texture2D的文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 转换为相对于项目的路径
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    inputFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 输出设置
        EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("输出文件夹:", GUILayout.Width(100));
        outputFolderPath = EditorGUILayout.TextField(outputFolderPath);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择输出文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    outputFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("文件名:", GUILayout.Width(100));
        outputFileName = EditorGUILayout.TextField(outputFileName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 纹理设置（注意：格式将自动从输入纹理获取）
        EditorGUILayout.LabelField("纹理设置", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("纹理格式将自动从输入的第一张纹理获取", MessageType.Info);
        
        generateMipmaps = EditorGUILayout.Toggle("生成 Mipmaps:", generateMipmaps);
        wrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup("包装模式:", wrapMode);
        filterMode = (FilterMode)EditorGUILayout.EnumPopup("过滤模式:", filterMode);

        EditorGUILayout.Space();

        // 预览信息
        if (!string.IsNullOrEmpty(inputFolderPath) && Directory.Exists(inputFolderPath))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { inputFolderPath });
            EditorGUILayout.HelpBox($"找到 {guids.Length} 张 Texture2D", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 生成按钮
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("生成 Texture3D", GUILayout.Height(30)))
        {
            GenerateTexture3D();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("注意：确保所有输入纹理已启用 Read/Write 并且尺寸、格式一致", MessageType.Warning);
    }

    void GenerateTexture3D()
    {
        if (string.IsNullOrEmpty(inputFolderPath) || !Directory.Exists(inputFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "请选择有效的输入文件夹！", "确定");
            return;
        }

        if (string.IsNullOrEmpty(outputFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "请选择有效的输出文件夹！", "确定");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { inputFolderPath });

        var textures = guids
            .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(tex => tex != null)
            .ToArray();

        if (textures.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "在指定文件夹中未找到任何 Texture2D！", "确定");
            return;
        }

        int width = textures[0].width;
        int height = textures[0].height;
        int depth = textures.Length;
        TextureFormat sourceFormat = textures[0].format; // 获取第一张纹理的格式

        // 验证纹理
        for (int i = 0; i < textures.Length; i++)
        {
            var tex = textures[i];
            
            if (!tex.isReadable)
            {
                EditorUtility.DisplayDialog("错误", $"纹理 {tex.name} 未开启 Read/Write！\n请在 Inspector 中启用 Read/Write Enabled。", "确定");
                return;
            }

            if (tex.width != width || tex.height != height)
            {
                EditorUtility.DisplayDialog("错误", $"纹理 {tex.name} 尺寸不一致！\n所有纹理必须具有相同的尺寸。", "确定");
                return;
            }

            // 检查格式是否一致
            if (tex.format != sourceFormat)
            {
                EditorUtility.DisplayDialog("错误", $"纹理 {tex.name} 格式不一致！\n所有纹理必须具有相同的格式 ({sourceFormat})。", "确定");
                return;
            }

            // 显示进度
            EditorUtility.DisplayProgressBar("验证纹理", $"检查纹理 {i + 1}/{textures.Length}", (float)i / textures.Length);
        }

        EditorUtility.ClearProgressBar();

        try
        {
            // 使用源纹理的格式创建 Texture3D
            Texture3D texture3D = new Texture3D(width, height, depth, sourceFormat, generateMipmaps);
            texture3D.wrapMode = wrapMode;
            texture3D.filterMode = filterMode;

            // 获取单张纹理的原始数据大小进行验证
            var firstTextureData = textures[0].GetRawTextureData<byte>();
            int singleTextureDataSize = firstTextureData.Length;
            
            // 创建3D纹理的总数据大小
            int totalDataSize = singleTextureDataSize * depth;
            
            // 创建临时的原始数据数组
            using (var allTextureData = new Unity.Collections.NativeArray<byte>(totalDataSize, Unity.Collections.Allocator.Temp))
            {
                // 直接拷贝原始数据到临时数组，无任何格式转换
                for (int z = 0; z < depth; z++)
                {
                    EditorUtility.DisplayProgressBar("生成 Texture3D", $"拷贝层 {z + 1}/{depth}", (float)z / depth);
                    
                    // 获取当前层的原始数据
                    var layerData = textures[z].GetRawTextureData<byte>();
                    
                    // 验证数据大小一致
                    if (layerData.Length != singleTextureDataSize)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("错误", $"纹理 {textures[z].name} 的数据大小不一致！", "确定");
                        return;
                    }
                    
                    // 直接内存拷贝到对应层的位置
                    Unity.Collections.NativeArray<byte>.Copy(layerData, 0, allTextureData, z * singleTextureDataSize, singleTextureDataSize);
                }
                
                // 直接设置到3D纹理
                texture3D.SetPixelData(allTextureData, 0);
            }
            texture3D.Apply();

            // 确保输出文件夹存在
            if (!Directory.Exists(outputFolderPath))
            {
                Directory.CreateDirectory(outputFolderPath);
            }

            // 保存资源
            string savePath = Path.Combine(outputFolderPath, outputFileName + ".asset").Replace("\\", "/");
            AssetDatabase.CreateAsset(texture3D, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
            
            // 显示成功对话框
            if (EditorUtility.DisplayDialog("成功", 
                $"✅ Texture3D 已生成！\n" +
                $"尺寸: {width}x{height}x{depth}\n" +
                $"格式: {sourceFormat}\n" +
                $"数据大小: {totalDataSize / 1024f / 1024f:F2} MB\n" +
                $"路径: {savePath}\n\n" +
                $"是否选中生成的资源？", "选中", "关闭"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Texture3D>(savePath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }

            Debug.Log($"✅ Texture3D 已生成：{savePath} (格式: {sourceFormat}, 尺寸: {width}x{height}x{depth}, 数据大小: {totalDataSize / 1024f / 1024f:F2} MB)");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("错误", $"生成 Texture3D 时发生错误：\n{e.Message}", "确定");
            Debug.LogError($"生成 Texture3D 失败：{e}");
        }
    }
}

// 保留原有的菜单项以兼容旧代码
public class NoiseGenerator
{
    [MenuItem("Tools/Generate Texture3D From Folder (Legacy)")]
    static void GenerateTexture3D()
    {
        // 打开新的窗口界面
        NoiseGeneratorWindow.ShowWindow();
    }
}