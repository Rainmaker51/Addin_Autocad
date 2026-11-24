using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Arch.Suite.Host
{
    internal sealed class FramePreview : FrameworkElement
    {
        private FrameSnapshot snapshot;
        private double zoom = 1;
        private Vector pan;
        private Point last;
        internal FramePreview()
        {
            MinHeight = 180;
            ClipToBounds = true;
            Focusable = true;
            MouseWheel += (s, e) =>
            {
                Point point = e.GetPosition(this);
                double next = Math.Max(0.1, Math.Min(50, zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15)));
                double ratio = next / zoom;
                pan = (Vector)point - ((Vector)point - pan) * ratio;
                zoom = next; InvalidateVisual(); e.Handled = true;
            };
            MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Middle) { last = e.GetPosition(this); CaptureMouse(); e.Handled = true; } };
            MouseMove += (s, e) =>
            {
                if (!IsMouseCaptured) return;
                Point point = e.GetPosition(this); pan += point - last; last = point; InvalidateVisual();
            };
            MouseUp += (s, e) => { if (e.ChangedButton == MouseButton.Middle) ReleaseMouseCapture(); };
        }
        internal void Show(FrameSnapshot value) { snapshot = value; Fit(); }
        internal void Fit() { zoom = 1; pan = new Vector(); InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 33)), null, new Rect(RenderSize));
            if (snapshot?.Boundary == null) return;
            double minX = snapshot.Boundary.Min(p => p.X), maxX = snapshot.Boundary.Max(p => p.X);
            double minY = snapshot.Boundary.Min(p => p.Y), maxY = snapshot.Boundary.Max(p => p.Y);
            double scale = Math.Min(Math.Max(1, ActualWidth - 48) / (maxX - minX),
                Math.Max(1, ActualHeight - 48) / (maxY - minY));
            double offsetX = (ActualWidth - (maxX - minX) * scale) / 2;
            double offsetY = (ActualHeight - (maxY - minY) * scale) / 2;
            Func<double, double, Point> project = (x, y) => new Point(
                (offsetX + (x - minX) * scale) * zoom + pan.X,
                (offsetY + (maxY - y) * scale) * zoom + pan.Y);
            foreach (var shape in snapshot.Shapes)
            {
                var pen = new Pen(new SolidColorBrush(shape.Crossing ?
                    Color.FromRgb(255, 208, 113) : Color.FromRgb(243, 172, 102)), 1);
                Draw(dc, shape.X.Zip(shape.Y, project).ToArray(), shape.Closed, pen);
            }
            var outline = new Pen(new SolidColorBrush(Color.FromRgb(148, 185, 255)), 1.5)
                { DashStyle = DashStyles.Dash };
            Draw(dc, snapshot.Boundary.Select(p => project(p.X, p.Y)).ToArray(), true, outline);
        }
        private static void Draw(DrawingContext dc, Point[] points, bool closed, Pen pen)
        {
            if (points.Length < 2) return;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], false, closed);
                context.PolyLineTo(points.Skip(1).ToArray(), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
    }
}
