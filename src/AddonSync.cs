using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WowLexikon
{
    /// <summary>
    /// Begleit-Programm für das Addon:
    ///  • findet den WoW-Ordner und installiert/aktualisiert das Addon
    ///  • erzeugt die Spieldaten (deutsche Questtexte + Namen) lokal als Addon-Dateien
    ///  • liest nach dem Ausloggen, was das Addon gemerkt hat (unbekannte Wörter, gesehene Questtexte, Spielversion)
    ///  • übersetzt Neues online und schreibt es ins Addon zurück (Data\Learned.lua)
    ///  • erkennt Spiel-Updates und geänderte Questtexte
    /// </summary>
    public class AddonSync
    {
        public const string AddonName = "WowLexikon", DataAddonName = "WowLexikon_Data";
        readonly Controller app;
        public string LastMessage = "";
        public bool Busy;

        public AddonSync(Controller app) { this.app = app; }

        // ---------------- Ordner ----------------

        static bool LooksLikeGameDir(string d)
        {
            try
            {
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return false;
                // Der eigene Programmordner ist kein WoW-Ordner (WowLexikon.exe beginnt auch mit „Wow“)
                if (string.Equals(Path.GetFullPath(d).TrimEnd('\\'), Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
                if (Directory.Exists(Path.Combine(d, "Interface")) || Directory.Exists(Path.Combine(d, "WTF"))) return true;
                // Spielprogramm im Ordner (Wow.exe, WowClassic.exe, „World of Warcraft Forever.exe“ …)
                return Directory.GetFiles(d, "*.exe").Any(x =>
                {
                    var n = Path.GetFileName(x).ToLowerInvariant();
                    if (n.Contains("lexikon") || n.Contains("launcher") || n.Contains("error") || n.Contains("setup") || n.Contains("uninstall") || n.Contains("updater")) return false;
                    return n.StartsWith("wow") || n.StartsWith("world of warcraft");
                });
            }
            catch { return false; }
        }

        /// <summary>Spielordner (der mit Interface\AddOns und WTF). Bei Blizzard-Installationen ein Unterordner wie _classic_era_.</summary>
        public static string FindGameDir(string saved)
        {
            if (LooksLikeGameDir(saved) && !HasFlavors(saved)) return saved;
            if (!string.IsNullOrEmpty(saved) && HasFlavors(saved)) return BestFlavor(saved);
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    string n = p.ProcessName.ToLowerInvariant();
                    if (!(n.StartsWith("wow") || n.Contains("warcraft"))) continue;
                    string d = Path.GetDirectoryName(p.MainModule.FileName);
                    if (LooksLikeGameDir(d)) return d;
                }
                catch { }
            }
            var roots = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
            foreach (var drive in DriveInfo.GetDrives()) { try { if (drive.DriveType == DriveType.Fixed) { roots.Add(drive.RootDirectory.FullName); roots.Add(Path.Combine(drive.RootDirectory.FullName, "Games")); roots.Add(Path.Combine(drive.RootDirectory.FullName, "Spiele")); } } catch { } }
            foreach (var r in roots)
                foreach (var name in new[] { "World of Warcraft", "WoW", "WoW Forever", "World of Warcraft Forever" })
                {
                    string b = Path.Combine(r, name);
                    if (HasFlavors(b)) return BestFlavor(b);
                    if (LooksLikeGameDir(b)) return b;
                }
            return null;
        }

        /// <summary>Unterordner, die selbst Spielordner sind (_retail_, _classic_era_, forever …).</summary>
        static IEnumerable<string> SubGameDirs(string b)
        {
            foreach (var d in Directory.GetDirectories(b))
            {
                var n = Path.GetFileName(d).ToLowerInvariant();
                if (n == "interface" || n == "wtf" || n == "data" || n == "logs" || n == "cache" || n == "errors" || n == "utils") continue;
                if (LooksLikeGameDir(d)) yield return d;
            }
        }

        static bool HasFlavors(string b)
        {
            try { return Directory.Exists(b) && SubGameDirs(b).Any(); } catch { return false; }
        }
        /// <summary>Unter mehreren Spielversionen die zuletzt gespielte (jüngster WTF-Ordner), Forever bevorzugt.</summary>
        static string BestFlavor(string b)
        {
            var dirs = SubGameDirs(b).ToList();
            var forever = dirs.FirstOrDefault(d => Path.GetFileName(d).ToLowerInvariant().Contains("forever"));
            if (forever != null) return forever;
            return dirs.OrderByDescending(d => { var w = Path.Combine(d, "WTF"); return Directory.Exists(w) ? Directory.GetLastWriteTimeUtc(w) : DateTime.MinValue; }).First();
        }

        /// <summary>
        /// Alle Spielversionen, in die das Addon soll. Der Battle.net-Launcher legt jede WoW-Version in einen eigenen
        /// Unterordner (_retail_, _classic_era_, …) – wie der von WoW Forever heißt, ist unbekannt, deshalb: alle.
        /// </summary>
        public static List<string> FindGameDirs(string saved)
        {
            var list = new List<string>();
            Action<string> add = d => { if (d != null && LooksLikeGameDir(d) && !list.Any(x => string.Equals(x, d, StringComparison.OrdinalIgnoreCase))) list.Add(d); };
            Action<string> addBase = b => { if (HasFlavors(b)) foreach (var d in SubGameDirs(b)) add(d); else add(b); };
            if (!string.IsNullOrEmpty(saved))
            {
                saved = saved.Trim().TrimEnd('\\');
                // …\Interface\AddOns oder …\Interface eingetragen? Dann zum Spielordner hochgehen.
                if (Path.GetFileName(saved).Equals("AddOns", StringComparison.OrdinalIgnoreCase)) saved = Path.GetDirectoryName(saved);
                if (saved != null && Path.GetFileName(saved).Equals("Interface", StringComparison.OrdinalIgnoreCase)) saved = Path.GetDirectoryName(saved);
                addBase(saved);
                // Unterordner gewählt? Dann auch die Geschwister im selben WoW-Ordner berücksichtigen
                var parent = Path.GetDirectoryName(saved.TrimEnd('\\'));
                if (parent != null && !LooksLikeGameDir(parent) && HasFlavors(parent) && SubGameDirs(parent).Count() > 1) addBase(parent);
                if (list.Count > 0) return Sort(list);
            }
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    string n = p.ProcessName.ToLowerInvariant();
                    if (!(n.StartsWith("wow") || n.Contains("warcraft"))) continue;
                    string d = Path.GetDirectoryName(p.MainModule.FileName);
                    add(d);
                    var parent = Path.GetDirectoryName(d);
                    if (parent != null && HasFlavors(parent)) addBase(parent);
                }
                catch { }
            }
            // Battle.net weiß, wo WoW installiert ist (Registry und eigene Datenbank)
            foreach (var d in BattleNetPaths()) { try { addBase(d); var parent = Path.GetDirectoryName(d); if (parent != null && HasFlavors(parent)) addBase(parent); } catch { } }
            if (list.Count > 0) return Sort(list);
            // Sonst alle Festplatten durchsuchen: Ordner mit „WoW“ oder „Warcraft“ im Namen, bis zwei Ebenen tief
            foreach (var drive in DriveInfo.GetDrives())
            {
                try { if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue; } catch { continue; }
                foreach (var d in Candidates(drive.RootDirectory.FullName, 0)) { try { addBase(d); } catch { } }
            }
            return Sort(list);
        }

        static readonly Regex wowName = new Regex(@"wow|warcraft", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly HashSet<string> skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Windows", "$Recycle.Bin", "System Volume Information", "ProgramData", "Recovery", "PerfLogs", "AppData", "node_modules", ".git" };

        static IEnumerable<string> Candidates(string dir, int depth)
        {
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { yield break; }
            foreach (var s in subs)
            {
                string n = Path.GetFileName(s);
                if (skipDirs.Contains(n) || n.StartsWith(".")) continue;
                if (wowName.IsMatch(n)) yield return s;
                else if (depth < 2) foreach (var c in Candidates(s, depth + 1)) yield return c;
            }
        }

        /// <summary>Installationsorte aus der Registry und aus der Datenbank des Battle.net-Launchers.</summary>
        static IEnumerable<string> BattleNetPaths()
        {
            var found = new List<string>();
            foreach (var key in new[] { @"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft", @"SOFTWARE\Blizzard Entertainment\World of Warcraft",
                                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\World of Warcraft", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\World of Warcraft" })
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))
                    {
                        if (k == null) continue;
                        foreach (var v in new[] { "InstallPath", "GamePath", "InstallLocation" })
                        {
                            var p = k.GetValue(v) as string;
                            if (string.IsNullOrEmpty(p)) continue;
                            if (File.Exists(p)) p = Path.GetDirectoryName(p);
                            found.Add(p.TrimEnd('\\'));
                        }
                    }
                }
                catch { }
            }
            try
            {
                string db = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Battle.net", "Agent", "product.db");
                if (File.Exists(db))
                {
                    string raw = Encoding.UTF8.GetString(File.ReadAllBytes(db));
                    foreach (Match m in Regex.Matches(raw, @"[A-Za-z]:[/\\][^\x00-\x1F""<>|*?]{2,200}"))
                    {
                        string p = m.Value.Replace('/', '\\').TrimEnd('\\');
                        if (wowName.IsMatch(p)) found.Add(p);
                    }
                }
            }
            catch { }
            return found.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);
        }

        /// <summary>Forever zuerst, dann die zuletzt gespielte Version.</summary>
        static List<string> Sort(List<string> dirs)
        {
            return dirs.OrderByDescending(d => Path.GetFileName(d).ToLowerInvariant().Contains("forever"))
                       .ThenByDescending(d => { var w = Path.Combine(d, "WTF"); return Directory.Exists(w) ? Directory.GetLastWriteTimeUtc(w) : DateTime.MinValue; })
                       .ToList();
        }

        public List<string> GameDirs { get { return FindGameDirs(app.Cfg.WowFolder); } }
        public string GameDir { get { var l = GameDirs; return l.Count > 0 ? l[0] : null; } }
        public string AddOnsDir { get { var g = GameDir; return g == null ? null : Path.Combine(g, "Interface", "AddOns"); } }
        public IEnumerable<string> AllAddOnsDirs { get { return GameDirs.Select(g => Path.Combine(g, "Interface", "AddOns")); } }
        public static string FlavorName(string gameDir) { return Path.Combine(gameDir, "Interface", "AddOns"); }

        public string InstalledVersion
        {
            get
            {
                try
                {
                    var toc = Path.Combine(AddOnsDir, AddonName, AddonName + ".toc");
                    var m = Regex.Match(File.ReadAllText(toc), @"##\s*Version:\s*(\S+)");
                    return m.Success ? m.Groups[1].Value : null;
                }
                catch { return null; }
            }
        }

        // ---------------- Installieren ----------------

        /// <summary>Installiert das Addon aus dem eingebauten Paket (oder einem heruntergeladenen ZIP). Gelerntes bleibt erhalten.</summary>
        public List<string> InstallAddon(Stream zip)
        {
            var dirs = AllAddOnsDirs.ToList();
            if (dirs.Count == 0) throw new Exception("WoW-Ordner nicht gefunden – bitte in den Einstellungen auswählen.");
            var ms = new MemoryStream(); zip.CopyTo(ms);
            foreach (var addons in dirs) { ms.Position = 0; InstallInto(addons, ms); }
            WriteLearned();
            UpdateTocVersions();
            return GameDirs.Select(FlavorName).ToList();
        }

        void InstallInto(string addons, Stream zip)
        {
            Directory.CreateDirectory(addons);
            using (var z = new ZipArchive(zip, ZipArchiveMode.Read, true))
            {
                foreach (var e in z.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string rel = e.FullName.Replace('/', '\\');
                    if (!rel.StartsWith(AddonName + "\\", StringComparison.OrdinalIgnoreCase)) continue;   // nur das Haupt-Addon
                    string target = Path.GetFullPath(Path.Combine(addons, rel));
                    if (!target.StartsWith(Path.GetFullPath(addons), StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.EndsWith("\\Data\\Learned.lua", StringComparison.OrdinalIgnoreCase) && File.Exists(target)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var s = e.Open()) using (var f = File.Create(target)) s.CopyTo(f);
                }
            }
        }

        // ---------------- Versionsnummer des Spiels („## Interface:“) ----------------

        public const string DefaultInterfaces = "16001, 11509, 11508, 11507";   // 16001 = WoW Forever 1.60.1 (wago.tools: Produkt „wowf“)

        /// <summary>
        /// Interface-Nummern, die zum installierten Spiel passen: aus .build.info (Version 1.15.8 → 11508) und aus dem,
        /// was das Addon im Spiel selbst gemeldet hat (GetBuildInfo). So gilt das Addon nicht als „veraltet“.
        /// </summary>
        public List<int> DetectInterfaces()
        {
            var nums = new List<int>();   // Reihenfolge = Wichtigkeit: zuerst, was das Spiel selbst meldet
            var bases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in GameDirs) { bases.Add(g); var p = Path.GetDirectoryName(g); if (p != null) bases.Add(p); }
            foreach (var b in bases)
            {
                try
                {
                    string info = Path.Combine(b, ".build.info");
                    if (!File.Exists(info)) continue;
                    var lines = File.ReadAllLines(info);
                    if (lines.Length < 2) continue;
                    var head = lines[0].Split('|').Select(h => h.Split('!')[0].Trim()).ToList();
                    int vi = head.FindIndex(h => h.Equals("Version", StringComparison.OrdinalIgnoreCase));
                    if (vi < 0) continue;
                    foreach (var l in lines.Skip(1))
                    {
                        var c = l.Split('|');
                        if (vi < c.Length) { int n = InterfaceFromVersion(c[vi]); if (n > 0 && !nums.Contains(n)) nums.Add(n); }
                    }
                }
                catch { }
            }
            foreach (var f in SavedVariableFiles())
            {
                try
                {
                    var m = Regex.Match(File.ReadAllText(f), @"\[""toc""\]\s*=\s*(\d+)");
                    if (m.Success) { int n = int.Parse(m.Groups[1].Value); nums.Remove(n); nums.Insert(0, n); }
                }
                catch { }
            }
            return nums;
        }

        /// <summary>„1.15.8.62345“ → 11508, „11.0.5.57000“ → 110005</summary>
        public static int InterfaceFromVersion(string v)
        {
            var m = Regex.Match(v ?? "", @"^(\d+)\.(\d+)\.(\d+)");
            if (!m.Success) return 0;
            return int.Parse(m.Groups[1].Value) * 10000 + int.Parse(m.Groups[2].Value) * 100 + int.Parse(m.Groups[3].Value);
        }

        /// <summary>Schreibt die passenden Interface-Nummern in die TOC-Dateien beider Addons (in allen Spielversionen).</summary>
        public string UpdateTocVersions()
        {
            var found = DetectInterfaces();
            var all = new List<int>(found);
            foreach (var s in DefaultInterfaces.Split(',')) { int n = int.Parse(s.Trim()); if (!all.Contains(n)) all.Add(n); }
            string line = "## Interface: " + string.Join(", ", all);
            foreach (var addons in AllAddOnsDirs)
                foreach (var toc in new[] { Path.Combine(addons, AddonName, AddonName + ".toc"), Path.Combine(addons, DataAddonName, DataAddonName + ".toc") })
                {
                    try
                    {
                        if (!File.Exists(toc)) continue;
                        string text = File.ReadAllText(toc, Encoding.UTF8);
                        string updated = Regex.Replace(text, @"^## Interface:.*$", line, RegexOptions.Multiline);
                        if (updated != text) File.WriteAllText(toc, updated, new UTF8Encoding(false));
                    }
                    catch { }
                }
            return found.Count > 0 ? string.Join(", ", found) : null;
        }

        public static Stream EmbeddedAddon() { return Assembly.GetExecutingAssembly().GetManifestResourceStream("WowLexikon.addon.zip"); }

        // ---------------- Spieldaten als Addon-Dateien ----------------

        /// <summary>Schreibt WowLexikon_Data (deutsche Questtexte + Namen) – lokal erzeugt, nie weitergegeben.</summary>
        public int WriteGameDataAddon(string lang)
        {
            string dir = Path.Combine(AddOnsDir, DataAddonName);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.lua")) File.Delete(f);
            var names = new Dictionary<string, string>(); var rank = new Dictionary<char, int> { { 'n', 0 }, { 'i', 1 }, { 'q', 2 }, { 'o', 3 } };
            var nameRank = new Dictionary<string, int>();
            foreach (var l in GzLines("namen-" + lang + ".txt.gz"))
            {
                var p = l.Split('\t'); if (p.Length < 3 || p[0].Length != 1) continue;
                string k = p[1].ToLowerInvariant(); int rk = rank.ContainsKey(p[0][0]) ? rank[p[0][0]] : 9; int old;
                if (nameRank.TryGetValue(k, out old) && old <= rk) continue;
                nameRank[k] = rk; names[k] = p[0] + "|" + p[2] + "|" + (p.Length > 4 ? p[4] : "");
            }
            var quests = new SortedDictionary<int, Dictionary<string, string>>();
            foreach (var l in GzLines("quests-" + lang + ".txt.gz"))
            {
                var p = l.Split('\t'); int id; if (p.Length != 4 || !int.TryParse(p[0], out id) || "TDOQRC".IndexOf(p[1], StringComparison.Ordinal) < 0) continue;
                Dictionary<string, string> q; if (!quests.TryGetValue(id, out q)) quests[id] = q = new Dictionary<string, string>();
                q[p[1]] = Regex.Replace(p[3], @"\$[Bb]", "\n");
            }
            var files = new List<string>();
            files.AddRange(WriteChunks(dir, "Names", "WowLexikon_Names", names.Select(kv => new KeyValuePair<string, string>(Lua.Str(kv.Key), Lua.Str(kv.Value))).ToList(), 15000));
            files.AddRange(WriteChunks(dir, "Quests", "WowLexikon_Quests", quests.Select(kv => new KeyValuePair<string, string>(kv.Key.ToString(CultureInfo.InvariantCulture),
                "{" + string.Join(",", kv.Value.Select(f => f.Key + "=" + Lua.Str(f.Value))) + "}")).ToList(), 1500));
            File.WriteAllText(Path.Combine(dir, DataAddonName + ".toc"),
                "## Interface: " + DefaultInterfaces + "\n## Title: WoW Lexikon – Spieldaten\n## Notes: Deutsche Questtexte und Namen (lokal erzeugt vom Begleit-Programm)\n" +
                "## Dependencies: WowLexikon\n## LoadOnDemand: 1\n## Version: " + DateTime.Now.ToString("yyyy.MM.dd") + "\n\n" + string.Join("\n", files) + "\n", new UTF8Encoding(false));
            // In alle anderen Spielversionen kopieren
            foreach (var other in AllAddOnsDirs.Skip(1))
            {
                string od = Path.Combine(other, DataAddonName);
                if (!Directory.Exists(Path.Combine(other, AddonName))) continue;
                Directory.CreateDirectory(od);
                foreach (var old in Directory.GetFiles(od, "*.*")) File.Delete(old);
                foreach (var src in Directory.GetFiles(dir)) File.Copy(src, Path.Combine(od, Path.GetFileName(src)), true);
            }
            UpdateTocVersions();
            return quests.Count;
        }

        static IEnumerable<string> GzLines(string name)
        {
            using (var s = QuestCorpus.Resource(name))
            {
                if (s == null) throw new Exception("Spieldaten fehlen (" + name + ") – in den Einstellungen „Spieldaten erzeugen“ wählen.");
                using (var gz = new GZipStream(s, CompressionMode.Decompress))
                using (var r = new StreamReader(gz, Encoding.UTF8))
                {
                    string line;
                    var all = new List<string>();
                    while ((line = r.ReadLine()) != null) all.Add(line);
                    return all;
                }
            }
        }

        static List<string> WriteChunks(string dir, string baseName, string global, List<KeyValuePair<string, string>> entries, int per)
        {
            var files = new List<string>();
            for (int i = 0, n = 1; i < entries.Count; i += per, n++)
            {
                string name = baseName + n + ".lua";
                var sb = new StringBuilder("-- Lokal erzeugt vom WoW-Lexikon-Begleit-Programm – nicht weitergeben\nlocal T = " + global + "\nlocal add = {\n");
                foreach (var kv in entries.Skip(i).Take(per)) sb.Append('[').Append(kv.Key).Append("]=").Append(kv.Value).Append(",\n");
                sb.Append("}\nfor k, v in pairs(add) do T[k] = v end\n");
                File.WriteAllText(Path.Combine(dir, name), sb.ToString(), new UTF8Encoding(false));
                files.Add(name);
            }
            return files;
        }

        // ---------------- Gelerntes ----------------

        public class LearnedStore
        {
            public Dictionary<string, string> Words = new Dictionary<string, string>();                       // wort → "ipa|de|hinweis|beispiel|beispiel de|l"
            public Dictionary<string, Dictionary<string, string>> Quests = new Dictionary<string, Dictionary<string, string>>();  // id → {T,D,O,Q,R, src_*=englisch, changed}
            public string Build = "", LastSync = "";
            public List<string> Updates = new List<string>();
            public Dictionary<string, string> Forever = new Dictionary<string, string>();   // Wörter aus den Forever-Spieldaten (Wörterbuch-Übersetzung)
            public string ForeverHash = "";
        }
        public static string StoreOverride;   // nur für den Selbsttest
        static string StoreFile { get { return StoreOverride ?? Path.Combine(Settings.Dir, "gelernt.json"); } }
        LearnedStore store;
        public LearnedStore Store
        {
            get
            {
                if (store != null) return store;
                try { if (File.Exists(StoreFile)) store = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<LearnedStore>(File.ReadAllText(StoreFile, Encoding.UTF8)); } catch { }
                return store ?? (store = new LearnedStore());
            }
        }
        void SaveStore() { File.WriteAllText(StoreFile, new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(Store), new UTF8Encoding(false)); }

        public void WriteLearned()
        {
            var targets = AllAddOnsDirs.Where(a => Directory.Exists(Path.Combine(a, AddonName))).ToList();
            if (targets.Count == 0) return;
            var s = Store;
            var sb = new StringBuilder("-- Vom WoW-Lexikon-Begleit-Programm geschrieben (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n");
            sb.Append("WowLexikon_Learned = { build = ").Append(Lua.Str(s.Build)).Append(", generated = ").Append(Lua.Str(s.LastSync)).Append(",\nwords = {\n");
            // Wörterbuch-Übersetzungen aus den Forever-Spieldaten gehen vor Online-Übersetzungen
            var merged = new Dictionary<string, string>(s.Words ?? new Dictionary<string, string>());
            if (s.Forever != null) foreach (var kv in s.Forever) merged[kv.Key] = kv.Value;
            foreach (var kv in merged) sb.Append('[').Append(Lua.Str(kv.Key)).Append("]=").Append(Lua.Str(kv.Value)).Append(",\n");
            sb.Append("},\nquests = {\n");
            foreach (var kv in s.Quests)
            {
                int id; if (!int.TryParse(kv.Key, out id)) continue;
                var fields = kv.Value.Where(f => f.Key.Length == 1 || f.Key == "changed").Select(f => f.Key + "=" + (f.Key == "changed" ? "true" : Lua.Str(f.Value)));
                sb.Append('[').Append(id).Append("]={").Append(string.Join(",", fields)).Append("},\n");
            }
            sb.Append("} }\n");
            foreach (var addons in targets)
            {
                Directory.CreateDirectory(Path.Combine(addons, AddonName, "Data"));
                File.WriteAllText(Path.Combine(addons, AddonName, "Data", "Learned.lua"), sb.ToString(), new UTF8Encoding(false));
            }
        }

        // ---------------- Abgleich ----------------

        public IEnumerable<string> SavedVariableFiles()
        {
            foreach (var g in GameDirs)
            {
                var wtf = Path.Combine(g, "WTF", "Account");
                if (!Directory.Exists(wtf)) continue;
                foreach (var f in Directory.GetFiles(wtf, AddonName + ".lua", SearchOption.AllDirectories))
                    if (f.IndexOf("SavedVariables", StringComparison.OrdinalIgnoreCase) >= 0) yield return f;
            }
        }

        public class SyncResult { public int NewWords, NewQuests, ChangedQuests, Pending; public string Build; public bool BuildChanged; }

        /// <summary>Liest, was das Addon gemerkt hat, übersetzt Neues online und schreibt es zurück.</summary>
        public async Task<SyncResult> Sync(Action<string> progress)
        {
            if (Busy) return null;
            Busy = true;
            var res = new SyncResult();
            try
            {
                var s = Store;
                var unknown = new Dictionary<string, int>(); var seen = new Dictionary<string, Dictionary<string, string>>();
                var vocab = new List<Dictionary<object, object>>();
                string build = null;
                foreach (var f in SavedVariableFiles())
                {
                    var db = Lua.ParseAssignments(File.ReadAllText(f, Encoding.UTF8));
                    object o; if (!db.TryGetValue("WowLexikonDB", out o)) continue;
                    var t = o as Dictionary<object, object>; if (t == null) continue;
                    var u = Get(t, "unknown"); if (u != null) foreach (var kv in u) { int c; int.TryParse(Convert.ToString(kv.Value, CultureInfo.InvariantCulture), out c); string w = Convert.ToString(kv.Key); unknown[w] = (unknown.ContainsKey(w) ? unknown[w] : 0) + c; }
                    var sn = Get(t, "seen"); if (sn != null) foreach (var kv in sn) { var q = kv.Value as Dictionary<object, object>; if (q == null) continue; var d = new Dictionary<string, string>(); foreach (var f2 in q) d[Convert.ToString(f2.Key)] = Convert.ToString(f2.Value, CultureInfo.InvariantCulture); seen[Convert.ToString(kv.Key, CultureInfo.InvariantCulture)] = d; }
                    var b = Get(t, "build"); if (b != null) { object v, bn; b.TryGetValue("version", out v); b.TryGetValue("build", out bn); build = v + " (Build " + bn + ")"; }
                    var vc = Get(t, "vocab"); if (vc != null) foreach (var kv in vc) { var e = kv.Value as Dictionary<object, object>; if (e != null) vocab.Add(e); }
                }
                // Spiel-Update erkannt?
                if (build != null)
                {
                    res.Build = build;
                    if (!string.IsNullOrEmpty(s.Build) && s.Build != build) { res.BuildChanged = true; s.Updates.Add(DateTime.Now.ToString("yyyy-MM-dd") + ": " + s.Build + " → " + build); }
                    s.Build = build;
                }
                // Vokabeln des Addons in die Vokabelliste übernehmen
                foreach (var e in vocab) { try { var w = Convert.ToString(e.ContainsKey("w") ? e["w"] : ""); if (!VocabHas(w)) Vocab.Add(w, Convert.ToString(e.ContainsKey("ipa") ? e["ipa"] : ""), Convert.ToString(e.ContainsKey("tr") ? e["tr"] : ""), "", "", "aus dem Addon"); } catch { } }

                // 1) Unbekannte Wörter
                var todoWords = unknown.Where(kv => !s.Words.ContainsKey(kv.Key) && (s.Forever == null || !s.Forever.ContainsKey(kv.Key)) && app.Glossary.Find(kv.Key) == null && Regex.IsMatch(kv.Key, @"^[a-z][a-z' -]*$"))
                                       .OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(150).ToList();
                int i = 0;
                foreach (var w in todoWords)
                {
                    progress("Wort " + (++i) + "/" + todoWords.Count + ": " + w);
                    try
                    {
                        var tr = await app.TranslateText(w);
                        if (string.IsNullOrEmpty(tr) || tr.Equals(w, StringComparison.OrdinalIgnoreCase)) continue;
                        string ipa = "", ex = "", exTr = "";
                        try { var d = await DictApi.Lookup(w); if (d != null) { ipa = d.Ipa ?? ""; ex = d.Example ?? ""; } } catch { }
                        if (ex != "") { try { exTr = await app.TranslateText(ex); } catch { } }
                        s.Words[w] = string.Join("|", new[] { ipa, tr, "", ex, exTr, "l" }.Select(x => (x ?? "").Replace("|", "/")));
                        res.NewWords++;
                    }
                    catch (Exception e) { progress("Übersetzung gestoppt: " + e.Message); break; }
                }

                // 2) Neue oder in WoW Forever geänderte Questtexte
                var corpus = app.Quests;
                var todoQuests = new List<KeyValuePair<string, Dictionary<string, string>>>();
                foreach (var kv in seen)
                {
                    int id; if (!int.TryParse(kv.Key, out id) || id <= 0) continue;
                    string officialEn = corpus.Ready ? corpus.EnglishText(id, "D") : null;
                    string seenEn = kv.Value.ContainsKey("D") ? kv.Value["D"] : null;
                    Dictionary<string, string> learned; s.Quests.TryGetValue(kv.Key, out learned);
                    bool known = officialEn != null;
                    bool changed = known && seenEn != null && Similarity(officialEn, seenEn) < 0.8;
                    bool learnedCurrent = learned != null && learned.ContainsKey("src_D") && seenEn != null && Similarity(learned["src_D"], seenEn) >= 0.95;
                    if ((!known || changed) && !learnedCurrent) todoQuests.Add(new KeyValuePair<string, Dictionary<string, string>>(kv.Key, kv.Value));
                }
                res.Pending = Math.Max(0, todoQuests.Count - 25);
                i = 0;
                foreach (var kv in todoQuests.Take(25))
                {
                    progress("Quest " + (++i) + "/" + Math.Min(25, todoQuests.Count) + ": " + (kv.Value.ContainsKey("T") ? kv.Value["T"] : kv.Key));
                    try
                    {
                        var q = new Dictionary<string, string>();
                        foreach (var field in new[] { "T", "D", "O", "Q", "R" })
                        {
                            if (!kv.Value.ContainsKey(field) || string.IsNullOrWhiteSpace(kv.Value[field])) continue;
                            q[field] = await app.TranslateText(kv.Value[field]);
                            q["src_" + field] = kv.Value[field];
                        }
                        int id = int.Parse(kv.Key);
                        if (corpus.Ready && corpus.EnglishText(id, "D") != null) { q["changed"] = "true"; res.ChangedQuests++; } else res.NewQuests++;
                        s.Quests[kv.Key] = q;
                    }
                    catch (Exception e) { progress("Übersetzung gestoppt: " + e.Message); break; }
                }

                s.LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                SaveStore();
                WriteLearned();
                UpdateTocVersions();
                Cache.Flush();
                LastMessage = "Abgleich " + s.LastSync + ": " + res.NewWords + " neue Wörter, " + res.NewQuests + " neue und " + res.ChangedQuests + " geänderte Quests übersetzt"
                              + (res.Pending > 0 ? " (" + res.Pending + " folgen beim nächsten Mal)" : "") + ".";
                progress(LastMessage);
                return res;
            }
            finally { Busy = false; }
        }

        static bool VocabHas(string w)
        {
            try { return File.Exists(Vocab.FilePath) && File.ReadAllText(Vocab.FilePath).Contains(";\"" + w.Replace("\"", "\"\"") + "\";"); } catch { return false; }
        }

        static Dictionary<object, object> Get(Dictionary<object, object> t, string k) { object v; return t.TryGetValue(k, out v) ? v as Dictionary<object, object> : null; }

        /// <summary>Ähnlichkeit zweier Texte (Anteil gemeinsamer Wörter), unabhängig von Spielername/Klasse.</summary>
        public static double Similarity(string a, string b)
        {
            var ta = new HashSet<string>(QuestCorpus.Tokens(a)); var tb = new HashSet<string>(QuestCorpus.Tokens(b));
            if (ta.Count == 0 || tb.Count == 0) return 0;
            int common = ta.Count(t => tb.Contains(t));
            return 2.0 * common / (ta.Count + tb.Count);
        }

        // ---------------- Updates von deiner Quelle ----------------

        /// <summary>
        /// Prüft die Update-Quelle (manifest.json, z. B. auf GitHub). Format:
        /// { "addonVersion": "1.0.1", "addonZip": "https://…/WowLexikon.zip", "appVersion": "1.0.1", "appExe": "https://…/WowLexikon.exe", "hinweis": "…" }
        /// </summary>
        /// <summary>Eigene Einstellung, sonst die fest eingebaute Adresse.</summary>
        public string UpdateUrl { get { return !string.IsNullOrWhiteSpace(app.Cfg.UpdateUrl) ? app.Cfg.UpdateUrl.Trim() : Program.DefaultUpdateUrl; } }
        public bool AppUpdatePending { get { return File.Exists(Assembly.GetExecutingAssembly().Location + ".neu"); } }

        public async Task<string> CheckUpdates()
        {
            string url = UpdateUrl;
            if (string.IsNullOrWhiteSpace(url)) return null;
            var m = Json.Parse(await Http.Client.GetStringAsync(url.Trim()));
            var msgs = new List<string>();
            string addonV = Json.Str(m, "addonVersion"), addonZip = Json.Str(m, "addonZip");
            if (!string.IsNullOrEmpty(addonV) && !string.IsNullOrEmpty(addonZip) && AddOnsDir != null && Newer(addonV, InstalledVersion ?? "0"))
            {
                var bytes = await Http.Client.GetByteArrayAsync(addonZip);
                InstallAddon(new MemoryStream(bytes));
                msgs.Add("Addon auf Version " + addonV + " aktualisiert (wirkt nach /reload oder beim nächsten Einloggen)");
            }
            string appV = Json.Str(m, "appVersion"), appExe = Json.Str(m, "appExe");
            if (!string.IsNullOrEmpty(appV) && !string.IsNullOrEmpty(appExe) && Newer(appV, Program.Version))
            {
                var bytes = await Http.Client.GetByteArrayAsync(appExe);
                string self = Assembly.GetExecutingAssembly().Location;
                File.WriteAllBytes(self + ".neu", bytes);
                msgs.Add("Neue Version " + appV + " des Begleit-Programms geladen – wird beim nächsten Start übernommen");
            }
            try
            {
                int nf = await UpdateForeverWords();
                if (nf > 0) msgs.Add(nf + " neue Wörter aus WoW Forever");
            }
            catch { }   // Wortliste nicht erreichbar: beim nächsten Mal
            string hint = Json.Str(m, "hinweis");
            if (msgs.Count > 0 && !string.IsNullOrEmpty(hint)) msgs.Add(hint);
            return msgs.Count > 0 ? string.Join(". ", msgs) + "." : null;
        }

        // ---------------- Wörter aus den Forever-Spieldaten (wöchentlich auf GitHub erzeugt, niemand muss dafür spielen) ----------------

        /// <summary>Aus der Update-Adresse (github.com/NAME/REPO/releases/…) die Wortliste im Repo ableiten.</summary>
        public string ForeverWordsUrl
        {
            get
            {
                var m = Regex.Match(UpdateUrl ?? "", @"github\.com/([^/]+/[^/]+)/releases/");
                return m.Success ? "https://raw.githubusercontent.com/" + m.Groups[1].Value + "/main/data/woerter-forever.txt" : null;
            }
        }

        public async Task<int> UpdateForeverWords()
        {
            string url = ForeverWordsUrl;
            if (url == null) return 0;
            return ApplyForeverWords(await Http.Client.GetStringAsync(url));
        }

        /// <summary>Übernimmt die Liste (je Zeile wort|lautschrift|deutsch). Rückgabe: Anzahl neuer Wörter.</summary>
        public int ApplyForeverWords(string text)
        {
            var s = Store;
            string hash;
            using (var sha = System.Security.Cryptography.SHA1.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "");
            if (hash == s.ForeverHash) return 0;
            var list = new Dictionary<string, string>();
            foreach (var line in (text ?? "").Split('\n'))
            {
                var p = line.TrimEnd('\r').Split('|');
                if (p.Length < 3 || !Regex.IsMatch(p[0], @"^[a-z][a-z' -]*$") || p[2].Trim() == "") continue;
                list[p[0]] = string.Join("|", new[] { p[1], p[2], "", "", "", "w" });
            }
            if (list.Count < 10) return 0;   // kaputte oder leere Antwort: nichts überschreiben
            int added = list.Keys.Count(k => s.Forever == null || !s.Forever.ContainsKey(k));
            s.Forever = list; s.ForeverHash = hash;
            SaveStore();
            WriteLearned();
            return added;
        }

        static bool Newer(string a, string b)
        {
            Version va, vb;
            return Version.TryParse(a, out va) && Version.TryParse(b, out vb) ? va > vb : string.CompareOrdinal(a, b) > 0;
        }

        /// <summary>Beim Start: liegt eine neue Programmversion bereit, wird sie übernommen und das Programm neu gestartet.</summary>
        public static bool ApplyPendingAppUpdate()
        {
            string self = Assembly.GetExecutingAssembly().Location, neu = self + ".neu";
            if (!File.Exists(neu)) return false;
            string bat = Path.Combine(Path.GetTempPath(), "wowlexikon-update.cmd");
            File.WriteAllText(bat, "@echo off\r\nping 127.0.0.1 -n 3 >nul\r\nmove /y \"" + neu + "\" \"" + self + "\" >nul\r\nstart \"\" \"" + self + "\"\r\ndel \"%~f0\"\r\n", Encoding.Default);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"") { WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, CreateNoWindow = true });
            return true;
        }
    }

    /// <summary>Lesen und Schreiben einfacher Lua-Daten (SavedVariables des Addons).</summary>
    public static class Lua
    {
        public static string Str(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '\\') sb.Append("\\\\"); else if (c == '"') sb.Append("\\\""); else if (c == '\n') sb.Append("\\n"); else if (c == '\r') { } else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Liest Dateien der Form „Name = { … }“ (so speichert WoW SavedVariables).</summary>
        public static Dictionary<string, object> ParseAssignments(string text)
        {
            var p = new Parser(text); var res = new Dictionary<string, object>();
            while (true)
            {
                p.Ws();
                if (p.End) break;
                string name = p.Ident();
                if (name == null) break;
                p.Ws(); p.Expect('=');
                res[name] = p.Value();
            }
            return res;
        }

        class Parser
        {
            readonly string s; int i;
            public Parser(string s) { this.s = s; }
            public bool End { get { return i >= s.Length; } }
            public void Ws()
            {
                while (i < s.Length)
                {
                    if (char.IsWhiteSpace(s[i])) i++;
                    else if (s[i] == '-' && i + 1 < s.Length && s[i + 1] == '-') { while (i < s.Length && s[i] != '\n') i++; }
                    else break;
                }
            }
            public void Expect(char c) { Ws(); if (i >= s.Length || s[i] != c) throw new FormatException("Lua: '" + c + "' erwartet bei " + i); i++; }
            public string Ident()
            {
                Ws(); int a = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                return i > a ? s.Substring(a, i - a) : null;
            }
            public object Value()
            {
                Ws();
                char c = s[i];
                if (c == '{') return Table();
                if (c == '"' || c == '\'') return String();
                if (char.IsDigit(c) || c == '-' || c == '.') return Number();
                string id = Ident();
                if (id == "true") return true; if (id == "false") return false; if (id == "nil") return null;
                throw new FormatException("Lua: unerwartet '" + id + "'");
            }
            object Number()
            {
                int a = i; i++;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '.' || s[i] == '-' || s[i] == '+')) i++;
                double d; double.TryParse(s.Substring(a, i - a), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                return d;
            }
            string String()
            {
                char q = s[i++]; var sb = new StringBuilder();
                while (i < s.Length && s[i] != q)
                {
                    char c = s[i++];
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': break;
                        case '\n': sb.Append('\n'); break;
                        default:
                            if (char.IsDigit(e))
                            {
                                int a = i - 1; while (i < s.Length && i - a < 3 && char.IsDigit(s[i])) i++;
                                // Lua schreibt Bytes als \ddd – zu UTF-8 zusammensetzen
                                sb.Append((char)int.Parse(s.Substring(a, i - a)));
                            }
                            else sb.Append(e);
                            break;
                    }
                }
                i++;
                // Byte-Folgen (\ddd) als UTF-8 deuten
                string r = sb.ToString();
                if (r.Any(ch => ch >= 0x80 && ch <= 0xFF))
                {
                    try { var bytes = r.Select(ch => ch <= 0xFF ? (byte)ch : (byte)'?').ToArray(); if (r.All(ch => ch <= 0xFF)) return Encoding.UTF8.GetString(bytes); } catch { }
                }
                return r;
            }
            Dictionary<object, object> Table()
            {
                var t = new Dictionary<object, object>(); int n = 1;
                i++;
                while (true)
                {
                    Ws();
                    if (s[i] == '}') { i++; return t; }
                    object key;
                    if (s[i] == '[') { i++; key = Value(); Expect(']'); Expect('='); }
                    else
                    {
                        int save = i; string id = Ident(); Ws();
                        if (id != null && i < s.Length && s[i] == '=' && (i + 1 >= s.Length || s[i + 1] != '=')) { i++; key = id; }
                        else { i = save; key = (double)n++; }
                    }
                    object v = Value();
                    if (key is double && (double)key == Math.Floor((double)key)) key = ((double)key).ToString("0", CultureInfo.InvariantCulture);
                    t[key] = v;
                    Ws();
                    if (i < s.Length && (s[i] == ',' || s[i] == ';')) i++;
                }
            }
        }
    }
}
