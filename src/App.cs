using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WF = System.Windows.Forms;
using Point = System.Drawing.Point;

namespace WowLexikon
{
    static class Program
    {
        public const string Version = "1.0.0";
        /// <summary>Fest eingebaute Update-Quelle (setzt tools/veroeffentlichen.ps1). Leer = nur die Einstellung zählt.</summary>
        public const string DefaultUpdateUrl = "https://github.com/Baougam/wow-lexikon/releases/latest/download/manifest.json";

        [STAThread]
        static void Main(string[] args)
        {
            bool selftest = args.Length > 0 && args[0] == "--selftest";
            // Liegt eine neue Version bereit (vom Update geladen)? Dann übernehmen und neu starten.
            if (!selftest) { try { if (AddonSync.ApplyPendingAppUpdate()) return; } catch { } }
            bool created;
            using (var mutex = new Mutex(true, selftest ? "WowLexikon_Selftest" : "WowLexikon_Einzelinstanz", out created))
            {
                if (!created) { WF.MessageBox.Show("WoW Lexikon läuft bereits (Symbol unten rechts in der Taskleiste).", "WoW Lexikon"); return; }
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Theme.Apply(app);
                var c = new Controller();
                app.DispatcherUnhandledException += (s, e) => { Log.Error(e.Exception); e.Handled = true; c.ShowError(e.Exception.Message); };
                TaskScheduler.UnobservedTaskException += (s, e) => { Log.Error(e.Exception); e.SetObserved(); };
                if (selftest) app.Startup += async (s, e) => { await SelfTest.Run(c, args.Length > 1 ? args[1] : Settings.Dir); app.Shutdown(); };
                else app.Startup += (s, e) => c.Start();
                app.Run();
            }
        }
    }

    static class Log
    {
        public static void Error(Exception e)
        {
            try { File.AppendAllText(Path.Combine(Settings.Dir, "fehler.log"), DateTime.Now.ToString("s") + "  " + e + "\r\n\r\n", Encoding.UTF8); } catch { }
        }
    }

    /// <summary>Überwacht die gehaltene Tastenkombination – global, ohne Tastatur-Hook.</summary>
    class HotkeyWatcher
    {
        readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(15) };
        readonly Func<int[]> keys;
        readonly Func<bool> allowed;
        bool active, blocked;
        DateTime heldSince = DateTime.MinValue;
        public event Action Pressed, Released, Escape;
        public bool IsActive { get { return active; } }

        // Mit diesen Tasten darf zusätzlich gedrückt sein (Maustasten, Umschalt, Strg, Alt)
        static readonly HashSet<int> neutral = new HashSet<int> { 0x01, 0x02, 0x04, 0x05, 0x06, 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5 };

        public HotkeyWatcher(Func<int[]> keys, Func<bool> allowed)
        {
            this.keys = keys; this.allowed = allowed;
            timer.Tick += (s, e) => Tick();
            timer.Start();
        }

        public bool AllHeld()
        {
            foreach (var k in keys()) if (!Native.IsDown(k)) return false;
            return true;
        }

