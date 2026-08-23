using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            var labels = new[] { "所有者读", "所有者写", "所有者执行", "组读", "组写", "组执行", "其他读", "其他写", "其他执行" };
            var checkGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 0) };
            for (var i = 0; i < checks.Length; i++)
            {
                var box = new CheckBox { Content = labels[i], Margin = new Thickness(0, 3, 8, 3) };
                checks[i] = box;
                box.Checked += delegate { SyncOctalFromChecks(); };
                box.Unchecked += delegate { SyncOctalFromChecks(); };
                checkGrid.Children.Add(box);
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
            int mode; if (!TryParseOctal(octal.Text, out mode)) return;
            for (var i = 0; i < 9; i++) checks[i].IsChecked = (mode & (1 << (8 - i))) != 0;
        }

        private void SyncOctalFromChecks()
        {
            var mode = 0; for (var i = 0; i < 9; i++) if (checks[i].IsChecked == true) mode |= 1 << (8 - i); octal.Text = mode.ToString("000");
        }

        private static bool TryParseOctal(string value, out int mode)
        {
            mode = 0; if (string.IsNullOrWhiteSpace(value) || value.Length > 4) return false;
            foreach (var c in value) { if (c < '0' || c > '7') return false; mode = mode * 8 + c - '0'; }
            return mode >= 0 && mode <= 511;
        }
    }
}
