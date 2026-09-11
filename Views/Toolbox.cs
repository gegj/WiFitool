using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using WiFitool.Models;
using WiFitool.Services;

namespace WiFitool
{
    public partial class MainWindow
    {
        private readonly ToolboxService toolboxService = new ToolboxService();
        private readonly List<ToolboxItem> toolboxItems = new List<ToolboxItem>();
        private readonly Dictionary<string, ImageSource> toolboxIcons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Button> toolboxTabs = new List<Button>();
        private WrapPanel toolboxCards;
        private TextBox toolboxSearch;
        private TextBlock toolboxEmpty;
        private TextBlock toolboxError;
        private string toolboxCategory = "all";
        private bool toolboxLoaded;

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        private void InitializeToolbox()
        {
            toolboxItems.AddRange(new[] {
                new ToolboxItem { Name = "设置 ADB", Description = "通过设备 Web 接口开启或关闭调试模式", Type = "builtin", BuiltinId = "adb-settings", Icon = "⌁" },
                new ToolboxItem { Name = "ADB离线修复", Description = "自动通过 AT 端口恢复离线 ADB", Type = "builtin", BuiltinId = "adb-repair", Icon = "⌁" },
                new ToolboxItem { Name = "修复无限重启", Description = "在设备重启间隙覆盖或删除设备文件", Type = "builtin", BuiltinId = "infinite-reboot-repair", Icon = "⌁" }
            });
            ToolboxView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ToolboxView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = "工具箱", FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 17) });
            toolboxError = new TextBlock { Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(toolboxError);
            var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            toolboxSearch = new TextBox { Width = 220, Margin = new Thickness(0, 0, 10, 8), ToolTip = "搜索工具名称或描述", VerticalContentAlignment = VerticalAlignment.Center };
            toolboxSearch.TextChanged += delegate { RefreshToolbox(); };
            toolbar.Children.Add(new TextBlock { Text = "搜索", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8) });
            toolbar.Children.Add(toolboxSearch);
            header.Children.Add(toolbar);
            var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            var categories = new[] { "all", "builtin" };
            var labels = new[] { "全部", "内置工具" };
            for (var index = 0; index < categories.Length; index++)
            {
                var tab = new Button { Content = labels[index], Tag = categories[index], Style = (Style)FindResource("CompactButton") };
                tab.Click += delegate(object sender, RoutedEventArgs e) { toolboxCategory = (string)((Button)sender).Tag; RefreshToolbox(); };
                toolboxTabs.Add(tab);
                tabs.Children.Add(tab);
            }
            header.Children.Add(tabs);
            ToolboxView.Children.Add(header);
            var body = new Grid();
            Grid.SetRow(body, 1);
            toolboxCards = new WrapPanel();
            var scroll = new ScrollViewer { Content = toolboxCards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            scroll.SizeChanged += delegate {
                var width = Math.Max(1, scroll.ActualWidth - 20);
                toolboxCards.ItemWidth = width / Math.Max(1, Math.Min(3, (int)(width / 245)));
            };
            body.Children.Add(scroll);
            toolboxEmpty = new TextBlock { Text = "没有匹配的工具", Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            body.Children.Add(toolboxEmpty);
            ToolboxView.Children.Add(body);
        }

        private void ToolboxNav_Click(object sender, RoutedEventArgs e)
        {
            SetView(ToolboxView);
            if (!toolboxLoaded)
            {
                try { toolboxItems.AddRange(toolboxService.Load()); toolboxLoaded = true; toolboxError.Visibility = Visibility.Collapsed; }
                catch (Exception ex) { toolboxError.Text = "工具配置读取失败，已停止写入：" + ex.Message; toolboxError.Visibility = Visibility.Visible; }
            }
            RefreshToolbox();
        }

        private void RefreshToolbox()
        {
            if (toolboxCards == null) return;
            toolboxCards.Children.Clear();
            foreach (var tab in toolboxTabs)
                tab.Background = (Brush)FindResource((string)tab.Tag == toolboxCategory ? "AccentBrush" : "PanelBrush");
            var keyword = toolboxSearch.Text.Trim();
            foreach (var item in toolboxItems.Where(item => (toolboxCategory == "all" || item.Type == toolboxCategory) &&
                (item.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || (item.Description ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                var card = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(16), Margin = new Thickness(0, 0, 12, 12), MinHeight = 155 };
                var content = new StackPanel();
                var title = new DockPanel();
                if (item.Type != "builtin")
                {
                    var menu = new ContextMenu();
                    var edit = new MenuItem { Header = "编辑名称和介绍" };
                    edit.Click += delegate { EditToolboxExecutable(item); };
                    var delete = new MenuItem { Header = "删除" };
                    delete.Click += delegate { DeleteToolboxItem(item); };
                    menu.Items.Add(edit);
                    menu.Items.Add(delete);
                    var more = new Button { Content = "⋯", Width = 28, MinWidth = 28, Height = 28, Padding = new Thickness(0), Margin = new Thickness(4, 0, 0, 0), ToolTip = "编辑或删除工具", ContextMenu = menu };
                    more.Click += delegate { menu.PlacementTarget = more; menu.IsOpen = true; };
                    DockPanel.SetDock(more, Dock.Right);
                    title.Children.Add(more);
                }
                var icon = GetToolboxIcon(item);
                if (icon != null) title.Children.Add(new Image { Source = icon, Width = 32, Height = 32, Margin = new Thickness(0, 0, 10, 0) });
                else title.Children.Add(new TextBlock { Text = item.Icon ?? "▣", Width = 32, FontSize = 22, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 10, 0) });
                title.Children.Add(new TextBlock { Text = item.Name, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(title);
                content.Children.Add(new TextBlock { Text = item.Description, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 12) });
                var footer = new DockPanel();
                var open = new Button { Content = "打开 →", Style = (Style)FindResource("CompactButton"), ToolTip = item.ExecutablePath };
                open.Click += delegate { OpenToolboxItem(item); };
                DockPanel.SetDock(open, Dock.Right);
                footer.Children.Add(open);
                footer.Children.Add(new TextBlock { Text = item.Type == "builtin" ? "内置工具" : "本地快捷方式", FontSize = 11, Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(footer);
                card.Child = content;
                toolboxCards.Children.Add(card);
            }
            toolboxEmpty.Visibility = toolboxCards.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private ImageSource GetToolboxIcon(ToolboxItem item)
        {
            if (item.Type != "exe") return null;
            ImageSource source;
            if (toolboxIcons.TryGetValue(item.ExecutablePath, out source)) return source;
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(item.ExecutablePath))
                using (var bitmap = icon.ToBitmap())
                {
                    var handle = bitmap.GetHbitmap();
                    try { source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); }
                    finally { DeleteObject(handle); }
                }
            }
            catch { source = null; }
            toolboxIcons[item.ExecutablePath] = source;
            return source;
        }

        private static string GetToolboxExecutable(DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            return paths == null ? null : paths.FirstOrDefault(path => string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path));
        }

        private void AddToolboxExecutable(string path)
        {
            if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) { StatusText.Text = "仅支持 EXE 文件"; return; }
            path = Path.GetFullPath(path);
            if (toolboxItems.Any(item => string.Equals(item.ExecutablePath, path, StringComparison.OrdinalIgnoreCase))) { StatusText.Text = "该 EXE 已添加"; return; }
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name)) { StatusText.Text = "工具名称不能为空"; return; }
            SaveToolboxItem(new ToolboxItem { Name = name, Description = "本地 EXE 快捷方式", Type = "exe", ExecutablePath = path, Icon = "▣" });
        }

        private bool SaveToolboxItem(ToolboxItem item, ToolboxItem original = null)
        {
            if (!toolboxLoaded) { StatusText.Text = "配置未成功读取，无法保存工具"; return false; }
            var updated = toolboxItems.ToList();
            if (original == null) updated.Add(item);
            else updated[updated.IndexOf(original)] = item;
            try { toolboxService.Save(updated); }
            catch (Exception ex) { StatusText.Text = "工具保存失败：" + ex.Message; return false; }
            toolboxItems.Clear();
            toolboxItems.AddRange(updated);
            if (original == null) { toolboxCategory = item.Type; toolboxSearch.Clear(); }
            RefreshToolbox();
            StatusText.Text = (original == null ? "已添加工具：" : "已更新工具：") + item.Name;
            return true;
        }

        private void DeleteToolboxItem(ToolboxItem item)
        {
            if (!toolboxLoaded || item.Type == "builtin") return;
            if (MessageBox.Show(this, "确定删除工具“" + item.Name + "”？\n仅移除工具箱记录，不删除本地文件。", "删除工具", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { toolboxService.Save(toolboxItems.Where(existing => existing != item)); }
            catch (Exception ex) { StatusText.Text = "工具删除失败：" + ex.Message; return; }
            toolboxItems.Remove(item);
            if (item.Type == "exe") toolboxIcons.Remove(item.ExecutablePath);
            RefreshToolbox();
            StatusText.Text = "已删除工具：" + item.Name;
        }

        private void EditToolboxExecutable(ToolboxItem original)
        {
            var dialog = new Window { Owner = this, Title = "编辑工具", Width = 440, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Icon = Icon };
            var form = new StackPanel { Margin = new Thickness(20) };
            form.Children.Add(new TextBlock { Text = "名称", Margin = new Thickness(0, 0, 0, 6) });
            var name = new TextBox { Text = original.Name, Margin = new Thickness(0, 0, 0, 12) };
            form.Children.Add(name);
            form.Children.Add(new TextBlock { Text = "EXE 路径", Margin = new Thickness(0, 0, 0, 6) });
            form.Children.Add(new TextBox { Text = original.ExecutablePath, IsReadOnly = true, Margin = new Thickness(0, 0, 0, 12) });
            form.Children.Add(new TextBlock { Text = "介绍（可选）", Margin = new Thickness(0, 0, 0, 6) });
            var description = new TextBox { Text = original.Description ?? "", Margin = new Thickness(0, 0, 0, 12) };
            form.Children.Add(description);
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedBrush") };
            form.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "取消", IsCancel = true };
            var save = new Button { Content = "保存", IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
            save.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(name.Text)) { error.Text = "请输入工具名称"; return; }
                var item = new ToolboxItem { Name = name.Text.Trim(), Description = description.Text.Trim(), Type = "exe", ExecutablePath = original.ExecutablePath, Icon = original.Icon };
                if (SaveToolboxItem(item, original)) dialog.DialogResult = true;
                else error.Text = StatusText.Text;
            };
            buttons.Children.Add(cancel); buttons.Children.Add(save); form.Children.Add(buttons);
            dialog.Content = form;
            dialog.Loaded += delegate { name.Focus(); name.SelectAll(); };
            dialog.ShowDialog();
        }
        private void OpenToolboxItem(ToolboxItem item)
        {
            if (item.Type == "builtin")
            {
                switch (item.BuiltinId)
                {
                    case "overview": OverviewNav_Click(this, new RoutedEventArgs()); break;
                    case "files": FilesNav_Click(this, new RoutedEventArgs()); break;
                    case "domain": DomainScanNav_Click(this, new RoutedEventArgs()); break;
                    case "process": ProcessNav_Click(this, new RoutedEventArgs()); break;
                    case "terminal": AdbTerminalNav_Click(this, new RoutedEventArgs()); break;
                    case "adb-repair": OpenAdbRepair(); break;
                    case "adb-settings": OpenAdbSettings(); break;
                    case "infinite-reboot-repair": ShowInfiniteRebootRepairDialog(); break;
                }
                return;
            }
            if (item.Type == "exe" && !File.Exists(item.ExecutablePath)) { StatusText.Text = "工具文件不存在，快捷方式已保留：" + item.ExecutablePath; return; }
            try
            {
                var start = new ProcessStartInfo { FileName = item.ExecutablePath, UseShellExecute = true };
                if (item.Type == "exe") start.WorkingDirectory = Path.GetDirectoryName(item.ExecutablePath);
                using (Process.Start(start)) { }
                StatusText.Text = "已打开：" + item.Name;
            }
            catch (Exception ex) { StatusText.Text = "工具打开失败：" + ex.Message; }
        }

        private async void OpenAdbRepair()
        {
            if (activeCancellation != null) { StatusText.Text = "请等待当前操作完成"; return; }
            LogService.Instance.Info("ADBRepair", "打开 ADB 离线修复");
            await CheckAdbStatusAsync();
            if (adbStatus == null || adbStatus.DeviceState != "offline")
            {
                var message = adbStatus != null && adbStatus.DeviceState == "online" ? "当前 ADB 已在线，无需修复。" : "未检测到 ADB 离线设备，请先连接设备并确认右上角状态为“ADB 设备离线”。";
                StatusText.Text = message;
                LogService.Instance.Warn("ADBRepair", message);
                MessageBox.Show(this, message, "ADB离线修复", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowAdbRepairDialog();
        }

        private void OpenAdbSettings()
        {
            if (activeCancellation != null) { StatusText.Text = "请等待当前操作完成"; return; }
            ShowAdbSettingsDialog();
        }

        private async Task RunAdbSettingsActionAsync(TextBox ip, TextBlock state, Button enableButton, Button disableButton, Button closeButton, bool enableAdb)
        {
            if (activeCancellation != null) { SetAdbSettingsStatus(state, "请等待当前操作完成。"); return; }

            IPAddress address;
            var value = ip.Text.Trim();
            if (!IPAddress.TryParse(value, out address) || address.AddressFamily != AddressFamily.InterNetwork)
            {
                SetAdbSettingsStatus(state, "请输入有效的设备 IPv4 地址。");
                return;
            }

            enableButton.IsEnabled = false;
            disableButton.IsEnabled = false;
            closeButton.IsEnabled = false;
            ip.IsEnabled = false;
            try
            {
                LogService.Instance.Info("ADBSettings", (enableAdb ? "开启" : "关闭") + " ADB，设备地址 " + address);
                await RunBusyAsync(enableAdb ? "正在开启 ADB…" : "正在关闭 ADB…", async token =>
                {
                    var paths = enableAdb
                        ? new[] { "/reqproc/proc_post?goformId=SET_DEVICE_MODE&debug_enable=1", "/goform/goform_set_cmd_process?goformId=SET_DEVICE_MODE&debug_enable=1", "/reqproc/proc_post?isTest=false&goformId=tw_telnet_config&telnetd_enable=1&debug_enable=1", "/goform/goform_set_cmd_process?isTest=false&goformId=tw_telnet_config&telnetd_enable=1&debug_enable=1" }
                        : new[] { "/reqproc/proc_post?goformId=SET_DEVICE_MODE&debug_enable=0", "/goform/goform_set_cmd_process?goformId=SET_DEVICE_MODE&debug_enable=0" };
                    var successPath = (string)null;
                    foreach (var path in paths)
                    {
                        try
                        {
                            LogService.Instance.Debug("ADBSettings", "尝试设备接口：" + path);
                            if (await SendRawHttpGetAsync(address.ToString(), path, token, enableAdb ? "success" : "successfully"))
                            {
                                successPath = path;
                                break;
                            }
                        }
                        catch (Exception ex) { LogService.Instance.Debug("ADBSettings", "设备接口请求失败：" + path, ex); }
                    }
                    if (successPath == null)
                    {
                        throw new InvalidOperationException(enableAdb
                            ? "设备未接受开启 ADB 请求，请确认 IP 和网络连接。\n可尝试在浏览器打开设备管理页面后重试。"
                            : "设备未接受关闭 ADB 请求，请确认 IP 和网络连接。\n已尝试两种设备接口。");
                    }
                    LogService.Instance.Info("ADBSettings", "设备接口执行成功：" + successPath);

                    var rebootPath = successPath.StartsWith("/reqproc", StringComparison.OrdinalIgnoreCase)
                        ? "/reqproc/proc_post?isTest=false&goformId=REBOOT_DEVICE"
                        : "/goform/goform_set_cmd_process?isTest=false&goformId=REBOOT_DEVICE";
                    try { LogService.Instance.Debug("ADBSettings", "请求设备重启：" + rebootPath); await SendRawHttpGetAsync(address.ToString(), rebootPath, token); }
                    catch (Exception ex) { LogService.Instance.Warn("ADBSettings", "设备重启请求失败", ex); }
                    SetAdbSettingsStatus(state, (enableAdb ? "开启" : "关闭") + "请求已成功，设备正在重启…");
                    await Task.Delay(5000, token);
                }, message => state.Text = message, delegate(Exception ex) { state.Text = "操作失败：" + ex.Message; });
            }
            finally
            {
                enableButton.IsEnabled = true;
                disableButton.IsEnabled = true;
                closeButton.IsEnabled = true;
                ip.IsEnabled = true;
            }
        }

        private void ShowInfiniteRebootRepairDialog()
        {
            var window = new Window
            {
                Owner = this,
                Title = "修复无限重启",
                Width = 620,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false
            };
            var border = new Border { Background = (Brush)FindResource("PanelBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20) };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };
            var form = new Grid();
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = "修复无限重启", FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var close = new Button { Content = "×", Width = 30, Height = 30, Padding = new Thickness(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 18 };
            Grid.SetColumn(close, 1); header.Children.Add(close);
            Grid.SetRow(header, 0); form.Children.Add(header);

            var note = new TextBlock { Text = "设备反复重启时，持续等待 ADB 短暂上线并执行一次操作。操作成功后自动停止，窗口会保持显示。", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 12, Margin = new Thickness(0, 0, 0, 14) };
            Grid.SetRow(note, 1); form.Children.Add(note);

            var replacePath = new TextBox { IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "要覆盖的本地文件，路径不能包含中文字符" };
            var replaceTarget = new TextBox { IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "选择设备目录后自动使用本地文件名" };
            var chooseFile = new Button { Content = "选择文件", Width = 88, MinWidth = 88, Margin = new Thickness(8, 0, 0, 0) };
            var chooseDirectory = new Button { Content = "选择目录", Width = 88, MinWidth = 88, Margin = new Thickness(8, 0, 0, 0) };
            var replaceStart = new Button { Content = "开始覆盖", Style = (Style)FindResource("PrimaryButton"), Width = 88, MinWidth = 88, Margin = new Thickness(0, 12, 0, 0) };
            var replaceSection = CreateInfiniteRebootSection("覆盖文件", "选择本地文件和设备目录，自动拼接同名目标文件。目标文件需要已经存在。", replacePath, replaceTarget, chooseFile, chooseDirectory, replaceStart, "本地文件", "设备路径");
            Grid.SetRow(replaceSection, 2); form.Children.Add(replaceSection);

            var deleteTarget = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "输入设备上的文件绝对路径，例如 /system/example/file" };
            var deleteStart = new Button { Content = "开始删除", Style = (Style)FindResource("PrimaryButton"), Width = 88, MinWidth = 88, Margin = new Thickness(0, 12, 0, 0) };
            var deleteSection = CreateInfiniteRebootDeleteSection(deleteTarget, deleteStart);
            Grid.SetRow(deleteSection, 3); form.Children.Add(deleteSection);

            var state = new TextBlock { Text = "等待开始", Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), MinHeight = 34 };
            Grid.SetRow(state, 4); form.Children.Add(state);

            var stop = new Button { Content = "停止", Width = 78, MinWidth = 78, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
            var closeBottom = new Button { Content = "关闭", Width = 78, MinWidth = 78 };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(stop); buttons.Children.Add(closeBottom);
            Grid.SetRow(buttons, 5); form.Children.Add(buttons);
            border.Child = form; window.Content = border;

            var operationRunning = false;
            var replaceDirectory = "";
            Action updateReplaceTarget = delegate
            {
                if (string.IsNullOrWhiteSpace(replaceDirectory) || !File.Exists(replacePath.Text.Trim())) { replaceTarget.Clear(); return; }
                var fileName = Path.GetFileName(replacePath.Text.Trim());
                var combined = replaceDirectory == "/" ? "/" + fileName : replaceDirectory.TrimEnd('/') + "/" + fileName;
                try { replaceTarget.Text = adbService.NormalizeDevicePath(combined); }
                catch { replaceTarget.Clear(); }
            };
            Action closeWindow = delegate
            {
                if (operationRunning) { state.Text = "任务正在执行，请先点击“停止”。"; return; }
                window.Close();
            };
            close.Click += delegate { closeWindow(); };
            closeBottom.Click += delegate { closeWindow(); };
            window.Closing += delegate(object sender, CancelEventArgs e)
            {
                if (!operationRunning) return;
                e.Cancel = true;
                state.Text = "任务正在执行，请先点击“停止”。";
            };
            chooseFile.Click += delegate
            {
                if (operationRunning) { state.Text = "任务正在执行，请先点击“停止”。"; return; }
                var dialog = new OpenFileDialog { Title = "选择要覆盖的文件", Filter = "所有文件|*.*", CheckFileExists = true, Multiselect = false };
                if (dialog.ShowDialog(window) == true)
                {
                    replacePath.Text = dialog.FileName;
                    updateReplaceTarget();
                    if (ContainsChineseCharacters(dialog.FileName)) state.Text = "提示：本地文件路径不能包含中文字符，请将文件移到纯英文路径后再选择。";
                }
            };
            chooseDirectory.Click += async delegate
            {
                if (operationRunning) { state.Text = "任务正在执行，请先点击“停止”。"; return; }
                var serial = await FindInfiniteRebootPickerSerialAsync(state);
                if (string.IsNullOrWhiteSpace(serial)) return;
                var directory = ShowDevicePathPicker(window, serial, true);
                if (directory == null) return;
                replaceDirectory = directory;
                updateReplaceTarget();
                state.Text = "已选择设备目录：" + directory + (string.IsNullOrWhiteSpace(replaceTarget.Text) ? "，请先选择本地文件。" : "");
            };
            stop.Click += delegate
            {
                if (activeCancellation == null) return;
                state.Text = "正在停止当前任务…";
                activeCancellation.Cancel();
            };
            replaceStart.Click += async delegate
            {
                if (operationRunning) return;
                var local = replacePath.Text.Trim();
                if (!File.Exists(local)) { state.Text = "请选择存在的本地文件。"; return; }
                if (ContainsChineseCharacters(local)) { state.Text = "本地文件路径不能包含中文字符，请将文件移到纯英文路径后再操作。"; return; }
                string target;
                try { target = adbService.NormalizeDevicePath(replaceTarget.Text.Trim()); }
                catch (Exception ex) { state.Text = ex.Message; return; }
            if (target == "/") { state.Text = "设备路径必须是文件绝对路径。"; return; }
                if (activeCancellation != null) { state.Text = "请等待当前操作完成。"; return; }
                operationRunning = true;
                try { await RunInfiniteRebootToolOperationAsync(window, state, replaceStart, deleteStart, stop, close, closeBottom, "覆盖", delegate(string serial, CancellationToken token) { return adbService.ReplaceRemoteFileQuickAsync(serial, target, local, token); }); }
                finally { operationRunning = false; }
            };
            deleteStart.Click += async delegate
            {
                if (operationRunning) return;
                string target;
                try { target = adbService.NormalizeDevicePath(deleteTarget.Text.Trim()); }
                catch (Exception ex) { state.Text = ex.Message; return; }
                if (target == "/") { state.Text = "设备路径必须是文件绝对路径。"; return; }
                if (MessageBox.Show(window, "确认在设备重启间隙删除文件？\n" + target, "修复无限重启", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                if (activeCancellation != null) { state.Text = "请等待当前操作完成。"; return; }
                operationRunning = true;
                try { await RunInfiniteRebootToolOperationAsync(window, state, replaceStart, deleteStart, stop, close, closeBottom, "删除", delegate(string serial, CancellationToken token) { return adbService.DeleteRemoteFileQuickAsync(serial, target, token); }); }
                finally { operationRunning = false; }
            };
            window.ShowDialog();
        }

        private Border CreateInfiniteRebootSection(string title, string description, TextBox localPath, TextBox targetPath, Button chooseFile, Button chooseDirectory, Button start, string localLabel, string targetLabel)
        {
            var border = new Border { Background = (Brush)FindResource("PanelAltBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 10) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold };
            Grid.SetRow(heading, 0); grid.Children.Add(heading);
            var note = new TextBlock { Text = description, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) };
            Grid.SetRow(note, 1); grid.Children.Add(note);
            var localGrid = new Grid();
            localGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            localGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            localGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            localGrid.Children.Add(new TextBlock { Text = localLabel, Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(localPath, 1); localGrid.Children.Add(localPath);
            Grid.SetColumn(chooseFile, 2); localGrid.Children.Add(chooseFile);
            Grid.SetRow(localGrid, 2); grid.Children.Add(localGrid);
            var targetGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            targetGrid.Children.Add(new TextBlock { Text = targetLabel, Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(targetPath, 1); targetGrid.Children.Add(targetPath);
            Grid.SetColumn(chooseDirectory, 2); targetGrid.Children.Add(chooseDirectory);
            Grid.SetRow(targetGrid, 3); grid.Children.Add(targetGrid);
            var action = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            action.Children.Add(start);
            Grid.SetRow(action, 4); grid.Children.Add(action);
            border.Child = grid;
            return border;
        }

        private Border CreateInfiniteRebootDeleteSection(TextBox targetPath, Button start)
        {
            var border = new Border { Background = (Brush)FindResource("PanelAltBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 0) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new TextBlock { Text = "删除设备文件", FontSize = 14, FontWeight = FontWeights.SemiBold };
            Grid.SetRow(heading, 0); grid.Children.Add(heading);
            var note = new TextBlock { Text = "输入要删除的设备文件绝对路径。只删除普通文件，不删除目录。", Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) };
            Grid.SetRow(note, 1); grid.Children.Add(note);
            var targetGrid = new Grid();
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            targetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            targetGrid.Children.Add(new TextBlock { Text = "设备路径", Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(targetPath, 1); targetGrid.Children.Add(targetPath);
            Grid.SetRow(targetGrid, 2); grid.Children.Add(targetGrid);
            var action = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            action.Children.Add(start);
            Grid.SetRow(action, 3); grid.Children.Add(action);
            border.Child = grid;
            return border;
        }

        private async Task<string> FindInfiniteRebootPickerSerialAsync(TextBlock state)
        {
            try
            {
                var serial = await adbService.FindOnlineSerialAsync(CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(serial)) return serial;
                state.Text = "选择设备路径需要设备暂时在线，请在设备启动间隙再点击选择按钮。";
            }
            catch (Exception ex) { state.Text = "读取在线设备失败：" + ShortRebootError(ex); }
            return "";
        }

        private string ShowDevicePathPicker(Window owner, string serial, bool selectDirectory)
        {
            var window = new Window
            {
                Owner = owner,
                Title = selectDirectory ? "选择设备目录" : "选择设备文件",
                Width = 620,
                Height = 500,
                MinWidth = 520,
                MinHeight = 420,
                ResizeMode = ResizeMode.CanResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Icon = Icon
            };
            var border = new Border { Background = (Brush)FindResource("PanelBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20) };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };
            var form = new Grid();
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = selectDirectory ? "选择设备目录" : "选择设备文件", FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var close = new Button { Content = "×", Width = 30, Height = 30, Padding = new Thickness(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 18 };
            Grid.SetColumn(close, 1); header.Children.Add(close);
            Grid.SetRow(header, 0); form.Children.Add(header);

            var pathGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pathGrid.Children.Add(new TextBlock { Text = "当前路径", Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            var currentPathBox = new TextBox { IsReadOnly = true, Height = 34, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "当前设备目录" };
            Grid.SetColumn(currentPathBox, 1); pathGrid.Children.Add(currentPathBox);
            var up = new Button { Content = "↑ 上级目录", Style = (Style)FindResource("CompactButton"), Margin = new Thickness(8, 0, 0, 0), ToolTip = "返回上级目录" };
            Grid.SetColumn(up, 2); pathGrid.Children.Add(up);
            Grid.SetRow(pathGrid, 1); form.Children.Add(pathGrid);

            var list = new ListView { MinHeight = 260, Background = (Brush)FindResource("PanelAltBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), ToolTip = selectDirectory ? "双击目录进入" : "双击目录进入，选择文件后确认" };
            var itemTemplate = new DataTemplate();
            var itemPanel = new FrameworkElementFactory(typeof(StackPanel));
            itemPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var itemIcon = new FrameworkElementFactory(typeof(TextBlock));
            itemIcon.SetBinding(TextBlock.TextProperty, new Binding("Icon"));
            itemIcon.SetValue(FrameworkElement.WidthProperty, 28.0);
            itemIcon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var itemName = new FrameworkElementFactory(typeof(TextBlock));
            itemName.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            itemName.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            itemName.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            itemPanel.AppendChild(itemIcon); itemPanel.AppendChild(itemName); itemTemplate.VisualTree = itemPanel;
            list.ItemTemplate = itemTemplate;
            Grid.SetRow(list, 2); form.Children.Add(list);

            var state = new TextBlock { Text = "正在读取设备目录…", Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 10, 0, 0), MinHeight = 32 };
            Grid.SetRow(state, 3); form.Children.Add(state);

            var cancel = new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 10, 8, 0) };
            var choose = new Button { Content = selectDirectory ? "选择当前目录" : "选择文件", Style = (Style)FindResource("PrimaryButton"), IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 10, 0, 0) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(cancel); buttons.Children.Add(choose);
            Grid.SetRow(buttons, 4); form.Children.Add(buttons);
            border.Child = form; window.Content = border;

            var currentPath = "/";
            WorkspaceEntry selectedEntry = null;
            var loading = false;
            var loaded = false;
            var loadingCancellation = new CancellationTokenSource();
            Func<Task> loadDirectory = async delegate
            {
                if (loading) return;
                loading = true;
                loaded = false;
                selectedEntry = null;
                choose.IsEnabled = false;
                up.IsEnabled = false;
                state.Text = "正在读取设备目录：" + currentPath;
                try
                {
                    var entries = await adbService.ListDirectoryAsync(serial, currentPath, loadingCancellation.Token);
                    list.ItemsSource = entries;
                    currentPathBox.Text = currentPath;
                    loaded = true;
                    choose.IsEnabled = selectDirectory;
                    state.Text = entries.Count == 0 ? "当前目录为空。" : (selectDirectory ? "双击目录进入，或选择当前目录。" : "双击目录进入，选择文件后确认。");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    list.ItemsSource = null;
                    state.Text = "读取设备目录失败：" + ShortRebootError(ex);
                }
                finally
                {
                    loading = false;
                    up.IsEnabled = currentPath != "/";
                    if (loaded) choose.IsEnabled = selectDirectory || (selectedEntry != null && selectedEntry.Kind != "目录");
                }
            };
            list.SelectionChanged += delegate
            {
                selectedEntry = list.SelectedItem as WorkspaceEntry;
                if (loaded && !loading) choose.IsEnabled = selectDirectory || (selectedEntry != null && selectedEntry.Kind != "目录");
            };
            list.MouseDoubleClick += async delegate
            {
                if (loading) return;
                var entry = list.SelectedItem as WorkspaceEntry;
                if (entry == null) return;
                if (entry.Kind == "目录")
                {
                    currentPath = entry.Path;
                    await loadDirectory();
                    return;
                }
                if (!selectDirectory && entry.Kind != "目录") { window.Tag = entry.Path; window.DialogResult = true; }
            };
            up.Click += async delegate
            {
                if (loading || currentPath == "/") return;
                var path = currentPath.TrimEnd('/');
                var separator = path.LastIndexOf('/');
                currentPath = separator <= 0 ? "/" : path.Substring(0, separator);
                await loadDirectory();
            };
            choose.Click += delegate
            {
                if (!loaded) return;
                if (selectDirectory) { window.Tag = currentPath; window.DialogResult = true; return; }
                if (selectedEntry != null && selectedEntry.Kind != "目录") { window.Tag = selectedEntry.Path; window.DialogResult = true; }
            };
            cancel.Click += delegate { window.Close(); };
            close.Click += delegate { window.Close(); };
            window.Closed += delegate { loadingCancellation.Cancel(); loadingCancellation.Dispose(); };
            window.Loaded += async delegate { await loadDirectory(); };
            window.ShowDialog();
            return window.Tag as string;
        }

        private async Task RunInfiniteRebootToolOperationAsync(Window window, TextBlock state, Button replaceStart, Button deleteStart, Button stop, Button close, Button closeBottom, string actionName, Func<string, CancellationToken, Task> action)
        {
            replaceStart.IsEnabled = false;
            deleteStart.IsEnabled = false;
            stop.IsEnabled = true;
            close.IsEnabled = false;
            closeBottom.IsEnabled = false;
            try
            {
                await RunBusyAsync("正在等待设备上线…", token => RunInfiniteRebootWindowLoopAsync(actionName, action, token, message => state.Text = message), message => state.Text = message, delegate(Exception ex)
                {
                    state.Text = "操作失败：" + ex.Message;
                    MessageBox.Show(window, ex.Message, "修复无限重启", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                replaceStart.IsEnabled = true;
                deleteStart.IsEnabled = true;
                stop.IsEnabled = false;
                close.IsEnabled = true;
                closeBottom.IsEnabled = true;
            }
        }

        private async Task RunInfiniteRebootWindowLoopAsync(string actionName, Func<string, CancellationToken, Task> action, CancellationToken token, Action<string> report)
        {
            var onlineAttempts = 0;
            var waitCount = 0;
            var lastWaitReport = DateTime.MinValue;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string serial = null;
                try { serial = await adbService.FindOnlineSerialAsync(token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    if ((DateTime.UtcNow - lastWaitReport).TotalSeconds >= 1) { report("等待 ADB 设备上线：" + ShortRebootError(ex)); lastWaitReport = DateTime.UtcNow; }
                    await Task.Delay(300, token);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(serial))
                {
                    waitCount++;
                    if (waitCount % 4 == 1) report("持续等待 ADB 设备上线…");
                    await Task.Delay(300, token);
                    continue;
                }
                onlineAttempts++;
                report("检测到设备，正在尝试" + actionName + "（第 " + onlineAttempts + " 次）…");
                try
                {
                    await action(serial, token);
                    report(actionName + "成功，已停止等待。设备序列号：" + serial);
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    report("本次" + actionName + "未完成：" + ShortRebootError(ex) + "，继续等待下一次上线…");
                    await Task.Delay(300, token);
                }
            }
        }

        private static string ShortRebootError(Exception exception)
        {
            var message = exception == null ? "未知错误" : exception.Message;
            message = (message ?? "未知错误").Replace("\r", " ").Replace("\n", " ").Trim();
            return message.Length > 120 ? message.Substring(0, 120) + "…" : message;
        }

        private static bool ContainsChineseCharacters(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var character in value)
                if (character >= '\u4e00' && character <= '\u9fff') return true;
            return false;
        }

        private static async Task<bool> SendRawHttpGetAsync(string host, string path, CancellationToken token, string successMarker = "success")
        {
            LogService.Instance.Debug("HTTP", "请求设备接口 " + host + path);
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, 80);
                using (var stream = client.GetStream())
                using (var writer = new StreamWriter(stream, System.Text.Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true })
                {
                    await writer.WriteAsync("GET " + path + " HTTP/1.0\r\nHost: " + host + "\r\nConnection: close\r\n\r\n");
                    using (var reader = new StreamReader(stream, System.Text.Encoding.ASCII))
                    {
                        var response = await reader.ReadToEndAsync();
                        var success = response.IndexOf(successMarker, StringComparison.OrdinalIgnoreCase) >= 0;
                        LogService.Instance.Debug("HTTP", "设备接口响应结果：" + success + "，匹配标记 " + successMarker);
                        return success;
                    }
                }
            }
        }

        private void ShowAdbSettingsDialog()
        {
            var window = new Window { Owner = this, Title = "设置 ADB", Width = 470, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false };
            var border = new Border { Background = (Brush)FindResource("PanelBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20) };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };
            var form = new Grid();
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = "设置 ADB", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
            var close = new Button { Content = "×", Width = 30, Height = 30, Padding = new Thickness(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 18 };
            Grid.SetColumn(close, 1); header.Children.Add(close);
            Grid.SetRow(header, 0); form.Children.Add(header);
            var hint = new TextBlock { Text = "通过设备 Web 接口开启或关闭调试模式，设备可能会自动重启。操作完成后窗口仍会保持显示。", Foreground = (Brush)FindResource("MutedBrush"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(hint, 1); form.Children.Add(hint);
            var ip = new TextBox { Text = "192.168.0.1", MinWidth = 260, Height = 32, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "设备管理 IP 地址" };
            var fields = new Grid(); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fields.Children.Add(new TextBlock { Text = "设备 IP", VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 0, 14, 0) }); Grid.SetColumn(ip, 1); fields.Children.Add(ip);
            Grid.SetRow(fields, 2); form.Children.Add(fields);
            var state = new TextBlock { Text = "等待操作", Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, MinHeight = 34, Margin = new Thickness(0, 14, 0, 0) };
            Grid.SetRow(state, 3); form.Children.Add(state);
            var enable = new Button { Content = "开启 ADB", Style = (Style)FindResource("PrimaryButton"), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var disable = new Button { Content = "关闭 ADB", Margin = new Thickness(0, 0, 8, 0) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(enable); buttons.Children.Add(disable); Grid.SetRow(buttons, 4); form.Children.Add(buttons); border.Child = form; window.Content = border;
            var operationRunning = false;
            Action closeWindow = delegate
            {
                if (operationRunning) { SetAdbSettingsStatus(state, "任务正在执行，请等待完成。"); return; }
                window.Close();
            };
            close.Click += delegate { closeWindow(); };
            window.Closing += delegate(object sender, CancelEventArgs e)
            {
                if (!operationRunning) return;
                e.Cancel = true;
                SetAdbSettingsStatus(state, "任务正在执行，请等待完成。");
            };
            enable.Click += async delegate
            {
                if (operationRunning) return;
                operationRunning = true;
                try { await RunAdbSettingsActionAsync(ip, state, enable, disable, close, true); }
                finally { operationRunning = false; }
            };
            disable.Click += async delegate
            {
                if (operationRunning) return;
                operationRunning = true;
                try { await RunAdbSettingsActionAsync(ip, state, enable, disable, close, false); }
                finally { operationRunning = false; }
            };
            window.ShowDialog();
        }

        private void SetAdbSettingsStatus(TextBlock state, string message)
        {
            state.Text = message;
            StatusText.Text = message;
        }

        private void ShowAdbRepairDialog()
        {
            var window = new Window
            {
                Owner = this,
                Title = "ADB离线修复",
                Width = 470,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false
            };
            var border = new Border { Background = (Brush)FindResource("PanelBrush"), BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20) };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };
            var form = new Grid();
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = "ADB离线修复", FontSize = 17, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var close = new Button { Content = "×", Width = 30, Height = 30, Padding = new Thickness(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 18 };
            close.Click += delegate { window.Close(); };
            Grid.SetColumn(close, 1); header.Children.Add(close);
            form.Children.Add(header);
            var note = new TextBlock { Text = "自动识别 115200 AT 端口并启动临时文件服务。请先连接设备网络和 USB 数据线。修复前请关闭毛坯助手、TFTPd 等可能占用 AT 端口的工具。", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 12, Margin = new Thickness(0, 0, 0, 14) };
            Grid.SetRow(note, 1); form.Children.Add(note);
            var fields = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ip = new TextBox { Text = GetPreferredLocalIp(), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "与设备同网段的本机 IPv4 地址" };
            var ports = new ComboBox { MinWidth = 250, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "固定 115200 波特率" };
            foreach (var port in AtPortService.GetAvailablePorts()) ports.Items.Add(port);
            fields.Children.Add(new TextBlock { Text = "本机 IP", VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("MutedBrush") });
            Grid.SetColumn(ip, 1); fields.Children.Add(ip);
            var portLabel = new TextBlock { Text = "AT 端口", VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(portLabel, 1); fields.Children.Add(portLabel);
            ports.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(ports, 1); Grid.SetColumn(ports, 1); fields.Children.Add(ports);
            Grid.SetRow(fields, 2); form.Children.Add(fields);
            var state = new TextBlock { Text = "正在自动识别 AT 端口…", Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 14) };
            Grid.SetRow(state, 3); form.Children.Add(state);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var redetect = new Button { Content = "重新检测", Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
            var start = new Button { Content = "开始修复", Style = (Style)FindResource("PrimaryButton"), IsDefault = true, IsEnabled = false };
            buttons.Children.Add(redetect); buttons.Children.Add(cancel); buttons.Children.Add(start);
            Grid.SetRow(buttons, 4); form.Children.Add(buttons);
            border.Child = form; window.Content = border;
            CancellationTokenSource detection = null;
            Func<Task> detect = async delegate
            {
                if (detection != null) detection.Cancel();
                detection = new CancellationTokenSource();
                var current = detection;
                start.IsEnabled = false; redetect.IsEnabled = false; state.Text = "正在自动识别 AT 端口…";
                try
                {
                    var found = await AtPortService.DetectAsync(current.Token);
                    if (current.IsCancellationRequested || !window.IsVisible) return;
                    if (found.Count == 1)
                    {
                        ports.SelectedItem = ports.Items.OfType<AtPortInfo>().FirstOrDefault(port => string.Equals(port.PortName, found[0], StringComparison.OrdinalIgnoreCase));
                        state.Text = "已识别 AT 端口：" + found[0] + "（115200）。请选择连接设备网络的本机 IP。";
                        try
                        {
                            state.Text = "已识别 AT 端口，正在读取设备网段…";
                            var network = await AtPortService.DetectDeviceNetworkAsync(found[0], current.Token);
                            var matchedIp = AtPortService.FindLocalIpForNetwork(network);
                            if (!string.IsNullOrWhiteSpace(matchedIp)) { ip.Text = matchedIp; state.Text = "已识别 AT 端口：" + found[0] + "，已匹配设备网段：" + network; }
                            else if (!string.IsNullOrWhiteSpace(network)) state.Text = "已识别设备网段：" + network + "，未找到匹配的电脑网卡，请手工填写 IP。";
                            else state.Text = "已识别 AT 端口：" + found[0] + "。未读取到设备网段，请手工填写 IP。";
                        }
                        catch (Exception ex) { state.Text = "已识别 AT 端口：" + found[0] + "。设备网段读取失败：" + ex.Message; }
                    }
                    else if (found.Count > 1) { ports.SelectedItem = ports.Items.OfType<AtPortInfo>().FirstOrDefault(port => string.Equals(port.PortName, found[0], StringComparison.OrdinalIgnoreCase)); state.Text = "检测到多个 AT 端口，请确认后继续。"; }
                    else state.Text = "未识别到 AT 端口，可手工选择端口后继续。";
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { state.Text = "AT 端口检测失败：" + ex.Message; }
                finally { if (!current.IsCancellationRequested && window.IsVisible) { start.IsEnabled = true; redetect.IsEnabled = true; } }
            };
            redetect.Click += async delegate { await detect(); };
            start.Click += async delegate
            {
                IPAddress parsed;
                if (!IPAddress.TryParse(ip.Text.Trim(), out parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) { state.Text = "请输入有效的本机 IPv4 地址。"; return; }
                var selectedPort = ports.SelectedItem as AtPortInfo;
                if (selectedPort == null) { state.Text = "请选择 AT 端口。"; return; }
                await CheckAdbStatusAsync();
                if (adbStatus == null || adbStatus.DeviceState != "offline")
                {
                    state.Text = "设备状态已变化，当前不是离线状态，已取消修复。";
                    return;
                }
                var options = new AdbRepairOptions { LocalIp = parsed.ToString(), PortName = selectedPort.PortName };
                start.IsEnabled = false;
                redetect.IsEnabled = false;
                cancel.IsEnabled = false;
                close.IsEnabled = false;
                var failed = false;
                try
                {
                    await RunBusyAsync("正在修复离线 ADB…", token => RepairAdbAsync(options, token, message => state.Text = message), message => state.Text = message, delegate(Exception ex)
                    {
                        failed = true;
                        state.Text = "修复失败：" + ex.Message;
                    });
                    if (!failed && adbStatus != null && adbStatus.DeviceState == "online") state.Text = "修复完成：ADB 已在线。";
                }
                catch (Exception ex)
                {
                    failed = true;
                    state.Text = "修复失败：" + ex.Message;
                }
                finally
                {
                    close.IsEnabled = true;
                    cancel.IsEnabled = true;
                    redetect.IsEnabled = true;
                    cancel.Content = "关闭";
                    start.Content = failed ? "重试" : "修复完成";
                    start.IsEnabled = failed;
                }
            };
            cancel.Click += delegate { window.Close(); };
            window.Loaded += async delegate { await detect(); };
            window.Closed += delegate { if (detection != null) detection.Cancel(); };
            window.ShowDialog();
        }

        private async Task RepairAdbAsync(AdbRepairOptions options, CancellationToken token, Action<string> reportStatus = null)
        {
            var adbdPath = Path.Combine(ToolEnvironment.Root, "adbd", "adbd");
            if (!File.Exists(adbdPath)) throw new FileNotFoundException("缺少内置 adbd 文件，请先等待工具环境下载完成。", adbdPath);
            LogService.Instance.Info("ADBRepair", "开始修复，AT 端口 " + options.PortName + "，本机 IP " + options.LocalIp);
            SetAdbRepairStatus(reportStatus, "正在连接 AT 端口：" + options.PortName);
            using (var at = AtPortService.Open(options.PortName))
            {
                var test = at.Send("AT", 500);
                if (test.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("AT 端口无响应，请确认端口没有被其他程序占用。");
                LogService.Instance.Debug("ADBRepair", "AT 端口响应正常");
                at.Send("AT+SHELL=echo 1 >/sys/devices/virtual/android_usb/android0/adb_enable", 700);
                at.Send("AT+SHELL=echo 1 >/sys/devices/virtual/android_usb/android0/enable", 700);
                // 部分设备只需通过专用 AT 命令重启 adbd，无需传输文件。
                foreach (var compatibilityCommand in new[] { "AT+ZKILL=foo;adbd &", "AT+RKILL=foo;adbd &" })
                {
                    SetAdbRepairStatus(reportStatus, "正在尝试兼容修复方案…");
                    LogService.Instance.Info("ADBRepair", "尝试兼容命令：" + compatibilityCommand);
                    at.Send(compatibilityCommand, 1000);
                    await Task.Delay(1800, token);
                    await CheckAdbStatusAsync();
                    if (adbStatus != null && adbStatus.DeviceState == "online")
                    {
                        LogService.Instance.Info("ADBRepair", "兼容命令已使 ADB 在线");
                        SetAdbRepairStatus(reportStatus, "修复完成：ADB 已连接");
                        return;
                    }
                }
                // 轻量探测设备已有的 adbd，存在且可执行时直接启动，避免重复上传。
                SetAdbRepairStatus(reportStatus, "正在检查设备已有 adbd…");
                LogService.Instance.Info("ADBRepair", "尝试启动设备已有 /bin/adbd");
                at.Send("AT+SHELL=/bin/adbd &", 1000);
                await Task.Delay(1200, token);
                await CheckAdbStatusAsync();
                if (adbStatus != null && adbStatus.DeviceState == "online")
                {
                    LogService.Instance.Info("ADBRepair", "设备已有 /bin/adbd 已使 ADB 在线");
                    SetAdbRepairStatus(reportStatus, "修复完成：ADB 已连接");
                    return;
                }
                LogService.Instance.Info("ADBRepair", "尝试启动设备已有 /mnt/userdata/etc_rw/nv/adbd");
                at.Send("AT+SHELL=/mnt/userdata/etc_rw/nv/adbd &", 1000);
                await Task.Delay(1200, token);
                await CheckAdbStatusAsync();
                if (adbStatus != null && adbStatus.DeviceState == "online")
                {
                    LogService.Instance.Info("ADBRepair", "设备已有 nv/adbd 已使 ADB 在线");
                    SetAdbRepairStatus(reportStatus, "修复完成：ADB 已连接");
                    return;
                }
                var address = IPAddress.Parse(options.LocalIp);
                SetAdbRepairStatus(reportStatus, "正在启动临时文件服务…");
                using (var server = new AdbdTftpServer(address, adbdPath))
                {
                    try { server.Start(); }
                    catch (System.Net.Sockets.SocketException ex) { throw new InvalidOperationException("UDP 69 端口被占用或无法监听：" + ex.Message); }
                    SetAdbRepairStatus(reportStatus, "正在等待设备下载 adbd…");
                    LogService.Instance.Info("ADBRepair", "等待设备通过 TFTP 下载 adbd");
                    // 将内置 adbd 下载到可写的 nv 目录。
                    at.Send("AT+SHELL=rm -f /mnt/userdata/etc_rw/nv/adbd", 700);
                    var download = at.Send("AT+SHELL=tftp -l /mnt/userdata/etc_rw/nv/adbd -r adbd -g " + options.LocalIp, 1500);
                    var completed = await Task.WhenAny(server.Downloaded, Task.Delay(20000, token));
                    token.ThrowIfCancellationRequested();
                    if (completed != server.Downloaded)
                        throw new InvalidOperationException("设备未请求 adbd 文件，请确认本机 IP、UDP 69 端口和 TFTP 服务。设备返回：" + download.Trim());
                }
                SetAdbRepairStatus(reportStatus, "正在启动 adbd…");
                LogService.Instance.Info("ADBRepair", "TFTP 下载完成，启动设备端 adbd");
                var startCommand = "chmod 777 /mnt/userdata/etc_rw/nv/adbd; sync; killall adbd 2>/dev/null; /mnt/userdata/etc_rw/nv/adbd &";
                at.Send("AT+SHELL=" + startCommand, 1500);
            }
            SetAdbRepairStatus(reportStatus, "正在等待 ADB 设备上线…");
            // adbd 启动后 USB gadget 可能需要重新枚举；重启本工具自己的 adb server，避免复用旧 transport。
            try { await adbService.RestartAdbServerAsync(token); } catch { }
            for (var attempt = 0; attempt < 30; attempt++)
            {
                token.ThrowIfCancellationRequested();
                var status = await adbService.CheckStatusAsync(token);
                if (status.DeviceState == "online")
                {
                    LogService.Instance.Info("ADBRepair", "ADB 已在线，轮询次数 " + (attempt + 1));
                    await CheckAdbStatusAsync();
                    SetAdbRepairStatus(reportStatus, "修复完成：ADB 已连接");
                    return;
                }
                await Task.Delay(1000, token);
            }
            LogService.Instance.Warn("ADBRepair", "adbd 已启动但 30 次轮询后仍未在线");
            throw new InvalidOperationException("adbd 已启动，但设备仍未进入 ADB online 状态。请检查 USB 是否重新枚举，或确认设备端 adb_enable 已开启。" );
        }

        private void SetAdbRepairStatus(Action<string> reportStatus, string message)
        {
            StatusText.Text = message;
            if (reportStatus != null) reportStatus(message);
        }

        private static string GetPreferredLocalIp()
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(x => new { Properties = x.GetIPProperties(), Addresses = x.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address) && a.Address.ToString().IndexOf("169.254.", StringComparison.Ordinal) != 0).ToList() })
                .Where(x => x.Addresses.Count > 0)
                .OrderByDescending(x => x.Properties.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                .ToList();
            return candidates.Count == 0 ? "" : candidates[0].Addresses[0].Address.ToString();
        }
    }
}

