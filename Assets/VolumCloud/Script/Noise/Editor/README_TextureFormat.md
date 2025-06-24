# Unity纹理格式转换工具

这是一套完整的Unity编辑器工具，用于处理纹理格式转换，提供比TextureImporter更多的GraphicsFormat选项。

## 功能特性

### 1. 纹理格式修改器 (TextureFormatChanger)
- 图形化界面，易于使用
- 支持选择多个纹理进行批量转换
- 实时预览纹理信息和转换后的大小估算
- 支持拖拽操作
- 格式搜索和过滤功能

### 2. 纹理格式工具类 (TextureFormatUtility)
- 提供程序化API用于纹理格式转换
- 支持创建指定格式的新纹理
- 批量转换功能
- 格式支持检测
- 大小计算工具

### 3. 快速菜单项 (TextureFormatMenuItems)
- 常用格式的快速转换菜单
- 右键菜单集成
- 一键批量转换

### 4. 使用示例和工具 (TextureFormatExample)
- 创建测试纹理
- 显示支持的格式列表
- 智能压缩转换
- 大小对比分析

## 使用方法

### 方法一：使用图形界面
1. 打开 `工具 -> 纹理格式修改器`
2. 点击"添加选中的纹理"或拖拽纹理到窗口
3. 在格式列表中选择目标格式
4. 点击"应用格式更改"

### 方法二：使用快速菜单
1. 在Project窗口中选择纹理
2. 右键选择"修改纹理格式"
3. 或使用 `工具 -> 纹理格式工具` 下的快速转换选项

### 方法三：使用代码API
```csharp
// 修改单个纹理格式
var texture = ... // 你的纹理
TextureFormatUtility.ChangeTextureImportFormat(texture, GraphicsFormat.RGB_DXT1_UNorm);

// 批量修改
var textures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
TextureFormatUtility.BatchChangeTextureFormat(textures, GraphicsFormat.RGBA_DXT5_UNorm);

// 创建新纹理
var newTexture = TextureFormatUtility.CreateTextureWithFormat(512, 512, GraphicsFormat.R16G16B16A16_SFloat);
```

## 支持的格式

工具支持所有Unity GraphicsFormat枚举中的格式，包括但不限于：

### 未压缩格式
- `R8G8B8A8_UNorm` - 标准RGBA 32位
- `R8G8B8A8_SRGB` - sRGB RGBA 32位
- `R16G16B16A16_SFloat` - HDR半精度
- `R32G32B32A32_SFloat` - HDR全精度
- `R8_UNorm` - 单通道8位
- `R16_UNorm` - 单通道16位

### 压缩格式
- `RGB_DXT1_UNorm` - DXT1压缩（PC）
- `RGBA_DXT5_UNorm` - DXT5压缩（PC）
- `RGBA_BC7_UNorm` - BC7高质量压缩
- `RGB_BC6H_UFloat` - BC6H HDR压缩
- `RGBA_ASTC4X4_UNorm` - ASTC压缩（移动端）

## 工具窗口说明

### 纹理格式修改器窗口
- **选择纹理区域**: 添加、删除、拖拽纹理
- **当前信息区域**: 显示纹理尺寸、格式、内存占用
- **格式选择区域**: 搜索、过滤、选择目标格式
- **操作按钮**: 应用更改、重置为自动格式

### 纹理格式信息检查器
- 详细显示纹理的所有格式信息
- 对比估算大小和实际内存占用
- 显示导入器设置

## 注意事项

1. **格式兼容性**: 某些格式可能在特定平台上不受支持
2. **内存影响**: 转换格式会影响内存占用和加载时间
3. **质量权衡**: 压缩格式可以减小文件大小但可能降低质量
4. **sRGB设置**: 工具会自动根据格式名称设置sRGB选项

## 推荐的格式选择

### 颜色纹理（Albedo）
- PC: `RGBA_BC7_SRGB` 或 `RGBA_DXT5_SRGB`
- 移动端: `RGBA_ASTC4X4_SRGB`

### 法线贴图
- PC: `RGB_BC5_UNorm` 或 `RGBA_DXT5_UNorm`
- 移动端: `RGBA_ASTC4X4_UNorm`

### HDR纹理
- `R16G16B16A16_SFloat` 或 `RGB_BC6H_UFloat`

### 单通道数据
- `R8_UNorm` 或 `R16_UNorm`

### 高度图/置换贴图
- `R16_UNorm` 或 `R32_SFloat`

## 文件结构

```
Assets/VolumCloud/Script/Noise/Editor/
├── TextureFormatChanger.cs        # 主要的图形界面工具
├── TextureFormatUtility.cs        # 核心工具类和API
├── TextureFormatExample.cs        # 使用示例和快速工具
├── TextureFormatConverter.cs      # 备用转换工具（更高级）
└── README_TextureFormat.md        # 本文档
```

## 故障排除

### 问题：某些格式不可选择
**解决方案**: 启用"仅显示支持的格式"选项，或检查目标平台的格式支持情况。

### 问题：转换后效果不理想
**解决方案**: 尝试不同的压缩格式，或检查sRGB设置是否正确。

### 问题：批量转换失败
**解决方案**: 检查纹理是否可读，确保有足够的磁盘空间，逐个转换找出问题纹理。 