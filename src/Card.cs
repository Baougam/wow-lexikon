using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using D = System.Drawing;

namespace WowLexikon
{
    public class LookupState
    {
        public string Sense, ContextTrHighlight, QuestTitle;
        public bool OfficialContext;
        public string Term, Context, Translation, Ipa, Pos, Definition, Note, Meaning, Example, ExampleTr, ContextTr, Audio, Error, Warn, Status;
        public List<string> Alternatives = new List<string>();
        public List<string> Sources = new List<string>();
        public bool Loading, FromGlossary, Starred;
    }

    /// <summary>Die schwebende Ergebnis-Karte neben dem Wort.</summary>
    class CardWindow : Window
    {
        const string Xaml = @"
<Grid " + Theme.Ns + @">
  <Grid.Resources>
    <!-- Symbolknöpfe der Karte: Farben kommen aus dem gewählten Stil (Pergament braucht dunkle Symbole) -->
    <Style x:Key='CI' TargetType='Button'>
      <Setter Property='Foreground' Value='{DynamicResource IconFg}'/>
      <Setter Property='FontFamily' Value='{StaticResource Icons}'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Width' Value='30'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Focusable' Value='False'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='B' Background='Transparent' CornerRadius='6'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='B' Property='Background' Value='{DynamicResource IconHoverBg}'/>
                <Setter Property='Foreground' Value='{DynamicResource IconHoverFg}'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.35'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <!-- Eckverzierung (oben links; die anderen Ecken werden gespiegelt) -->
    <Style x:Key='Corner' TargetType='Path'>
      <Setter Property='Data' Value='M0,0 L20,0 L20,2.5 L2.5,2.5 L2.5,20 L0,20 Z M6,6 L10,2.5 L14,6 L10,9.5 Z M2.5,10 L6,14 L2.5,18 Z'/>
      <Setter Property='Fill' Value='{DynamicResource OrnamentBrush}'/>
      <Setter Property='Width' Value='20'/>
      <Setter Property='Height' Value='20'/>
      <Setter Property='Visibility' Value='{DynamicResource OrnamentVis}'/>
      <Setter Property='IsHitTestVisible' Value='False'/>
      <Setter Property='RenderTransformOrigin' Value='0.5,0.5'/>
    </Style>
  </Grid.Resources>
  <Border x:Name='Root' Margin='22' Width='400' CornerRadius='{DynamicResource CardRadius}' Background='{DynamicResource CardBg}'
          BorderBrush='{DynamicResource CardBorder}' BorderThickness='{DynamicResource CardBorderThickness}'>
    <Border.Effect><DropShadowEffect BlurRadius='26' ShadowDepth='6' Opacity='0.6'/></Border.Effect>
    <Border.RenderTransform><TranslateTransform x:Name='Slide'/></Border.RenderTransform>
    <Grid>
      <Border IsHitTestVisible='False' Background='{DynamicResource CardTexture}' CornerRadius='{DynamicResource CardRadius}'/>
      <Border IsHitTestVisible='False' Margin='3' BorderThickness='1' BorderBrush='{DynamicResource CardInner}' CornerRadius='{DynamicResource CardRadius}'/>
      <Path Style='{StaticResource Corner}' HorizontalAlignment='Left' VerticalAlignment='Top' Margin='1'/>
      <Path Style='{StaticResource Corner}' HorizontalAlignment='Right' VerticalAlignment='Top' Margin='1'><Path.RenderTransform><ScaleTransform ScaleX='-1'/></Path.RenderTransform></Path>
      <Path Style='{StaticResource Corner}' HorizontalAlignment='Left' VerticalAlignment='Bottom' Margin='1'><Path.RenderTransform><ScaleTransform ScaleY='-1'/></Path.RenderTransform></Path>
      <Path Style='{StaticResource Corner}' HorizontalAlignment='Right' VerticalAlignment='Bottom' Margin='1'><Path.RenderTransform><ScaleTransform ScaleX='-1' ScaleY='-1'/></Path.RenderTransform></Path>
      <Canvas x:Name='BarHost' Height='2' ClipToBounds='True' VerticalAlignment='Top' Margin='16,4,16,0'>
        <Rectangle Width='110' Height='2' RadiusX='1' RadiusY='1'>
          <Rectangle.Fill>
            <LinearGradientBrush StartPoint='0,0' EndPoint='1,0'>
              <GradientStop Color='#00D9A33A' Offset='0'/><GradientStop Color='#FFD9A33A' Offset='0.5'/><GradientStop Color='#00D9A33A' Offset='1'/>
            </LinearGradientBrush>
          </Rectangle.Fill>
          <Rectangle.RenderTransform><TranslateTransform x:Name='BarX'/></Rectangle.RenderTransform>
        </Rectangle>
      </Canvas>
      <DockPanel>
      <!-- Titelleiste wie beim Questfenster (dunkles Metall, helle Schrift, roter Schließen-Knopf) -->
      <Border x:Name='TitleBar' DockPanel.Dock='Top' Height='28' Background='{DynamicResource TitleBarBg}' Visibility='{DynamicResource TitleBarVis}'
              BorderBrush='#FF5A5A5A' BorderThickness='0,0,0,1'>
        <Grid>
          <TextBlock x:Name='TitleText' Text='Wörterbuch' HorizontalAlignment='Center' VerticalAlignment='Center' FontSize='13'
                     FontFamily='{DynamicResource BodyFont}' Foreground='{DynamicResource TitleBarFg}'/>
          <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,4,0'>
            <Button x:Name='Pin2' Width='22' Height='22' Cursor='Hand' Focusable='False' ToolTip='Anheften (bleibt offen)' Margin='0,0,4,0'>
              <Button.Template>
                <ControlTemplate TargetType='Button'>
                  <Border x:Name='B' Background='Transparent' CornerRadius='3'>
                    <TextBlock x:Name='T' Text='&#xE718;' FontFamily='{StaticResource Icons}' FontSize='12' Foreground='#FFB8B0A0' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='Foreground' Value='#FFFFD100'/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Button.Template>
            </Button>
            <Button x:Name='Close2' Width='20' Height='20' Cursor='Hand' Focusable='False' ToolTip='Schließen'>
              <Button.Template>
                <ControlTemplate TargetType='Button'>
                  <Border x:Name='B' CornerRadius='3' BorderBrush='#FFC9A24A' BorderThickness='1'>
                    <Border.Background>
                      <LinearGradientBrush StartPoint='0,0' EndPoint='0,1'><GradientStop Color='#FFB02A12' Offset='0'/><GradientStop Color='#FF6A0F05' Offset='1'/></LinearGradientBrush>
                    </Border.Background>
                    <TextBlock Text='&#xE711;' FontFamily='{StaticResource Icons}' FontSize='9' FontWeight='Bold' Foreground='#FFFFD100' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#FFFFE27A'/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Button.Template>
            </Button>
          </StackPanel>
        </Grid>
      </Border>
      <StackPanel x:Name='Body' Margin='20,14,14,14' TextElement.FontFamily='{DynamicResource BodyFont}' TextElement.Foreground='{DynamicResource MainText}'>
        <Grid>
          <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
          <TextBox x:Name='Term' FontSize='25' Foreground='{DynamicResource TermBrush}' FontFamily='{DynamicResource HeadFont}' TextWrapping='Wrap' Margin='-1,0,8,0' Padding='0' Cursor='IBeam'
                   CaretBrush='{DynamicResource TermBrush}' ToolTip='Falsch erkannt? Anklicken, korrigieren, Enter.'>
            <TextBox.Template>
              <ControlTemplate TargetType='TextBox'>
                <Border x:Name='B' Background='Transparent' BorderBrush='Transparent' BorderThickness='0,0,0,1'>
                  <ScrollViewer x:Name='PART_ContentHost'/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='{DynamicResource LineBrush}'/></Trigger>
                  <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='{DynamicResource HighlightBrush}'/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </TextBox.Template>
          </TextBox>
          <StackPanel Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Top' Visibility='{DynamicResource BodyButtonsVis}'>
            <Button x:Name='Pin' Style='{StaticResource CI}' Content='&#xE718;' ToolTip='Anheften (bleibt offen)'/>
            <Button x:Name='Close' Style='{StaticResource CI}' Content='&#xE711;' ToolTip='Schließen'/>
          </StackPanel>
        </Grid>
        <WrapPanel Margin='0,1,0,0'>
          <TextBlock x:Name='Ipa' Foreground='{DynamicResource IpaBrush}' FontSize='15' VerticalAlignment='Center' Margin='0,0,8,0' FontFamily='Segoe UI'/>
          <TextBlock x:Name='Pos' Foreground='{DynamicResource FaintText}' FontSize='12' FontStyle='Italic' VerticalAlignment='Center' Margin='0,0,6,0'/>
          <Button x:Name='Speak' Style='{StaticResource CI}' Content='&#xE767;' ToolTip='Vorlesen'/>
          <Button x:Name='SpeakSlow' Style='{StaticResource CI}' Width='38' FontFamily='Segoe UI' FontSize='11' FontWeight='SemiBold' Content='0,6×' ToolTip='Langsam vorlesen'/>
          <Button x:Name='Audio' Style='{StaticResource CI}' Content='&#xE7F6;' ToolTip='Echte Aufnahme anhören (Muttersprachler)'/>
        </WrapPanel>
        <TextBlock x:Name='Status' Foreground='{DynamicResource FaintText}' FontSize='13' Margin='0,8,0,0'/>
        <TextBlock x:Name='Trans' FontSize='21' FontWeight='SemiBold' Foreground='{DynamicResource TransBrush}' FontFamily='{DynamicResource HeadFont}' TextWrapping='Wrap' Margin='0,6,6,0'/>
        <WrapPanel x:Name='Alts' Margin='0,6,0,0'/>
        <TextBlock x:Name='Note' Foreground='{DynamicResource NoteBrush}' FontSize='13' FontStyle='Italic' TextWrapping='Wrap' Margin='0,8,6,0'/>
        <TextBlock x:Name='Def' Foreground='{DynamicResource FaintText}' FontSize='12' TextWrapping='Wrap' Margin='0,6,6,0'/>
        <StackPanel x:Name='CtxBox' Margin='0,2,6,0'>
          <Border Height='1' Background='{DynamicResource LineBrush}' Margin='0,10,0,9'/>
          <DockPanel>
            <TextBlock x:Name='QuestTitle' DockPanel.Dock='Right' Foreground='{DynamicResource FaintText}' FontSize='11.5' FontStyle='Italic' TextTrimming='CharacterEllipsis' MaxWidth='220' VerticalAlignment='Center'/>
            <TextBlock x:Name='CtxHead' Text='IM KONTEXT' Foreground='{DynamicResource CtxHeadBrush}' FontFamily='{DynamicResource HeadFont}' FontSize='12.5' FontWeight='Bold' VerticalAlignment='Center'/>
          </DockPanel>
          <TextBlock x:Name='Sense' Foreground='{DynamicResource HighlightBrush}' FontSize='13.5' FontWeight='SemiBold' TextWrapping='Wrap' Margin='0,5,0,2'/>
          <TextBlock x:Name='CtxText' FontSize='13.5' TextWrapping='Wrap' Margin='0,5,0,0'/>
          <TextBlock x:Name='CtxTr' Foreground='{DynamicResource SoftText}' FontSize='13.5' FontStyle='Italic' TextWrapping='Wrap' Margin='0,5,0,0'/>
          <TextBlock x:Name='Meaning' Foreground='{DynamicResource NoteBrush}' FontSize='12.5' TextWrapping='Wrap' Margin='0,6,0,0'/>
        </StackPanel>
        <StackPanel x:Name='ExBox' Margin='0,2,6,0'>
          <Border Height='1' Background='{DynamicResource LineBrush}' Margin='0,10,0,6'/>
          <DockPanel>
            <Button x:Name='SpeakEx' DockPanel.Dock='Right' Style='{StaticResource CI}' Content='&#xE767;' Width='26' Height='24' FontSize='12' ToolTip='Beispiel vorlesen'/>
            <TextBlock Text='Beispiel' Foreground='{DynamicResource ExHeadBrush}' FontFamily='{DynamicResource HeadFont}' FontSize='12.5' FontWeight='Bold' VerticalAlignment='Center'/>
          </DockPanel>
          <TextBlock x:Name='Ex' FontSize='13.5' TextWrapping='Wrap' Margin='0,2,0,0'/>
          <TextBlock x:Name='ExTr' Foreground='{DynamicResource SoftText}' FontSize='13.5' FontStyle='Italic' TextWrapping='Wrap' Margin='0,3,0,0'/>
        </StackPanel>
        <TextBlock x:Name='Err' Foreground='{DynamicResource DangerBrush}' FontSize='12' TextWrapping='Wrap' Margin='0,8,6,0'/>
        <TextBlock x:Name='Src' Foreground='{DynamicResource FaintText}' FontSize='11' Margin='0,10,0,0' TextTrimming='CharacterEllipsis'/>
        <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,8,0,0'>
          <Button x:Name='SpeakBtn' Style='{DynamicResource FooterBtn}' Content='Vorlesen' ToolTip='Wort vorlesen'/>
          <Button x:Name='Star' Style='{DynamicResource FooterBtn}' Content='Merken' ToolTip='In die Vokabelliste'/>
          <Button x:Name='Copy' Style='{DynamicResource FooterBtn}' Content='Kopieren' ToolTip='Wort und Übersetzung kopieren'/>
        </StackPanel>
      </StackPanel>
      </DockPanel>
    </Grid>
  </Border>
</Grid>";

