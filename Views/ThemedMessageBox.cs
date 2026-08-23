using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WiFitool
{
    // 统一替代系统 MessageBox，保持现有调用的返回值行为。
    internal static class MessageBox
    {
        public static MessageBoxResult Show(Window owner, string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            var result = MessageBoxResult.None;
            var window = new Window
            {
                Title = caption,
                Width = 470,
                MinWidth = 390,
                MaxHeight = 560,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Owner = owner
            };

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

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = GetIconBrush(image),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = GetIconText(image),
                    Foreground = Brushes.White,
                    FontSize = image == MessageBoxImage.Error ? 22 : 17,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            header.Children.Add(icon);
            header.Children.Add(new TextBlock { Text = caption, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)) });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var messageViewer = new ScrollViewer { MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(46, 16, 4, 20) };
            messageViewer.Content = new TextBlock { Text = message ?? "", TextWrapping = TextWrapping.Wrap, Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252)), FontSize = 13, LineHeight = 21 };
            Grid.SetRow(messageViewer, 1);
            root.Children.Add(messageViewer);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var hasCancel = buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel;
            AddButtons(window, buttonPanel, buttons, delegate(MessageBoxResult value) { result = value; });
            Grid.SetRow(buttonPanel, 2);
            root.Children.Add(buttonPanel);

            window.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && hasCancel)
                {
                    result = MessageBoxResult.Cancel;
                    window.Close();
                    e.Handled = true;
                }
            };
            border.Child = root;
            window.Content = border;
            window.ShowDialog();
            return result;
        }

        private static void AddButtons(Window window, Panel panel, MessageBoxButton buttons, Action<MessageBoxResult> setResult)
        {
            if (buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel)
            {
                if (buttons == MessageBoxButton.OKCancel) AddButton(window, panel, "取消", MessageBoxResult.Cancel, false, setResult);
                AddButton(window, panel, "确定", MessageBoxResult.OK, true, setResult);
                return;
            }
            if (buttons == MessageBoxButton.YesNo)
            {
                AddButton(window, panel, "否", MessageBoxResult.No, false, setResult);
                AddButton(window, panel, "是", MessageBoxResult.Yes, true, setResult);
                return;
            }
            AddButton(window, panel, "取消", MessageBoxResult.Cancel, false, setResult);
            AddButton(window, panel, "否", MessageBoxResult.No, false, setResult);
            AddButton(window, panel, "是", MessageBoxResult.Yes, true, setResult);
        }

        private static void AddButton(Window window, Panel panel, string text, MessageBoxResult value, bool primary, Action<MessageBoxResult> setResult)
        {
            var button = new Button { Content = text, MinWidth = 82, Height = 34, Margin = new Thickness(8, 0, 0, 0), IsDefault = primary };
            if (primary)
            {
                button.Background = GetBrush("AccentBrush", Color.FromRgb(91, 131, 255));
                button.Foreground = Brushes.White;
            }
            else
            {
                button.Background = GetBrush("PanelAltBrush", Color.FromRgb(27, 42, 64));
                button.Foreground = GetBrush("TextBrush", Color.FromRgb(240, 245, 252));
            }
            button.Click += delegate { setResult(value); window.Close(); };
            panel.Children.Add(button);
        }

        private static string GetIconText(MessageBoxImage image)
        {
            if (image == MessageBoxImage.Error) return "×";
            if (image == MessageBoxImage.Warning) return "!";
            if (image == MessageBoxImage.Question) return "?";
            return "i";
        }

        private static Brush GetIconBrush(MessageBoxImage image)
        {
            if (image == MessageBoxImage.Error) return GetBrush("DangerBrush", Color.FromRgb(240, 113, 136));
            if (image == MessageBoxImage.Warning) return GetBrush("WarningBrush", Color.FromRgb(229, 162, 61));
            return GetBrush("AccentBrush", Color.FromRgb(91, 131, 255));
        }

        private static Brush GetBrush(string key, Color fallback)
        {
            var brush = Application.Current == null ? null : Application.Current.TryFindResource(key) as Brush;
            return brush ?? new SolidColorBrush(fallback);
        }
    }
}
