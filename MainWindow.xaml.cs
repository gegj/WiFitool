using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WiFitool.Models;
using WiFitool.Services;

namespace WiFitool
{
    public partial class MainWindow : Window
    {
        private const double UpdateDialogWidth = 390;
        private readonly FirmwareAnalyzer analyzer = new FirmwareAnalyzer();
        private readonly WorkspaceService workspaceService = new WorkspaceService();
        private readonly WorkspaceFileService fileService = new WorkspaceFileService();
        private readonly ToolRunner toolRunner = new ToolRunner();
        private readonly FileSystemService fileSystemService;
        private readonly AdbService adbService;
        private readonly RootfsFeatureService rootfsFeatureService = new RootfsFeatureService();
        private readonly UpdateService updateService = new UpdateService();
        private readonly DispatcherTimer adbTimer;
        private readonly ObservableCollection<WorkspaceEntry> files = new ObservableCollection<WorkspaceEntry>();
        private readonly Dictionary<int, List<StartupSource>> startupSourcesByPid = new Dictionary<int, List<StartupSource>>();
        private ImageInfo image;
        private WorkspaceSession workspace;
        private PartitionInfo selectedPartition;
        private string selectedPartitionName;
        private string currentDirectory = "/";
        private string currentRoot;
        private bool adbMode;
        private string adbSerial;
        private AdbStatusInfo adbStatus = new AdbStatusInfo();
        private CancellationTokenSource activeCancellation;
        private bool adbChecking;
        private bool processRefreshing;
        private string processCacheSerial;
        private bool processCacheReady;
        private bool responsiveLayoutInitialized;
        private bool compactLayout;
        private bool compactSidebar;
        private bool compactContentMargin;
        private bool updateChecking;
        private bool toolEnvironmentChecking;
        private bool includeEmptyDirectories;
        private int activeTaskCount;

        public MainWindow()
        {
            InitializeComponent();
            Title = "WiFitool v" + typeof(MainWindow).Assembly.GetName().Version.ToString(3);
            fileSystemService = new FileSystemService(toolRunner);
            adbService = new AdbService(toolRunner);
            workspaceService.CleanupStaleWorkspaces();
            adbTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            adbTimer.Tick += async delegate { await CheckAdbStatusAsync(); };
            FileGrid.ItemsSource = files;
            SizeChanged += MainWindow_SizeChanged;
            Loaded += async delegate { ApplyResponsiveLayout(); SetView(OverviewView); await EnsureToolEnvironmentAsync(); adbTimer.Start(); await CheckAdbStatusAsync(); if (updateService.IsAutomaticCheckDue()) await CheckForUpdatesAsync(false); };
            Closing += delegate { adbTimer.Stop(); adbService.StopOwnedAdbServer(); if (workspace != null) workspaceService.Cleanup(workspace); if (activeCancellation != null) activeCancellation.Cancel(); };
            ProcessGrid.ContextMenu = CreateProcessMenu(false);
            CoreProcessGrid.ContextMenu = CreateProcessMenu(true);
            FileGrid.ContextMenu = CreateFileMenu();
            FileGrid.PreviewMouseRightButtonDown += FileGrid_PreviewMouseRightButtonDown;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            var useCompactLayout = ActualWidth < 1100;
            var useCompactSidebar = ActualWidth < 960;
            var useCompactMargin = useCompactLayout || ActualHeight < 700;

            if (!responsiveLayoutInitialized || compactLayout != useCompactLayout)
            {
                HeaderRow.Height = new GridLength(useCompactLayout ? 112 : 78);
                HeaderStatusRow.Height = useCompactLayout ? GridLength.Auto : new GridLength(0);
                Grid.SetRow(AdbStatusButton, useCompactLayout ? 1 : 0);
                Grid.SetColumn(AdbStatusButton, useCompactLayout ? 0 : 2);
                Grid.SetColumnSpan(AdbStatusButton, useCompactLayout ? 3 : 1);
                AdbStatusButton.HorizontalAlignment = useCompactLayout ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
                AdbStatusButton.Height = useCompactLayout ? 44 : 52;
                HeaderActionsPanel.Margin = useCompactLayout ? new Thickness(12, 0, 0, 0) : new Thickness(18, 0, 20, 0);
                HeaderTitleText.FontSize = useCompactLayout ? 18 : 20;
                HeaderSubtitleText.Visibility = useCompactLayout ? Visibility.Collapsed : Visibility.Visible;

                OverviewSummaryGrid.RowDefinitions[1].Height = useCompactLayout ? GridLength.Auto : new GridLength(0);
                OverviewSummaryGrid.ColumnDefinitions[2].Width = useCompactLayout ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
                Grid.SetRow(WorkflowCard, useCompactLayout ? 1 : 0);
                Grid.SetColumn(WorkflowCard, useCompactLayout ? 0 : 2);
                Grid.SetColumnSpan(WorkflowCard, useCompactLayout ? 2 : 1);
                ImageSummaryCard.Margin = useCompactLayout ? new Thickness(0, 0, 6, 10) : new Thickness(0, 0, 10, 0);
                SelectedPartitionCard.Margin = useCompactLayout ? new Thickness(6, 0, 0, 10) : new Thickness(5, 0, 5, 0);
                WorkflowCard.Margin = useCompactLayout ? new Thickness(0) : new Thickness(10, 0, 0, 0);

                FilesHeaderGrid.RowDefinitions[1].Height = useCompactLayout ? GridLength.Auto : new GridLength(0);
                Grid.SetRow(FilesActionsPanel, useCompactLayout ? 1 : 0);
                Grid.SetColumn(FilesActionsPanel, useCompactLayout ? 0 : 1);
                Grid.SetColumnSpan(FilesActionsPanel, useCompactLayout ? 2 : 1);
                FilesActionsPanel.HorizontalAlignment = useCompactLayout ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                FilesActionsPanel.Margin = useCompactLayout ? new Thickness(0, 12, 0, 0) : new Thickness(0);

                ProcessHeaderGrid.RowDefinitions[1].Height = useCompactLayout ? GridLength.Auto : new GridLength(0);
                Grid.SetRow(ProcessActionsPanel, useCompactLayout ? 1 : 0);
                Grid.SetColumn(ProcessActionsPanel, useCompactLayout ? 0 : 1);
                Grid.SetColumnSpan(ProcessActionsPanel, useCompactLayout ? 2 : 1);
                ProcessActionsPanel.HorizontalAlignment = useCompactLayout ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                ProcessActionsPanel.Margin = useCompactLayout ? new Thickness(0, 12, 0, 0) : new Thickness(0);

                FileToolbarGrid.RowDefinitions[1].Height = useCompactLayout ? GridLength.Auto : new GridLength(0);
                Grid.SetRow(BreadcrumbScrollViewer, useCompactLayout ? 1 : 0);
                Grid.SetColumn(BreadcrumbScrollViewer, useCompactLayout ? 0 : 1);
                Grid.SetColumnSpan(BreadcrumbScrollViewer, useCompactLayout ? 2 : 1);
                BreadcrumbScrollViewer.Margin = useCompactLayout ? new Thickness(0, 6, 0, 0) : new Thickness(10, 0, 0, 0);

                compactLayout = useCompactLayout;
            }

            if (!responsiveLayoutInitialized || compactSidebar != useCompactSidebar)
            {
                var sidebarWidth = new GridLength(useCompactSidebar ? 200 : 240);
                HeaderSidebarColumn.Width = sidebarWidth;
                WorkspaceSidebarColumn.Width = sidebarWidth;
                compactSidebar = useCompactSidebar;
            }

            if (!responsiveLayoutInitialized || compactContentMargin != useCompactMargin)
            {
                WorkspaceContentGrid.Margin = useCompactMargin ? new Thickness(16, 16, 16, 14) : new Thickness(26, 20, 26, 18);
                compactContentMargin = useCompactMargin;
            }

            responsiveLayoutInitialized = true;
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(true);
        }

