using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using D = System.Drawing;

namespace WowLexikon
{
    public class Selection
    {
        public D.Bitmap Shot;          // eingefrorenes Bild des Monitors
        public D.Rectangle Monitor;    // Monitor in Bildschirm-Pixeln
        public D.Rectangle? WordRect; public D.Point? WordPoint;
        public D.Rectangle? CtxRect; public D.Point? CtxPoint;

        /// <summary>Stelle, an der die Ergebnis-Karte andocken soll (Bildschirm-Pixel).</summary>
        public D.Rectangle Anchor
        {
            get
            {
                D.Rectangle r = WordRect.HasValue ? WordRect.Value
                    : WordPoint.HasValue ? new D.Rectangle(WordPoint.Value.X - 10, WordPoint.Value.Y - 10, 20, 20)
                    : new D.Rectangle(Monitor.Width / 2, Monitor.Height / 2, 1, 1);
                r.Offset(Monitor.Location);
                return r;
            }
        }
    }

    /// <summary>
    /// Vollbild-Auswahl im Stil eines Snipping-Tools: friert den Bildschirm ein, Rahmen 1 = Wort, Rahmen 2 = Kontext.
    /// Nimmt keinen Fokus, damit WoW aktiv bleibt.
    /// </summary>
    class OverlayWindow : Window
    {
        readonly D.Bitmap shot;
        readonly D.Rectangle mon;
        readonly BitmapSource src;
        double scale = 1;   // DIP pro Bildschirm-Pixel
        IntPtr hwnd;

        readonly Canvas canvas = new Canvas();
        readonly Path dim = new Path();
        readonly Line hLine = new Line(), vLine = new Line();
        readonly Rectangle wordBox, ctxBox, dragBox;
        readonly Ellipse wordDot, ctxDot;
        readonly Border wordTag, ctxTag, loupe;
        readonly ImageBrush loupeBrush;
        readonly Border step1Num, step2Num;
        readonly TextBlock step1Text, step2Text;
        readonly Grid grid = new Grid();
        double W { get { return grid.ActualWidth; } }
        double H { get { return grid.ActualHeight; } }

        D.Rectangle? wordRect, ctxRect; D.Point? wordPt, ctxPt;
        bool dragging; D.Point dragStart; D.Rectangle dragRect;

        static readonly Color Gold = Color.FromRgb(0xF2, 0xC4, 0x6D), Cyan = Color.FromRgb(0x5F, 0xD4, 0xE8);

        public OverlayWindow(D.Bitmap shot, D.Rectangle mon)
        {
            this.shot = shot; this.mon = mon;
            src = ScreenShot.ToWpf(shot);

            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
            ShowActivated = false; Background = Brushes.Black; Cursor = Cursors.Cross;
            WindowStartupLocation = WindowStartupLocation.Manual;
            double sys = SystemScale();
            Left = mon.X / sys; Top = mon.Y / sys; Width = mon.Width / sys; Height = mon.Height / sys;

            var img = new Image { Source = src, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            grid.Children.Add(img);
            dim.Fill = new SolidColorBrush(Color.FromArgb(0x96, 0x05, 0x07, 0x0A));
            dim.IsHitTestVisible = false;
            grid.Children.Add(dim);
            grid.Children.Add(canvas);

            foreach (var l in new[] { hLine, vLine })
            {
                l.Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
                l.StrokeThickness = 1; l.StrokeDashArray = new DoubleCollection { 4, 4 }; l.IsHitTestVisible = false;
                canvas.Children.Add(l);
            }
            wordBox = MakeBox(Gold); ctxBox = MakeBox(Cyan); dragBox = MakeBox(Gold);
            dragBox.StrokeDashArray = new DoubleCollection { 3, 2 };
            wordDot = MakeDot(Gold); ctxDot = MakeDot(Cyan);
            wordTag = MakeTag("1 · WORT", Gold); ctxTag = MakeTag("2 · KONTEXT", Cyan);

            loupeBrush = new ImageBrush(src) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
            var loupeGrid = new Grid();
            loupeGrid.Children.Add(new Rectangle { Fill = loupeBrush });
            loupeGrid.Children.Add(new Rectangle { Width = 1.5, Height = 14, Fill = new SolidColorBrush(Color.FromArgb(0xCC, 0xF2, 0xC4, 0x6D)) });
            loupeGrid.Children.Add(new Rectangle { Width = 14, Height = 1.5, Fill = new SolidColorBrush(Color.FromArgb(0xCC, 0xF2, 0xC4, 0x6D)) });
            loupe = new Border
            {
                Width = 136, Height = 136, CornerRadius = new CornerRadius(68), BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Gold), Child = loupeGrid, IsHitTestVisible = false, ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.6 }
            };
            loupeGrid.Clip = new EllipseGeometry(new Point(66, 66), 66, 66);
            RenderOptions.SetBitmapScalingMode(loupeGrid, BitmapScalingMode.NearestNeighbor);
            canvas.Children.Add(loupe);

