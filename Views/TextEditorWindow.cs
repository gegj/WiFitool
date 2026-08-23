using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WiFitool.Services;

namespace WiFitool
{
    internal sealed class TextEditorWindow : Window
    {
        private readonly TextBox textBox;
        private string findKeyword;
        private bool findCaseSensitive;
        public string EditorText { get { return textBox.Text; } }
        public TextEditorWindow(string name, TextFileData data)
            : this(name, data, 0)
        {
        }

        public TextEditorWindow(string name, TextFileData data, int lineNumber)
        {
            Title = "编辑：" + name;
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

            var panel = new DockPanel();
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
                Padding = new Thickness(10, 8, 10, 8)
            };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var find = new Button { Content = "查找", MinWidth = 74, Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = "保存", MinWidth = 74, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Background = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)), Foreground = Brushes.White, BorderBrush = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)) };
            var cancel = new Button { Content = "取消", MinWidth = 74, IsCancel = true, Background = GetBrush("PanelAltBrush", Color.FromRgb(27, 42, 64)), Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)), BorderBrush = GetBrush("BorderBrush", Color.FromRgb(43, 58, 80)) };
            find.Click += delegate { BeginFind(); };
            save.Click += delegate { DialogResult = true; Close(); };
            buttons.Children.Add(find); buttons.Children.Add(save); buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            panel.Children.Add(buttons);
            panel.Children.Add(textBox);
            Grid.SetRow(panel, 1);
            root.Children.Add(panel);

            border.Child = root;
            Content = border;
            Loaded += delegate { textBox.Focus(); if (lineNumber > 0) SelectLine(lineNumber); };
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

        private void TextEditorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { BeginFind(); e.Handled = true; }
            else if (e.Key == Key.F3) { FindNext(); e.Handled = true; }
        }

        private void BeginFind()
        {
            var dialog = new FindWindow(); dialog.Owner = this; if (dialog.ShowDialog() != true) return; findKeyword = dialog.Keyword; findCaseSensitive = dialog.CaseSensitive; FindNext();
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
        public string Keyword { get; private set; } public bool CaseSensitive { get { return check.IsChecked == true; } }
        private readonly TextBox input; private readonly CheckBox check;
        public FindWindow()
        {
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
                Keyword = input.Text;
                if (!string.IsNullOrEmpty(Keyword)) DialogResult = true;
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

        private static Brush GetBrush(string key, Color fallback)
        {
            var brush = Application.Current == null ? null : Application.Current.TryFindResource(key) as Brush;
            return brush ?? new SolidColorBrush(fallback);
        }
    }
}