        private async Task CheckForUpdatesAsync(bool userInitiated)
        {
            if (updateChecking) return;
            if (activeCancellation != null)
            {
                if (userInitiated) MessageBox.Show(this, "请等待当前操作完成后再检查更新。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information, UpdateDialogWidth);
                return;
            }

            updateChecking = true;
            CheckUpdateButton.IsEnabled = false;
            StatusText.Text = "正在检查更新…";
            BeginTaskProgress();
            try
            {
                var result = await updateService.CheckForUpdateAsync(typeof(MainWindow).Assembly.GetName().Version);
                if (string.IsNullOrEmpty(result.Error)) updateService.MarkCheckCompleted();
                if (!string.IsNullOrEmpty(result.Error))
                {
                    StatusText.Text = "更新检查失败";
                    if (userInitiated) MessageBox.Show(this, result.Error, "检查更新失败", MessageBoxButton.OK, MessageBoxImage.Error, UpdateDialogWidth);
                    return;
                }
                if (!result.HasUpdate)
                {
                    StatusText.Text = "当前已是最新版本";
                    if (userInitiated) MessageBox.Show(this, "当前已是最新版本。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information, UpdateDialogWidth);
                    return;
                }

                var message = "检测到新版本 v" + result.Update.Version + "\n当前版本 v" + typeof(MainWindow).Assembly.GetName().Version.ToString(3) + "\n\n是否立即下载并安装？";
                if (MessageBox.Show(this, message, "发现新版本", MessageBoxButton.YesNo, MessageBoxImage.Information, UpdateDialogWidth) != MessageBoxResult.Yes)
                {
                    StatusText.Text = "已发现新版本 v" + result.Update.Version;
                    return;
                }

                StatusText.Text = "正在下载更新…";
                var progress = new Progress<int>(value =>
                {
                    UpdateTaskProgress(value);
                    StatusText.Text = "正在下载更新… " + value + "%";
                });
                var downloadedPath = await updateService.DownloadUpdateAsync(result.Update, progress);
                updateService.ReplaceAfterExit(downloadedPath);
                StatusText.Text = "更新准备完成，正在重启…";
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                StatusText.Text = "更新失败";
                MessageBox.Show(this, ex.Message, "更新失败", MessageBoxButton.OK, MessageBoxImage.Error, UpdateDialogWidth);
            }
            finally
            {
                EndTaskProgress();
                updateChecking = false;
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async Task EnsureToolEnvironmentAsync()
        {
            if (toolEnvironmentChecking) return;
            toolEnvironmentChecking = true;
            try
            {
                if (ToolEnvironment.IsReady())
                {
                    EnvironmentText.Text = "工具环境：正常";
                    return;
                }
                BeginTaskProgress();
                EnvironmentText.Text = "工具环境：正在下载";
                StatusText.Text = "正在下载工具环境…";
                try
                {
                    var progress = new Progress<int>(value => UpdateTaskProgress(value));
                    await ToolEnvironment.EnsureReadyAsync(progress);
                    EnvironmentText.Text = "工具环境：正常";
                    StatusText.Text = "工具环境准备完成";
                }
                catch
                {
                    EnvironmentText.Text = "工具环境：下载失败";
                    StatusText.Text = "工具环境下载失败，请手动解压 tools 到 " + ToolEnvironment.Root;
                }
                finally { EndTaskProgress(); }
            }
            finally { toolEnvironmentChecking = false; }
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "打开固件", Filter = "固件文件|*.bin;*.mtd;*.mtd4" };
            if (dialog.ShowDialog() != true) return;
            await OpenFirmwareAsync(dialog.FileName);
        }

        private async Task OpenFirmwareAsync(string fileName)
        {
            if (activeCancellation != null) { StatusText.Text = "请等待当前操作完成"; return; }
            await RunBusyAsync("正在分析固件…", async token =>
            {
                var result = await Task.Run(() => analyzer.Analyze(fileName), token);
                var session = await workspaceService.CreateAsync(result, token);
                var rootfs = result.Partitions.FirstOrDefault(x => x.Name.Equals("rootfs", StringComparison.OrdinalIgnoreCase));
                var rootfsError = "";
                if (rootfs != null && rootfs.CanExtract)
                {
                    try { await fileSystemService.ExtractAsync(rootfs, session, token, delegate(string line, bool error) { Dispatcher.Invoke(delegate { StatusText.Text = (error ? "错误：" : "") + line; }); }); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { rootfsError = ex.Message; }
                }
                Dispatcher.Invoke(delegate
                {
                    if (workspace != null) workspaceService.Cleanup(workspace);
                    image = result; workspace = session; PartitionGrid.ItemsSource = result.Partitions; ImageSummary.Text = result.Name + "  |  " + FormatSize(result.Size) + "  |  分区 " + result.Partitions.Count + " 个"; CloseButton.IsEnabled = true; ExportButton.IsEnabled = true;
                    if (rootfs != null && rootfs.Extracted) { PartitionGrid.SelectedItem = rootfs; ActivateLocalPartition(rootfs); SetView(FilesView); StatusText.Text = "已自动解包 rootfs"; }
                    else { if (rootfs != null) PartitionGrid.SelectedItem = rootfs; else if (result.Partitions.Count > 0) PartitionGrid.SelectedIndex = 0; StatusText.Text = rootfs == null ? "未找到 rootfs 分区，选择分区开始解包" : rootfs.CanExtract ? "rootfs 自动解包失败：" + rootfsError : "rootfs 分区不支持自动解包"; }
                });
            });
        }

        private void Window_PreviewDragEnter(object sender, DragEventArgs e) { UpdateFirmwareDropState(e); }
        private void Window_PreviewDragOver(object sender, DragEventArgs e) { UpdateFirmwareDropState(e); }
        private void Window_PreviewDragLeave(object sender, DragEventArgs e) { FirmwareDropOverlay.Visibility = Visibility.Collapsed; }

        private async void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            FirmwareDropOverlay.Visibility = Visibility.Collapsed;
            string fileName;
            if (TryGetDroppedFirmware(e, out fileName))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                await OpenFirmwareAsync(fileName);
                return;
            }
            if (IsFileGridDropTarget(e.OriginalSource)) return;
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void UpdateFirmwareDropState(DragEventArgs e)
        {
            string fileName;
            var isFirmware = TryGetDroppedFirmware(e, out fileName);
            var canUpload = !isFirmware && IsFileGridDropTarget(e.OriginalSource) && CanUploadToCurrentSource() && e.Data.GetDataPresent(DataFormats.FileDrop);
            FirmwareDropOverlay.Visibility = isFirmware || canUpload ? Visibility.Visible : Visibility.Collapsed;
            if (isFirmware)
            {
                FirmwareDropText.Text = "松开以打开固件";
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
            if (canUpload)
            {
                FirmwareDropText.Text = "松开以上传";
                e.Effects = DragDropEffects.Copy;
                return;
            }
            if (IsFileGridDropTarget(e.OriginalSource)) return;
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private static bool TryGetDroppedFirmware(DragEventArgs e, out string fileName)
        {
            fileName = null;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1 || !File.Exists(files[0])) return false;
            var extension = Path.GetExtension(files[0]);
            if (!string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".mtd", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".mtd4", StringComparison.OrdinalIgnoreCase)) return false;
            fileName = files[0];
            return true;
        }

        private bool IsFileGridDropTarget(object source)
        {
            var element = source as DependencyObject;
            while (element != null)
            {
                if (ReferenceEquals(element, FileGrid)) return true;
                element = element is Visual || element is Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
            }
            return false;
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e) { await ExtractSelectedAsync(); }
        private async void PartitionGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) { await ExtractSelectedAsync(); }

        private async Task ExtractSelectedAsync()
        {
            if (selectedPartition == null || !selectedPartition.CanExtract || workspace == null) return;
            await RunBusyAsync("正在解包 " + selectedPartition.Name + "…", async token =>
            {
                await fileSystemService.ExtractAsync(selectedPartition, workspace, token, delegate(string line, bool error) { Dispatcher.Invoke(delegate { StatusText.Text = (error ? "错误：" : "") + line; }); });
                Dispatcher.Invoke(delegate { ActivateLocalPartition(selectedPartition); SetView(FilesView); StatusText.Text = "解包完成：" + selectedPartition.Name; });
            });
        }

        private void PartitionGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedPartition = PartitionGrid.SelectedItem as PartitionInfo;
            if (selectedPartition == null) { SelectedPartitionText.Text = "未选择分区"; ExtractButton.IsEnabled = false; return; }
            selectedPartitionName = selectedPartition.Name; SelectedPartitionText.Text = selectedPartition.Name + "  " + selectedPartition.FileSystem + "  " + FormatSize(selectedPartition.Size); ExtractButton.IsEnabled = selectedPartition.CanExtract && !selectedPartition.Extracted;
        }

        private void LoadFiles()
        {
            files.Clear(); if (string.IsNullOrEmpty(currentRoot)) { ShowBreadcrumbMessage("未选择已解包分区"); UpdateFileSourceButtons(); return; }
            try { foreach (var entry in fileService.ListDirectory(currentRoot, currentDirectory)) files.Add(entry); UpdateBreadcrumb(); StatusText.Text = selectedPartitionName + " " + currentDirectory; UpdateFileSourceButtons(); } catch (Exception ex) { StatusText.Text = "读取目录失败：" + ex.Message; }
        }

        private async void FileGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var entry = FileGrid.SelectedItem as WorkspaceEntry; if (entry == null) return;
            if (entry.Kind == "目录") { currentDirectory = entry.Path; if (adbMode) await LoadAdbFilesAsyncTask(); else LoadFiles(); return; }
            if (adbMode) { await EditAdbFileAsync(entry); return; }
            try { var data = fileService.ReadText(currentRoot, entry.Path); var editor = new TextEditorWindow(entry.Name, data); if (editor.ShowDialog() == true) { await RunTaskProgressAsync(() => fileService.SaveTextAsync(currentRoot, entry.Path, data, editor.EditorText)); var p = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName); if (p != null) p.Modified = true; LoadFiles(); StatusText.Text = "已保存：" + entry.Path; } }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法编辑", MessageBoxButton.OK, MessageBoxImage.Information); }
        }