        void Tick()
        {
            bool all = AllHeld();
            if (active)
            {
                if (Native.IsDown(0x1B)) { active = false; blocked = true; if (Escape != null) Escape(); return; }
                if (!all) { active = false; if (Released != null) Released(); }
                return;
            }
            if (!all) { blocked = false; heldSince = DateTime.MinValue; return; }
            if (blocked) return;
            // Wird nebenbei eine andere Taste gedrückt (z. B. Strg+Umschalt+1 für eine Aktionsleiste), nicht auslösen.
            var own = new HashSet<int>(keys());
            for (int vk = 0x08; vk <= 0xFE; vk++)
                if (!neutral.Contains(vk) && !own.Contains(vk) && Native.IsDown(vk)) { blocked = true; return; }
            if (heldSince == DateTime.MinValue) { heldSince = DateTime.Now; return; }
            if ((DateTime.Now - heldSince).TotalMilliseconds < 140) return;
            if (!allowed()) { blocked = true; return; }
            active = true;
            if (Pressed != null) Pressed();
        }
    }

    public class Controller
    {
        public Settings Cfg;
        public readonly Glossary Glossary = new Glossary();
        public QuestCorpus Quests = new QuestCorpus();
        public NameIndex Names = new NameIndex();
        public Task GameDataTask;
        bool fontSearched;

        /// <summary>Stil anwenden; im Spiel-Look die Schrift aus der WoW-Installation holen (und den Fundort merken).</summary>
        public void ApplyLook()
        {
            bool gameLook = Cfg.Look != "modern";
            string font = gameLook ? Theme.FindGameFont(Cfg.GameFontPath) : Cfg.GameFontPath;
            Theme.ApplyLook(Application.Current, Cfg.Look ?? "quest", font);
            if (gameLook && font != null && font != Cfg.GameFontPath) { Cfg.GameFontPath = font; Cfg.Save(); }
        }

        /// <summary>Spieldaten (Questtexte + Namen) für die Zielsprache im Hintergrund laden.</summary>
        public Task LoadGameData()
        {
            var q = new QuestCorpus(); var n = new NameIndex();
            Quests = q; Names = n;
            return Task.WhenAll(q.LoadAsync(Cfg.TargetLang), n.LoadAsync(Cfg.TargetLang));
        }
        internal readonly Speech Speech = new Speech();
        HotkeyWatcher hk;
        OverlayWindow overlay;
        internal CardWindow Card;
        Tray tray;
        SettingsWindow settingsWin;
        public bool Paused;
        int lookupId;

        public void Init()
        {
            Cfg = Settings.Load();
            Glossary.Load();
            GameDataTask = LoadGameData();
            ApplyLook();
            Card = new CardWindow();
            Card.Relookup += t => { var ctx = Card.State != null ? Card.State.Context : null; var _ = Lookup(t, ctx, ++lookupId); };
            Card.SpeakTerm += slow => SpeakSafe(Card.State != null ? Card.State.Term : null, slow);
            Card.SpeakExample += () => SpeakSafe(Card.State != null ? Card.State.Example : null, false);
            Card.PlayAudio += () => { if (Card.State != null) Speech.PlayUrl(Card.State.Audio); };
            Card.StarClicked += () =>
            {
                var s = Card.State;
                if (s == null || s.Starred) return;
                try { Vocab.Add(s.Term, s.Ipa, s.Translation, s.Example, s.ExampleTr, s.Context); s.Starred = true; Card.Render(s); }
                catch (Exception e) { ShowError("Vokabelliste: " + e.Message); }
            };
        }

        public void Start()
        {
            Init();
            hk = new HotkeyWatcher(() => HotkeyDef.Get(Cfg.Hotkey).Keys, CanActivate);
            hk.Pressed += OnPressed;
            hk.Released += OnReleased;
            hk.Escape += CloseOverlay;
            tray = new Tray(this);
            string hint = "In WoW " + HotkeyDef.Get(Cfg.Hotkey).Label + " gedrückt halten und ein Wort einrahmen oder anklicken. Optional zweiter Rahmen = Satz als Kontext.";
            if (Cfg.FirstRun)
            {
                Cfg.FirstRun = false; Cfg.Save();
                OpenSettings();
            }
            tray.Balloon("WoW Lexikon ist bereit", hint);
            StartCompanion();
        }

        // ---------------- Begleit-Programm fürs Addon ----------------
        public AddonSync Addon;
        DispatcherTimer companionTimer, watchDelay;
        readonly List<FileSystemWatcher> svWatchers = new List<FileSystemWatcher>();

        void StartCompanion()
        {
            Addon = new AddonSync(this);
            companionTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
            companionTimer.Tick += (s, e) => { var _ = CompanionRound(false); };
            companionTimer.Start();
            watchDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            watchDelay.Tick += (s, e) => { watchDelay.Stop(); var _ = CompanionRound(true); };
            WatchSavedVariables();
            var __ = CompanionRound(false);
        }

        /// <summary>Beobachtet den WTF-Ordner: Sobald WoW beim Ausloggen die Addon-Daten schreibt, wird abgeglichen.</summary>
        public void WatchSavedVariables()
        {
            foreach (var old in svWatchers) { old.EnableRaisingEvents = false; old.Dispose(); }
            svWatchers.Clear();
            if (Addon == null) return;
            foreach (var g in Addon.GameDirs)
            {
                if (!Directory.Exists(Path.Combine(g, "WTF"))) continue;
                var wtch = new FileSystemWatcher(Path.Combine(g, "WTF"), AddonSync.AddonName + ".lua") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
                FileSystemEventHandler h = (s, e) => Application.Current.Dispatcher.BeginInvoke(new Action(() => { watchDelay.Stop(); watchDelay.Start(); }));
                wtch.Changed += h; wtch.Created += h; wtch.Renamed += (s, e) => h(s, e);
                wtch.EnableRaisingEvents = true;
                svWatchers.Add(wtch);
            }
        }

        /// <summary>Updates prüfen + Addon-Daten abgleichen. Läuft beim Start, alle 6 Stunden und nach dem Ausloggen.</summary>
        public async Task CompanionRound(bool afterLogout)
        {
            if (Addon == null || !Cfg.AutoSync) return;
            try
            {
                string upd = await Addon.CheckUpdates();
                if (upd != null) tray.Balloon("WoW Lexikon – Update", upd);
            }
            catch (Exception e) { Log.Error(e); }
            if (Addon.GameDir == null || !Addon.SavedVariableFiles().Any()) return;
            try
            {
                var r = await Addon.Sync(msg => { });
                if (r == null) return;
                if (r.BuildChanged) tray.Balloon("Spiel-Update erkannt", "WoW wurde aktualisiert: " + r.Build + ". Neue und geänderte Questtexte werden ab jetzt mitübersetzt.");
                else if (afterLogout && (r.NewWords + r.NewQuests + r.ChangedQuests) > 0) tray.Balloon("WoW Lexikon hat dazugelernt", Addon.LastMessage + " Beim nächsten Einloggen sind sie im Addon.");
            }
            catch (Exception e) { Log.Error(e); }
        }

        /// <summary>„Nach Updates suchen“ (Knopf/Menü): Ergebnis anzeigen, bei neuem Programm Neustart anbieten.</summary>
        public async Task<string> CheckUpdatesNow(bool showBalloon)
        {
            var sync = Addon ?? new AddonSync(this);
            string msg;
            try
            {
                if (string.IsNullOrEmpty(sync.UpdateUrl)) msg = "Keine Update-Quelle eingerichtet (Einstellungen → Update-Quelle).";
                else msg = await sync.CheckUpdates() ?? ("Alles aktuell – Programm " + Program.Version + (sync.InstalledVersion != null ? ", Addon " + sync.InstalledVersion : "") + ".");
            }
            catch (Exception e) { msg = "Update-Prüfung fehlgeschlagen: " + e.Message; }
            if (showBalloon && tray != null) tray.Balloon("WoW Lexikon – Updates", msg);
            if (sync.AppUpdatePending &&
                WF.MessageBox.Show("Eine neue Version des Begleit-Programms ist geladen. Jetzt neu starten?", "WoW Lexikon", WF.MessageBoxButtons.YesNo, WF.MessageBoxIcon.Question) == WF.DialogResult.Yes)
            {
                if (AddonSync.ApplyPendingAppUpdate()) Quit();
            }
            return msg;
        }

        /// <summary>Übersetzt einen beliebig langen Text mit dem eingestellten Dienst (lange Texte stückweise).</summary>
        public async Task<string> TranslateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            string provider = ResolveProvider();
            if (provider == "ai")
            {
                try { return await Ai.TranslateText(Cfg, text); } catch { provider = Fallback(); }
            }
            if (provider == "deepl" || text.Length <= 450) return (await Translate(provider, text)).Text;
            // MyMemory nimmt höchstens ~500 Zeichen: an Satzgrenzen teilen, Absätze erhalten
            var outParts = new List<string>();
            foreach (var para in text.Replace("\r", "").Split('\n'))
            {
                if (para.Trim().Length == 0) { outParts.Add(""); continue; }
                var chunk = new StringBuilder(); var res = new StringBuilder();
                foreach (var sent in System.Text.RegularExpressions.Regex.Split(para, @"(?<=[.!?])\s+"))
                {
                    if (chunk.Length + sent.Length > 450 && chunk.Length > 0) { res.Append((await Translate(provider, chunk.ToString())).Text).Append(' '); chunk.Clear(); }
                    chunk.Append(sent).Append(' ');
                }
                if (chunk.Length > 0) res.Append((await Translate(provider, chunk.ToString().Trim())).Text);
                outParts.Add(res.ToString().Trim());
            }
            return string.Join("\n", outParts);
        }

        bool CanActivate()
        {
            if (Paused || overlay != null) return false;
            if (!Cfg.OnlyInWow) return true;
            if (Native.IsWowForeground()) return true;
            // Eigene Karte im Vordergrund (z. B. nach dem Korrigieren eines Wortes) zählt auch
            return Native.ForegroundProcessName().Equals(System.Diagnostics.Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);
        }

        async void OnPressed()
        {
            // WoW läuft jetzt sicher – falls die Spielschrift noch fehlt, im Ordner des Spiels nachsehen
            if (Cfg.Look != "modern" && !Theme.FontLoaded && !fontSearched) { fontSearched = true; ApplyLook(); }
            bool wasVisible = Card.IsVisible;
            if (wasVisible) Card.HideNow();
            if (wasVisible) await Task.Delay(70);          // Karte nicht mitfotografieren
            if (!hk.IsActive) return;
            var cur = Native.Cursor();
            var mon = WF.Screen.FromPoint(cur).Bounds;
            Bitmap shot;
            try { shot = ScreenShot.Capture(mon); }
            catch (Exception e) { ShowError("Bildschirm konnte nicht gelesen werden: " + e.Message); return; }
            overlay = new OverlayWindow(shot, mon);
            overlay.Cancelled += CloseOverlay;
            overlay.Show();
        }

        void OnReleased()
        {
            if (overlay == null) return;
            var sel = overlay.Finish();
            overlay.Close(); overlay = null;
            if (sel != null) Process(sel);
        }

        void CloseOverlay()
        {
            if (overlay == null) return;
            overlay.Close(); overlay = null;
        }

        async void Process(Selection sel)
        {
            int id = ++lookupId;
            var st = new LookupState { Loading = true, Status = "Lese Text …" };
            Card.Render(st);
            Card.ShowAt(sel.Anchor);
            string term, ctx = null, autoCtx = null;
            string lang = Cfg.SourceLang;
            try
            {
                if (Ocr.FindLanguage(lang) == null)
                    throw new Exception("Für " + Lang.Get(lang).Name + " ist keine Windows-Texterkennung installiert – Hinweise in den Einstellungen.");
                term = sel.WordRect.HasValue ? await Ocr.TextIn(sel.Shot, sel.WordRect.Value, lang) : await Ocr.WordAt(sel.Shot, sel.WordPoint.Value, lang);
                if (sel.CtxRect.HasValue) ctx = await Ocr.TextIn(sel.Shot, sel.CtxRect.Value, lang);
                else if (sel.CtxPoint.HasValue) ctx = await Ocr.LineAt(sel.Shot, sel.CtxPoint.Value, lang);
                else if (Quests.Ready && !string.IsNullOrWhiteSpace(term))
                {
                    // Ohne zweiten Rahmen: Zeile um das Wort mitlesen, um den Questtext zu finden
                    var c = sel.WordPoint ?? new System.Drawing.Point(sel.WordRect.Value.X + sel.WordRect.Value.Width / 2, sel.WordRect.Value.Y + sel.WordRect.Value.Height / 2);
                    autoCtx = await Ocr.LineAt(sel.Shot, c, lang);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                if (id == lookupId) { st.Loading = false; st.Error = e.Message; Card.Render(st); }
                return;
            }
            finally { sel.Shot.Dispose(); }
            if (id != lookupId) return;
            if (string.IsNullOrWhiteSpace(term))
            {
                st.Loading = false;
                st.Error = "Kein Text erkannt. Tipp: Rahmen etwas großzügiger ziehen oder direkt auf das Wort klicken.";
                Card.Render(st);
                Card.StartAutoHide(Cfg.AutoHideSeconds);
                return;
            }
            if (string.IsNullOrWhiteSpace(ctx) || ctx.Equals(term, StringComparison.OrdinalIgnoreCase)) ctx = null;
            await Lookup(term, ctx, id, autoCtx);
        }

        string ResolveProvider()
        {
            bool hasAi = !string.IsNullOrEmpty(Cfg.GetAiKey()) || (Cfg.AiPreset == "ollama" && !string.IsNullOrEmpty(Cfg.AiEndpoint));
            bool hasDeepL = !string.IsNullOrEmpty(Cfg.GetDeepLKey());
            switch (Cfg.Provider)
            {
                case "ai": return hasAi || !AiPresets.Get(Cfg.AiPreset).NeedsKey ? "ai" : Fallback();
                case "deepl": return hasDeepL ? "deepl" : "mymemory";
                case "mymemory": return "mymemory";
            }
            return hasAi ? "ai" : Fallback();
        }
        string Fallback() { return string.IsNullOrEmpty(Cfg.GetDeepLKey()) ? "mymemory" : "deepl"; }

        string ProviderName(string p) { return p == "deepl" ? "DeepL" : p == "ai" ? "KI · " + Cfg.AiModel : "MyMemory"; }

        Task<TrResult> Translate(string provider, string text)
        {
            if (provider == "deepl") return DeepL.Translate(text, Cfg.SourceLang, Cfg.TargetLang, Cfg.GetDeepLKey());
            return MyMemory.Translate(text, Cfg.SourceLang, Cfg.TargetLang, Cfg.MyMemoryEmail);
        }

        public async Task<LookupState> Lookup(string term, string ctx, int id, string autoCtx = null)
        {
            var st = new LookupState { Term = term, Context = ctx, Loading = true };
            Card.Render(st);
            string src = Cfg.SourceLang, tgt = Cfg.TargetLang;
            bool gameData = src == "en" && Quests.Ready && Quests.Language == tgt;
            bool wholeHit = false;

            // 0) Ganzer Satz eingerahmt? Dann direkt den offiziellen Questtext suchen.
            if (gameData && new List<string>(QuestCorpus.Tokens(term)).Count >= 5)
            {
                var whole = Quests.Find(term, null);
                if (whole != null)
                {
                    st.Translation = whole.Tr; st.QuestTitle = whole.TitleTr ?? whole.TitleEn;
                    st.Note = "Offizieller Text aus dem Spielclient" + (st.QuestTitle != null ? " – Quest „" + st.QuestTitle + "“" : "");
                    st.FromGlossary = true; st.Sources.Add("Questtext aus dem Spiel");
                    wholeHit = true;
                }
            }

            // 1) WoW-Glossar – sofort und offline
            if (src == "en" && st.Translation == null)
            {
                var g = Glossary.Find(term);
                if (g != null && g.Tr(tgt) != null)
                {
                    st.Translation = g.Tr(tgt); st.Ipa = g.Ipa; st.Note = g.Note(tgt);
                    st.Example = g.Example; st.ExampleTr = g.ExTr(tgt);
                    st.FromGlossary = true; st.Sources.Add(g.IsAuto ? "Wörterbuch (Ding)" : g.IsQuestWord ? "Questwörterbuch" : "WoW-Glossar");
                    Card.Render(st);
                }
            }
            // 1b) Namen aus dem Spielclient (NPCs, Gegenstände, Objekte, Questtitel)
            if (gameData && st.Translation == null)
            {
                var ne = Names.Find(term);
                if (ne != null)
                {
                    st.Translation = ne.Tr;
                    st.Note = ne.TypeLabel + (string.IsNullOrEmpty(ne.SubTr) ? "" : " · " + ne.SubTr) + " – offizieller Name im Spiel";
                    st.FromGlossary = true; st.Sources.Add("Namen aus dem Spiel");
                }
            }

            // 1c) Questtext: Originalsatz + offizielle Übersetzung aus dem Spielclient
            if (gameData && !wholeHit && (ctx != null || autoCtx != null))
            {
                var qm = Quests.Find(ctx ?? autoCtx, term);
                if (qm != null)
                {
                    st.Context = qm.En; st.ContextTr = qm.Tr; st.OfficialContext = true;
                    st.QuestTitle = qm.TitleTr ?? qm.TitleEn;
                    st.Sources.Add("Questtext aus dem Spiel");
                    ctx = qm.En;
                }
            }
            Card.Render(st);
            if (Cfg.AutoSpeak) SpeakSafe(term, false);

            Task<DictInfo> dictTask = src == "en" ? DictApi.Lookup(term) : Task.FromResult<DictInfo>(null);
            string provider = ResolveProvider();

            // 2) KI: Übersetzung + Bedeutung im Kontext + Beispiel in einem Rutsch
            if (provider == "ai")
            {
                try
                {
                    var ai = await Ai.Ask(Cfg, term, ctx);
                    if (id != lookupId) return st;
                    if (st.Translation == null) st.Translation = ai.Translation;
                    else if (!string.IsNullOrEmpty(ai.Translation) && !ai.Translation.Equals(st.Translation, StringComparison.OrdinalIgnoreCase)) st.Alternatives.Add(ai.Translation);
                    foreach (var a in ai.Alternatives) if (!st.Alternatives.Contains(a) && st.Alternatives.Count < 5) st.Alternatives.Add(a);
                    if (st.Ipa == null) st.Ipa = ai.Ipa;
                    st.Pos = ai.PartOfSpeech; st.Meaning = ai.Meaning;
                    if (!st.OfficialContext) st.ContextTr = ai.ContextTr;
                    if (st.Example == null) { st.Example = ai.Example; st.ExampleTr = ai.ExampleTr; }
                    st.Sources.Add(ProviderName("ai"));
                }
                catch (Exception e)
                {
                    st.Warn = e.Message + " – nutze stattdessen " + ProviderName(Fallback()) + ".";
                    provider = Fallback();
                }
                Card.Render(st);
            }

            // 3) Klassische Übersetzung (falls nicht Glossar/KI)
            if (provider != "ai")
            {
                Task<TrResult> termTask = st.Translation == null ? Translate(provider, term) : null;
                Task<TrResult> ctxTask = ctx != null && !st.OfficialContext ? Translate(provider, ctx) : null;
                if (termTask != null)
                {
                    try
                    {
                        var r = await termTask;
                        if (id != lookupId) return st;
                        st.Translation = r.Text; st.Alternatives.AddRange(r.Alternatives);
                        st.Sources.Add(ProviderName(provider));
                    }
                    catch (Exception e) { st.Error = e.Message; }
                    Card.Render(st);
                }
                if (ctxTask != null)
                {
                    try { var r = await ctxTask; if (id != lookupId) return st; st.ContextTr = r.Text; if (!st.Sources.Contains(ProviderName(provider))) st.Sources.Add(ProviderName(provider)); }
                    catch (Exception e) { if (st.Error == null) st.Warn = e.Message; }
                    Card.Render(st);
                }
            }

            // 3b) Ohne KI: welche der Bedeutungen steckt in der Satzübersetzung?
            if (st.Sense == null) st.Sense = SenseMatcher.Match(st.Translation, st.ContextTr);
            st.ContextTrHighlight = SenseMatcher.Surface(st.ContextTr, st.Sense ?? st.Translation);
            Card.Render(st);

            // 4) Wörterbuch: Lautschrift, echte Aufnahme, Definition, Beispiel
            try
            {
                var d = await dictTask;
                if (id != lookupId) return st;
                if (d != null)
                {
                    if (string.IsNullOrEmpty(st.Ipa)) st.Ipa = d.Ipa;
                    st.Audio = d.Audio;
                    if (st.Pos == null && tgt == "de") st.Pos = DictApi.PosGerman(d.PartOfSpeech);
                    if (!st.FromGlossary && st.Meaning == null && d.Definition != null && d.Definition.Length < 160) st.Definition = d.Definition;
                    if (st.Example == null && d.Example != null) st.Example = d.Example;
                    st.Sources.Add("dictionaryapi.dev");
                }
            }
            catch { }
            Card.Render(st);

            // 5) Beispielsatz übersetzen
            if (!string.IsNullOrEmpty(st.Example) && string.IsNullOrEmpty(st.ExampleTr))
            {
                try { var r = await Translate(Fallback(), st.Example); if (id != lookupId) return st; st.ExampleTr = r.Text; }
                catch { }
            }
            st.Loading = false;
            if (st.Translation == null && st.Error == null) st.Error = "Keine Übersetzung gefunden.";
            Card.Render(st);
            Card.StartAutoHide(Cfg.AutoHideSeconds);
            Cache.Flush();
            return st;
        }

        async void SpeakSafe(string text, bool slow)
        {
            try { await Speech.Speak(text, Cfg.SourceLang, Cfg.Voice, Cfg.SpeechRate * (slow ? 0.6 : 1.0)); }
            catch (Exception e) { Log.Error(e); }
        }

        public void OpenSettings()
        {
            if (settingsWin != null) { settingsWin.Activate(); return; }
            settingsWin = new SettingsWindow(Cfg, this);
            settingsWin.Saved += s =>
            {
                bool langChanged = s.TargetLang != Cfg.TargetLang;
                Cfg = s; Cfg.Save(); tray.UpdateMenu(); ApplyLook();
                if (langChanged) { var _r = LoadGameData(); }
            };
            settingsWin.Closed += (s, e) => settingsWin = null;
            settingsWin.Show();
            settingsWin.Activate();
        }

        public void ShowError(string msg) { if (tray != null) tray.Balloon("WoW Lexikon – Fehler", msg); }

        public void Quit()
        {
            Cache.Flush();
            if (tray != null) tray.Dispose();
            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// Kontext ohne KI: Hat ein Wort mehrere Bedeutungen („Haut; verstecken“), wird geprüft,
    /// welche davon in der Übersetzung des ganzen Satzes auftaucht.
    /// </summary>
    public static class SenseMatcher
    {
        public static string Match(string translation, string contextTr)
        {
            if (string.IsNullOrEmpty(translation) || string.IsNullOrEmpty(contextTr)) return null;
            var senses = new List<string>();
            foreach (var raw in translation.Split(new[] { ';', ',', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = System.Text.RegularExpressions.Regex.Replace(raw, @"\(.*?\)", "").Trim();
                if (s.Length > 1) senses.Add(s);
            }
            if (senses.Count < 2) return null;
            string ctx = contextTr.ToLowerInvariant();
            string best = null; int bestLen = 0;
            foreach (var s in senses)
            {
                foreach (var w in s.Split(' '))
                {
                    string word = w.Trim('.', '!', '?', '-').ToLowerInvariant();
                    if (word.Length < 3) continue;
                    // Wortstamm vergleichen, damit „Fell“ auch „Felle“ und „verstecken“ auch „versteckt“ findet
                    int stemLen = word.Length <= 4 ? word.Length : Math.Max(4, word.Length - 2);
                    string stem = word.Substring(0, stemLen);
                    if (System.Text.RegularExpressions.Regex.IsMatch(ctx, @"(^|[^\p{L}])" + System.Text.RegularExpressions.Regex.Escape(stem))
                        && stem.Length > bestLen) { best = s; bestLen = stem.Length; }
                }
            }
            return best;
        }

        /// <summary>Findet das Wort im übersetzten Satz, das zur Übersetzung gehört (zum Hervorheben).</summary>
        public static string Surface(string text, string translation)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(translation)) return null;
            foreach (var raw in translation.Split(new[] { ';', ',', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = System.Text.RegularExpressions.Regex.Replace(raw, @"\(.*?\)", "").Trim();
                // Ganzer Ausdruck („Bandit der Defias“) zuerst
                if (s.Length > 3 && text.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                    return text.Substring(text.IndexOf(s, StringComparison.OrdinalIgnoreCase), s.Length);
                foreach (var w in s.Split(' '))
                {
                    string word = w.Trim('.', '!', '?', '-');
                    if (word.Length < 3) continue;
                    int stemLen = word.Length <= 4 ? word.Length : Math.Max(4, word.Length - 2);
                    var m = System.Text.RegularExpressions.Regex.Match(text, @"(?<![\p{L}])" + System.Text.RegularExpressions.Regex.Escape(word.Substring(0, stemLen)) + @"\p{L}*",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) return m.Value;
                }
            }
            return null;
        }
    }

    class Tray : IDisposable
    {
        readonly WF.NotifyIcon icon;
        readonly Controller c;
        readonly WF.ToolStripMenuItem header, pause;

        public Tray(Controller c)
        {
            this.c = c;
            var menu = new WF.ContextMenuStrip();
            header = new WF.ToolStripMenuItem { Enabled = false };
            menu.Items.Add(header);
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add("Wort suchen …", null, (s, e) => c.Card.ShowForInput());
            menu.Items.Add("Nach Updates suchen", null, async (s, e) => await c.CheckUpdatesNow(true));
            menu.Items.Add("Addon jetzt abgleichen", null, async (s, e) =>
            {
                if (c.Addon == null) return;
                if (!c.Addon.SavedVariableFiles().Any()) { Balloon("WoW Lexikon", "Noch keine Addon-Daten – Addon in den Einstellungen installieren, einloggen, ausloggen."); return; }
                Balloon("WoW Lexikon", "Gleiche mit dem Addon ab …");
                try { await c.Addon.Sync(m => { }); Balloon("WoW Lexikon", c.Addon.LastMessage); } catch (Exception ex) { Balloon("WoW Lexikon – Fehler", ex.Message); }
            });
            menu.Items.Add("Einstellungen …", null, (s, e) => c.OpenSettings());
            menu.Items.Add("Vokabelliste öffnen", null, (s, e) => OpenFile(Vocab.FilePath, "Die Vokabelliste ist noch leer. Klicke in der Ergebnis-Karte auf den Stern."));
            menu.Items.Add("Eigenes Glossar bearbeiten", null, (s, e) => OpenFile(c.Glossary.UserFile, null));
            menu.Items.Add("Glossar neu laden", null, (s, e) => { c.Glossary.Load(); Balloon("Glossar neu geladen", c.Glossary.Count + " Einträge" + (c.Glossary.LoadError != null ? "\n" + c.Glossary.LoadError : "")); });
            pause = new WF.ToolStripMenuItem("Pausieren", null, (s, e) => { c.Paused = !c.Paused; UpdateMenu(); });
            menu.Items.Add(pause);
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add("Beenden", null, (s, e) => c.Quit());
            icon = new WF.NotifyIcon { Icon = MakeIcon(), Text = "WoW Lexikon", Visible = true, ContextMenuStrip = menu };
            icon.DoubleClick += (s, e) => c.Card.ShowForInput();
            UpdateMenu();
        }

        public void UpdateMenu()
        {
            header.Text = c.Paused ? "WoW Lexikon – pausiert" : "Aktiv: " + HotkeyDef.Get(c.Cfg.Hotkey).Label + " halten";
            pause.Checked = c.Paused;
            icon.Text = "WoW Lexikon – " + (c.Paused ? "pausiert" : HotkeyDef.Get(c.Cfg.Hotkey).Label + " halten");
        }

        void OpenFile(string path, string missing)
        {
            if (!File.Exists(path)) { if (missing != null) Balloon("WoW Lexikon", missing); return; }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }

        public void Balloon(string title, string text) { icon.ShowBalloonTip(6000, title, text, WF.ToolTipIcon.None); }

        public void Dispose() { icon.Visible = false; icon.Dispose(); }

        static Bitmap IconBitmap(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                float r = size * 0.24f;
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddArc(0, 0, r * 2, r * 2, 180, 90); path.AddArc(size - r * 2 - 1, 0, r * 2, r * 2, 270, 90);
                path.AddArc(size - r * 2 - 1, size - r * 2 - 1, r * 2, r * 2, 0, 90); path.AddArc(0, size - r * 2 - 1, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, size, size), Color.FromArgb(0xFF, 0xD7, 0x8A), Color.FromArgb(0xD9, 0x9A, 0x3A), 45f))
                    g.FillPath(br, path);
                string fam = new System.Drawing.Text.InstalledFontCollection().Families.Length > 0 && FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
                using (var f = new Font(fam, size * 0.5f, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var b = new SolidBrush(Color.FromArgb(0x1A, 0x15, 0x08)))
                    g.DrawString("", f, b, new RectangleF(0, size * 0.04f, size, size), sf);
            }
            return bmp;
        }

        static bool FontExists(string name)
        {
            foreach (var f in new System.Drawing.Text.InstalledFontCollection().Families) if (f.Name == name) return true;
            return false;
        }

        static Icon MakeIcon()
        {
            using (var bmp = IconBitmap(64))
            {
                IntPtr h = bmp.GetHicon();
                var ico = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return ico;
            }
        }

        public static System.Windows.Media.ImageSource WpfIcon()
        {
            using (var bmp = IconBitmap(64)) return ScreenShot.ToWpf(new Bitmap(bmp));
        }
    }

    /// <summary>Selbsttest: WoW-Lexikon.exe --selftest &lt;Ordner&gt; – prüft Glossar, Texterkennung, Dienste und rendert Vorschaubilder.</summary>
    static class SelfTest
    {
        public static async Task Run(Controller c, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            Action<string, bool, string> check = (name, ok, detail) => log.AppendLine((ok ? "OK    " : "FEHLER") + "  " + name + (detail != null ? "  →  " + detail : ""));
            try
            {
                c.Init();
                check("Glossar geladen", c.Glossary.Count > 3300 && c.Glossary.QuestWords == 3000, c.Glossary.Count + " Einträge, davon " + c.Glossary.QuestWords + " Questwörter");
                foreach (var t in new[] { "assaulted", "Ravagers", "slaughtered", "phylactery", "beckons", "Gratitude" })
                {
                    var g = c.Glossary.Find(t);
                    check("Questwort: " + t, g != null && g.IsQuestWord, g != null ? g.Term + " = " + g.Tr("de") + " /" + g.Ipa + "/" : "nicht gefunden");
                }
                var sw0 = System.Diagnostics.Stopwatch.StartNew();
                await c.GameDataTask;
                check("Spieldaten geladen", c.Quests.Ready && c.Names.Ready, c.Quests.TextCount + " Questtexte, " + c.Quests.SentenceCount + " Sätze, " + c.Names.Count + " Namen in "
                    + sw0.ElapsedMilliseconds + " ms, Speicher " + (GC.GetTotalMemory(true) / 1048576) + " MB");
                foreach (var t in new[] { "Defias Bandits", "Murloc Eye", "The Fargodeep Mine", "Linen Cloth", "Murloc Raider" })
                {
                    var ne = c.Names.Find(t);
                    check("Name: " + t, ne != null, ne != null ? ne.TypeLabel + ": " + ne.Tr : "nicht gefunden");
                }
                foreach (var line in new[] { "travel to the eastern Elwynn bridge and speak with Guard Thomas.",
                                             "travel to the eastem Elwynn bridqe and speak with Guard Th0mas",
                                             "stationed at the bridge for the past week and will know" })
                {
                    var t0 = System.Diagnostics.Stopwatch.StartNew();
                    var qm = c.Quests.Find(line, "bridge");
                    check("Questsatz: " + line, qm != null && qm.QuestId == 35, qm == null ? "kein Treffer" : "Quest " + qm.QuestId + " „" + qm.TitleTr + "“ (" + qm.Score.ToString("0.00") + ", " + t0.ElapsedMilliseconds + " ms): " + qm.Tr);
                }
                check("Kein Fehltreffer bei Chat-Text", c.Quests.Find("lol anyone wanna group for deadmines pst", null) == null, null);
                check("Bedeutung im Satz (offline)", SenseMatcher.Match("Haut (eines Tieres); verstecken", "Bringt mir die Häute der Eber. Ihr könnt Euch hier verstecken.") != null, SenseMatcher.Match("Haut (eines Tieres); verstecken", "Ihr könnt Euch hier nicht verstecken."));
                foreach (var t in new[] { "Murlocs", "wolves", "LFG", "Elwynn Forest", "slain", "Hearthstone", "Thunder Bluff", "Crossroads", "Onyxia's Lair", "rezzing" })
                {
                    var g = c.Glossary.Find(t);
                    check("Glossar: " + t, g != null, g != null ? g.Term + " = " + g.Tr("de") : "nicht gefunden");
                }

                // Texterkennung an einem nachgebauten Questtext
                using (var bmp = new Bitmap(900, 220))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.FromArgb(0x2B, 0x22, 0x18));
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        using (var f = new Font("Georgia", 15))
                        using (var b = new SolidBrush(Color.FromArgb(0x3A, 0x2A, 0x10)))
                        using (var paper = new SolidBrush(Color.FromArgb(0xE8, 0xD9, 0xB5)))
                        {
                            g.FillRectangle(paper, 20, 20, 860, 180);
                            g.DrawString("The murlocs along the coast have grown bold.", f, b, 40, 40);
                            g.DrawString("Slay 10 Murloc Raiders and return to Captain Grayson.", f, b, 40, 80);
                            g.DrawString("Beware of their ambush near the old tower.", f, b, 40, 120);
                        }
                    }
                    bmp.Save(Path.Combine(outDir, "ocr-test.png"));
                    string full = await Ocr.TextIn(bmp, new Rectangle(30, 30, 840, 130), "en");
                    check("OCR Rahmen (ganzer Text)", full.Contains("murlocs") && full.Contains("ambush"), full);
                    // Klick auf „Raiders“ (zweite Zeile, ca. Mitte)
                    var o = await Ocr.Recognize(bmp, new Rectangle(30, 30, 840, 130), "en");
                    OcrWord raiders = o.Words.Find(w => w.Text.StartsWith("Raiders"));
                    if (raiders != null)
                    {
                        var p = new Point((int)raiders.Center.X, (int)raiders.Center.Y);
                        string word = await Ocr.WordAt(bmp, p, "en");
                        check("OCR Klick auf Wort", word == "Raiders", word);
                        string line = await Ocr.LineAt(bmp, p, "en");
                        check("OCR Klick-Kontext (Zeile)", line.Contains("Slay") && line.Contains("Grayson"), line);
                        string boxed = await Ocr.TextIn(bmp, Rectangle.Round(new RectangleF(raiders.Box.X - 4, raiders.Box.Y - 4, raiders.Box.Width + 8, raiders.Box.Height + 8)), "en");
                        check("OCR enger Rahmen um ein Wort", boxed == "Raiders", boxed);
                    }
                    else check("OCR Wortpositionen", false, o.Text);

                    // Vorschau des Overlays
                    var ov = new OverlayWindow(new Bitmap(bmp), new Rectangle(0, 0, bmp.Width, bmp.Height));
                    if (raiders != null)
                    {
                        var wr = Rectangle.Round(raiders.Box); wr.Inflate(5, 4);
                        ov.SaveDemoImage(Path.Combine(outDir, "overlay.png"), wr, new Rectangle(34, 74, 640, 34), new Point(wr.Right + 2, wr.Bottom + 2));
                    }
                }

                // Dienste
                try { var r = await MyMemory.Translate("sword", "en", "de", ""); check("MyMemory", !string.IsNullOrEmpty(r.Text), r.Text + " | " + string.Join(", ", r.Alternatives)); }
                catch (Exception e) { check("MyMemory", false, e.Message); }
                try { var r = await MyMemory.Translate("He hides his magazines under the bed.", "en", "de", ""); check("MyMemory Satz", !string.IsNullOrEmpty(r.Text) && r.Text.Contains("Bett"), r.Text); }
                catch (Exception e) { check("MyMemory Satz", false, e.ToString()); }
                try { var d = await DictApi.Lookup("murlocs"); check("Wörterbuch (murlocs → kein Eintrag erwartet)", true, d == null ? "kein Eintrag" : d.Ipa); }
                catch (Exception e) { check("Wörterbuch", false, e.Message); }
                try { var d = await DictApi.Lookup("slaying"); check("Wörterbuch slaying→slay", d != null && d.Ipa != null, d == null ? "null" : d.Ipa + " | " + d.Audio + " | " + d.Example); }
                catch (Exception e) { check("Wörterbuch", false, e.Message); }
                check("Stimmen", Speech.Voices().Count > 0, Speech.Voices().Count + " Stimme(n), Englisch: " + Speech.HasVoiceFor("en"));

                // Komplettablauf ohne Vorlesen, Karten als Bild
                c.Cfg = new Settings { AutoSpeak = false, Provider = "mymemory" };
                var st = await c.Lookup("aggro", "Don't pull aggro before the tank is ready.", 0);
                check("Ablauf Glossar + Kontext", st.Translation != null && st.ContextTr != null, st.Translation + " | " + st.ContextTr + " | " + st.Ipa);
                c.Card.SaveImage(Path.Combine(outDir, "karte-glossar.png"));
                st = await c.Lookup("withered", null, 0);
                check("Ablauf freie Übersetzung", st.Translation != null, st.Translation + " | " + st.Ipa + " | " + st.Example + " | " + st.ExampleTr + " | " + string.Join(", ", st.Sources));
                c.Card.SaveImage(Path.Combine(outDir, "karte-frei.png"));
                // Echter Questtext → Bild → Texterkennung der Zeile → Questsatz + offizielle Übersetzung
                using (var qb = new Bitmap(980, 120))
                {
                    using (var g = Graphics.FromImage(qb))
                    {
                        g.Clear(Color.FromArgb(0xE8, 0xD9, 0xB5));
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        using (var f = new Font("Georgia", 15))
                        using (var b = new SolidBrush(Color.FromArgb(0x3A, 0x2A, 0x10)))
                        {
                            g.DrawString("If you are concerned that the rumors of murlocs are true, then do this --", f, b, 20, 20);
                            g.DrawString("travel to the eastern Elwynn bridge and speak with Guard Thomas.", f, b, 20, 60);
                        }
                    }
                    qb.Save(Path.Combine(outDir, "quest-test.png"));
                    var o = await Ocr.Recognize(qb, new Rectangle(0, 0, qb.Width, qb.Height), "en");
                    var bw = o.Words.Find(w => w.Text.StartsWith("bridge"));
                    string word = await Ocr.WordAt(qb, new Point((int)bw.Center.X, (int)bw.Center.Y), "en");
                    string autoLine = await Ocr.LineAt(qb, new Point((int)bw.Center.X, (int)bw.Center.Y), "en");
                    st = await c.Lookup(word, null, 0, autoLine);
                    check("Ablauf Klick auf Wort im Questtext", st.OfficialContext && st.ContextTr != null,
                        word + " = " + st.Translation + " | " + st.ContextTr + " | Hervorhebung: " + st.ContextTrHighlight + " | " + st.QuestTitle);
                    c.Card.SaveImage(Path.Combine(outDir, "karte-questtext.png"));
                    var ff = Theme.LoadFont(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "georgia.ttf"));
                    check("Schrift aus Datei laden", ff != null, ff != null ? ff.Source : "fehlgeschlagen");
                    foreach (var lk in new[] { "modern", "tooltip", "quest" })
                    {
                        Theme.ApplyLook(Application.Current, lk, null);
                        c.Card.SaveImage(Path.Combine(outDir, "karte-" + lk + ".png"));
                    }
                    var rootB = (System.Windows.Controls.Border)((FrameworkElement)c.Card.Content).FindName("Root");
                    var inner = (FrameworkElement)((FrameworkElement)c.Card.Content).FindName("Body");
                    check("Karte misst Inhalt komplett", rootB.ActualHeight + 1 >= inner.ActualHeight + inner.Margin.Top + inner.Margin.Bottom,
                        "Rahmen " + rootB.ActualHeight.ToString("0") + " / Inhalt " + (inner.ActualHeight + inner.Margin.Top + inner.Margin.Bottom).ToString("0"));
                }
                st = await c.Lookup("Defias Bandit", null, 0);
                check("Ablauf Name", st.Translation == "Bandit der Defias", st.Translation + " | " + st.Note);
                st = await c.Lookup("travel to the eastern Elwynn bridge and speak with Guard Thomas.", null, 0);
                check("Ablauf ganzer Satz eingerahmt", st.Translation != null && st.Sources.Contains("Questtext aus dem Spiel"), st.Translation);

                st = await c.Lookup("hide", "Bring me the hide of the old boar.", 0);
                check("Kontext ohne KI: hide", st.Sense != null, st.Translation + " | " + st.ContextTr + " | Hier gemeint: " + st.Sense);
                c.Card.SaveImage(Path.Combine(outDir, "karte-kontext.png"));

                // ---------- Begleit-Programm mit nachgebautem WoW-Ordner ----------
                string fakeRoot = Path.Combine(outDir, "FakeWoW"), fake = Path.Combine(fakeRoot, "_classic_era_");
                if (Directory.Exists(fakeRoot)) Directory.Delete(fakeRoot, true);
                Directory.CreateDirectory(Path.Combine(fake, "Interface", "AddOns"));
                string fake2 = Path.Combine(fakeRoot, "_retail_");
                Directory.CreateDirectory(Path.Combine(fake2, "Interface", "AddOns"));
                // Wie beim Spieler: Ordner heißt einfach „forever“, noch ohne Interface-Ordner – nur WTF und Spielprogramm
                string fake3 = Path.Combine(fakeRoot, "forever");
                Directory.CreateDirectory(Path.Combine(fake3, "WTF"));
                File.WriteAllBytes(Path.Combine(fake3, "WowForever.exe"), new byte[] { 0x4D, 0x5A });
                File.WriteAllText(Path.Combine(fakeRoot, ".build.info"),
                    "Branch!STRING:0|Active!DEC:1|Build Key!HEX:16|CDN Key!HEX:16|Install Key!HEX:16|IM Size!DEC:4|CDN Path!STRING:0|CDN Hosts!STRING:0|CDN Servers!STRING:0|Tags!STRING:0|Armadillo!STRING:0|Last Activated!STRING:0|Version!STRING:0|KeyRing!HEX:16|Product!STRING:0\n" +
                    "eu|1|aa|bb|cc||tpr/wow|eu.cdn|https://x|Windows x86_64 EU? enUS speech?:Windows x86_64 EU? enUS text?|||1.15.9.70000||wow_classic_era\n" +
                    "eu|1|aa|bb|cc||tpr/wow|eu.cdn|https://x|Windows x86_64 EU? enUS speech?:Windows x86_64 EU? enUS text?|||1.16.2.70001||wow_forever\n");
                string svDir = Path.Combine(fake, "WTF", "Account", "TESTKONTO", "SavedVariables");
                Directory.CreateDirectory(svDir);
                File.WriteAllText(Path.Combine(svDir, "WowLexikon.lua"),
                    "\nWowLexikonDB = {\n[\"seen\"] = {\n[35] = {\n[\"T\"] = \"Further Concerns\",\n" +
                    "[\"D\"] = \"If you are concerned that the rumors of murlocs are true, then do this -- travel to the eastern Elwynn bridge and speak with Guard Thomas.  He has been stationed at the bridge for the past week and will know the state of the area.\\n\\nBring me his report.\",\n[\"t\"] = 1700000000,\n},\n" +
                    "[990001] = {\n[\"T\"] = \"A Bundle of Furs\",\n[\"D\"] = \"You there! Would you happen to be passing by Hatherford?\\n\\nI have a delivery to make there, but my normal delivery person fell ill after a trip to Anglepoint.\",\n" +
                    "[\"O\"] = \"Deliver the Bundle of Warm Furs to Dandy Jones in Hatherford.\",\n},\n},\n" +
                    "[\"unknown\"] = {\n[\"brouhaha\"] = 2,\n[\"hullabaloo\"] = 1,\n},\n[\"build\"] = {\n[\"version\"] = \"1.15.8\",\n[\"build\"] = \"99999\",\n[\"toc\"] = 11610,\n},\n" +
                    "[\"vocab\"] = {\n},\n[\"options\"] = {\n[\"autoPanel\"] = true,\n},\n}\n", new UTF8Encoding(false));
                AddonSync.StoreOverride = Path.Combine(outDir, "gelernt-test.json");
                if (File.Exists(AddonSync.StoreOverride)) File.Delete(AddonSync.StoreOverride);
                c.Cfg.WowFolder = fakeRoot;
                c.Addon = new AddonSync(c);
                check("WoW-Ordner gefunden, Forever zuerst", c.Addon.GameDir == fake3 && c.Addon.GameDirs.Count == 3, string.Join(" | ", c.Addon.GameDirs.Select(Path.GetFileName)));
                var installedIn = c.Addon.InstallAddon(AddonSync.EmbeddedAddon());
                check("Addon installiert", c.Addon.InstalledVersion == "1.0.0", "Version " + c.Addon.InstalledVersion);
                check("In alle WoW-Versionen installiert (auch forever\\Interface\\AddOns)", installedIn.Count == 3 && File.Exists(Path.Combine(fake3, "Interface", "AddOns", "WowLexikon", "WowLexikon.toc")) && File.Exists(Path.Combine(fake2, "Interface", "AddOns", "WowLexikon", "WowLexikon.toc")), string.Join(" | ", installedIn));
                string tocLine = File.ReadAllLines(Path.Combine(fake3, "Interface", "AddOns", "WowLexikon", "WowLexikon.toc")).First(l => l.StartsWith("## Interface:"));
                check("Spielversion aus .build.info + Addon-Meldung in TOC eingetragen (nicht „veraltet“)", tocLine.Contains("11509") && tocLine.Contains("11602") && tocLine.StartsWith("## Interface: 11610,"), tocLine);
                int nq = c.Addon.WriteGameDataAddon("de");
                check("Spieldaten ins Addon geschrieben", nq > 4000 && File.Exists(Path.Combine(fake, "Interface", "AddOns", "WowLexikon_Data", "WowLexikon_Data.toc"))
                      && File.Exists(Path.Combine(fake2, "Interface", "AddOns", "WowLexikon_Data", "WowLexikon_Data.toc")) && File.Exists(Path.Combine(fake3, "Interface", "AddOns", "WowLexikon_Data", "WowLexikon_Data.toc")), nq + " Quests, in allen Versionen");
                var sr = await c.Addon.Sync(m => { });
                check("Abgleich: Spielversion erkannt", sr.Build == "1.15.8 (Build 99999)", sr.Build);
                check("Abgleich: unbekannte Wörter online gelernt", sr.NewWords >= 1, c.Addon.LastMessage);
                check("Abgleich: neue Quest übersetzt, bekannte nicht", sr.NewQuests == 1 && sr.ChangedQuests == 0, sr.NewQuests + " neu, " + sr.ChangedQuests + " geändert");
                string learnedLua = File.ReadAllText(Path.Combine(fake3, "Interface", "AddOns", "WowLexikon", "Data", "Learned.lua"), Encoding.UTF8);
                check("Gelerntes ins Addon zurückgeschrieben", learnedLua.Contains("[990001]={") && learnedLua.Contains("[\"brouhaha\"]"),
                    learnedLua.Length > 400 ? learnedLua.Substring(0, 400).Replace("\n", " ") : learnedLua);
                // Wörter aus den Forever-Spieldaten (von GitHub) → ins Addon, ohne Gelerntes zu verlieren
                var fw = new StringBuilder();
                foreach (var w in new[] { "condor", "gorilla", "quartermaster", "banishment", "iridescent", "rasp", "ankh", "pinecone", "stinky", "lockout", "midsummer", "undesired" })
                    fw.Append(w).Append("||Test-").Append(w).Append('\n');
                fw.Append("Ragnaros||kaputt\nkaputte zeile\n");
                int nfw = c.Addon.ApplyForeverWords(fw.ToString());
                string learnedFw = File.ReadAllText(Path.Combine(fake3, "Interface", "AddOns", "WowLexikon", "Data", "Learned.lua"), Encoding.UTF8);
                check("Forever-Wörter übernommen (Gelerntes bleibt, Unsinn nicht)", nfw == 12 && learnedFw.Contains("[\"condor\"]=\"|Test-condor") && learnedFw.Contains("[\"brouhaha\"]") && !learnedFw.Contains("Ragnaros"), nfw + " neu");
                check("Dieselbe Liste nochmal: nichts Neues", c.Addon.ApplyForeverWords(fw.ToString()) == 0, null);
                check("Adresse der Wortliste aus der Update-Adresse", c.Addon.ForeverWordsUrl == "https://raw.githubusercontent.com/Baougam/wow-lexikon/main/data/woerter-forever.txt", c.Addon.ForeverWordsUrl);
                var sr2 = await c.Addon.Sync(m => { });
                check("Zweiter Abgleich übersetzt nichts doppelt", sr2.NewWords == 0 && sr2.NewQuests == 0, c.Addon.LastMessage);
                File.WriteAllText(Path.Combine(svDir, "WowLexikon.lua"), File.ReadAllText(Path.Combine(svDir, "WowLexikon.lua")).Replace("99999", "100123"));
                var sr3 = await c.Addon.Sync(m => { });
                check("Spiel-Update erkannt", sr3.BuildChanged, sr3.Build);
                AddonSync.StoreOverride = null;
                var swScan = System.Diagnostics.Stopwatch.StartNew();
                var scan = AddonSync.FindGameDirs(null);
                check("Suche auf allen Laufwerken (inkl. Battle.net-Angaben) ist schnell", swScan.ElapsedMilliseconds < 20000, scan.Count + " WoW-Ordner auf diesem PC, " + swScan.ElapsedMilliseconds + " ms");

                // Spieldaten selbst erzeugen (Weg für weitergegebene Programme ohne eingebaute Daten)
                if (Directory.Exists(Path.Combine(outDir, "gdb", "sql")))
                {
                    GameDataBuilder.DirOverride = Path.Combine(outDir, "gdb");
                    string last = "";
                    var swb = System.Diagnostics.Stopwatch.StartNew();
                    await GameDataBuilder.Build("de", m => last = m);
                    check("Spieldaten selbst erzeugt", last.Contains("18747 Questtexte") && last.Contains("75375 Namen"), last + " in " + swb.Elapsed.TotalSeconds.ToString("0") + " s");
                    GameDataBuilder.DirOverride = null;
                }

                c.Cfg.Provider = "auto";
                var sw = new SettingsWindow(c.Cfg, c);
                var ui = (FrameworkElement)sw.Content;
                sw.Content = null;
                ui.Measure(new System.Windows.Size(620, 1500)); ui.Arrange(new Rect(0, 0, 620, 1500)); ui.UpdateLayout();
                Shots.Save(ui, Path.Combine(outDir, "einstellungen.png"), 620, 1500);
                check("Einstellungsfenster", true, null);
            }
            catch (Exception e) { check("Ausnahme", false, e.ToString()); }
            File.WriteAllText(Path.Combine(outDir, "selbsttest.txt"), log.ToString(), new UTF8Encoding(true));
        }
    }
}
