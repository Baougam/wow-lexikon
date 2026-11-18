using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinOcr = global::Windows.Media.Ocr;
using WinImg = global::Windows.Graphics.Imaging;
using WinStreams = global::Windows.Storage.Streams;
using WinFound = global::Windows.Foundation;

namespace WowLexikon
{
    public class OcrWord
    {
        public string Text; public RectangleF Box; public int Line;
        public PointF Center { get { return new PointF(Box.X + Box.Width / 2, Box.Y + Box.Height / 2); } }
    }

    public class OcrOutcome
    {
        public List<OcrWord> Words = new List<OcrWord>();
        public List<string> Lines = new List<string>();
        public string Text
        {
            get
            {
                // Silbentrennung am Zeilenende wieder zusammenfügen
                var sb = new System.Text.StringBuilder();
                foreach (var l in Lines)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] == '-') sb.Length--;
                    else if (sb.Length > 0) sb.Append(' ');
                    sb.Append(l);
                }
                return sb.ToString().Trim();
            }
        }
    }

    static class WinRt
    {
        /// <summary>Wartet auf eine WinRT-Operation, ohne die .NET-Hilfsbibliothek zu benötigen.</summary>
        public static Task<T> Await<T>(WinFound.IAsyncOperation<T> op)
        {
            var tcs = new TaskCompletionSource<T>();
            op.Completed = (o, s) =>
            {
                if (s == WinFound.AsyncStatus.Completed) tcs.TrySetResult(o.GetResults());
                else if (s == WinFound.AsyncStatus.Error) tcs.TrySetException(o.ErrorCode);
                else tcs.TrySetCanceled();
            };
            return tcs.Task;
        }
    }

    static class Ocr
    {
        static readonly Dictionary<string, WinOcr.OcrEngine> engines = new Dictionary<string, WinOcr.OcrEngine>();

        /// <summary>Sucht ein installiertes Texterkennungs-Paket für den Sprachcode (z. B. "en" → "en-US").</summary>
        public static string FindLanguage(string code)
        {
            string prefix = code.Split('-')[0].ToLowerInvariant();
            string found = null;
            foreach (var l in WinOcr.OcrEngine.AvailableRecognizerLanguages)
            {
                string tag = l.LanguageTag;
                if (tag.Equals(code, StringComparison.OrdinalIgnoreCase)) return tag;
                if (found == null && tag.ToLowerInvariant().StartsWith(prefix)) found = tag;
            }
            return found;
        }

        public static List<string> Installed()
        {
            var r = new List<string>();
            foreach (var l in WinOcr.OcrEngine.AvailableRecognizerLanguages) r.Add(l.LanguageTag);
            return r;
        }

        static WinOcr.OcrEngine Engine(string code)
        {
            WinOcr.OcrEngine e;
            if (engines.TryGetValue(code, out e)) return e;
            string tag = FindLanguage(code);
            e = tag != null ? WinOcr.OcrEngine.TryCreateFromLanguage(new global::Windows.Globalization.Language(tag)) : null;
            if (e == null) e = WinOcr.OcrEngine.TryCreateFromUserProfileLanguages();
            engines[code] = e;
            return e;
        }

        /// <summary>Erkennt Text im Bereich <paramref name="region"/> des Bildes. Koordinaten der Wörter beziehen sich auf das Gesamtbild.</summary>
        public static async Task<OcrOutcome> Recognize(Bitmap src, Rectangle region, string lang)
        {
            region.Intersect(new Rectangle(0, 0, src.Width, src.Height));
            var outc = new OcrOutcome();
            if (region.Width < 4 || region.Height < 4) return outc;
            var engine = Engine(lang);
            if (engine == null) throw new InvalidOperationException("Keine Texterkennung installiert.");

            // Kleine Spielschrift vergrößern – Windows-OCR liest große Buchstaben deutlich besser.
            float scale = region.Height < 40 ? 4f : region.Height < 120 ? 3f : region.Height < 300 ? 2f : 1f;
            uint max = WinOcr.OcrEngine.MaxImageDimension;
            while (scale > 1f && (region.Width * scale > max || region.Height * scale > max)) scale -= 0.5f;

            outc = await RunOnce(engine, src, region, scale, false);
            if (outc.Words.Count == 0) outc = await RunOnce(engine, src, region, scale, true);
            return outc;
        }

        static async Task<OcrOutcome> RunOnce(WinOcr.OcrEngine engine, Bitmap src, Rectangle region, float scale, bool invert)
        {
            int w = Math.Max(1, (int)(region.Width * scale)), h = Math.Max(1, (int)(region.Height * scale));
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    if (invert)
                    {
                        // Graustufen + invertiert: hilft bei heller Schrift mit Schatten auf unruhigem Hintergrund
                        var cm = new ColorMatrix(new float[][] {
                            new float[] { -0.3f, -0.3f, -0.3f, 0, 0 },
                            new float[] { -0.59f, -0.59f, -0.59f, 0, 0 },
                            new float[] { -0.11f, -0.11f, -0.11f, 0, 0 },
                            new float[] { 0, 0, 0, 1, 0 },
                            new float[] { 1, 1, 1, 0, 1 } });
                        using (var ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm);
                            g.DrawImage(src, new Rectangle(0, 0, w, h), region.X, region.Y, region.Width, region.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                    else g.DrawImage(src, new Rectangle(0, 0, w, h), region, GraphicsUnit.Pixel);
                }
                var sb = ToSoftwareBitmap(bmp);
                var res = await WinRt.Await(engine.RecognizeAsync(sb));
                var outc = new OcrOutcome();
                int li = 0;
                foreach (var line in res.Lines)
                {
                    outc.Lines.Add(line.Text);
                    foreach (var word in line.Words)
                    {
                        var b = word.BoundingRect;
                        outc.Words.Add(new OcrWord
                        {
                            Text = word.Text,
                            Line = li,
                            Box = new RectangleF((float)(region.X + b.X / scale), (float)(region.Y + b.Y / scale), (float)(b.Width / scale), (float)(b.Height / scale))
                        });
                    }
                    li++;
                }
                return outc;
            }
        }

        static WinImg.SoftwareBitmap ToSoftwareBitmap(Bitmap bmp)
        {
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var px = new byte[d.Stride * bmp.Height];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            bmp.UnlockBits(d);
            var dw = new WinStreams.DataWriter();
            dw.WriteBytes(px);
            return WinImg.SoftwareBitmap.CreateCopyFromBuffer(dw.DetachBuffer(), WinImg.BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height, WinImg.BitmapAlphaMode.Premultiplied);
        }

        // ---------------- Auswahl-Logik ----------------

        /// <summary>Wort an einem angeklickten Punkt.</summary>
        public static async Task<string> WordAt(Bitmap shot, Point p, string lang)
        {
            var region = new Rectangle(p.X - 260, p.Y - 36, 520, 72);
            var o = await Recognize(shot, region, lang);
            OcrWord best = null; double bestD = double.MaxValue;
            foreach (var w in o.Words)
            {
                var box = w.Box; box.Inflate(3, 4);
                if (box.Contains(p)) return Clean(w.Text);
                var c = w.Center;
                double dx = Math.Max(0, Math.Abs(p.X - c.X) - w.Box.Width / 2), dy = Math.Max(0, Math.Abs(p.Y - c.Y) - w.Box.Height / 2);
                double dist = dx + dy * 3;
                if (dist < bestD) { bestD = dist; best = w; }
            }
            return best != null && bestD < 40 ? Clean(best.Text) : "";
        }

        /// <summary>Text in einem gezogenen Rahmen.</summary>
        public static async Task<string> TextIn(Bitmap shot, Rectangle r, string lang)
        {
            var region = r; region.Inflate(3, 3);
            var o = await Recognize(shot, region, lang);
            return Clean(o.Text);
        }

        /// <summary>Ganze Zeile an einem Punkt (für Kontext per Klick).</summary>
        public static async Task<string> LineAt(Bitmap shot, Point p, string lang)
        {
            var region = new Rectangle(0, p.Y - 40, shot.Width, 80);
            var o = await Recognize(shot, region, lang);
            int line = -1; double bestD = double.MaxValue;
            foreach (var w in o.Words)
            {
                double d = Math.Abs(w.Center.Y - p.Y) + (w.Box.Left <= p.X && w.Box.Right >= p.X ? 0 : 20);
                if (d < bestD) { bestD = d; line = w.Line; }
            }
            if (line < 0) return "";
            // Nur zusammenhängende Wörter um den Klickpunkt nehmen (andere Fenster in derselben Höhe ignorieren)
            var words = o.Words.FindAll(w => w.Line == line);
            words.Sort((a, b) => a.Box.X.CompareTo(b.Box.X));
            int idx = 0; double near = double.MaxValue;
            for (int i = 0; i < words.Count; i++) { double d = Math.Abs(words[i].Center.X - p.X); if (d < near) { near = d; idx = i; } }
            int lo = idx, hi = idx;
            while (lo > 0 && words[lo].Box.Left - words[lo - 1].Box.Right < words[lo].Box.Height * 2.5) lo--;
            while (hi < words.Count - 1 && words[hi + 1].Box.Left - words[hi].Box.Right < words[hi].Box.Height * 2.5) hi++;
            var parts = new List<string>();
            for (int i = lo; i <= hi; i++) parts.Add(words[i].Text);
            return string.Join(" ", parts).Trim();
        }

        const string Trim = " \t\r\n.,;:!?\"'“”„‘’()[]{}<>«»*•·…–—_/\\|";
        public static string Clean(string s)
        {
            if (s == null) return "";
            s = s.Replace('’', '\'').Replace('‘', '\'');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
            s = s.Trim(Trim.ToCharArray());
            // Mengenangaben aus Questzielen entfernen („Murlocs slain: 8/10“)
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*:?\s*\d+\s*/\s*\d+$", "");
            return s.Trim(Trim.ToCharArray());
        }
    }

    static class ScreenShot
    {
        public static Bitmap Capture(Rectangle r)
        {
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        public static System.Windows.Media.Imaging.BitmapSource ToWpf(Bitmap bmp)
        {
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var src = System.Windows.Media.Imaging.BitmapSource.Create(bmp.Width, bmp.Height, 96, 96,
                    System.Windows.Media.PixelFormats.Pbgra32, null, d.Scan0, d.Stride * bmp.Height, d.Stride);
                src.Freeze();
                return src;
            }
            finally { bmp.UnlockBits(d); }
        }
    }
}
