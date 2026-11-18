using System;
using System.Windows;
using System.Windows.Markup;

namespace WowLexikon
{
    /// <summary>Gemeinsames dunkles Design. Wird zur Laufzeit als XAML geladen (kein Build-Werkzeug nötig).</summary>
    static class Theme
    {
        public const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

        public static T Parse<T>(string xaml) { return (T)XamlReader.Parse(xaml); }

        public static void Apply(Application app)
        {
            app.Resources.MergedDictionaries.Add(Parse<ResourceDictionary>(Xaml));
            ApplyLook(app, "modern", null);
        }

        public static string FontStatus = "";
        public static bool FontLoaded;

        static System.Windows.Media.Color Col(string hex) { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        static System.Windows.Media.SolidColorBrush B(string hex) { var b = new System.Windows.Media.SolidColorBrush(Col(hex)); b.Freeze(); return b; }
        static System.Windows.Media.Brush Lin(string a, string b, double angle)
        {
            var g = new System.Windows.Media.LinearGradientBrush(Col(a), Col(b), angle); g.Freeze(); return g;
        }

        /// <summary>
        /// Aussehen der Karte:
        ///  „quest“   = wie das Questlog: Pergament, dunkler Rahmen mit Goldlinie und Eckverzierungen, dunkle Schrift (Standard)
        ///  „tooltip“ = wie ein Tooltip im Spiel: dunkelblau, silberner Rahmen, Gold/Weiß/Grün
        ///  „modern“  = schlichtes dunkles Design
        /// Im Spiel-Look wird die Schrift aus der eigenen WoW-Installation geladen (FRIZQT__.TTF, Titel MORPHEUS.TTF).
        /// </summary>
        public static void ApplyLook(Application app, string look, string fontPath)
        {
            if (look == "game") look = "tooltip";
            var r = app.Resources;
            var ui = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
            System.Windows.Media.FontFamily body = ui, head = ui;
            FontStatus = ""; FontLoaded = false;
            if (look != "modern")
            {
                var friz = LoadFont(fontPath);
                string morphPath = string.IsNullOrEmpty(fontPath) ? null : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fontPath), "MORPHEUS.TTF");
                var morph = LoadFont(morphPath);
                FontLoaded = friz != null;
                FontStatus = friz != null ? "Spielschrift geladen" + (morph != null ? " (mit Questtitel-Schrift)" : "") + ": " + fontPath + "."
                                          : "Spielschrift nicht gefunden – Ersatzschrift wird genutzt.";
                body = friz ?? new System.Windows.Media.FontFamily("Palatino Linotype, Book Antiqua, Georgia");
                head = look == "quest" ? (morph ?? friz ?? new System.Windows.Media.FontFamily("Book Antiqua, Palatino Linotype, Georgia")) : body;
            }
            r["HeadFont"] = head; r["BodyFont"] = body;

            // Gemeinsame Teile: Titelleiste nur im Questfenster-Stil, Fußzeilen-Knöpfe je Stil
            r["TitleBarVis"] = look == "quest" ? Visibility.Visible : Visibility.Collapsed;
            r["BodyButtonsVis"] = look == "quest" ? Visibility.Collapsed : Visibility.Visible;
            r["FooterBtn"] = FooterStyle(look == "quest" ? "red" : look == "tooltip" ? "dark" : "flat");

