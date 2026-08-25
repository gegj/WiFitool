using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WiFitool.Models;

namespace WiFitool
{
    internal sealed class PermissionWindow : Window
    {
        private readonly TextBox octal;
        private readonly TextBox owner;
        private readonly CheckBox[] checks;
        private bool syncing;
        public int Mode { get; private set; }
        public string OwnerValue { get { return owner.Text.Trim(); } }

        public PermissionWindow(WorkspaceEntry entry)
        {
            Title = "编辑权限：" + entry.Name;
            Width = 430;
            MinWidth = 430;
            MinHeight = 370;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
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

            var panel = new StackPanel { Margin = new Thickness(0, 20, 0, 20) };
            panel.Children.Add(new TextBlock
            {
                Text = "八进制权限（例如 755）",
                FontSize = 12,
                Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192))
            });
            octal = new TextBox
            {
                Text = Convert.ToString(entry.UnixMode & 0xFFF, 8),
                Height = 36,
                Width = 100,
                Margin = new Thickness(0, 8, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            octal.TextChanged += delegate { SyncChecksFromOctal(); };
            panel.Children.Add(octal);
            panel.Children.Add(new TextBlock
            {
                Text = "所有者 UID:GID",
                FontSize = 12,
                Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192))
            });
            owner = new TextBox
            {
                Text = entry.Owner,
                Height = 36,
                Margin = new Thickness(0, 8, 0, 14),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(owner);

            checks = new CheckBox[9];
            var columnHeaders = new[] { "所有者", "组", "其他" };
            var permissionNames = new[] { "读", "写", "执行" };
            var checkGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            for (var column = 0; column < 3; column++) checkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var row = 0; row < 4; row++) checkGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < 3; column++)
            {
                var columnHeader = new TextBlock
                {
                    Text = columnHeaders[column],
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = GetBrush("MutedBrush", Color.FromRgb(154, 170, 192))
                };
                Grid.SetRow(columnHeader, 0);
                Grid.SetColumn(columnHeader, column);
                checkGrid.Children.Add(columnHeader);
                for (var row = 0; row < 3; row++)
                {
                    var index = column * 3 + row;
                    var box = new CheckBox { Content = permissionNames[row], Margin = new Thickness(4, 3, 4, 3), Style = (Style)Application.Current.FindResource("PermissionCheckBoxStyle") };
                    checks[index] = box;
                    box.Checked += delegate { SyncOctalFromChecks(); };
                    box.Unchecked += delegate { SyncOctalFromChecks(); };
                    Grid.SetRow(box, row + 1);
                    Grid.SetColumn(box, column);
                    checkGrid.Children.Add(box);
                }
            }
            panel.Children.Add(checkGrid);
            Grid.SetRow(panel, 1);
            root.Children.Add(panel);

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
            var save = new Button
            {
                Content = "保存",
                Width = 82,
                Height = 36,
                Background = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)),
                Foreground = Brushes.White,
                BorderBrush = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255)),
                IsDefault = true
            };
            save.Click += delegate
            {
                int mode;
                if (!TryParseOctal(octal.Text, out mode))
                {
                    MessageBox.Show(this, "请输入 000 到 777 的权限。", "权限", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Mode = mode;
                DialogResult = true;
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
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
            border.Child = root;
            Content = border;
            Loaded += delegate { octal.Focus(); octal.SelectAll(); };
            SyncChecksFromOctal();
        }

        private static Brush GetBrush(string key, Color fallback)
        {
            var brush = Application.Current == null ? null : Application.Current.TryFindResource(key) as Brush;
            return brush ?? new SolidColorBrush(fallback);
        }

        private void SyncChecksFromOctal()
        {
            if (syncing) return;
            int mode; if (!TryParseOctal(octal.Text, out mode)) return;
            syncing = true;
            try { for (var i = 0; i < 9; i++) checks[i].IsChecked = (mode & (1 << (8 - i))) != 0; }
            finally { syncing = false; }
        }

        private void SyncOctalFromChecks()
        {
            if (syncing) return;
            var mode = 0; for (var i = 0; i < 9; i++) if (checks[i].IsChecked == true) mode |= 1 << (8 - i);
            syncing = true;
            try { octal.Text = Convert.ToString(mode, 8).PadLeft(3, '0'); }
            finally { syncing = false; }
        }

        private static bool TryParseOctal(string value, out int mode)
        {
            mode = 0; if (string.IsNullOrWhiteSpace(value) || value.Length > 4) return false;
            foreach (var c in value) { if (c < '0' || c > '7') return false; mode = mode * 8 + c - '0'; }
            return mode >= 0 && mode <= 511;
        }
    }
}
