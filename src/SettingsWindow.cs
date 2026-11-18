using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WowLexikon
{
    class SettingsWindow : Window
    {
        const string Xaml = @"
<Border " + Theme.Ns + @" Background='{StaticResource Bg}' CornerRadius='14' BorderBrush='#2B313C' BorderThickness='1' Margin='16'
        TextElement.Foreground='{StaticResource Text}' TextElement.FontFamily='Segoe UI Variable Text, Segoe UI'>
  <Border.Effect><DropShadowEffect BlurRadius='24' ShadowDepth='4' Opacity='0.5'/></Border.Effect>
  <Border.Resources>
    <Style x:Key='Sec' TargetType='Border'>
      <Setter Property='Background' Value='{StaticResource Panel}'/>
      <Setter Property='CornerRadius' Value='12'/>
      <Setter Property='Padding' Value='18,14,18,16'/>
      <Setter Property='Margin' Value='0,0,0,12'/>
      <Setter Property='BorderBrush' Value='#232832'/>
      <Setter Property='BorderThickness' Value='1'/>
    </Style>
    <Style x:Key='H' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='11'/><Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Foreground' Value='{DynamicResource Accent}'/><Setter Property='Margin' Value='0,0,0,2'/>
      <Setter Property='FontFamily' Value='{DynamicResource HeadFont}'/>
    </Style>
    <Style x:Key='L' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12'/><Setter Property='Foreground' Value='{StaticResource Muted}'/><Setter Property='Margin' Value='0,12,0,6'/>
    </Style>
    <Style x:Key='Info' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12'/><Setter Property='Foreground' Value='{StaticResource Muted}'/><Setter Property='TextWrapping' Value='Wrap'/><Setter Property='Margin' Value='0,8,0,0'/>
    </Style>
  </Border.Resources>
  <DockPanel>
    <Grid x:Name='TitleBar' DockPanel.Dock='Top' Background='Transparent' Height='64'>
      <StackPanel Orientation='Horizontal' Margin='22,0,0,0' VerticalAlignment='Center'>
        <Border Width='34' Height='34' CornerRadius='9' Background='{StaticResource Accent}'>
          <TextBlock Text='&#xE82D;' FontFamily='{StaticResource Icons}' FontSize='17' Foreground='#1A1508' HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border>
        <StackPanel Margin='12,0,0,0' VerticalAlignment='Center'>
          <TextBlock Text='WoW Lexikon' FontSize='17' FontWeight='SemiBold' FontFamily='{DynamicResource HeadFont}' Foreground='{DynamicResource Accent}'/>
          <TextBlock Text='Einstellungen' FontSize='12' Foreground='{StaticResource Muted}'/>
        </StackPanel>
      </StackPanel>
      <Button x:Name='X' Style='{StaticResource IconBtn}' Content='&#xE711;' HorizontalAlignment='Right' Margin='0,0,14,0'/>
    </Grid>
    <Border DockPanel.Dock='Bottom' BorderBrush='#232832' BorderThickness='0,1,0,0' Padding='22,14'>
      <DockPanel>
        <StackPanel Orientation='Horizontal' DockPanel.Dock='Right'>
          <Button x:Name='Cancel' Content='Abbrechen' Margin='0,0,10,0'/>
          <Button x:Name='Save' Content='Speichern' Style='{StaticResource Primary}'/>
        </StackPanel>
        <Button x:Name='OpenFolder' Style='{StaticResource Link}' Content='Datenordner öffnen' VerticalAlignment='Center'/>
      </DockPanel>
    </Border>
    <ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' Padding='22,2,14,8'>
      <StackPanel Margin='0,0,8,0'>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='SPRACHEN' Style='{StaticResource H}'/>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='14'/><ColumnDefinition/></Grid.ColumnDefinitions>
              <StackPanel><TextBlock Text='Spieltext ist auf' Style='{StaticResource L}'/><ComboBox x:Name='Src'/></StackPanel>
              <StackPanel Grid.Column='2'><TextBlock Text='Übersetzen nach' Style='{StaticResource L}'/><ComboBox x:Name='Tgt'/></StackPanel>
            </Grid>
            <TextBlock x:Name='OcrInfo' Style='{StaticResource Info}'/>
            <Button x:Name='LangSettings' Style='{StaticResource Link}' Content='Windows-Spracheinstellungen öffnen' Margin='0,6,0,0' HorizontalAlignment='Left'/>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='ADDON (IM SPIEL)' Style='{StaticResource H}'/>
            <TextBlock x:Name='AddonInfo' Style='{StaticResource Info}'/>
            <TextBlock Text='WoW-Ordner (leer = automatisch suchen)' Style='{StaticResource L}'/>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
              <TextBox x:Name='WowDir'/>
              <Button x:Name='WowPick' Grid.Column='1' Content='Wählen …' Margin='8,0,0,0'/>
              <Button x:Name='WowFind' Grid.Column='2' Content='Suchen' Margin='8,0,0,0'/>
            </Grid>
            <WrapPanel Margin='0,12,0,0'>
              <Button x:Name='AddonInstall' Content='Addon installieren / aktualisieren' Style='{StaticResource Primary}' Margin='0,0,8,8'/>
              <Button x:Name='AddonSyncNow' Content='Jetzt abgleichen' Margin='0,0,8,8'/>
              <Button x:Name='GameDataBuild' Content='Spieldaten erzeugen' Margin='0,0,8,8'/>
            </WrapPanel>
            <TextBlock x:Name='AddonStatus' Style='{StaticResource Info}'/>
            <CheckBox x:Name='AutoSync' Content='Automatisch abgleichen und Updates holen (nach dem Ausloggen und alle 6 Stunden)' Margin='0,12,0,0'/>
            <TextBlock Text='Update-Quelle (Adresse der manifest.json – leer = eingebaute Adresse)' Style='{StaticResource L}' TextWrapping='Wrap'/>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
              <TextBox x:Name='UpdateUrl'/>
              <Button x:Name='UpdateNow' Grid.Column='1' Content='Nach Updates suchen' Margin='8,0,0,0'/>
            </Grid>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='AUSSEHEN' Style='{StaticResource H}'/>
            <TextBlock Text='Stil der Ergebnis-Karte' Style='{StaticResource L}'/>
            <ComboBox x:Name='Look'/>
            <TextBlock x:Name='FontInfo' Style='{StaticResource Info}'/>
            <StackPanel Orientation='Horizontal' Margin='0,10,0,0'>
              <Button x:Name='FontPick' Content='Schriftdatei wählen …' Margin='0,0,10,0'/>
              <Button x:Name='FontFind' Content='Automatisch suchen'/>
            </StackPanel>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='BEDIENUNG' Style='{StaticResource H}'/>
            <TextBlock Text='Tastenkombination (gedrückt halten, dann Rahmen ziehen)' Style='{StaticResource L}'/>
            <ComboBox x:Name='Hotkey'/>
            <TextBlock Text='Ergebnis-Karte ausblenden nach' Style='{StaticResource L}'/>
            <ComboBox x:Name='Hide'/>
            <CheckBox x:Name='OnlyWow' Content='Nur aktiv, wenn WoW im Vordergrund ist' Margin='0,16,0,0'/>
            <CheckBox x:Name='Autostart' Content='Mit Windows starten' Margin='0,12,0,0'/>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='ÜBERSETZUNG' Style='{StaticResource H}'/>
            <TextBlock Text='Dienst' Style='{StaticResource L}'/>
            <ComboBox x:Name='Provider'/>
            <TextBlock x:Name='ProviderInfo' Style='{StaticResource Info}'/>
            <StackPanel x:Name='MmPanel'>
              <TextBlock Text='E-Mail für MyMemory (optional – erhöht das Tageskontingent auf das Zehnfache)' Style='{StaticResource L}' TextWrapping='Wrap'/>
              <TextBox x:Name='MmEmail'/>
            </StackPanel>
            <StackPanel x:Name='DlPanel'>
              <TextBlock Text='DeepL-API-Schlüssel (Free)' Style='{StaticResource L}'/>
              <PasswordBox x:Name='DlKey'/>
              <Button x:Name='DlLink' Style='{StaticResource Link}' Content='Kostenlosen DeepL-Schlüssel holen' Margin='0,8,0,0' HorizontalAlignment='Left'/>
            </StackPanel>
            <StackPanel x:Name='AiPanel'>
              <TextBlock Text='KI-Anbieter' Style='{StaticResource L}'/>
              <ComboBox x:Name='AiPreset'/>
              <TextBlock Text='Adresse' Style='{StaticResource L}'/>
              <TextBox x:Name='AiEndpoint'/>
              <TextBlock Text='Modell' Style='{StaticResource L}'/>
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                <TextBox x:Name='AiModel'/>
                <Button x:Name='AiModels' Grid.Column='1' Content='Modelle laden' Margin='8,0,0,0'/>
              </Grid>
              <ComboBox x:Name='AiModelList' Margin='0,8,0,0' Visibility='Collapsed'/>
              <TextBlock Text='API-Schlüssel' Style='{StaticResource L}'/>
              <PasswordBox x:Name='AiKey'/>
              <DockPanel Margin='0,10,0,0'>
                <Button x:Name='AiTest' DockPanel.Dock='Right' Content='Verbindung testen'/>
                <Button x:Name='AiLink' Style='{StaticResource Link}' Content='Kostenlosen Schlüssel holen' VerticalAlignment='Center' HorizontalAlignment='Left'/>
              </DockPanel>
              <TextBlock x:Name='AiStatus' Style='{StaticResource Info}'/>
            </StackPanel>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='AUSSPRACHE' Style='{StaticResource H}'/>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width='2*'/><ColumnDefinition Width='14'/><ColumnDefinition/></Grid.ColumnDefinitions>
              <StackPanel><TextBlock Text='Stimme' Style='{StaticResource L}'/><ComboBox x:Name='Voice'/></StackPanel>
              <StackPanel Grid.Column='2'><TextBlock Text='Tempo' Style='{StaticResource L}'/><ComboBox x:Name='Rate'/></StackPanel>
            </Grid>
            <TextBlock x:Name='VoiceInfo' Style='{StaticResource Info}'/>
            <DockPanel Margin='0,14,0,0'>
              <Button x:Name='TestVoice' DockPanel.Dock='Right' Content='Probe anhören'/>
              <CheckBox x:Name='AutoSpeak' Content='Wort automatisch vorlesen' VerticalAlignment='Center'/>
            </DockPanel>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Sec}'>
          <StackPanel>
            <TextBlock Text='WÖRTERBUCH' Style='{StaticResource H}'/>
            <TextBlock x:Name='GlossInfo' Style='{StaticResource Info}'/>
            <StackPanel Orientation='Horizontal' Margin='0,10,0,0'>
              <Button x:Name='EditGloss' Style='{StaticResource Link}' Content='Eigenes Glossar bearbeiten' Margin='0,0,18,0'/>
              <Button x:Name='OpenVocab' Style='{StaticResource Link}' Content='Vokabelliste öffnen'/>
            </StackPanel>
          </StackPanel>
        </Border>

      </StackPanel>
    </ScrollViewer>
  </DockPanel>
</Border>";

        readonly FrameworkElement ui;
        readonly Settings cfg;
        readonly Controller app;
        public event Action<Settings> Saved;

        ComboBox C(string n) { return (ComboBox)ui.FindName(n); }
        TextBox TB(string n) { return (TextBox)ui.FindName(n); }
        TextBlock TX(string n) { return (TextBlock)ui.FindName(n); }
        Button BT(string n) { return (Button)ui.FindName(n); }
        CheckBox CB(string n) { return (CheckBox)ui.FindName(n); }
        PasswordBox PB(string n) { return (PasswordBox)ui.FindName(n); }
        FrameworkElement FE(string n) { return (FrameworkElement)ui.FindName(n); }

        public SettingsWindow(Settings current, Controller app)
        {
            this.app = app;
            cfg = current.Clone();
            Title = "WoW Lexikon – Einstellungen";
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Width = 620; Height = 820; WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.NoResize;
            Icon = Tray.WpfIcon();
            ui = Theme.Parse<FrameworkElement>(Xaml);
            Content = ui;
            double maxH = SystemParameters.WorkArea.Height - 20;
            if (Height > maxH) Height = maxH;

            FE("TitleBar").MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            BT("X").Click += (s, e) => Close();
            BT("Cancel").Click += (s, e) => Close();
            BT("Save").Click += (s, e) => DoSave();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            BT("OpenFolder").Click += (s, e) => Open(Settings.Dir);
            BT("LangSettings").Click += (s, e) => Open("ms-settings:regionlanguage");
            BT("EditGloss").Click += (s, e) => Open(app.Glossary.UserFile);
            BT("OpenVocab").Click += (s, e) => { if (System.IO.File.Exists(Vocab.FilePath)) Open(Vocab.FilePath); else MessageBox.Show(this, "Die Vokabelliste ist noch leer. Klicke in der Ergebnis-Karte auf den Stern, um Wörter zu sammeln.", "WoW Lexikon"); };
            BT("DlLink").Click += (s, e) => Open("https://www.deepl.com/pro-api");

            // Sprachen
            foreach (var l in Lang.All) { Add(C("Src"), l.Name, l.Code); Add(C("Tgt"), l.Name, l.Code); }
            Select(C("Src"), cfg.SourceLang); Select(C("Tgt"), cfg.TargetLang);
            C("Src").SelectionChanged += (s, e) => { UpdateOcrInfo(); FillVoices(); };
            UpdateOcrInfo();

            // Aussehen – Änderungen sofort als Vorschau, bei „Abbrechen“ zurück
            Add(C("Look"), "Questlog – Pergament mit Goldrahmen (empfohlen)", "quest");
            Add(C("Look"), "Tooltip – dunkel wie Gegenstands-Tooltips", "tooltip");
            Add(C("Look"), "Modern – schlicht, dunkel", "modern");
            Select(C("Look"), cfg.Look == "game" ? "tooltip" : (cfg.Look ?? "quest"));
            if (C("Look").SelectedIndex < 0) Select(C("Look"), "quest");
            fontPath = cfg.GameFontPath;
            Action preview = () => { Theme.ApplyLook(Application.Current, Val(C("Look")), fontPath); UpdateFontInfo(); };
            C("Look").SelectionChanged += (s, e) => preview();
            BT("FontPick").Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Spielschrift auswählen (z. B. FRIZQT__.TTF im Ordner „Fonts“ von WoW)", Filter = "Schriften (*.ttf;*.otf)|*.ttf;*.otf" };
                if (dlg.ShowDialog(this) == true) { fontPath = dlg.FileName; if (Val(C("Look")) == "modern") Select(C("Look"), "quest"); preview(); }
            };
            BT("FontFind").Click += (s, e) =>
            {
                var found = Theme.FindGameFont(null);
                if (found != null) { fontPath = found; preview(); }
                else TX("FontInfo").Text = "Nicht gefunden. Tipp: WoW starten und erneut suchen – oder die Datei FRIZQT__.TTF im WoW-Ordner unter „Fonts“ selbst auswählen.";
            };
            Closed += (s, e) => { if (!saved) Theme.ApplyLook(Application.Current, current.Look, current.GameFontPath); };
            UpdateFontInfo();

            // Addon
            TB("WowDir").Text = cfg.WowFolder ?? "";
            TB("UpdateUrl").Text = cfg.UpdateUrl ?? "";
            CB("AutoSync").IsChecked = cfg.AutoSync;
            BT("WowPick").Click += (s, e) =>
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "WoW-Ordner auswählen (der Ordner mit „Interface“ und „WTF“ oder der Hauptordner „World of Warcraft“)" })
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) { TB("WowDir").Text = dlg.SelectedPath; UpdateAddonInfo(); }
            };
            BT("WowFind").Click += (s, e) => { var found = AddonSync.FindGameDirs(null); var d = found.Count == 0 ? null : (found.Count > 1 && Path.GetDirectoryName(found[0]) == Path.GetDirectoryName(found[1]) ? Path.GetDirectoryName(found[0]) : found[0]); TB("WowDir").Text = d ?? ""; UpdateAddonInfo(); if (d == null) AddonMsg("Nicht gefunden – WoW starten und erneut suchen oder den Ordner selbst wählen.", true); };
            TB("WowDir").LostFocus += (s, e) => UpdateAddonInfo();
            BT("AddonInstall").Click += async (s, e) => await AddonAction("Installiere …", async () =>
            {
                ApplyAddonFields();
                var sync = app.Addon ?? new AddonSync(app);
                var where = sync.InstallAddon(AddonSync.EmbeddedAddon());
                // Gefundenen Ordner merken, damit nicht jedes Mal gesucht werden muss
                if (string.IsNullOrEmpty(app.Cfg.WowFolder) && sync.GameDir != null)
                {
                    app.Cfg.WowFolder = sync.GameDir; app.Cfg.Save();
                    Dispatcher.BeginInvoke(new Action(() => TB("WowDir").Text = app.Cfg.WowFolder));
                }
                string ziel = "Installiert in:\n   " + string.Join("\n   ", where) + "\n";
                const string neustart = " Jetzt WoW KOMPLETT beenden und neu starten – neue Addons erkennt WoW nur beim Spielstart (/reload reicht nicht).";
                if (!QuestCorpus.Available(app.Cfg.TargetLang))
                    return ziel + " Für die deutschen Questtexte und Namen noch „Spieldaten erzeugen“ wählen." + neustart;
                int n = await Task.Run(() => sync.WriteGameDataAddon(app.Cfg.TargetLang));
                app.WatchSavedVariables();
                return ziel + " Mit " + n.ToString("N0") + " deutschen Questtexten und Namen." + neustart;
            });
            BT("AddonSyncNow").Click += async (s, e) => await AddonAction("Gleiche ab …", async () =>
            {
                ApplyAddonFields();
                var sync = app.Addon ?? new AddonSync(app);
                if (!sync.SavedVariableFiles().Any()) return "Noch keine Addon-Daten gefunden – einmal mit dem Addon einloggen und wieder ausloggen.";
                await sync.Sync(msg => Dispatcher.BeginInvoke(new Action(() => AddonMsg(msg, false))));
                return sync.LastMessage;
            });
            BT("GameDataBuild").Click += async (s, e) => await AddonAction("Erzeuge Spieldaten …", async () =>
            {
                ApplyAddonFields();
                await GameDataBuilder.Build(app.Cfg.TargetLang, msg => Dispatcher.BeginInvoke(new Action(() => AddonMsg(msg, false))));
                await app.LoadGameData();
                var sync = app.Addon ?? new AddonSync(app);
                if (sync.AddOnsDir != null && Directory.Exists(Path.Combine(sync.AddOnsDir, AddonSync.AddonName)))
                {
                    int n = await Task.Run(() => sync.WriteGameDataAddon(app.Cfg.TargetLang));
                    return "Spieldaten erzeugt und ins Addon geschrieben (" + n.ToString("N0") + " Quests). Im Spiel: /reload.";
                }
                return "Spieldaten erzeugt. Jetzt „Addon installieren“ wählen.";
            });
            BT("UpdateNow").Click += async (s, e) => await AddonAction("Suche nach Updates …", async () => { ApplyAddonFields(); return await app.CheckUpdatesNow(false); });
            if (string.IsNullOrEmpty(cfg.UpdateUrl) && !string.IsNullOrEmpty(Program.DefaultUpdateUrl)) TB("UpdateUrl").ToolTip = "Eingebaut: " + Program.DefaultUpdateUrl;
            UpdateAddonInfo();

            // Bedienung
            foreach (var h in HotkeyDef.All) Add(C("Hotkey"), h.Label, h.Id);
            Select(C("Hotkey"), cfg.Hotkey);
            foreach (var v in new[] { 0, 8, 15, 30, 60 }) Add(C("Hide"), v == 0 ? "Nie (nur über ✕ schließen)" : v + " Sekunden", v.ToString());
            Select(C("Hide"), cfg.AutoHideSeconds.ToString());
            CB("OnlyWow").IsChecked = cfg.OnlyInWow;
            CB("Autostart").IsChecked = Autostart.IsEnabled();

            // Übersetzung
            Add(C("Provider"), "Automatisch (empfohlen)", "auto");
            Add(C("Provider"), "Frei & ohne Konto – MyMemory", "mymemory");
            Add(C("Provider"), "DeepL Free – Schlüssel nötig", "deepl");
            Add(C("Provider"), "Freie KI – Schlüssel nötig, versteht Kontext", "ai");
            Select(C("Provider"), cfg.Provider);
            C("Provider").SelectionChanged += (s, e) => UpdateProvider();
            TB("MmEmail").Text = cfg.MyMemoryEmail;
            PB("DlKey").Password = cfg.GetDeepLKey();
            foreach (var p in AiPresets.All) Add(C("AiPreset"), p.Label, p.Id);
            Select(C("AiPreset"), cfg.AiPreset);
            TB("AiEndpoint").Text = cfg.AiEndpoint; TB("AiModel").Text = cfg.AiModel; PB("AiKey").Password = cfg.GetAiKey();
            C("AiPreset").SelectionChanged += (s, e) =>
            {
                var p = AiPresets.Get(Val(C("AiPreset")));
                if (p.Id != "custom") { TB("AiEndpoint").Text = p.Endpoint; TB("AiModel").Text = p.Model; }
                FE("AiModelList").Visibility = Visibility.Collapsed;
                UpdateProvider();
            };
            BT("AiLink").Click += (s, e) => { var p = AiPresets.Get(Val(C("AiPreset"))); if (!string.IsNullOrEmpty(p.KeyUrl)) Open(p.KeyUrl); };
            BT("AiModels").Click += async (s, e) => await LoadModels();
            C("AiModelList").SelectionChanged += (s, e) => { var v = Val(C("AiModelList")); if (v != null) TB("AiModel").Text = v; };
            BT("AiTest").Click += async (s, e) => await TestAi();
            UpdateProvider();

            // Aussprache
            FillVoices();
            foreach (var r in new[] { 0.6, 0.8, 1.0, 1.2, 1.4 })
                Add(C("Rate"), r == 1.0 ? "Normal" : r.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + "×", r.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Select(C("Rate"), cfg.SpeechRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (C("Rate").SelectedIndex < 0) Select(C("Rate"), "1");
            CB("AutoSpeak").IsChecked = cfg.AutoSpeak;
            BT("TestVoice").Click += async (s, e) =>
            {
                try { await app.Speech.Speak(SampleFor(Val(C("Src"))), Val(C("Src")), Val(C("Voice")), RateVal()); }
                catch (Exception ex) { TX("VoiceInfo").Text = "Fehler: " + ex.Message; }
            };

            var gi = app.Glossary.Count + " Einträge offline: die " + app.Glossary.QuestWords + " häufigsten Wörter aus echten Questtexten plus WoW-Glossar (Chat-Abkürzungen, Spielbegriffe, Orte mit deutschen Namen).";
            gi += app.Quests.Ready
                ? "\nSpieldaten: " + app.Quests.TextCount.ToString("N0") + " Questtexte und " + app.Names.Count.ToString("N0") + " Namen (NPCs, Gegenstände, Objekte) mit offizieller Übersetzung."
                : QuestCorpus.Available(app.Cfg.TargetLang) ? "\nSpieldaten werden geladen …" : "\nFür " + Lang.Get(app.Cfg.TargetLang).Name + " sind keine Spieldaten eingebaut (siehe README: tools/daten-bauen.mjs).";
            if (app.Glossary.LoadError != null) gi += "\n⚠ " + app.Glossary.LoadError;
            TX("GlossInfo").Text = gi;
        }

        string fontPath;
        bool saved;

        /// <summary>WoW-Ordner und Update-Quelle sofort übernehmen (die Knöpfe sollen auch ohne „Speichern“ wirken).</summary>
        void ApplyAddonFields()
        {
            app.Cfg.WowFolder = TB("WowDir").Text.Trim();
            app.Cfg.UpdateUrl = TB("UpdateUrl").Text.Trim();
            app.Cfg.Save();
        }

        void UpdateAddonInfo()
        {
            string saved0 = app.Cfg.WowFolder;
            app.Cfg.WowFolder = TB("WowDir").Text.Trim();
            var sync = app.Addon ?? new AddonSync(app);
            var all = sync.GameDirs;
            string g = all.Count > 0 ? string.Join("\n   ", all.Select(AddonSync.FlavorName)) : null;
            string v = all.Count > 0 ? sync.InstalledVersion : null;
            int words = sync.Store.Words.Count, quests = sync.Store.Quests.Count;
            TX("AddonInfo").Text = (g == null ? "WoW-Ordner nicht gefunden (auf diesem PC ist WoW vielleicht nicht installiert)." : "Addon-Ordner:\n   " + g)
                + "\n" + (v != null ? "Addon installiert: Version " + v : "Addon noch nicht installiert.")
                + "\nSpieldaten: " + (QuestCorpus.Available(app.Cfg.TargetLang) ? "vorhanden" : "fehlen – „Spieldaten erzeugen“")
                + "\nGelernt: " + words + " Wörter, " + quests + " Quests" + (string.IsNullOrEmpty(sync.Store.LastSync) ? "" : " · letzter Abgleich " + sync.Store.LastSync)
                + (string.IsNullOrEmpty(sync.Store.Build) ? "" : "\nSpielversion: " + sync.Store.Build);
            app.Cfg.WowFolder = saved0;
        }

        void AddonMsg(string msg, bool error)
        {
            TX("AddonStatus").Text = msg;
            TX("AddonStatus").Foreground = error ? (Brush)FindResource("Danger") : (Brush)FindResource("Muted");
        }

        async Task AddonAction(string busy, Func<Task<string>> work)
        {
            foreach (var n in new[] { "AddonInstall", "AddonSyncNow", "GameDataBuild", "UpdateNow" }) BT(n).IsEnabled = false;
            AddonMsg(busy, false);
            try { AddonMsg(await work(), false); }
            catch (Exception ex) { AddonMsg("✕ " + ex.Message, true); }
            finally { foreach (var n in new[] { "AddonInstall", "AddonSyncNow", "GameDataBuild", "UpdateNow" }) BT(n).IsEnabled = true; UpdateAddonInfo(); }
        }

        void UpdateFontInfo()
        {
            bool game = Val(C("Look")) != "modern";
            FE("FontPick").Visibility = FE("FontFind").Visibility = game ? Visibility.Visible : Visibility.Collapsed;
            TX("FontInfo").Text = game ? Theme.FontStatus + " Die Schrift wird aus deiner eigenen WoW-Installation geladen und nicht mitgeliefert." : "";
            TX("FontInfo").Visibility = game ? Visibility.Visible : Visibility.Collapsed;
        }

        static void Add(ComboBox c, string label, string tag) { c.Items.Add(new ComboBoxItem { Content = label, Tag = tag }); }
        static void Select(ComboBox c, string tag)
        {
            foreach (ComboBoxItem i in c.Items) if ((string)i.Tag == tag) { c.SelectedItem = i; return; }
        }
        static string Val(ComboBox c) { var i = c.SelectedItem as ComboBoxItem; return i == null ? null : (string)i.Tag; }
        double RateVal() { double r; return double.TryParse(Val(C("Rate")), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r) ? r : 1.0; }

        static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
        }

        static string SampleFor(string lang)
        {
            switch ((lang ?? "en").Split('-')[0])
            {
                case "en": return "Slay ten murlocs and return to the Crossroads.";
                case "de": return "Erschlagt zehn Murlocs und kehrt zum Wegekreuz zurück.";
                case "fr": return "Tuez dix murlocs et retournez à la Croisée.";
                case "es": return "Mata a diez múrlocs y vuelve al Cruce.";
            }
            return "World of Warcraft";
        }

        void UpdateOcrInfo()
        {
            string code = Val(C("Src")) ?? "en";
            string tag = Ocr.FindLanguage(code);
            TX("OcrInfo").Text = tag != null
                ? "✓ Texterkennung für " + Lang.Get(code).Name + " ist installiert (" + tag + ")."
                : "⚠ Für " + Lang.Get(code).Name + " ist keine Texterkennung installiert. Windows-Einstellungen → Zeit und Sprache → Sprache und Region → " +
                  "Sprache hinzufügen. Dabei „Optische Zeichenerkennung“ mitinstallieren. Installiert: " + string.Join(", ", Ocr.Installed());
            TX("OcrInfo").Foreground = tag != null ? new SolidColorBrush(Color.FromRgb(0x8F, 0xD1, 0x9E)) : (Brush)FindResource("Danger");
        }

        void FillVoices()
        {
            var c = C("Voice");
            string keep = Val(c) ?? cfg.Voice;
            c.Items.Clear();
            string code = Val(C("Src")) ?? "en";
            Add(c, "Automatisch", "");
            string prefix = code.Split('-')[0].ToLowerInvariant();
            int matching = 0;
            foreach (var v in Speech.Voices())
                if (v.Language.ToLowerInvariant().StartsWith(prefix)) { Add(c, v.DisplayName + "  ·  " + v.Language, v.Id); matching++; }
            Select(c, keep ?? "");
            if (c.SelectedIndex < 0) c.SelectedIndex = 0;
            TX("VoiceInfo").Text = matching > 0
                ? matching + " Stimme(n) für " + Lang.Get(code).Name + " installiert. Zusätzlich gibt es bei vielen englischen Wörtern eine echte Aufnahme (Kopfhörer-Symbol in der Karte)."
                : "⚠ Keine Stimme für " + Lang.Get(code).Name + " installiert – Windows-Einstellungen → Zeit und Sprache → Sprache → " + Lang.Get(code).Name + " → „Sprachausgabe“. Bis dahin wird die Standardstimme genutzt.";
        }

        void UpdateProvider()
        {
            string p = Val(C("Provider")) ?? "auto";
            FE("MmPanel").Visibility = p == "mymemory" || p == "auto" ? Visibility.Visible : Visibility.Collapsed;
            FE("DlPanel").Visibility = p == "deepl" || p == "auto" ? Visibility.Visible : Visibility.Collapsed;
            FE("AiPanel").Visibility = p == "ai" || p == "auto" ? Visibility.Visible : Visibility.Collapsed;
            string info;
            switch (p)
            {
                case "mymemory": info = "Kostenlos ohne Anmeldung, ca. 5.000 Wörter pro Tag. Gut für einzelne Wörter, bei Sätzen manchmal holprig."; break;
                case "deepl": info = "Sehr gute Übersetzungen. Kostenloses Konto mit 500.000 Zeichen pro Monat (Kreditkarte zur Bestätigung nötig)."; break;
                case "ai": info = "Bester Modus für Kontext: erklärt, was das Wort genau in diesem Satz bedeutet, kennt WoW-Slang, liefert Lautschrift und Beispiel. Gemini und Groq haben ein kostenloses Kontingent, das man mit einer normalen Anmeldung bekommt."; break;
                default: info = "Nimmt automatisch das Beste, was eingerichtet ist: KI (wenn Schlüssel da) → DeepL (wenn Schlüssel da) → MyMemory. Das WoW-Glossar wird immer zuerst gefragt."; break;
            }
            TX("ProviderInfo").Text = info;
            var preset = AiPresets.Get(Val(C("AiPreset")));
            BT("AiLink").Visibility = string.IsNullOrEmpty(preset.KeyUrl) ? Visibility.Collapsed : Visibility.Visible;
            BT("AiLink").Content = preset.Id == "ollama" ? "Ollama herunterladen" : "Kostenlosen Schlüssel holen";
        }

        Settings Collect()
        {
            var s = cfg.Clone();
            s.SourceLang = Val(C("Src")) ?? "en"; s.TargetLang = Val(C("Tgt")) ?? "de";
            s.Hotkey = Val(C("Hotkey")) ?? "CtrlShift";
            s.AutoHideSeconds = int.Parse(Val(C("Hide")) ?? "15");
            s.OnlyInWow = CB("OnlyWow").IsChecked == true;
            s.Autostart = CB("Autostart").IsChecked == true;
            s.Provider = Val(C("Provider")) ?? "auto";
            s.MyMemoryEmail = TB("MmEmail").Text.Trim();
            s.SetDeepLKey(PB("DlKey").Password.Trim());
            s.AiPreset = Val(C("AiPreset")) ?? "gemini";
            s.AiEndpoint = TB("AiEndpoint").Text.Trim(); s.AiModel = TB("AiModel").Text.Trim();
            s.SetAiKey(PB("AiKey").Password.Trim());
            s.Voice = Val(C("Voice")) ?? ""; s.SpeechRate = RateVal(); s.AutoSpeak = CB("AutoSpeak").IsChecked == true;
            s.Look = Val(C("Look")) ?? "quest"; s.GameFontPath = fontPath ?? "";
            s.WowFolder = TB("WowDir").Text.Trim(); s.UpdateUrl = TB("UpdateUrl").Text.Trim(); s.AutoSync = CB("AutoSync").IsChecked == true;
            return s;
        }

        void DoSave()
        {
            var s = Collect();
            if (s.SourceLang == s.TargetLang) { MessageBox.Show(this, "Quelle und Ziel sind dieselbe Sprache.", "WoW Lexikon"); return; }
            if (s.Provider == "deepl" && string.IsNullOrEmpty(s.GetDeepLKey())) { MessageBox.Show(this, "Für DeepL bitte einen Schlüssel eintragen – oder „Automatisch“ wählen.", "WoW Lexikon"); return; }
            if (s.Provider == "ai" && (string.IsNullOrEmpty(s.AiEndpoint) || string.IsNullOrEmpty(s.AiModel))) { MessageBox.Show(this, "Für die KI bitte Adresse und Modell angeben.", "WoW Lexikon"); return; }
            try { Autostart.Set(s.Autostart); } catch { }
            saved = true;
            if (Saved != null) Saved(s);
            Close();
        }

        async Task LoadModels()
        {
            var s = Collect();
            TX("AiStatus").Text = "Lade Modelle …"; TX("AiStatus").Foreground = (Brush)FindResource("Muted");
            try
            {
                var list = await Ai.Models(s);
                var c = C("AiModelList");
                c.Items.Clear();
                foreach (var m in list) Add(c, m, m);
                c.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                Select(c, s.AiModel);
                TX("AiStatus").Text = list.Count + " Modelle gefunden – unten auswählen.";
            }
            catch (Exception ex) { TX("AiStatus").Text = "✕ " + ex.Message; TX("AiStatus").Foreground = (Brush)FindResource("Danger"); }
        }

        async Task TestAi()
        {
            var s = Collect();
            TX("AiStatus").Text = "Teste …"; TX("AiStatus").Foreground = (Brush)FindResource("Muted");
            BT("AiTest").IsEnabled = false;
            try
            {
                var r = await Ai.Ask(s, "aggro", "Don't pull aggro before the tank is ready.");
                TX("AiStatus").Text = "✓ Funktioniert: „aggro“ → " + r.Translation + (string.IsNullOrEmpty(r.Meaning) ? "" : " – " + r.Meaning);
                TX("AiStatus").Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xD1, 0x9E));
            }
            catch (Exception ex) { TX("AiStatus").Text = "✕ " + ex.Message; TX("AiStatus").Foreground = (Brush)FindResource("Danger"); }
            finally { BT("AiTest").IsEnabled = true; }
        }
    }

    static class Autostart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool IsEnabled()
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key)) return k != null && k.GetValue("WowLexikon") != null;
        }
        public static void Set(bool on)
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Key))
            {
                if (on) k.SetValue("WowLexikon", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                else if (k.GetValue("WowLexikon") != null) k.DeleteValue("WowLexikon");
            }
        }
    }
}