            if (look == "quest")
            {
                // Wie das Questfenster im Spiel: sandfarbenes Pergament mit dunklen Rändern in grauem Metallrahmen
                var paper = new System.Windows.Media.DrawingBrush { TileMode = System.Windows.Media.TileMode.None, Stretch = System.Windows.Media.Stretch.Fill };
                var dg = new System.Windows.Media.DrawingGroup();
                dg.Children.Add(new System.Windows.Media.GeometryDrawing(Lin("#FFEDD7A8", "#FFDDBD80", 75), null, new System.Windows.Media.RectangleGeometry(new Rect(0, 0, 1, 1))));
                var vign = new System.Windows.Media.RadialGradientBrush { RadiusX = 0.72, RadiusY = 0.78 };
                vign.GradientStops.Add(new System.Windows.Media.GradientStop(Col("#00000000"), 0.5));
                vign.GradientStops.Add(new System.Windows.Media.GradientStop(Col("#3A6B4416"), 0.85));
                vign.GradientStops.Add(new System.Windows.Media.GradientStop(Col("#806B4416"), 1.0));
                dg.Children.Add(new System.Windows.Media.GeometryDrawing(vign, null, new System.Windows.Media.RectangleGeometry(new Rect(0, 0, 1, 1))));
                paper.Drawing = dg; paper.Freeze();
                r["CardBg"] = paper;
                r["CardTexture"] = Noise();
                r["CardBorder"] = Lin("#FF4C4C4C", "#FF1A1A1A", 90);
                r["CardInner"] = B("#B04A3112");
                r["CardRadius"] = new CornerRadius(4);
                r["CardBorderThickness"] = new Thickness(4);
                r["OrnamentVis"] = Visibility.Collapsed;
                r["OrnamentBrush"] = B("#00000000");
                r["TitleBarBg"] = Lin("#FF3C3C3C", "#FF151515", 90);
                r["TitleBarFg"] = B("#FFE6DFCB");
                r["TermBrush"] = B("#FF1C0F04");
                r["TransBrush"] = B("#FF5E1608");
                r["NoteBrush"] = B("#FF3E2A10");
                r["CtxHeadBrush"] = B("#FF1C0F04");
                r["ExHeadBrush"] = B("#FF1C0F04");
                r["HighlightBrush"] = B("#FF6E1A08");
                r["SoftText"] = B("#FF4A3418");
                r["MainText"] = B("#FF231407");
                r["IpaBrush"] = B("#FF5E4626");
                r["FaintText"] = B("#FF7A6038");
                r["LineBrush"] = B("#66704A1E");
                r["ChipBg"] = B("#22603C14"); r["ChipFg"] = B("#FF2E1D0A");
                r["IconFg"] = B("#FF5A4020"); r["IconHoverBg"] = B("#22402A10"); r["IconHoverFg"] = B("#FF1C0F04");
                r["DangerBrush"] = B("#FF8E1A0A");
                r["Accent"] = B("#FFF2C46D"); r["Accent2"] = B("#FF5FD4E8");
            }
            else if (look == "tooltip")
            {
                r["CardBg"] = Lin("#F2141530", "#F5080912", 90);
                r["CardTexture"] = B("#00000000");
                r["CardBorder"] = B("#FF8C8C96");
                r["CardInner"] = B("#40FFFFFF");
                r["CardRadius"] = new CornerRadius(6);
                r["CardBorderThickness"] = new Thickness(1.5);
                r["OrnamentVis"] = Visibility.Collapsed;
                r["OrnamentBrush"] = B("#00000000");
                r["TermBrush"] = B("#FFFFD100");
                r["TransBrush"] = B("#FFFFFFFF");
                r["NoteBrush"] = B("#FFFFD100");
                r["CtxHeadBrush"] = B("#FF1EFF00");
                r["ExHeadBrush"] = B("#FFFFD100");
                r["HighlightBrush"] = B("#FFFFD100");
                r["SoftText"] = B("#FFC8C8C8");
                r["MainText"] = B("#FFFFFFFF");
                r["IpaBrush"] = B("#FFA9B3C1");
                r["FaintText"] = B("#FF7A8290");
                r["LineBrush"] = B("#FF2A2D3E");
                r["ChipBg"] = B("#1CFFFFFF"); r["ChipFg"] = B("#FFD3D9E2");
                r["IconFg"] = B("#FF8E98A7"); r["IconHoverBg"] = B("#FF2C323D"); r["IconHoverFg"] = B("#FFE9ECF1");
                r["DangerBrush"] = B("#FFFF8A80");
                r["Accent"] = B("#FFFFD100"); r["Accent2"] = B("#FF1EFF00");
            }
            else
            {
                r["CardBg"] = B("#F5171A20");
                r["CardTexture"] = B("#00000000");
                r["CardBorder"] = Lin("#77F2C46D", "#225FD4E8", 45);
                r["CardInner"] = B("#00000000");
                r["CardRadius"] = new CornerRadius(14);
                r["CardBorderThickness"] = new Thickness(1);
                r["OrnamentVis"] = Visibility.Collapsed;
                r["OrnamentBrush"] = B("#00000000");
                r["TermBrush"] = B("#FFE9ECF1");
                r["TransBrush"] = B("#FFF2C46D");
                r["NoteBrush"] = B("#FFB9C1CD");
                r["CtxHeadBrush"] = B("#FF5FD4E8");
                r["ExHeadBrush"] = B("#D9F2C46D");
                r["HighlightBrush"] = B("#FFF2C46D");
                r["SoftText"] = B("#FF8E98A7");
                r["MainText"] = B("#FFE9ECF1");
                r["IpaBrush"] = B("#FFA9B3C1");
                r["FaintText"] = B("#FF5C6674");
                r["LineBrush"] = B("#FF262B35");
                r["ChipBg"] = B("#1CFFFFFF"); r["ChipFg"] = B("#FFD3D9E2");
                r["IconFg"] = B("#FF8E98A7"); r["IconHoverBg"] = B("#FF2C323D"); r["IconHoverFg"] = B("#FFE9ECF1");
                r["DangerBrush"] = B("#FFFF8A80");
                r["Accent"] = B("#FFF2C46D"); r["Accent2"] = B("#FF5FD4E8");
            }
        }