            // Hinweisleiste oben
            var bar = new StackPanel { Orientation = Orientation.Horizontal };
            step1Num = StepNum("1"); step1Text = StepText("Wort einrahmen oder anklicken");
            step2Num = StepNum("2"); step2Text = StepText("Satz als Kontext (optional)");
            bar.Children.Add(step1Num); bar.Children.Add(step1Text);
            bar.Children.Add(new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("Icons"), FontSize = 10, Foreground = Muted(), Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            bar.Children.Add(step2Num); bar.Children.Add(step2Text);
            bar.Children.Add(new Border { Width = 1, Height = 18, Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x41, 0x50)), Margin = new Thickness(16, 0, 16, 0) });
            bar.Children.Add(new TextBlock { Text = "Tasten loslassen = übersetzen  ·  Rechtsklick = zurück  ·  Esc = abbrechen", Foreground = Muted(), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var hint = new Border
            {
                Child = bar, Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x16, 0x19, 0x1F)), CornerRadius = new CornerRadius(20),
                Padding = new Thickness(14, 9, 18, 9), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 26, 0, 0), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xF2, 0xC4, 0x6D)),
                IsHitTestVisible = false, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.5 }
            };
            TextElement.SetFontFamily(hint, new FontFamily("Segoe UI Variable Text, Segoe UI"));
            grid.Children.Add(hint);
            Content = grid;

            SourceInitialized += (s, e) =>
            {
                hwnd = new WindowInteropHelper(this).Handle;
                Native.SetNoActivate(hwnd, true);
                PlaceExactly();
            };
            Loaded += (s, e) => { PlaceExactly(); UpdateCursor(); Refresh(); };
            grid.SizeChanged += (s, e) => { if (W > 0) scale = W / mon.Width; Refresh(); UpdateCursor(); };
            MouseMove += (s, e) => { UpdateCursor(); if (dragging) { dragRect = Norm(dragStart, Local()); Refresh(); } };
            MouseLeftButtonDown += (s, e) => { dragging = true; dragStart = Local(); dragRect = new D.Rectangle(dragStart, D.Size.Empty); CaptureMouse(); Refresh(); };
            MouseLeftButtonUp += (s, e) => { if (dragging) { ReleaseMouseCapture(); CommitDrag(); } };
            MouseRightButtonDown += (s, e) => StepBack();
        }

        void PlaceExactly()
        {
            if (hwnd == IntPtr.Zero) return;
            var r = Native.WindowRect(hwnd);
            if (r != mon)
                Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, mon.X, mon.Y, mon.Width, mon.Height, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        }

        static double SystemScale()
        {
            using (var g = D.Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96.0;
        }

        Rectangle MakeBox(Color c)
        {
            var r = new Rectangle { Stroke = new SolidColorBrush(c), StrokeThickness = 2, RadiusX = 3, RadiusY = 3, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                Fill = new SolidColorBrush(Color.FromArgb(0x10, c.R, c.G, c.B)) };
            r.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = c, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.7 };
            canvas.Children.Add(r);
            return r;
        }
        Ellipse MakeDot(Color c)
        {
            var e = new Ellipse { Width = 18, Height = 18, Stroke = new SolidColorBrush(c), StrokeThickness = 2.5, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                Fill = new SolidColorBrush(Color.FromArgb(0x40, c.R, c.G, c.B)) };
            canvas.Children.Add(e);
            return e;
        }
        Border MakeTag(string text, Color c)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(c), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 2), Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                Child = new TextBlock { Text = text, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x14, 0x12, 0x0A)), FontFamily = new FontFamily("Segoe UI") }
            };
            canvas.Children.Add(b);
            return b;
        }
        static Brush Muted() { return new SolidColorBrush(Color.FromRgb(0x8E, 0x98, 0xA7)); }
        static Border StepNum(string n)
        {
            return new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock { Text = n, FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        }
        static TextBlock StepText(string t) { return new TextBlock { Text = t, FontSize = 13, VerticalAlignment = VerticalAlignment.Center }; }

        D.Point Local()
        {
            var c = Native.Cursor();
            return new D.Point(c.X - mon.X, c.Y - mon.Y);
        }
        static D.Rectangle Norm(D.Point a, D.Point b)
        {
            return D.Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
        Rect Dip(D.Rectangle r) { return new Rect(r.X * scale, r.Y * scale, Math.Max(0, r.Width * scale), Math.Max(0, r.Height * scale)); }

        bool InContextPhase { get { return wordRect.HasValue || wordPt.HasValue; } }

        void CommitDrag()
        {
            dragging = false;
            var r = dragRect;
            bool click = r.Width < 6 && r.Height < 6;
            if (!InContextPhase)
            {
                if (click) { wordPt = dragStart; wordRect = null; } else { wordRect = r; wordPt = null; }
            }
            else
            {
                if (click) { ctxPt = dragStart; ctxRect = null; } else { ctxRect = r; ctxPt = null; }
            }
            Refresh();
        }

        void StepBack()
        {
            if (dragging) { dragging = false; ReleaseMouseCapture(); }
            else if (ctxRect.HasValue || ctxPt.HasValue) { ctxRect = null; ctxPt = null; }
            else if (wordRect.HasValue || wordPt.HasValue) { wordRect = null; wordPt = null; }
            else { if (Cancelled != null) Cancelled(); return; }
            Refresh();
        }

        public event Action Cancelled;

        /// <summary>Wird beim Loslassen der Tasten aufgerufen.</summary>
        public Selection Finish()
        {
            if (dragging) { ReleaseMouseCapture(); CommitDrag(); }
            if (!InContextPhase) return null;
            return new Selection { Shot = shot, Monitor = mon, WordRect = wordRect, WordPoint = wordPt, CtxRect = ctxRect, CtxPoint = ctxPt };
        }

        void UpdateCursor()
        {
            if (W <= 0 || demo) return;
            ShowCursorAt(Local());
        }

        void ShowCursorAt(D.Point p)
        {
            double x = p.X * scale, y = p.Y * scale;
            hLine.X1 = 0; hLine.X2 = W; hLine.Y1 = hLine.Y2 = Math.Round(y) + 0.5;
            vLine.Y1 = 0; vLine.Y2 = H; vLine.X1 = vLine.X2 = Math.Round(x) + 0.5;

            // Lupe: 3,4-fache Vergrößerung, weicht an den Rändern aus
            const double zoomSrc = 40;
            loupeBrush.Viewbox = new Rect(p.X - zoomSrc / 2, p.Y - zoomSrc / 2, zoomSrc, zoomSrc);
            double lx = x + 28, ly = y + 28;
            if (lx + 140 > W) lx = x - 28 - 136;
            if (ly + 140 > H) ly = y - 28 - 136;
            Canvas.SetLeft(loupe, lx); Canvas.SetTop(loupe, ly);
            loupe.BorderBrush = new SolidColorBrush(InContextPhase ? Cyan : Gold);
        }

        void Refresh()
        {
            if (W <= 0) return;
            Geometry g = new RectangleGeometry(new Rect(0, 0, W, H));
            Action<Rect> cut = rr => { if (rr.Width > 0 && rr.Height > 0) { var c = rr; c.Inflate(2, 2); g = new CombinedGeometry(GeometryCombineMode.Exclude, g, new RectangleGeometry(c, 3, 3)); } };

            Place(wordBox, wordTag, wordRect, cut);
            Place(ctxBox, ctxTag, ctxRect, cut);
            PlaceDot(wordDot, wordTag, wordPt);
            PlaceDot(ctxDot, ctxTag, ctxPt);

            if (dragging && (dragRect.Width > 0 || dragRect.Height > 0))
            {
                var c = InContextPhase ? Cyan : Gold;
                dragBox.Stroke = new SolidColorBrush(c);
                var d = Dip(dragRect);
                dragBox.Width = d.Width; dragBox.Height = d.Height; Canvas.SetLeft(dragBox, d.X); Canvas.SetTop(dragBox, d.Y);
                dragBox.Visibility = Visibility.Visible;
                cut(d);
            }
            else dragBox.Visibility = Visibility.Collapsed;
            dim.Data = g;

            // Hinweisleiste
            var gold = new SolidColorBrush(Gold); var cyan = new SolidColorBrush(Cyan);
            var idle = new SolidColorBrush(Color.FromRgb(0x2C, 0x32, 0x3D)); var dark = new SolidColorBrush(Color.FromRgb(0x14, 0x12, 0x0A));
            var text = new SolidColorBrush(Color.FromRgb(0xE9, 0xEC, 0xF1));
            bool p2 = InContextPhase;
            step1Num.Background = p2 ? idle : gold; ((TextBlock)step1Num.Child).Text = p2 ? "✓" : "1";
            ((TextBlock)step1Num.Child).Foreground = p2 ? gold : dark;
            step1Text.Foreground = p2 ? Muted() : text;
            step2Num.Background = p2 ? cyan : idle; ((TextBlock)step2Num.Child).Foreground = p2 ? dark : Muted();
            step2Text.Foreground = p2 ? text : Muted();
        }

        bool demo;

        /// <summary>Für den Selbsttest: Overlay mit vorgegebener Auswahl als Bild speichern, ohne es anzuzeigen.</summary>
        public void SaveDemoImage(string path, D.Rectangle word, D.Rectangle ctx, D.Point cursor)
        {
            demo = true;
            Content = null;
            wordRect = word; ctxRect = ctx;
            grid.Measure(new Size(mon.Width, mon.Height));
            grid.Arrange(new Rect(0, 0, mon.Width, mon.Height));
            grid.UpdateLayout();
            scale = W / mon.Width;
            Refresh(); ShowCursorAt(cursor);
            grid.UpdateLayout();
            Shots.Save(grid, path, mon.Width, mon.Height);
        }

        void Place(Rectangle box, Border tag, D.Rectangle? r, Action<Rect> cut)
        {
            if (!r.HasValue) { box.Visibility = Visibility.Collapsed; if (tag.Tag == box) tag.Visibility = Visibility.Collapsed; return; }
            var d = Dip(r.Value);
            box.Width = d.Width; box.Height = d.Height; Canvas.SetLeft(box, d.X); Canvas.SetTop(box, d.Y);
            box.Visibility = Visibility.Visible;
            tag.Tag = box;
            Canvas.SetLeft(tag, d.X); Canvas.SetTop(tag, Math.Max(0, d.Y - 22));
            tag.Visibility = Visibility.Visible;
            cut(d);
        }
        void PlaceDot(Ellipse dot, Border tag, D.Point? p)
        {
            if (!p.HasValue) { dot.Visibility = Visibility.Collapsed; if (tag.Tag == dot) tag.Visibility = Visibility.Collapsed; return; }
            double x = p.Value.X * scale, y = p.Value.Y * scale;
            Canvas.SetLeft(dot, x - 9); Canvas.SetTop(dot, y - 9);
            dot.Visibility = Visibility.Visible;
            tag.Tag = dot;
            Canvas.SetLeft(tag, x + 12); Canvas.SetTop(tag, Math.Max(0, y - 26));
            tag.Visibility = Visibility.Visible;
        }
    }
}