        readonly FrameworkElement ui;
        readonly Border root;
        readonly TextBox term;
        readonly TextBlock sense, questTitle, ctxHead;
        readonly TextBlock ipa, pos, status, trans, note, def, ctxText, ctxTr, meaning, ex, exTr, err, src;
        readonly WrapPanel alts;
        readonly StackPanel ctxBox, exBox;
        readonly Button pin, close, speak, speakSlow, audio, speakEx, star, copy;
        readonly Canvas barHost;
        readonly TranslateTransform barX, slide;
        readonly DispatcherTimer hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        IntPtr hwnd;
        D.Rectangle anchor;
        bool pinned, userMoved, barRunning;
        int hideSeconds, remaining;

        public LookupState State { get; private set; }
        public event Action<string> Relookup;
        public event Action<bool> SpeakTerm;
        public event Action SpeakExample, PlayAudio, StarClicked;

        public CardWindow()
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; Left = -10000; Top = -10000; Title = "WoW Lexikon";

            ui = Theme.Parse<FrameworkElement>(Xaml);
            Content = ui;
            root = (Border)F("Root"); term = (TextBox)F("Term");
            ipa = T("Ipa"); pos = T("Pos"); status = T("Status"); trans = T("Trans"); note = T("Note"); def = T("Def");
            sense = T("Sense"); ctxText = T("CtxText"); questTitle = T("QuestTitle"); ctxHead = T("CtxHead"); ctxTr = T("CtxTr"); meaning = T("Meaning"); ex = T("Ex"); exTr = T("ExTr"); err = T("Err"); src = T("Src");
            alts = (WrapPanel)F("Alts"); ctxBox = (StackPanel)F("CtxBox"); exBox = (StackPanel)F("ExBox");
            pin = B("Pin"); close = B("Close"); speak = B("Speak"); speakSlow = B("SpeakSlow"); audio = B("Audio");
            speakEx = B("SpeakEx"); star = B("Star"); copy = B("Copy");
            barHost = (Canvas)F("BarHost"); barX = (TranslateTransform)F("BarX"); slide = (TranslateTransform)F("Slide");