        /// <summary>Knöpfe unten in der Karte: „red“ wie Annehmen/Ablehnen im Questfenster, sonst dunkel bzw. flach.</summary>
        static Style FooterStyle(string kind)
        {
            string bg, border, fg, hover, pad;
            if (kind == "red") { bg = "<LinearGradientBrush StartPoint='0,0' EndPoint='0,1'><GradientStop Color='#FF9A2412' Offset='0'/><GradientStop Color='#FF6A0F05' Offset='0.55'/><GradientStop Color='#FF4E0A03' Offset='1'/></LinearGradientBrush>"; border = "#FF2A0703"; fg = "#FFFFD100"; hover = "#FFFFFFFF"; pad = "14,4"; }
            else if (kind == "dark") { bg = "<SolidColorBrush Color='#FF1E2030'/>"; border = "#FF5A5E70"; fg = "#FFFFD100"; hover = "#FFFFFFFF"; pad = "12,4"; }
            else { bg = "<SolidColorBrush Color='#14FFFFFF'/>"; border = "#26FFFFFF"; fg = "#FFD3D9E2"; hover = "#FFFFFFFF"; pad = "12,4"; }
            string x = @"
<Style TargetType='Button' " + Ns + @">
  <Setter Property='Foreground' Value='" + fg + @"'/>
  <Setter Property='FontFamily' Value='{DynamicResource BodyFont}'/>
  <Setter Property='FontSize' Value='12.5'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Focusable' Value='False'/>
  <Setter Property='Margin' Value='6,0,0,0'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Button'>
        <Border x:Name='B' BorderBrush='" + border + @"' BorderThickness='1' CornerRadius='" + (kind == "red" ? "3" : "6") + @"' Padding='" + pad + @"'>
          <Border.Background>" + bg + @"</Border.Background>
          <Grid>
            <Border BorderBrush='#30FFFFFF' BorderThickness='0,1,0,0' Margin='-" + pad.Split(',')[0] + @",-" + pad.Split(',')[1] + @",-" + pad.Split(',')[0] + @",0' CornerRadius='3' Visibility='" + (kind == "red" ? "Visible" : "Collapsed") + @"'/>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Grid>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='" + hover + @"'/></Trigger>
          <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.8'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.45'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            return Parse<Style>(x);
        }