        private void ActivateLocalPartition(PartitionInfo partition)
        {
            if (workspace == null || partition == null || !workspace.ExtractedDirectories.ContainsKey(partition.Name)) return;
            adbMode = false;
            selectedPartition = partition;
            selectedPartitionName = partition.Name;
            currentRoot = workspace.ExtractedDirectories[partition.Name];
            currentDirectory = "/";
            HostsButton.IsEnabled = true;
            ExportFilesButton.IsEnabled = true;
            AdbdButton.IsEnabled = true;
            AtWebButton.IsEnabled = true;
            UpdateFileSourceButtons();
            LoadFiles();
        }

        private void LocalSourceButton_Click(object sender, RoutedEventArgs e)
        {
            if (workspace == null || image == null) return;
            var partition = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName && x.Extracted && workspace.ExtractedDirectories.ContainsKey(x.Name))
                ?? image.Partitions.FirstOrDefault(x => x.Extracted && workspace.ExtractedDirectories.ContainsKey(x.Name));
            if (partition == null) { ShowBreadcrumbMessage("未选择已解包分区"); StatusText.Text = "请先解包一个分区"; UpdateFileSourceButtons(); return; }
            ActivateLocalPartition(partition);
        }

        private async void AdbSourceButton_Click(object sender, RoutedEventArgs e)
        {
            await ActivateAdbSourceAsync();
        }

        private async Task ActivateAdbSourceAsync(bool showProgress = true)
        {
            if (adbStatus.DeviceState != "online") return;
            adbMode = true; currentRoot = null; currentDirectory = "/";
            HostsButton.IsEnabled = true; ExportFilesButton.IsEnabled = false; AdbdButton.IsEnabled = false; AtWebButton.IsEnabled = false; UpdateFileSourceButtons();
            await LoadAdbFilesAsyncTask(showProgress);
        }

        private async void NavigateUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentDirectory == "/") return;
            var path = currentDirectory.TrimEnd('/');
            var separator = path.LastIndexOf('/');
            currentDirectory = separator <= 0 ? "/" : path.Substring(0, separator);
            UpdateFileSourceButtons();
            if (adbMode) await LoadAdbFilesAsyncTask(); else LoadFiles();
        }

        private void UpdateFileSourceButtons()
        {
            var localAvailable = workspace != null && image != null && image.Partitions.Any(x => x.Extracted && workspace.ExtractedDirectories.ContainsKey(x.Name));
            var localExportAvailable = !adbMode && localAvailable;
            LocalSourceButton.IsEnabled = localAvailable;
            AdbSourceButton.IsEnabled = adbStatus.DeviceState == "online";
            ExportFilesButton.Visibility = localExportAvailable ? Visibility.Visible : Visibility.Collapsed;
            AdbdButton.Visibility = localExportAvailable ? Visibility.Visible : Visibility.Collapsed;
            AtWebButton.Visibility = localExportAvailable ? Visibility.Visible : Visibility.Collapsed;
            ExportButton.Visibility = localExportAvailable ? Visibility.Visible : Visibility.Collapsed;
            ExportButton.IsEnabled = localExportAvailable;
            LocalSourceButton.Background = !adbMode && localAvailable ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("PanelAltBrush");
            AdbSourceButton.Background = adbMode && adbStatus.DeviceState == "online" ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("PanelAltBrush");
            LocalSourceButton.Foreground = !adbMode && localAvailable ? Brushes.White : (Brush)FindResource("TextBrush");
            AdbSourceButton.Foreground = adbMode && adbStatus.DeviceState == "online" ? Brushes.White : (Brush)FindResource("TextBrush");
            NavigateUpButton.IsEnabled = currentDirectory != "/";
        }

        private void ShowBreadcrumbMessage(string message)
        {
            BreadcrumbPanel.Children.Clear();
            var text = new TextBlock { Text = message, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            text.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            BreadcrumbPanel.Children.Add(text);
        }

        private void UpdateBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();
            var rootLabel = adbMode ? "根目录" : string.IsNullOrEmpty(selectedPartitionName) ? "根目录" : selectedPartitionName;
            AddBreadcrumbSegment(rootLabel, "/", currentDirectory != "/");
            var segments = currentDirectory.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var path = "";
            for (var i = 0; i < segments.Length; i++)
            {
                var separator = new TextBlock { Text = "/", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
                separator.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                BreadcrumbPanel.Children.Add(separator);
                path += "/" + segments[i];
                AddBreadcrumbSegment(segments[i], path, i < segments.Length - 1);
            }
        }

        private void AddBreadcrumbSegment(string label, string path, bool canNavigate)
        {
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 180 };
            if (canNavigate)
            {
                text.Cursor = Cursors.Hand;
                text.FontWeight = FontWeights.SemiBold;
                text.Tag = path;
                text.ToolTip = "打开 " + path;
                text.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                text.MouseLeftButtonUp += BreadcrumbSegment_MouseLeftButtonUp;
            }
            else text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            BreadcrumbPanel.Children.Add(text);
        }

        private async void BreadcrumbSegment_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var text = sender as TextBlock;
            var path = text == null ? null : text.Tag as string;
            if (string.IsNullOrEmpty(path) || path == currentDirectory) return;
            currentDirectory = path;
            if (adbMode) await LoadAdbFilesAsyncTask(); else LoadFiles();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { if (workspace != null) workspaceService.Cleanup(workspace); workspace = null; image = null; selectedPartition = null; selectedPartitionName = null; currentRoot = null; adbMode = false; PartitionGrid.ItemsSource = null; ProcessGrid.ItemsSource = null; CoreProcessGrid.ItemsSource = null; ClearProcessCache(); files.Clear(); ImageSummary.Text = "未打开固件"; ShowBreadcrumbMessage("未选择来源"); CloseButton.IsEnabled = false; ExportButton.IsEnabled = false; HostsButton.IsEnabled = false; ExportFilesButton.IsEnabled = false; AdbdButton.IsEnabled = false; AtWebButton.IsEnabled = false; UpdateFileSourceButtons(); SetView(OverviewView); StatusText.Text = "项目已关闭"; }
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (image == null || workspace == null) return;
            string folder; if (!FolderDialog.TrySelect(this, "选择固件导出文件夹", out folder)) return;
            var fileName = Path.GetFileNameWithoutExtension(image.Name);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "firmware";
            var extension = Path.GetExtension(image.Name);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var outputPath = Path.Combine(folder, fileName + "-" + timestamp + extension);
            if (File.Exists(outputPath) && MessageBox.Show(this, "目标文件已存在，是否覆盖？\n" + outputPath, "确认导出", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await RunBusyAsync("正在导出固件…", async token => { foreach (var p in image.Partitions.Where(x => x.Extracted && x.Modified)) await fileSystemService.RepackAsync(p, workspace, token); await workspaceService.ExportAsync(image, workspace, outputPath, token); if (!image.IsStandalone && image.Size <= 8 * 1024 * 1024 && image.Partitions.Any(x => x.Name.Equals("rootfs", StringComparison.OrdinalIgnoreCase))) { var rootfs = image.Partitions.First(x => x.Name.Equals("rootfs", StringComparison.OrdinalIgnoreCase)); await workspaceService.ExportPartitionAsync(outputPath, rootfs, Path.Combine(folder, fileName + "-" + timestamp + "-mtd4-rootfs.bin"), token); } Dispatcher.Invoke(delegate { StatusText.Text = "固件导出完成"; }); });
        }

        private void OverviewNav_Click(object sender, RoutedEventArgs e) { SetView(OverviewView); }
        private async void FilesNav_Click(object sender, RoutedEventArgs e)
        {
            SetView(FilesView);
            if (adbMode) await LoadAdbFilesAsyncTask();
            else if (currentRoot != null) LoadFiles();
            else if (adbStatus.DeviceState == "online") await ActivateAdbSourceAsync();
            else { ShowBreadcrumbMessage("未连接 ADB 设备"); StatusText.Text = "请先连接在线 ADB 设备"; UpdateFileSourceButtons(); }
        }
        private async void ProcessNav_Click(object sender, RoutedEventArgs e)
        {
            SetView(ProcessView);
            if (adbStatus.DeviceState != "online")
            {
                StatusText.Text = "请先连接在线 ADB 设备";
                return;
            }
            try { await RefreshProcessesAsync(); StatusText.Text = "已读取进程和启动来源"; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "读取进程失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void SetView(UIElement visible)
        {
            OverviewView.Visibility = visible == OverviewView ? Visibility.Visible : Visibility.Collapsed;
            FilesView.Visibility = visible == FilesView ? Visibility.Visible : Visibility.Collapsed;
            ProcessView.Visibility = visible == ProcessView ? Visibility.Visible : Visibility.Collapsed;
            OverviewNav.Background = visible == OverviewView ? (Brush)FindResource("SidebarSelectedBrush") : Brushes.Transparent;
            FilesNav.Background = visible == FilesView ? (Brush)FindResource("SidebarSelectedBrush") : Brushes.Transparent;
            ProcessNav.Background = visible == ProcessView ? (Brush)FindResource("SidebarSelectedBrush") : Brushes.Transparent;
        }
        private void AdbStatusButton_Click(object sender, RoutedEventArgs e)
        {
            AdbDetailsPopup.IsOpen = !AdbDetailsPopup.IsOpen;
        }

        private async void AdbRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckAdbStatusAsync(true);
            AdbDetailsPopup.IsOpen = true;
        }

        private async void HostsButton_Click(object sender, RoutedEventArgs e) { await OpenHostsAsync(); }
        private async void AdbExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (adbStatus.DeviceState != "online") { MessageBox.Show(this, "请先连接在线 ADB 设备。", "一键提取固件", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            string folder; if (!FolderDialog.TrySelect(this, "选择镜像导出文件夹", out folder)) return;
            try { await RunTaskProgressAsync(() => adbService.ExportMtdImageAsync(adbSerial, adbStatus.SoftwareVersion, folder, CancellationToken.None)); StatusText.Text = "ADB 整机镜像导出完成"; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "ADB 导出失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void ExportFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (adbMode || string.IsNullOrEmpty(currentRoot)) { MessageBox.Show(this, "请先在项目概览中解包并选择分区。", "导出系统文件", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            IncludeEmptyDirectoriesCheckBox.IsChecked = includeEmptyDirectories;
            ExportFilesButton.ContextMenu.PlacementTarget = ExportFilesButton;
            ExportFilesButton.ContextMenu.IsOpen = true;
        }

        private async void ExportFilesMenuExport_Click(object sender, RoutedEventArgs e)
        {
            if (adbMode || string.IsNullOrEmpty(currentRoot)) { MessageBox.Show(this, "请先在项目概览中解包并选择分区。", "导出系统文件", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            string folder; if (!FolderDialog.TrySelect(this, "选择导出目录", out folder)) return; var target = CreateExportFolder(folder, Path.GetFileNameWithoutExtension(image.Name)); try { await RunTaskProgressAsync(async () => { Directory.CreateDirectory(target); await fileService.ExportAllAsync(currentRoot, target, includeEmptyDirectories); }); StatusText.Text = "系统文件导出完成"; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void IncludeEmptyDirectoriesCheckBox_Checked(object sender, RoutedEventArgs e) { includeEmptyDirectories = true; }
        private void IncludeEmptyDirectoriesCheckBox_Unchecked(object sender, RoutedEventArgs e) { includeEmptyDirectories = false; }
        private async void AdbdButton_Click(object sender, RoutedEventArgs e) { if (adbMode || selectedPartition == null || workspace == null) return; try { await RunTaskProgressAsync(() => rootfsFeatureService.ApplyAdbdAsync(selectedPartition, workspace)); StatusText.Text = "adbd 已固化"; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "固化 adbd 失败", MessageBoxButton.OK, MessageBoxImage.Error); } }
        private async void AtWebButton_Click(object sender, RoutedEventArgs e) { if (adbMode || selectedPartition == null || workspace == null) return; try { await RunTaskProgressAsync(() => rootfsFeatureService.ApplyAtWebAsync(selectedPartition, workspace)); StatusText.Text = "ATWeb 已添加"; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "添加 ATWeb 失败", MessageBoxButton.OK, MessageBoxImage.Error); } }
        private async void RefreshProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (adbStatus.DeviceState != "online") return;
            try { await RefreshProcessesAsync(); StatusText.Text = "已刷新进程和启动来源"; } catch (Exception ex) { MessageBox.Show(this, ex.Message, "读取进程失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private ContextMenu CreateProcessMenu(bool core)
        {
            var menu = new ContextMenu(); var stop = new MenuItem { Header = "停止进程" }; var locate = new MenuItem { Header = "定位并编辑启动来源" };
            stop.Click += async delegate
            {
                var grid = core ? CoreProcessGrid : ProcessGrid; var process = grid.SelectedItem as ProcessInfo; if (process == null) return;
                if (process.Pid <= 1) { MessageBox.Show(this, "PID 1 受保护，不能停止。", "安全限制", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (MessageBox.Show(this, "确定停止进程 " + process.Pid + "（" + process.Name + "）？", "确认停止", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { await RunTaskProgressAsync(async () => { await adbService.StopProcessAsync(adbSerial, process.Pid, CancellationToken.None); await RefreshProcessesAsync(); }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "停止失败", MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            locate.Click += async delegate
            {
                var grid = core ? CoreProcessGrid : ProcessGrid; var process = grid.SelectedItem as ProcessInfo; if (process == null) return;
                try
                {
                    if (!processCacheReady || !string.Equals(processCacheSerial, adbSerial, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(this, "启动来源缓存已失效，请先点击“刷新进程”。", "启动来源", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    List<StartupSource> result;
                    if (!startupSourcesByPid.TryGetValue(process.Pid, out result) || result.Count == 0)
                    {
                        MessageBox.Show(this, "未找到启动来源文件。", "启动来源", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    var sources = result.GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
                    var source = sources.Count == 1 ? sources[0] : SelectStartupSource(sources);
                    if (source != null) await EditAdbPathAsync(source.FilePath, source.LineNumber);
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "定位失败", MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            menu.Items.Add(stop); menu.Items.Add(locate); return menu;
        }

        private StartupSource SelectStartupSource(System.Collections.Generic.List<StartupSource> sources)
        {
            var window = new Window
            {
                Title = "选择启动来源文件",
                Width = 720,
                Height = 420,
                MinWidth = 560,
                MinHeight = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false
            };
            var border = new Border
            {
                Background = (Brush)FindResource("PanelBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20)
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Grid { Cursor = Cursors.SizeAll, Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock
            {
                Text = window.Title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextBrush")
            });
            var close = new Button
            {
                Content = "×",
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 18,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            close.Click += delegate { window.Close(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left) window.DragMove();
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = "找到多个启动来源文件，请选择要编辑的文件：",
                Foreground = (Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 0, 0, 10)
            });
            var list = new ListBox
            {
                ItemsSource = sources.Select(x => x.FilePath + "    第 " + x.LineNumber + " 行").ToList(),
                MinHeight = 250
            };
            list.SelectedIndex = 0;
            content.Children.Add(list);
            Grid.SetRow(content, 1);
            root.Children.Add(content);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var cancel = new Button
            {
                Content = "取消",
                IsCancel = true,
                MinWidth = 82,
                Background = (Brush)FindResource("PanelAltBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var open = new Button
            {
                Content = "打开编辑器",
                IsDefault = true,
                MinWidth = 104,
                Background = (Brush)FindResource("AccentBrush"),
                Foreground = Brushes.White,
                BorderBrush = (Brush)FindResource("AccentBrush")
            };
            open.Click += delegate { if (list.SelectedIndex >= 0) window.DialogResult = true; };
            cancel.Click += delegate { window.Close(); };
            buttons.Children.Add(cancel);
            buttons.Children.Add(open);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);
            window.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    window.Close();
                    e.Handled = true;
                }
            };
            list.MouseDoubleClick += delegate { if (list.SelectedIndex >= 0) window.DialogResult = true; };
            border.Child = root;
            window.Content = border;
            return window.ShowDialog() == true && list.SelectedIndex >= 0 ? sources[list.SelectedIndex] : null;
        }

        private async Task RefreshProcessesAsync()
        {
            if (processRefreshing) return;
            if (adbStatus.DeviceState != "online" || string.IsNullOrWhiteSpace(adbSerial)) return;
            processRefreshing = true;
            var serial = adbSerial;
            ClearProcessCache();
            BeginTaskProgress();
            try
            {
                var scan = await adbService.ListProcessesWithStartupAsync(serial, CancellationToken.None);
                if (adbStatus.DeviceState != "online" || !string.Equals(serial, adbSerial, StringComparison.OrdinalIgnoreCase)) return;
                foreach (var pair in scan.StartupSources) startupSourcesByPid[pair.Key] = pair.Value;
                processCacheSerial = serial;
                processCacheReady = true;
                ProcessGrid.ItemsSource = scan.Processes.Where(x => !x.IsCoreProcess).ToList();
                CoreProcessGrid.ItemsSource = scan.Processes.Where(x => x.IsCoreProcess).ToList();
            }
            finally { EndTaskProgress(); processRefreshing = false; }
        }

        private void ClearProcessCache()
        {
            startupSourcesByPid.Clear();
            processCacheSerial = null;
            processCacheReady = false;
        }

        private void FileGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject; while (element != null && !(element is DataGridRow)) element = VisualTreeHelper.GetParent(element); var row = element as DataGridRow; if (row != null) { row.IsSelected = true; row.Focus(); }
        }

        private ContextMenu CreateFileMenu()
        {
            var menu = new ContextMenu(); var edit = new MenuItem { Header = "编辑" }; var permissions = new MenuItem { Header = "编辑权限" }; var rename = new MenuItem { Header = "重命名" }; var uploadNew = new MenuItem { Header = "上传文件" }; var uploadFolder = new MenuItem { Header = "上传文件夹" }; var newDirectory = new MenuItem { Header = "新建目录" }; var download = new MenuItem { Header = "下载" }; var delete = new MenuItem { Header = "删除" }; var refresh = new MenuItem { Header = "刷新" };
            edit.Click += async delegate { var entry = FileGrid.SelectedItem as WorkspaceEntry; if (entry != null && entry.Kind != "目录" && entry.Kind != "符号链接") { if (adbMode) await EditAdbFileAsync(entry); else FileGrid_MouseDoubleClick(null, null); } };
            permissions.Click += async delegate { var entry = FileGrid.SelectedItem as WorkspaceEntry; if (entry == null || entry.Kind == "符号链接") return; var dialog = new PermissionWindow(entry); dialog.Owner = this; if (adbMode) { if (dialog.ShowDialog() == true) { try { await RunTaskProgressAsync(async () => { await adbService.SetModeAsync(adbSerial, entry.Path, dialog.Mode, CancellationToken.None); await LoadAdbFilesAsyncTask(); }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "权限", MessageBoxButton.OK, MessageBoxImage.Error); } } } else if (dialog.ShowDialog() == true) { try { await RunTaskProgressAsync(() => Task.Run(() => fileService.SetPermissions(currentRoot, entry.Path, dialog.Mode, dialog.OwnerValue))); var p = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName); if (p != null) p.Modified = true; LoadFiles(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "权限", MessageBoxButton.OK, MessageBoxImage.Error); } } };
            rename.Click += async delegate
            {
                var entry = FileGrid.SelectedItem as WorkspaceEntry;
                if (entry == null || entry.Path == "/") return;
                var name = Prompt("重命名", "新名称：");
                if (string.IsNullOrWhiteSpace(name)) return;
                try
                {
                    if (adbMode)
                    {
                        await RunTaskProgressAsync(async () => { await adbService.RenameAsync(adbSerial, entry.Path, name, CancellationToken.None); await LoadAdbFilesAsyncTask(); });
                    }
                    else
                    {
                        await RunTaskProgressAsync(() => fileService.RenameAsync(currentRoot, entry.Path, name));
                        var p = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName);
                        if (p != null) p.Modified = true;
                        LoadFiles();
                    }
                    StatusText.Text = "已重命名：" + name;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "重命名失败", MessageBoxButton.OK, MessageBoxImage.Error); }
            };
            uploadNew.Click += async delegate { var directory = GetUploadDirectory(); var dialog = new OpenFileDialog { Title = "选择要上传的文件" }; if (dialog.ShowDialog() == true) await UploadSourcesAsync(new[] { dialog.FileName }, directory); };
            uploadFolder.Click += async delegate { var directory = GetUploadDirectory(); string folder; if (FolderDialog.TrySelect(this, "选择要上传的文件夹", out folder)) await UploadSourcesAsync(new[] { folder }, directory); };
            newDirectory.Click += async delegate { var entry = FileGrid.SelectedItem as WorkspaceEntry; var directory = entry == null ? currentDirectory : entry.Kind == "目录" ? entry.Path : currentDirectory; var name = Prompt("新建目录", "目录名称："); if (string.IsNullOrWhiteSpace(name)) return; try { if (adbMode) { if (MessageBox.Show(this, "确认在设备目录创建：" + directory + "/" + name + "？", "确认创建", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; await RunTaskProgressAsync(async () => { await adbService.CreateDirectoryAsync(adbSerial, directory, name, CancellationToken.None); await LoadAdbFilesAsyncTask(); }); } else { await RunTaskProgressAsync(() => fileService.CreateDirectoryAsync(currentRoot, directory, name)); var p = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName); if (p != null) p.Modified = true; LoadFiles(); } } catch (Exception ex) { MessageBox.Show(this, ex.Message, "创建目录失败", MessageBoxButton.OK, MessageBoxImage.Error); } };
            download.Click += async delegate { await DownloadEntryAsync(FileGrid.SelectedItem as WorkspaceEntry); };
            delete.Click += async delegate { var entry = FileGrid.SelectedItem as WorkspaceEntry; if (entry == null) return; if (MessageBox.Show(this, "确认删除：" + entry.Path + "？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { if (adbMode) { await RunTaskProgressAsync(async () => { await adbService.DeleteRemoteAsync(adbSerial, entry.Path, entry.Kind == "目录", CancellationToken.None); await LoadAdbFilesAsyncTask(); }); } else { await RunTaskProgressAsync(() => fileService.DeleteAsync(currentRoot, entry.Path)); var p = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName); if (p != null) p.Modified = true; LoadFiles(); } } catch (Exception ex) { MessageBox.Show(this, ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Error); } };
            refresh.Click += async delegate { if (adbMode) await LoadAdbFilesAsyncTask(); else LoadFiles(); };
            menu.Opened += delegate { var entry = FileGrid.SelectedItem as WorkspaceEntry; edit.Visibility = entry != null && entry.Kind != "目录" && entry.Kind != "符号链接" ? Visibility.Visible : Visibility.Collapsed; };
            menu.Items.Add(refresh); menu.Items.Add(edit); menu.Items.Add(permissions); menu.Items.Add(rename); menu.Items.Add(uploadNew); menu.Items.Add(uploadFolder); menu.Items.Add(newDirectory); menu.Items.Add(download); menu.Items.Add(delete); return menu;
        }

        private void FileGrid_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = CanUploadToCurrentSource() && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void FileGrid_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (!CanUploadToCurrentSource()) return;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;
            await UploadSourcesAsync(paths, currentDirectory);
        }

        private string GetUploadDirectory()
        {
            var entry = FileGrid.SelectedItem as WorkspaceEntry;
            return entry != null && entry.Kind == "目录" ? entry.Path : currentDirectory;
        }

        private bool CanUploadToCurrentSource()
        {
            return adbMode ? adbStatus.DeviceState == "online" && !string.IsNullOrWhiteSpace(adbSerial) : !string.IsNullOrWhiteSpace(currentRoot);
        }

        private async Task UploadSourcesAsync(IEnumerable<string> sourcePaths, string targetDirectory)
        {
            if (!CanUploadToCurrentSource()) { MessageBox.Show(this, "请先选择本地固件分区或连接在线 ADB 设备。", "上传", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var sources = sourcePaths.Where(x => File.Exists(x) || Directory.Exists(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (sources.Count == 0) return;
            var targetIsAdb = adbMode;
            var targetSerial = adbSerial;
            var targetRoot = currentRoot;
            await RunBusyAsync("正在上传…", async token =>
            {
                var changed = false;
                foreach (var sourcePath in sources)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (Directory.Exists(sourcePath)) changed = await UploadSourceDirectoryAsync(sourcePath, targetDirectory, targetIsAdb, targetSerial, targetRoot, token, false) || changed;
                        else changed = await UploadSourceFileAsync(sourcePath, targetDirectory, Path.GetFileName(sourcePath), false, targetIsAdb, targetSerial, targetRoot, token) || changed;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("上传 " + Path.GetFileName(sourcePath) + " 失败：" + ex.Message, ex);
                    }
                }
                await RefreshAfterUploadAsync(targetIsAdb, targetSerial, targetRoot, changed);
                StatusText.Text = changed ? "上传完成" : "已跳过上传";
            });
        }

        private async Task UploadNamedFileAsync(string sourcePath, string targetDirectory, string targetName, bool forceOverwrite)
        {
            if (!CanUploadToCurrentSource()) { MessageBox.Show(this, "请先选择本地固件分区或连接在线 ADB 设备。", "上传", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var targetIsAdb = adbMode;
            var targetSerial = adbSerial;
            var targetRoot = currentRoot;
            await RunBusyAsync("正在上传…", async token =>
            {
                var changed = await UploadSourceFileAsync(sourcePath, targetDirectory, targetName, forceOverwrite, targetIsAdb, targetSerial, targetRoot, token);
                await RefreshAfterUploadAsync(targetIsAdb, targetSerial, targetRoot, changed);
                StatusText.Text = changed ? "上传完成" : "已跳过上传";
            });
        }

        private async Task DownloadEntryAsync(WorkspaceEntry entry)
        {
            if (entry == null) return;
            if (!adbMode && entry.Kind == "符号链接") { MessageBox.Show(this, "工作区符号链接仅保存在元数据中，不能作为普通文件下载。", "下载提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (entry.Kind != "目录")
            {
                var dialog = new SaveFileDialog { Title = "保存文件", FileName = entry.Name };
                if (dialog.ShowDialog() != true) return;
                try
                {
                    await RunTaskProgressAsync(async () =>
                    {
                        if (adbMode) await adbService.DownloadFileAsync(adbSerial, entry.Path, dialog.FileName, CancellationToken.None);
                        else await Task.Run(() => File.Copy(fileService.Resolve(currentRoot, entry.Path, true), dialog.FileName, true));
                    });
                    StatusText.Text = "已下载：" + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "下载失败", MessageBoxButton.OK, MessageBoxImage.Error); }
                return;
            }

            string parentFolder;
            if (!FolderDialog.TrySelect(this, "选择文件夹保存位置", out parentFolder)) return;
            var destination = Path.Combine(parentFolder, entry.Name);
            if (File.Exists(destination)) { MessageBox.Show(this, "目标路径已存在同名文件：" + destination, "下载文件夹", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (Directory.Exists(destination) && MessageBox.Show(this, "目标已存在同名文件夹：" + destination + "\n将合并下载并覆盖同名文件，是否继续？", "下载文件夹", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var targetIsAdb = adbMode;
            var targetSerial = adbSerial;
            var targetRoot = currentRoot;
            await RunBusyAsync("正在下载文件夹…", async token =>
            {
                if (targetIsAdb) await adbService.DownloadDirectoryAsync(targetSerial, entry.Path, destination, token);
                else await fileService.DownloadDirectoryAsync(targetRoot, entry.Path, destination);
                StatusText.Text = "文件夹下载完成：" + destination;
            });
        }

        private async Task<bool> UploadSourceDirectoryAsync(string sourcePath, string targetDirectory, bool targetIsAdb, string targetSerial, string targetRoot, CancellationToken token, bool overwriteExisting)
        {
            var source = new DirectoryInfo(sourcePath);
            if ((source.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("不支持上传链接文件夹。");
            var targetPath = CombineVirtualPath(targetDirectory, source.Name);
            var exists = targetIsAdb ? await adbService.RemoteDirectoryExistsAsync(targetSerial, targetPath, token) : Directory.Exists(fileService.Resolve(targetRoot, targetPath, false));
            if (exists)
            {
                if (!overwriteExisting && MessageBox.Show(this, "目标已有同名文件夹：" + targetPath + "\n将合并上传并覆盖所有同名文件，不会删除或清空原有内容。是否继续？", "合并上传", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
            }
            else
            {
                var fileExists = targetIsAdb ? await adbService.RemoteFileExistsAsync(targetSerial, targetPath, token) : File.Exists(fileService.Resolve(targetRoot, targetPath, false));
                if (fileExists) throw new InvalidOperationException("目标路径已存在同名文件：" + targetPath);
                if (targetIsAdb) await adbService.CreateDirectoryAsync(targetSerial, targetDirectory, source.Name, token); else await fileService.CreateDirectoryAsync(targetRoot, targetDirectory, source.Name);
            }
            var changed = !exists;
            foreach (var directory in source.EnumerateDirectories()) changed = await UploadSourceDirectoryAsync(directory.FullName, targetPath, targetIsAdb, targetSerial, targetRoot, token, true) || changed;
            foreach (var file in source.EnumerateFiles())
            {
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                changed = await UploadSourceFileAsync(file.FullName, targetPath, file.Name, true, targetIsAdb, targetSerial, targetRoot, token) || changed;
            }
            return changed;
        }

        private async Task<bool> UploadSourceFileAsync(string sourcePath, string targetDirectory, string targetName, bool forceOverwrite, bool targetIsAdb, string targetSerial, string targetRoot, CancellationToken token)
        {
            var targetPath = CombineVirtualPath(targetDirectory, targetName);
            var existing = targetIsAdb ? await adbService.RemoteFileExistsAsync(targetSerial, targetPath, token) : File.Exists(fileService.Resolve(targetRoot, targetPath, false));
            var folderExists = targetIsAdb ? await adbService.RemoteDirectoryExistsAsync(targetSerial, targetPath, token) : Directory.Exists(fileService.Resolve(targetRoot, targetPath, false));
            if (folderExists) throw new InvalidOperationException("目标路径是文件夹：" + targetPath);
            if (existing && !forceOverwrite && MessageBox.Show(this, "目标已有文件：" + targetPath + "\n是否覆盖？", "确认覆盖", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
            StatusText.Text = "正在上传：" + targetName;
            if (targetIsAdb)
            {
                var length = new FileInfo(sourcePath).Length;
                var free = await adbService.GetFreeBytesAsync(targetSerial, targetDirectory, token);
                var direct = free < length + 1024L * 1024L;
                if (direct && MessageBox.Show(this, "设备可用空间为 " + FormatSize(free) + "，不足以安全上传 " + targetName + "。\n直接写入可减少临时空间占用，但中断或失败可能损坏目标文件。是否继续？", "设备空间不足", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
                await adbService.UploadFileAsync(targetSerial, targetPath, sourcePath, direct, token);
            }
            else
            {
                await fileService.UploadFileAsync(targetRoot, targetDirectory, sourcePath, existing);
            }
            return true;
        }

        private async Task RefreshAfterUploadAsync(bool targetIsAdb, string targetSerial, string targetRoot, bool changed)
        {
            if (targetIsAdb)
            {
                if (adbMode && string.Equals(adbSerial, targetSerial, StringComparison.OrdinalIgnoreCase)) await LoadAdbFilesAsyncTask();
                return;
            }
            if (changed)
            {
                if (image != null && workspace != null)
                {
                    foreach (var partition in image.Partitions)
                    {
                        string partitionRoot;
                        if (workspace.ExtractedDirectories.TryGetValue(partition.Name, out partitionRoot) && string.Equals(partitionRoot, targetRoot, StringComparison.OrdinalIgnoreCase)) { partition.Modified = true; break; }
                    }
                }
            }
            if (!adbMode && string.Equals(currentRoot, targetRoot, StringComparison.OrdinalIgnoreCase)) LoadFiles();
        }

        private static string CombineVirtualPath(string directory, string name) { return directory == "/" ? "/" + name : directory.TrimEnd('/') + "/" + name; }
        private static string ParentVirtualPath(string path) { var normalized = (path ?? "/").TrimEnd('/'); var index = normalized.LastIndexOf('/'); return index <= 0 ? "/" : normalized.Substring(0, index); }

        private string Prompt(string title, string label)
        {
            var window = new Window
            {
                Title = title,
                Width = 390,
                MinWidth = 390,
                MinHeight = 205,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false
            };
            var border = new Border
            {
                Background = (Brush)FindResource("PanelBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20)
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid { Cursor = Cursors.SizeAll };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("TextBrush") });
            var close = new Button
            {
                Content = "×",
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 18,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            close.Click += delegate { window.Close(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left) window.DragMove();
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var content = new StackPanel { Margin = new Thickness(0, 20, 0, 20) };
            content.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = (Brush)FindResource("MutedBrush") });
            var input = new TextBox { Height = 38, Margin = new Thickness(0, 8, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
            content.Children.Add(input);
            Grid.SetRow(content, 1);
            root.Children.Add(content);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "取消", Width = 82, Height = 36, Margin = new Thickness(0, 0, 8, 0), IsCancel = true, Background = (Brush)FindResource("PanelAltBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderBrush = (Brush)FindResource("BorderBrush") };
            var confirm = new Button { Content = "确定", Width = 82, Height = 36, IsDefault = true, Background = (Brush)FindResource("AccentBrush"), Foreground = Brushes.White, BorderBrush = (Brush)FindResource("AccentBrush") };
            cancel.Click += delegate { window.Close(); };
            confirm.Click += delegate { window.DialogResult = true; };
            buttons.Children.Add(cancel);
            buttons.Children.Add(confirm);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);
            window.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    window.Close();
                    e.Handled = true;
                }
            };
            border.Child = root;
            window.Content = border;
            window.Loaded += delegate { input.Focus(); };
            return window.ShowDialog() == true ? input.Text : null;
        }

        private async Task LoadAdbFilesAsyncTask(bool showProgress = true)
        {
            if (!adbMode || adbStatus.DeviceState != "online" || string.IsNullOrWhiteSpace(adbSerial)) return;
            var path = currentDirectory;
            if (showProgress) BeginTaskProgress();
            try
            {
                var list = await adbService.ListDirectoryAsync(adbSerial, path, CancellationToken.None);
                if (!adbMode || path != currentDirectory) return;
                files.Clear(); foreach (var entry in list) files.Add(entry); UpdateBreadcrumb(); StatusText.Text = "ADB 设备目录  " + path; UpdateFileSourceButtons();
            }
            catch (Exception ex) { StatusText.Text = "读取设备目录失败：" + ex.Message; }
            finally { if (showProgress) EndTaskProgress(); }
        }

        private async Task EditAdbFileAsync(WorkspaceEntry entry)
        {
            await EditAdbPathAsync(entry.Path, 0);
        }

        private async Task EditAdbPathAsync(string path, int lineNumber)
        {
            try
            {
                byte[] bytes = null;
                await RunTaskProgressAsync(async () => { bytes = await adbService.ReadFileAsync(adbSerial, path, CancellationToken.None); });
                var temp = Path.Combine(Path.GetTempPath(), "wifitool-edit-" + Guid.NewGuid().ToString("N"));
                File.WriteAllBytes(temp, bytes);
                TextFileData data;
                try { data = fileService.ReadText(Path.GetDirectoryName(temp), "/" + Path.GetFileName(temp)); }
                finally { try { File.Delete(temp); } catch { } }
                var editor = new TextEditorWindow(path, data, lineNumber);
                if (editor.ShowDialog() != true) return;
                var encoding = EncodingForName(data.EncodingName);
                var normalized = NormalizeLineEndings(editor.EditorText, data.LineEnding);
                var updated = encoding.GetBytes(normalized);
                if (data.EncodingName == "UTF-8 BOM") updated = Prepend(new byte[] { 0xEF, 0xBB, 0xBF }, updated);
                if (MessageBox.Show(this, "确认应用到设备：" + path + "？", "应用设备文件", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await RunTaskProgressAsync(async () => { await adbService.WriteFileAsync(adbSerial, path, updated, CancellationToken.None); await LoadAdbFilesAsyncTask(); });
                    if (lineNumber > 0) ClearProcessCache();
                    StatusText.Text = "已保存启动来源：" + path;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, "设备文件无法编辑：" + ex.Message, "ADB 文件", MessageBoxButton.OK, MessageBoxImage.Information); }
        }

        private static Encoding EncodingForName(string name) { if (name == "UTF-16 LE") return new UnicodeEncoding(false, false); if (name == "UTF-16 BE") return new UnicodeEncoding(true, false); if (name == "GB18030") return Encoding.GetEncoding(936); return new UTF8Encoding(false); }
        private static string NormalizeLineEndings(string text, string ending) { var value = text.Replace("\r\n", "\n").Replace('\r', '\n'); if (ending == "CRLF") return value.Replace("\n", "\r\n"); if (ending == "CR") return value.Replace('\n', '\r'); if (ending == "无换行") return value.Replace("\n", ""); return value; }
        private static byte[] Prepend(byte[] prefix, byte[] value) { var result = new byte[prefix.Length + value.Length]; Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length); Buffer.BlockCopy(value, 0, result, prefix.Length, value.Length); return result; }

        private async Task OpenHostsAsync()
        {
            const string hostsPath = "/etc/hosts";
            try
            {
                if (adbMode)
                {
                    TextFileData data = null;
                    await RunTaskProgressAsync(async () =>
                    {
                        if (await adbService.RemoteFileExistsAsync(adbSerial, hostsPath, CancellationToken.None))
                        {
                            var createMissingFile = false;
                            try
                            {
                                var bytes = await adbService.ReadFileAsync(adbSerial, hostsPath, CancellationToken.None);
                                data = new TextFileData { Text = System.Text.Encoding.UTF8.GetString(bytes), EncodingName = "UTF-8", LineEnding = "LF" };
                            }
                            catch (Exception ex)
                            {
                                if (!IsMissingRemoteFileError(ex)) throw;
                                createMissingFile = true;
                                data = new TextFileData { Text = "", EncodingName = "UTF-8", LineEnding = "LF" };
                            }
                            if (createMissingFile) await adbService.CreateFileAsync(adbSerial, hostsPath, new byte[0], CancellationToken.None);
                        }
                        else
                        {
                            await adbService.CreateFileAsync(adbSerial, hostsPath, new byte[0], CancellationToken.None);
                            data = new TextFileData { Text = "", EncodingName = "UTF-8", LineEnding = "LF" };
                        }
                    });
                    var editor = new TextEditorWindow("/etc/hosts", data); if (editor.ShowDialog() == true && MessageBox.Show(this, "确认应用 /etc/hosts 到设备？", "Hosts", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { await RunTaskProgressAsync(async () => { await adbService.CreateFileAsync(adbSerial, hostsPath, System.Text.Encoding.UTF8.GetBytes(editor.EditorText), CancellationToken.None); await LoadAdbFilesAsyncTask(); }); }
                }
                else
                {
                    if (string.IsNullOrEmpty(currentRoot)) { MessageBox.Show(this, "请先在项目概览中解包并选择分区。", "Hosts", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                    TextFileData data; try { data = fileService.ReadText(currentRoot, hostsPath); } catch { data = new TextFileData { Text = "", EncodingName = "UTF-8", LineEnding = "LF" }; }
                    var editor = new TextEditorWindow("/etc/hosts", data); if (editor.ShowDialog() == true) { await RunTaskProgressAsync(async () => { var path = fileService.Resolve(currentRoot, hostsPath, false); Directory.CreateDirectory(Path.GetDirectoryName(path)); if (File.Exists(path)) await fileService.SaveTextAsync(currentRoot, hostsPath, data, editor.EditorText); else { File.WriteAllText(path, editor.EditorText, System.Text.Encoding.UTF8); new WorkspaceMetadataService().Update(currentRoot, hostsPath, new WorkspaceMetadata { Kind = "file", Mode = Convert.ToInt32("644", 8), Owner = "0:0", Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }); } }); var p = image.Partitions.FirstOrDefault(x => x.Name == selectedPartitionName); if (p != null) p.Modified = true; LoadFiles(); }
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Hosts 管理失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private static bool IsMissingRemoteFileError(Exception exception)
        {
            var message = exception == null ? "" : exception.ToString();
            return message.IndexOf("error: closed", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("no such file", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("can't open", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("cannot open", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task CheckAdbStatusAsync(bool showProgress = false)
        {
            if (adbChecking) return;
            adbChecking = true;
            if (showProgress) BeginTaskProgress();
            try
            {
                var previousState = adbStatus == null ? "" : adbStatus.DeviceState;
                var previousSerial = adbSerial;
                var state = await adbService.CheckStatusAsync(CancellationToken.None);
                var nextSerial = state.Serial;
                var deviceChanged = !string.Equals(previousSerial, nextSerial, StringComparison.OrdinalIgnoreCase) || !string.Equals(previousState, state.DeviceState, StringComparison.OrdinalIgnoreCase);
                adbStatus = state; adbSerial = nextSerial;
                var remountError = "";
                if (deviceChanged && state.DeviceState == "online")
                {
                    try { await adbService.RemountRootAsync(adbSerial, CancellationToken.None); }
                    catch (Exception ex) { remountError = ex.Message; }
                }
                if (deviceChanged || state.DeviceState != "online")
                {
                    ClearProcessCache();
                    if (state.DeviceState != "online") { ProcessGrid.ItemsSource = null; CoreProcessGrid.ItemsSource = null; }
                }
                UpdateAdbDetails(state);
                AdbExportButton.IsEnabled = state.DeviceState == "online";
                if (state.DeviceState == "online") { AdbDot.Fill = (Brush)FindResource("SuccessBrush"); AdbStatusText.Text = "ADB 设备在线"; AdbStatusSummaryText.Text = "系统 " + FormatAdbSpace(state.System == null ? 0 : state.System.FreeBytes) + " · 用户 " + FormatAdbSpace(state.Userdata == null ? 0 : state.Userdata.FreeBytes); ProcessDeviceText.Text = "设备在线"; RefreshProcessButton.IsEnabled = true; }
                else { AdbDot.Fill = (Brush)FindResource("DisabledBrush"); AdbStatusText.Text = state.DeviceState == "no-device" ? "ADB 等待设备" : state.DeviceState == "offline" ? "ADB 设备离线" : "ADB 服务未启动"; AdbStatusSummaryText.Text = "等待设备连接"; ProcessDeviceText.Text = "未连接设备"; RefreshProcessButton.IsEnabled = false; }
                if (deviceChanged && state.DeviceState == "online") StatusText.Text = string.IsNullOrWhiteSpace(remountError) ? "已将系统根分区挂载为读写" : "系统根分区挂载读写失败：" + remountError;
                UpdateFileSourceButtons();
                if (state.DeviceState == "online" && FilesView.Visibility == Visibility.Visible && !adbMode && string.IsNullOrEmpty(currentRoot)) await ActivateAdbSourceAsync(false);
            }
            catch (Exception ex) { ClearProcessCache(); ProcessGrid.ItemsSource = null; CoreProcessGrid.ItemsSource = null; AdbStatusText.Text = "ADB 检测失败：" + ex.Message; AdbExportButton.IsEnabled = false; RefreshProcessButton.IsEnabled = false; }
            finally { if (showProgress) EndTaskProgress(); adbChecking = false; }
        }

        private void UpdateAdbDetails(AdbStatusInfo state)
        {
            var online = state != null && state.DeviceState == "online";
            AdbDeviceTypeText.Text = state == null || string.IsNullOrWhiteSpace(state.DeviceType) ? "设备类型未知" : "设备类型：" + state.DeviceType;
            AdbVersionText.Text = state == null || string.IsNullOrWhiteSpace(state.SoftwareVersion) ? "软件版本未知" : "软件版本：" + state.SoftwareVersion;
            AdbPortText.Text = state != null && state.PortConnected ? "127.0.0.1:5037" : "--";
            AdbPortStateText.Text = state != null && state.PortConnected ? "已连接" : "未连接";
            AdbDeviceStateText.Text = online ? "在线" : GetAdbStateText(state == null ? "no-port" : state.DeviceState);
            UpdatePartitionDetails(state == null ? null : state.System, AdbSystemTitleText, AdbSystemFreeText, AdbSystemUsageText, AdbSystemProgress, "系统分区");
            UpdatePartitionDetails(state == null ? null : state.Userdata, AdbUserdataTitleText, AdbUserdataFreeText, AdbUserdataUsageText, AdbUserdataProgress, "用户分区");
        }

        private static string GetAdbStateText(string state)
        {
            if (state == "offline") return "离线/未授权";
            if (state == "no-device") return "未连接";
            if (state == "no-port") return "ADB 未启动";
            return "未知";
        }

        private static void UpdatePartitionDetails(AdbPartitionSpace space, TextBlock title, TextBlock free, TextBlock usage, ProgressBar progress, string label)
        {
            if (space == null || space.TotalBytes <= 0)
            {
                title.Text = label + " --";
                free.Text = "-- 剩余";
                usage.Text = "未获取分区信息";
                progress.Value = 0;
                return;
            }
            var mount = string.IsNullOrWhiteSpace(space.Mount) ? "/" : space.Mount;
            title.Text = label + " " + mount;
            free.Text = FormatAdbSpace(space.FreeBytes) + " 剩余";
            usage.Text = "已用 " + FormatAdbSpace(space.UsedBytes) + " / 共 " + FormatAdbSpace(space.TotalBytes) + " · " + mount;
            progress.Value = Math.Max(0, Math.Min(100, space.UsedBytes * 100d / space.TotalBytes));
        }

        private static string FormatAdbSpace(long value)
        {
            if (value <= 0) return "0 B";
            if (value >= 1024 * 1024) return (value / 1024d / 1024d).ToString("0.##") + " MiB";
            if (value >= 1024) return (value / 1024d).ToString("0.##") + " KiB";
            return value + " B";
        }

        private string CreateExportFolder(string parentFolder, string imageFallback)
        {
            if (string.IsNullOrWhiteSpace(imageFallback)) imageFallback = "firmware";
            foreach (var invalid in Path.GetInvalidFileNameChars()) imageFallback = imageFallback.Replace(invalid, '_');
            return Path.Combine(parentFolder, imageFallback + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        private void AdbVersionText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var version = adbStatus == null ? "" : adbStatus.SoftwareVersion;
            if (string.IsNullOrWhiteSpace(version)) return;
            try { Clipboard.SetText(version); StatusText.Text = "软件版本已复制"; } catch { StatusText.Text = "复制软件版本失败"; }
            e.Handled = true;
        }

        private async Task RunBusyAsync(string message, Func<CancellationToken, Task> action)
        {
            if (activeCancellation != null) return; activeCancellation = new CancellationTokenSource(); StatusText.Text = message;
            BeginTaskProgress();
            try { await action(activeCancellation.Token); } catch (OperationCanceledException) { StatusText.Text = "操作已取消"; } catch (Exception ex) { StatusText.Text = "操作失败：" + ex.Message; MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Error); } finally { EndTaskProgress(); activeCancellation.Dispose(); activeCancellation = null; }
        }

        private async Task RunTaskProgressAsync(Func<Task> action)
        {
            BeginTaskProgress();
            try { await action(); }
            finally { EndTaskProgress(); }
        }

        private void BeginTaskProgress()
        {
            activeTaskCount++;
            TaskProgressBar.IsIndeterminate = true;
            TaskProgressBar.Value = 0;
            TaskProgressBar.Visibility = Visibility.Visible;
        }

        private void UpdateTaskProgress(int value)
        {
            TaskProgressBar.IsIndeterminate = false;
            TaskProgressBar.Value = Math.Max(0, Math.Min(100, value));
        }

        private void EndTaskProgress()
        {
            activeTaskCount = Math.Max(0, activeTaskCount - 1);
            if (activeTaskCount != 0) return;
            TaskProgressBar.Visibility = Visibility.Collapsed;
            TaskProgressBar.IsIndeterminate = true;
            TaskProgressBar.Value = 0;
        }

        private static string FormatSize(long value) { if (value >= 1024 * 1024) return (value / 1024d / 1024d).ToString("0.##") + " MiB"; return (value / 1024d).ToString("0.##") + " KiB"; }
    }
}