            close.Click += (s, e) => HideCard();
            B("Close2").Click += (s, e) => HideCard();
            B("Pin2").Click += (s, e) => { pinned = !pinned; UpdatePin(); };
            B("SpeakBtn").Click += (s, e) => { if (SpeakTerm != null) SpeakTerm(false); };
            pin.Click += (s, e) => { pinned = !pinned; UpdatePin(); };
            speak.Click += (s, e) => { if (SpeakTerm != null) SpeakTerm(false); };
            speakSlow.Click += (s, e) => { if (SpeakTerm != null) SpeakTerm(true); };
            audio.Click += (s, e) => { if (PlayAudio != null) PlayAudio(); };
            speakEx.Click += (s, e) => { if (SpeakExample != null) SpeakExample(); };
            star.Click += (s, e) => { if (StarClicked != null) StarClicked(); };
            copy.Click += (s, e) =>
            {
                if (State == null) return;
                try { Clipboard.SetText(State.Term + " – " + (State.Translation ?? "") + (string.IsNullOrEmpty(State.Ipa) ? "" : "  /" + State.Ipa + "/")); } catch { }
                Flash(copy, "Kopiert ✓");
            };

            term.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (term.IsKeyboardFocused) return;
                Native.SetNoActivate(hwnd, false);
                Activate(); term.Focus();
                pinned = true; UpdatePin();
            };
            term.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; string t = term.Text.Trim(); if (t.Length > 0 && Relookup != null) Relookup(t); }
                else if (e.Key == Key.Escape) { e.Handled = true; HideCard(); }
            };

            root.MouseLeftButtonDown += (s, e) =>
            {
                if (IsInside<ButtonBase>(e.OriginalSource as DependencyObject) || IsInside<TextBox>(e.OriginalSource as DependencyObject)) return;
                pinned = true; userMoved = true; UpdatePin();
                try { DragMove(); } catch { }
            };

            SourceInitialized += (s, e) => { hwnd = new WindowInteropHelper(this).Handle; Native.SetNoActivate(hwnd, true); };
            SizeChanged += (s, e) => Reposition();
            hideTimer.Tick += (s, e) => TickHide();
            Render(new LookupState());
        }

        object F(string n) { return ui.FindName(n); }
        TextBlock T(string n) { return (TextBlock)F(n); }
        Button B(string n) { return (Button)F(n); }

        static bool IsInside<TT>(DependencyObject d) where TT : DependencyObject
        {
            while (d != null) { if (d is TT) return true; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            return false;
        }

        // ---------------- Anzeigen / Ausblenden ----------------

        /// <summary>Suchfeld: Karte in der Bildschirmmitte öffnen, Cursor im Wortfeld – tippen, Enter.</summary>
        public void ShowForInput()
        {
            var wa = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            Render(new LookupState { Status = "Wort oder Satz eintippen und Enter drücken" });
            ShowAt(new D.Rectangle(wa.Left + wa.Width / 2 - 220, wa.Top + wa.Height / 3, 1, 1));
            pinned = true; UpdatePin();
            Native.SetNoActivate(hwnd, false);
            Activate();
            term.Text = "";
            term.Focus(); Keyboard.Focus(term);
        }

        public void ShowAt(D.Rectangle anchorScreen)
        {
            anchor = anchorScreen; userMoved = false; pinned = false; UpdatePin();
            hideTimer.Stop();
            bool wasVisible = IsVisible;
            if (!wasVisible) { root.Opacity = 0; Show(); }
            Reposition();
            if (!wasVisible)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
                slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
            }
        }

        public void HideNow()
        {
            hideTimer.Stop();
            root.BeginAnimation(OpacityProperty, null);
            Hide();
            if (hwnd != IntPtr.Zero) Native.SetNoActivate(hwnd, true);
        }

        public void HideCard()
        {
            hideTimer.Stop();
            var a = new DoubleAnimation(root.Opacity, 0, TimeSpan.FromMilliseconds(160));
            a.Completed += (s, e) => { if (root.Opacity < 0.05) HideNow(); };
            root.BeginAnimation(OpacityProperty, a);
        }

        public void StartAutoHide(int seconds)
        {
            hideSeconds = seconds; remaining = seconds;
            if (seconds > 0) hideTimer.Start(); else hideTimer.Stop();
        }

        void TickHide()
        {
            if (!IsVisible) { hideTimer.Stop(); return; }
            if (pinned || root.IsMouseOver || term.IsKeyboardFocused) { remaining = hideSeconds; return; }
            if (--remaining <= 0) HideCard();
        }

        void UpdatePin()
        {
            pin.Content = pinned ? "" : "";
            if (pinned) pin.SetResourceReference(ForegroundProperty, "HighlightBrush"); else pin.ClearValue(ForegroundProperty);
            B("Pin2").Opacity = pinned ? 1.0 : 0.7;
            B("Pin2").Content = pinned ? "angeheftet" : null;
        }

        void Reposition()
        {
            if (userMoved || hwnd == IntPtr.Zero || !IsVisible || ActualWidth <= 0) return;
            var wr = Native.WindowRect(hwnd);
            int w = wr.Width, h = wr.Height;
            double k = w / ActualWidth;                  // Bildschirm-Pixel pro DIP
            int m = (int)(22 * k);                       // Rand für den Schatten
            var wa = System.Windows.Forms.Screen.FromRectangle(anchor).WorkingArea;
            int x = anchor.Right + 10 - m, y = anchor.Top - m - 6;
            if (x + w - m > wa.Right)
            {
                x = anchor.Left - w + m - 10;            // links vom Wort
                if (x + m < wa.Left) { x = Math.Max(wa.Left - m, Math.Min(anchor.Left - m, wa.Right - w + m)); y = anchor.Bottom + 8 - m; }
            }
            if (y + h - m > wa.Bottom) y = wa.Bottom - h + m;
            if (y + m < wa.Top) y = wa.Top - m;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        // ---------------- Inhalt ----------------

        public void Render(LookupState s)
        {
            State = s;
            if (!term.IsKeyboardFocused) term.Text = s.Term ?? "";
            Set(ipa, string.IsNullOrEmpty(s.Ipa) ? null : "/" + s.Ipa + "/");
            Set(pos, s.Pos);
            bool hasTerm = !string.IsNullOrEmpty(s.Term);
            speak.Visibility = speakSlow.Visibility = hasTerm ? Visibility.Visible : Visibility.Collapsed;
            audio.Visibility = string.IsNullOrEmpty(s.Audio) ? Visibility.Collapsed : Visibility.Visible;
            Set(status, s.Translation == null && (s.Loading || s.Status != null) ? (s.Status ?? "Übersetze …") : null);
            Set(trans, s.Translation);
            Set(note, s.Note ?? s.Meaning);
            Set(def, string.IsNullOrEmpty(s.Definition) ? null : "„" + s.Definition + "“");

            alts.Children.Clear();
            foreach (var a in s.Alternatives)
            {
                if (a.Equals(s.Translation, StringComparison.OrdinalIgnoreCase)) continue;
                var chipText = new TextBlock { Text = a, FontSize = 12.5 };
                chipText.SetResourceReference(TextBlock.ForegroundProperty, "ChipFg");
                var chip = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 3, 9, 4), Margin = new Thickness(0, 0, 6, 6), Child = chipText };
                chip.SetResourceReference(Border.BackgroundProperty, "ChipBg");
                alts.Children.Add(chip);
            }
            alts.Visibility = alts.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Kontext mit hervorgehobenem Wort
            ctxBox.Visibility = string.IsNullOrEmpty(s.Context) ? Visibility.Collapsed : Visibility.Visible;
            ctxText.Inlines.Clear();
            if (!string.IsNullOrEmpty(s.Context)) Highlight(ctxText, s.Context, s.Term);
            Set(ctxTr, s.ContextTr);
            if (!string.IsNullOrEmpty(s.ContextTr) && !string.IsNullOrEmpty(s.ContextTrHighlight))
            {
                ctxTr.Inlines.Clear();
                Highlight(ctxTr, s.ContextTr, s.ContextTrHighlight);
            }
            Set(sense, s.Sense == null ? null : "Hier gemeint: " + s.Sense);
            Set(questTitle, s.QuestTitle == null ? null : "Quest: " + s.QuestTitle);
            ctxHead.Text = s.OfficialContext ? "IM QUESTTEXT" : "IM KONTEXT";
            Set(meaning, s.Note != null ? s.Meaning : null);

            exBox.Visibility = string.IsNullOrEmpty(s.Example) ? Visibility.Collapsed : Visibility.Visible;
            ex.Inlines.Clear();
            if (!string.IsNullOrEmpty(s.Example)) Highlight(ex, s.Example, s.Term);
            Set(exTr, s.ExampleTr);

            string e2 = s.Error ?? s.Warn;
            Set(err, e2);
            err.SetResourceReference(TextBlock.ForegroundProperty, s.Error != null ? "DangerBrush" : "NoteBrush");
            Set(src, s.Sources.Count > 0 ? "Quelle: " + string.Join(" · ", s.Sources) : null);
            star.Content = s.Starred ? "Gemerkt ✓" : "Merken";
            
            star.IsEnabled = copy.IsEnabled = !string.IsNullOrEmpty(s.Translation);

            if (s.Loading && !barRunning)
            {
                barRunning = true; barHost.Visibility = Visibility.Visible;
                barX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-110, 380, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            else if (!s.Loading && barRunning)
            {
                barRunning = false; barHost.Visibility = Visibility.Hidden;
                barX.BeginAnimation(TranslateTransform.XProperty, null);
            }
            if (!s.Loading) barHost.Visibility = Visibility.Hidden;
        }

        static void Set(TextBlock t, string v)
        {
            t.Text = v ?? "";
            t.Visibility = string.IsNullOrEmpty(v) ? Visibility.Collapsed : Visibility.Visible;
        }

        void Highlight(TextBlock tb, string text, string word)
        {
            int pos2 = 0;
            string w = (word ?? "").Trim();
            while (w.Length > 0)
            {
                int i = text.IndexOf(w, pos2, StringComparison.OrdinalIgnoreCase);
                if (i < 0) break;
                if (i > pos2) tb.Inlines.Add(new Run(text.Substring(pos2, i - pos2)));
                var hit = new Run(text.Substring(i, w.Length)) { FontWeight = FontWeights.SemiBold };
                hit.SetResourceReference(TextElement.ForegroundProperty, "HighlightBrush");   // folgt dem Stil auch nach dem Umschalten
                tb.Inlines.Add(hit);
                pos2 = i + w.Length;
            }
            if (pos2 < text.Length) tb.Inlines.Add(new Run(text.Substring(pos2)));
        }

        void Flash(Button b, string glyph)
        {
            var old = b.Content;
            b.Content = glyph;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            t.Tick += (s, e) => { t.Stop(); b.Content = old; };
            t.Start();
        }

        /// <summary>Für den Selbsttest: Karte als Bild speichern, ohne sie anzuzeigen.</summary>
        public void SaveImage(string path)
        {
            Content = null;
            for (int i = 0; i < 3; i++)   // mehrfach, bis Schriftwechsel und Umbrüche durchgerechnet sind
            {
                ui.InvalidateMeasure();
                ui.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                ui.Arrange(new Rect(ui.DesiredSize));
                ui.UpdateLayout();
            }
            Shots.Save(ui, path, (int)Math.Ceiling(ui.ActualWidth), (int)Math.Ceiling(ui.ActualHeight));
            Content = ui;
        }
    }

    static class Shots
    {
        public static void Save(FrameworkElement e, string path, int w, int h)
        {
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x3B, 0x33, 0x28)), null, new Rect(0, 0, w, h));
            rtb.Render(bg);
            rtb.Render(e);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using (var f = System.IO.File.Create(path)) enc.Save(f);
        }
    }
}