        static System.Windows.Media.Brush noise;
        /// <summary>Feine Papierstruktur (zufällige Fasern/Flecken), wird gekachelt.</summary>
        static System.Windows.Media.Brush Noise()
        {
            if (noise != null) return noise;
            const int n = 160;
            var px = new byte[n * n * 4];
            var rnd = new Random(7);
            for (int i = 0; i < n * n; i++)
            {
                int v = rnd.Next(100);
                byte a = (byte)(v < 6 ? 34 : v < 30 ? 14 : 0);
                px[i * 4 + 0] = 0x18; px[i * 4 + 1] = 0x30; px[i * 4 + 2] = 0x50; px[i * 4 + 3] = a;
            }
            // vormultiplizierte Farbe
            for (int i = 0; i < n * n; i++) { int a = px[i * 4 + 3]; for (int c = 0; c < 3; c++) px[i * 4 + c] = (byte)(px[i * 4 + c] * a / 255); }
            var bmp = System.Windows.Media.Imaging.BitmapSource.Create(n, n, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, px, n * 4);
            bmp.Freeze();
            var b = new System.Windows.Media.ImageBrush(bmp) { TileMode = System.Windows.Media.TileMode.Tile, ViewportUnits = System.Windows.Media.BrushMappingMode.Absolute, Viewport = new Rect(0, 0, n, n) };
            b.Freeze();
            return noise = b;
        }

        /// <summary>Lädt eine Schriftdatei (z. B. FRIZQT__.TTF aus dem WoW-Ordner) direkt von der Festplatte.</summary>
        public static System.Windows.Media.FontFamily LoadFont(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            try
            {
                var gt = new System.Windows.Media.GlyphTypeface(new Uri(path));
                string fam = null;
                foreach (var kv in gt.FamilyNames) { fam = kv.Value; if (kv.Key.Name.StartsWith("en")) break; }
                string dir = System.IO.Path.GetDirectoryName(path).TrimEnd('\\') + "\\";
                return new System.Windows.Media.FontFamily(new Uri(dir), "./" + System.IO.Path.GetFileName(path) + "#" + fam);
            }
            catch { return null; }
        }

        static readonly string[] FontFiles = { "FRIZQT__.TTF", "frizqt__.ttf", "FRIZQT___CYR.TTF" };

        /// <summary>Sucht die Spielschrift: gespeicherter Pfad → Ordner des laufenden WoW → übliche Installationsorte.</summary>
        public static string FindGameFont(string saved)
        {
            if (!string.IsNullOrEmpty(saved) && System.IO.File.Exists(saved)) return saved;
            var dirs = new System.Collections.Generic.List<string>();
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    string n = p.ProcessName.ToLowerInvariant();
                    if (!(n.StartsWith("wow") || n.Contains("warcraft"))) continue;
                    string d = System.IO.Path.GetDirectoryName(p.MainModule.FileName);
                    dirs.Add(d); dirs.Add(System.IO.Path.GetDirectoryName(d));
                }
                catch { }
            }
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"C:\Games", @"D:\Games", @"D:\", @"C:\" })
                foreach (var name in new[] { "World of Warcraft", "WoW", "WoW Forever", "World of Warcraft Forever" })
                    foreach (var sub in new[] { "", "_classic_", "_classic_era_", "_retail_", "_anniversary_" })
                        dirs.Add(System.IO.Path.Combine(root, name, sub));
            foreach (var d in dirs)
                foreach (var f in FontFiles)
                {
                    try { string path = System.IO.Path.Combine(d, "Fonts", f); if (System.IO.File.Exists(path)) return path; } catch { }
                }
            return null;
        }

        const string Xaml = @"
