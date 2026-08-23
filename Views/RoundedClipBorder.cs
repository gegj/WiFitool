using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WiFitool.Views
{
    public sealed class RoundedClipBorder : Border
    {
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            var radius = Math.Max(0, CornerRadius.TopLeft);
            Clip = new RectangleGeometry(new Rect(RenderSize), radius, radius);
        }
    }
}
