using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WiFitool.Services;

namespace WiFitool
{
    internal sealed class TextEditorWindow : Window
    {
        private const uint ClipboardUnicodeTextFormat = 13;
        private const uint GlobalMemoryMoveable = 0x0002;
        private readonly TextFileData data;
        private readonly TextBox textBox;
        private readonly ComboBox encodingCombo;
        private readonly ComboBox lineEndingCombo;
        private readonly TextBlock positionText;
        private readonly bool readOnly;
        private string findKeyword;
        private string editorClipboardText;
        private bool findCaseSensitive;
        private FindWindow findWindow;
        public string EditorText { get { return textBox.Text; } }
        public string SelectedEncodingName { get { return encodingCombo.SelectedItem as string; } }
        public string SelectedLineEnding { get { return lineEndingCombo.SelectedItem as string; } }
        public TextEditorWindow(string name, TextFileData data)
            : this(name, data, 0, false)
        {
        }

        public TextEditorWindow(string name, TextFileData data, int lineNumber)
            : this(name, data, lineNumber, false)
        {
        }

        public TextEditorWindow(string name, TextFileData data, int lineNumber, bool readOnly)
        {
            this.data = data;
            this.readOnly = readOnly;
            Title = (readOnly ? "只读：" : "编辑：") + name;
            Width = 900;
            Height = 700;
            MinWidth = 560;
            MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Owner = Application.Current.MainWindow;

            var border = new Border
            {
                Background = GetBrush("PanelBrush", Color.FromRgb(23, 34, 53)),
                BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16)
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Opacity = 0.42, Color = Colors.Black };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Grid { Cursor = Cursors.SizeAll, Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock
            {
                Text = Title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252))
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
                Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192)),
                FontSize = 18,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            close.Click += delegate { Close(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left) DragMove();
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            textBox = new TextBox
            {
                Text = data.Text,
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                TextWrapping = TextWrapping.NoWrap,
                Background = GetBrush("InputBrush", Color.FromRgb(17, 28, 45)),
                Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)),
                BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                IsReadOnly = readOnly
            };
            textBox.ContextMenu = CreateTextContextMenu();
            Grid.SetRow(textBox, 0);
            panel.Children.Add(textBox);

            var statusBar = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var encodingPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            encodingPanel.Children.Add(new TextBlock { Text = "编码", FontSize = 12, Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192)), VerticalAlignment = VerticalAlignment.Center });
            encodingCombo = new ComboBox { Width = 118, Margin = new Thickness(6, 0, 16, 0), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            foreach (var item in new[] { "UTF-8", "UTF-8 BOM", "UTF-16 LE", "UTF-16 BE", "GB18030" }) encodingCombo.Items.Add(item);
            encodingCombo.SelectedItem = data.EncodingName;
            encodingCombo.IsEnabled = !readOnly;
            encodingPanel.Children.Add(encodingCombo);
            Grid.SetColumn(encodingPanel, 0);
            statusBar.Children.Add(encodingPanel);

            var lineEndingPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lineEndingPanel.Children.Add(new TextBlock { Text = "换行符", FontSize = 12, Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192)), VerticalAlignment = VerticalAlignment.Center });
            lineEndingCombo = new ComboBox { Width = 96, Margin = new Thickness(6, 0, 0, 0), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            foreach (var item in new[] { "LF", "CRLF", "CR", "无换行" }) lineEndingCombo.Items.Add(item);
            lineEndingCombo.SelectedItem = data.LineEnding;
            lineEndingCombo.IsEnabled = !readOnly;
            lineEndingPanel.Children.Add(lineEndingCombo);
            Grid.SetColumn(lineEndingPanel, 1);
            statusBar.Children.Add(lineEndingPanel);

            positionText = new TextBlock { Text = "行 1 列 1", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192)) };
            Grid.SetColumn(positionText, 2);
            statusBar.Children.Add(positionText);
            Grid.SetRow(statusBar, 1);
            panel.Children.Add(statusBar);
            textBox.SelectionChanged += delegate { UpdatePosition(); };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var repair = new Button { Content = "修复为 UTF-8", MinWidth = 100, Margin = new Thickness(0, 0, 8, 0), IsEnabled = !readOnly };
            var find = new Button { Content = "查找", MinWidth = 74, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = readOnly ? "关闭" : "保存", MinWidth = 74, IsDefault = !readOnly, Margin = new Thickness(0, 0, 8, 0), Background = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)), Foreground = Brushes.White, BorderBrush = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)) };
            var cancel = new Button { Content = "取消", MinWidth = 74, IsCancel = true, Background = GetBrush("PanelAltBrush", Color.FromRgb(27, 42, 64)), Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)), BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)) };
            repair.Click += delegate { RepairToUtf8(); };
            find.Click += delegate { BeginFind(); };
            if (readOnly) save.Click += delegate { Close(); };
            else save.Click += delegate { DialogResult = true; Close(); };
            buttons.Children.Add(repair); buttons.Children.Add(find); buttons.Children.Add(save); buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 2);
            panel.Children.Add(buttons);
            Grid.SetRow(panel, 1);
            root.Children.Add(panel);

            border.Child = root;
            Content = border;
            Loaded += delegate { textBox.Focus(); UpdatePosition(); if (lineNumber > 0) SelectLine(lineNumber); };
            textBox.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(TextBox_PreviewKeyDown), true);
            KeyDown += TextEditorWindow_KeyDown;
        }

        private void SelectLine(int lineNumber)
        {
            if (textBox.LineCount <= 0) return;
            var lineIndex = Math.Max(0, Math.Min(lineNumber - 1, textBox.LineCount - 1));
            var start = textBox.GetCharacterIndexFromLineIndex(lineIndex);
            var end = lineIndex + 1 < textBox.LineCount ? textBox.GetCharacterIndexFromLineIndex(lineIndex + 1) : textBox.Text.Length;
            while (end > start && (textBox.Text[end - 1] == '\r' || textBox.Text[end - 1] == '\n')) end--;
            textBox.Select(start, Math.Max(0, end - start));
            textBox.ScrollToLine(lineIndex);
        }

        private void UpdatePosition()
        {
            var line = textBox.GetLineIndexFromCharacterIndex(Math.Max(0, textBox.SelectionStart)) + 1;
            var lineStart = textBox.GetCharacterIndexFromLineIndex(line - 1);
            var column = textBox.SelectionStart - lineStart + 1;
            positionText.Text = "行 " + line + " 列 " + column;
        }

        private void RepairToUtf8()
        {
            if (readOnly) return;
            if (data == null || data.RawBytes == null) return;
            try
            {
                var bytes = data.RawBytes;
                var builder = new StringBuilder();
                var utf8 = new UTF8Encoding(false, true);
                var gb18030 = Encoding.GetEncoding(54936);
                var index = 0;
                while (index < bytes.Length)
                {
                    if (bytes[index] < 0x80) { builder.Append((char)bytes[index]); index++; continue; }
                    var end = index;
                    while (end < bytes.Length && bytes[end] >= 0x80) end++;
                    var segment = new byte[end - index];
                    Buffer.BlockCopy(bytes, index, segment, 0, segment.Length);
                    try { builder.Append(utf8.GetString(segment)); }
                    catch (DecoderFallbackException) { builder.Append(gb18030.GetString(segment)); }
                    index = end;
                }
                textBox.Text = builder.ToString();
                encodingCombo.SelectedItem = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? "UTF-8 BOM" : "UTF-8";
                UpdatePosition();
            }
            catch (Exception ex) { MessageBox.Show(this, "修复失败：" + ex.Message, "修复为 UTF-8", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void TextEditorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { BeginFind(); e.Handled = true; }
        }

        private async void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key != Key.X && e.Key != Key.C && e.Key != Key.V) return;
            e.Handled = true;
            if (e.Key == Key.X) await CutSelectedTextAsync();
            else if (e.Key == Key.C) await CopySelectedTextAsync();
            else await PasteClipboardTextAsync();
        }

        private ContextMenu CreateTextContextMenu()
        {
            var menu = new ContextMenu();
            var cut = new MenuItem { Header = "剪切" };
            var copy = new MenuItem { Header = "复制" };
            var paste = new MenuItem { Header = "粘贴" };
            var selectAll = new MenuItem { Header = "全选" };
            cut.Click += async delegate { await CutSelectedTextAsync(); };
            copy.Click += async delegate { await CopySelectedTextAsync(); };
            paste.Click += async delegate { await PasteClipboardTextAsync(); };
            selectAll.Click += delegate { textBox.SelectAll(); };
            menu.Items.Add(cut);
            menu.Items.Add(copy);
            menu.Items.Add(paste);
            menu.Items.Add(new Separator());
            menu.Items.Add(selectAll);
            menu.Opened += delegate
            {
                var hasSelection = textBox.SelectionLength > 0;
                cut.IsEnabled = !readOnly && hasSelection;
                copy.IsEnabled = hasSelection;
                paste.IsEnabled = !readOnly;
                selectAll.IsEnabled = textBox.Text.Length > 0;
            };
            return menu;
        }

        private async Task CutSelectedTextAsync()
        {
            if (readOnly || textBox.SelectionLength <= 0) return;
            var start = textBox.SelectionStart;
            var length = textBox.SelectionLength;
            var selectedText = textBox.SelectedText;
            editorClipboardText = selectedText;
            textBox.Text = textBox.Text.Remove(start, length);
            textBox.Select(start, 0);
            textBox.Focus();
            await SetClipboardTextAsync(selectedText);
        }

        private async Task CopySelectedTextAsync()
        {
            if (textBox.SelectionLength <= 0) return;
            var selectedText = textBox.SelectedText;
            editorClipboardText = selectedText;
            await SetClipboardTextAsync(selectedText);
        }

        private async Task PasteClipboardTextAsync()
        {
            if (readOnly) return;
            var clipboardText = await GetClipboardTextAsync();
            if (clipboardText == null) clipboardText = editorClipboardText;
            if (clipboardText == null || clipboardText.Length == 0) return;
            var start = textBox.SelectionStart;
            var length = textBox.SelectionLength;
            textBox.Text = textBox.Text.Remove(start, length).Insert(start, clipboardText);
            textBox.Select(start + clipboardText.Length, 0);
            textBox.Focus();
        }

        private static Task SetClipboardTextAsync(string value)
        {
            return Task.Run(() => SetClipboardTextNative(value));
        }

        private static Task<string> GetClipboardTextAsync()
        {
            return Task.Run(GetClipboardTextNative);
        }

        private static bool SetClipboardTextNative(string value)
        {
            var bytes = Encoding.Unicode.GetBytes((value ?? "") + "\0");
            var memory = GlobalAlloc(GlobalMemoryMoveable, (UIntPtr)bytes.Length);
            if (memory == IntPtr.Zero) return false;
            var locked = false;
            try
            {
                var target = GlobalLock(memory);
                if (target == IntPtr.Zero) return false;
                locked = true;
                Marshal.Copy(bytes, 0, target, bytes.Length);
                GlobalUnlock(memory);
                locked = false;
                if (!OpenClipboard(IntPtr.Zero)) return false;
                try
                {
                    if (!EmptyClipboard() || SetClipboardData(ClipboardUnicodeTextFormat, memory) == IntPtr.Zero) return false;
                    memory = IntPtr.Zero;
                    return true;
                }
                finally { CloseClipboard(); }
            }
            finally
            {
                if (locked) GlobalUnlock(memory);
                if (memory != IntPtr.Zero) GlobalFree(memory);
            }
        }

        private static string GetClipboardTextNative()
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;
            try
            {
                if (!IsClipboardFormatAvailable(ClipboardUnicodeTextFormat)) return "";
                var memory = GetClipboardData(ClipboardUnicodeTextFormat);
                if (memory == IntPtr.Zero) return null;
                var target = GlobalLock(memory);
                if (target == IntPtr.Zero) return null;
                try { return Marshal.PtrToStringUni(target) ?? ""; }
                finally { GlobalUnlock(memory); }
            }
            finally { CloseClipboard(); }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr memory);

        private void BeginFind()
        {
            if (findWindow != null)
            {
                findWindow.Activate();
                findWindow.FocusInput();
                return;
            }
            findWindow = new FindWindow(findKeyword, findCaseSensitive, delegate(string keyword, bool caseSensitive)
            {
                findKeyword = keyword;
                findCaseSensitive = caseSensitive;
                FindNext();
            });
            findWindow.Owner = this;
            findWindow.Closed += delegate { findWindow = null; };
            findWindow.Show();
        }

        private void FindNext()
        {
            if (string.IsNullOrEmpty(findKeyword)) { BeginFind(); return; }
            var comparison = findCaseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase; var start = textBox.SelectionStart + Math.Max(1, textBox.SelectionLength); var index = textBox.Text.IndexOf(findKeyword, start, comparison); if (index < 0 && start > 0) index = textBox.Text.IndexOf(findKeyword, 0, comparison); if (index >= 0) { textBox.Focus(); textBox.Select(index, findKeyword.Length); } else MessageBox.Show(this, "未找到匹配内容。", "查找", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static Brush GetBrush(string key, Color fallback)
        {
            var brush = Application.Current == null ? null : Application.Current.TryFindResource(key) as Brush;
            return brush ?? new SolidColorBrush(fallback);
        }
    }

    internal sealed class FindWindow : Window
    {
        private readonly TextBox input; private readonly CheckBox check;
        private readonly Action<string, bool> findAction;
        public FindWindow(string keyword, bool caseSensitive, Action<string, bool> findAction)
        {
            this.findAction = findAction;
            Title = "查找";
            Width = 430;
            MinWidth = 430;
            MinHeight = 220;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;

            var border = new Border
            {
                Background = GetBrush("PanelBrush", Color.FromRgb(23, 34, 53)),
                BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)),
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
            header.Children.Add(new TextBlock
            {
                Text = "查找",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252))
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
                Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192)),
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            close.Click += delegate { Close(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left) DragMove();
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var content = new StackPanel { Margin = new Thickness(0, 20, 0, 20) };
            content.Children.Add(new TextBlock
            {
                Text = "关键词",
                FontSize = 12,
                Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192))
            });
            input = new TextBox
            {
                Text = keyword ?? "",
                Height = 38,
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = GetBrush("InputBrush", Color.FromRgb(17, 28, 45)),
                Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)),
                BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)),
                BorderThickness = new Thickness(1)
            };
            check = new CheckBox
            {
                Content = "区分大小写",
                IsChecked = caseSensitive,
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 13,
                Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252))
            };
            content.Children.Add(input);
            content.Children.Add(check);
            Grid.SetRow(content, 1);
            root.Children.Add(content);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button
            {
                Content = "取消",
                Width = 82,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0),
                Background = GetBrush("PanelAltBrush", Color.FromRgb(27, 42, 64)),
                Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)),
                BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)),
                IsCancel = true
            };
            cancel.Click += delegate { Close(); };
            var find = new Button
            {
                Content = "查找",
                Width = 82,
                Height = 36,
                Background = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)),
                Foreground = Brushes.White,
                BorderBrush = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)),
                IsDefault = true
            };
            find.Click += delegate
            {
                if (!string.IsNullOrEmpty(input.Text) && findAction != null) findAction(input.Text, check.IsChecked == true);
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(find);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };
            Content = border;
            border.Child = root;
            Loaded += delegate { input.Focus(); };
        }

        public void FocusInput()
        {
            input.Focus();
            input.SelectAll();
        }

        private static Brush GetBrush(string key, Color fallback)
        {
            var brush = Application.Current == null ? null : Application.Current.TryFindResource(key) as Brush;
            return brush ?? new SolidColorBrush(fallback);
        }
    }
}