<ResourceDictionary " + Ns + @">
  <SolidColorBrush x:Key='Accent' Color='#F2C46D'/>
  <SolidColorBrush x:Key='Accent2' Color='#5FD4E8'/>
  <SolidColorBrush x:Key='Bg' Color='#14171C'/>
  <SolidColorBrush x:Key='Panel' Color='#1B1F26'/>
  <SolidColorBrush x:Key='Field' Color='#232831'/>
  <SolidColorBrush x:Key='FieldHover' Color='#2A303A'/>
  <SolidColorBrush x:Key='Line' Color='#313744'/>
  <SolidColorBrush x:Key='Text' Color='#E9ECF1'/>
  <SolidColorBrush x:Key='Muted' Color='#8E98A7'/>
  <SolidColorBrush x:Key='Danger' Color='#FF8A80'/>
  <FontFamily x:Key='Icons'>Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>

  <Style x:Key='IconBtn' TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
    <Setter Property='FontFamily' Value='{StaticResource Icons}'/>
    <Setter Property='FontSize' Value='14'/>
    <Setter Property='Width' Value='30'/>
    <Setter Property='Height' Value='30'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='Transparent' CornerRadius='8'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#2C323D'/>
              <Setter Property='Foreground' Value='{StaticResource Text}'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#38404D'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Background' Value='{StaticResource Field}'/>
    <Setter Property='Padding' Value='16,8'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='8' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.88'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.75'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
    <Setter Property='Background' Value='{StaticResource Accent}'/>
    <Setter Property='Foreground' Value='#1A1508'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
  </Style>
  <Style x:Key='Link' TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Accent2}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <TextBlock x:Name='T' Text='{TemplateBinding Content}' Foreground='{TemplateBinding Foreground}'/>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='TextDecorations' Value='Underline'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='CaretBrush' Value='{StaticResource Text}'/>
    <Setter Property='SelectionBrush' Value='{StaticResource Accent}'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Padding' Value='10,7'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='B' Background='{StaticResource Field}' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='8'>
            <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#4A5262'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='{StaticResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='PasswordBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='CaretBrush' Value='{StaticResource Text}'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Padding' Value='10,7'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='PasswordBox'>
          <Border x:Name='B' Background='{StaticResource Field}' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='8'>
            <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='{StaticResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <StackPanel Orientation='Horizontal' Background='Transparent'>
            <Border x:Name='Box' Width='18' Height='18' CornerRadius='5' Background='{StaticResource Field}' BorderBrush='{StaticResource Line}' BorderThickness='1' VerticalAlignment='Center'>
              <Path x:Name='Mark' Data='M3,9 L7,13 L14,5' Stroke='#1A1508' StrokeThickness='2' Visibility='Collapsed' Margin='0,-1,0,0'/>
            </Border>
            <ContentPresenter Margin='10,0,0,0' VerticalAlignment='Center'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='Box' Property='Background' Value='{StaticResource Accent}'/>
              <Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource Accent}'/>
              <Setter TargetName='Mark' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ComboBoxItem'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBoxItem'>
          <Border x:Name='B' Background='Transparent' CornerRadius='6' Padding='10,7'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='B' Property='Background' Value='#2E3440'/></Trigger>
            <Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='{StaticResource Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='ComboBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBox'>
          <Grid>
            <ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='ToggleButton'>
                  <Border x:Name='B' Background='{StaticResource Field}' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='8'>
                    <Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,13,0' Data='M0,0 L4.5,4.5 L9,0' Stroke='{StaticResource Muted}' StrokeThickness='1.6'/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#4A5262'/></Trigger>
                    <Trigger Property='IsChecked' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='{StaticResource Accent}'/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter IsHitTestVisible='False' Margin='11,7,32,7' VerticalAlignment='Center'
                              Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'/>
            <Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>
              <Border Background='#1F242C' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='9' Margin='0,4,0,0' Padding='4'
                      MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='340'>
                <ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
                  <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='8'/>
    <Setter Property='MinWidth' Value='8'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'><Border Background='#3A4150' CornerRadius='4' Margin='1'/></ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ToolTip'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToolTip'>
          <Border Background='#262B34' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='6' Padding='8,5'>
            <ContentPresenter TextElement.FontSize='12'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
    }
}
