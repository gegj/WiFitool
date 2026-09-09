# 工具箱页面开发文档

## 目标

在现有 WPF 工作台中新增“工具箱”页面，用于集中启动三类工具：

1. 内置工具：由 WiFitool 自身实现或调用现有服务。
2. URL 外链工具：通过 URL 打开网页工具，默认使用系统浏览器。
3. 本地快捷方式：用户拖入 EXE 后创建快捷方式，名称不显示 `.exe` 后缀。

页面使用现有深色主题资源，不新增独立主题。

## 页面结构

在 `MainWindow.xaml` 的工作台导航中增加“工具箱”按钮，并在 `WorkspaceContentGrid` 中增加 `ToolboxView`。

页面从上到下包括：

- 标题“工具箱”和简短说明。
- “添加 URL 工具”按钮。
- 工具搜索框。
- EXE 拖入区域，同时支持点击选择文件。
- 分类标签：全部、内置工具、URL 外链工具、本地快捷方式。
- 工具卡片网格。

不包含“我的收藏”分类，也不实现收藏状态。

## 工具数据模型

建议新增 `Models/ToolboxItem.cs`：

```csharp
public class ToolboxItem
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; } // builtin、url、exe
    public string Url { get; set; }
    public string ExecutablePath { get; set; }
    public string Icon { get; set; }
}
```

字段规则：

- `builtin` 使用 `Name`、`Description` 和内置工具标识，不需要 URL。
- `url` 必须保存绝对 URL，并在打开前检查 `http://` 或 `https://` 协议。
- `exe` 保存 EXE 的完整路径；`Name` 使用文件名去除最后一个 `.exe` 后缀的结果。
- 名称为空或路径不存在时，不创建快捷方式。

## 持久化

用户添加的 URL 工具和 EXE 快捷方式保存到：

`%AppData%\\WiFitool\\tools.json`

启动时读取该文件，并与程序内置工具合并。保存时只写入用户工具，避免覆盖内置工具定义。文件不存在时按空数组处理，目录不存在时自动创建。

示例：

```json
[
  {
    "Name": "在线 JSON 格式化",
    "Description": "格式化和校验 JSON",
    "Type": "url",
    "Url": "https://example.com/json",
    "ExecutablePath": null,
    "Icon": "↗"
  },
  {
    "Name": "工具箱助手",
    "Description": "本地 EXE 快捷方式",
    "Type": "exe",
    "Url": null,
    "ExecutablePath": "C:\\Tools\\toolbox-helper.exe",
    "Icon": "▣"
  }
]
```

## EXE 拖入流程

1. `ToolboxView` 或拖入区域处理 `PreviewDragOver` 和 `Drop`。
2. 从 `DataFormats.FileDrop` 获取文件列表，只处理第一个 `.exe` 文件。
3. 使用 `Path.GetFileNameWithoutExtension` 获取显示名称。
4. 检查路径存在、扩展名为 `.exe` 且未重复添加。
5. 创建 `ToolboxItem`，类型设为 `exe`，写入配置并刷新卡片。
6. 点击卡片时使用 `ProcessStartInfo` 启动 EXE。

拖入非 EXE 文件时在底部状态栏提示“仅支持 EXE 文件”，不弹出复杂对话框。

## 打开行为

- 内置工具：调用现有服务或跳转到对应工作台功能。
- URL 外链工具：使用 `Process.Start` 配合 `UseShellExecute = true` 打开 URL。
- 本地快捷方式：启动 `ExecutablePath`，路径失效时提示用户并保留该记录，便于后续编辑或删除。

## 主题与布局

沿用 `App.xaml` 中的资源：

- 窗口背景：`WindowBrush`
- 卡片和输入区：`PanelBrush`
- 侧栏：`SidebarBrush`
- 边框：`BorderBrush`
- 主色：`AccentBrush`
- 次要文字：`MutedBrush`

卡片建议使用现有 `CardStyle`，圆角、间距和按钮样式与文件管理、项目概览页面保持一致。窗口宽度较小时，卡片从三列降为两列或一列。

## 实现顺序

1. 新增 `ToolboxItem` 模型和配置读写服务。
2. 在 XAML 增加导航按钮、页面布局和拖入区域。
3. 在 `MainWindow.xaml.cs` 增加视图切换、分类筛选和搜索逻辑。
4. 接入内置工具打开动作和 URL/EXE 启动动作。
5. 编译运行，验证配置读写、EXE 拖入、重复文件和失效路径提示。

## 暂不实现

- 我的收藏
- 工具云同步
- EXE 文件复制或打包
- 自动下载外部工具
- 复杂的插件系统
