using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WowLexikon
{
    /// <summary>
    /// Erzeugt die Spieldaten (Questtexte + Namen in der Zielsprache) auf diesem PC aus der öffentlichen
    /// AzerothCore-Datenbank. So werden keine Texte von Blizzard weitergegeben – jeder PC baut sie selbst.
    /// Ergebnis: %APPDATA%\WowLexikon\spieldaten\quests-xx.txt.gz und namen-xx.txt.gz (gleiches Format wie tools/daten-bauen.mjs).
    /// </summary>
    static class GameDataBuilder
    {
        const string Base = "https://raw.githubusercontent.com/azerothcore/azerothcore-wotlk/master/data/sql/base/db_world/";
        static readonly Dictionary<string, string> Locale = new Dictionary<string, string> { { "de", "deDE" }, { "fr", "frFR" }, { "es", "esES" }, { "ru", "ruRU" }, { "ko", "koKR" }, { "zh-CN", "zhCN" } };

        public static string DirOverride;   // nur für den Selbsttest
        public static string Dir { get { var d = DirOverride ?? Path.Combine(Settings.Dir, "spieldaten"); Directory.CreateDirectory(d); return d; } }
        public static string QuestFile(string lang) { return Path.Combine(Dir, "quests-" + lang + ".txt.gz"); }
        public static string NameFile(string lang) { return Path.Combine(Dir, "namen-" + lang + ".txt.gz"); }
        public static bool Supported(string lang) { return Locale.ContainsKey(lang); }

        public static async Task Build(string lang, Action<string> progress)
        {
            string loc;
            if (!Locale.TryGetValue(lang, out loc)) throw new Exception("Für diese Sprache gibt es keine Übersetzung in der Datenbank.");
            var cache = Path.Combine(Dir, "sql"); Directory.CreateDirectory(cache);
            Func<string, Task<SqlTable>> load = async name =>
            {
                string f = Path.Combine(cache, name + ".sql");
                if (!File.Exists(f))
                {
                    progress("Lade " + name + " …");
                    var bytes = await Http.Client.GetByteArrayAsync(Base + name + ".sql");
                    File.WriteAllBytes(f, bytes);
                }
                progress("Lese " + name + " …");
                return await Task.Run(() => SqlTable.Parse(File.ReadAllText(f, Encoding.UTF8)));
            };
            var qt = await load("quest_template");
            var qor = await load("quest_offer_reward");
            var qri = await load("quest_request_items");
            var qtl = await load("quest_template_locale");
            var qorl = await load("quest_offer_reward_locale");
            var qril = await load("quest_request_items_locale");
            var ct = await load("creature_template"); var ctl = await load("creature_template_locale");
            var it = await load("item_template"); var itl = await load("item_template_locale");
            var gt = await load("gameobject_template"); var gtl = await load("gameobject_template_locale");
            progress("Baue Spieldaten …");
            await Task.Run(() =>
            {
                var enQuest = qt.ByKey("ID");
                var enReward = qor.ByKey("ID"); var enRequest = qri.ByKey("ID");
                var lines = new List<string>(); var seen = new HashSet<string>(); var skipped = new HashSet<string>();
                Action<string, string, string, string> add = (id, kind, en, tr) =>
                {
                    en = Clean(en); tr = Clean(tr);
                    if (en.Length < 3 || tr.Length == 0) return;
                    if (!seen.Add(en + "\u0001" + tr)) return;
                    lines.Add(id + "\t" + kind + "\t" + en + "\t" + tr);
                };
                var fields = new[] { new[] { "T", "LogTitle", "Title" }, new[] { "D", "QuestDescription", "Details" }, new[] { "O", "LogDescription", "Objectives" },
                                     new[] { "E", "AreaDescription", "EndText" }, new[] { "C", "QuestCompletionLog", "CompletedText" },
                                     new[] { "1", "ObjectiveText1", "ObjectiveText1" }, new[] { "2", "ObjectiveText2", "ObjectiveText2" },
                                     new[] { "3", "ObjectiveText3", "ObjectiveText3" }, new[] { "4", "ObjectiveText4", "ObjectiveText4" } };
                foreach (var r in qtl.Rows)
                {
                    if (qtl.Get(r, "locale") != loc) continue;
                    string id = qtl.Get(r, "ID");
                    string[] e;
                    if (!enQuest.TryGetValue(id, out e)) continue;
                    string rew = enReward.ContainsKey(id) ? qor.Get(enReward[id], "RewardText") : "", req = enRequest.ContainsKey(id) ? qri.Get(enRequest[id], "CompletionText") : "";
                    if (!IsClassic(qt, e, rew, req)) { skipped.Add(id); continue; }
                    foreach (var f in fields) add(id, f[0], qt.Get(e, f[1]), qtl.Get(r, f[2]));
                }
                foreach (var r in qorl.Rows)
                {
                    string id = qorl.Get(r, "ID");
                    if (qorl.Get(r, "locale") == loc && !skipped.Contains(id) && enReward.ContainsKey(id)) add(id, "R", qor.Get(enReward[id], "RewardText"), qorl.Get(r, "RewardText"));
                }
                foreach (var r in qril.Rows)
                {
                    string id = qril.Get(r, "ID");
                    if (qril.Get(r, "locale") == loc && !skipped.Contains(id) && enRequest.ContainsKey(id)) add(id, "Q", qri.Get(enRequest[id], "CompletionText"), qril.Get(r, "CompletionText"));
                }
                WriteGz(QuestFile(lang), lines);

                var names = new Dictionary<string, string>();
                Action<string, string, string, string, string> addName = (type, en, tr, subEn, subTr) =>
                {
                    en = Clean(en); tr = Clean(tr);
                    if (Junk(en) || tr.Length == 0 || en.Length < 2) return;
                    string key = type + "|" + en.ToLowerInvariant();
                    if (!names.ContainsKey(key)) names[key] = type + "\t" + en + "\t" + tr + "\t" + Clean(subEn) + "\t" + Clean(subTr);
                };
                var enC = ct.ByKey("entry"); var enI = it.ByKey("entry"); var enG = gt.ByKey("entry");
                string[] x;
                foreach (var r in ctl.Rows) if (ctl.Get(r, "locale") == loc && enC.TryGetValue(ctl.Get(r, "entry"), out x)) addName("n", ct.Get(x, "name"), ctl.Get(r, "Name"), ct.Get(x, "subname"), ctl.Get(r, "Title"));
                foreach (var r in itl.Rows) if (itl.Get(r, "locale") == loc && enI.TryGetValue(itl.Get(r, "ID"), out x)) addName("i", it.Get(x, "name"), itl.Get(r, "Name"), "", "");
                foreach (var r in gtl.Rows) if (gtl.Get(r, "locale") == loc && enG.TryGetValue(gtl.Get(r, "entry"), out x)) addName("o", gt.Get(x, "name"), gtl.Get(r, "name"), "", "");
                foreach (var r in qtl.Rows) if (qtl.Get(r, "locale") == loc && enQuest.TryGetValue(qtl.Get(r, "ID"), out x)) addName("q", qt.Get(x, "LogTitle"), qtl.Get(r, "Title"), "", "");
                WriteGz(NameFile(lang), names.Values.ToList());
                progress("Fertig: " + lines.Count + " Questtexte, " + names.Count + " Namen.");
            });
        }

        static void WriteGz(string path, List<string> lines)
        {
            using (var fs = File.Create(path))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            using (var w = new StreamWriter(gz, new UTF8Encoding(false)))
                w.Write(string.Join("\n", lines));
        }

        static string Clean(string s)
        {
            return (s ?? "").Replace("\r", "").Replace("\n", "$B").Replace("\t", " ").Trim();
        }
        static readonly Regex junk = new Regex(@"\[(UNUSED|PH|DEPRECATED|DND|TEST|NOT USED|NYI)\]|\b(UNUSED|DEPRECATED|OLD)\b|^(zz|ZZ|Test |TEST |\[|\()", RegexOptions.Compiled);
        static bool Junk(string s) { return string.IsNullOrEmpty(s) || junk.IsMatch(s); }

        // Gleiche Regeln wie tools/daten-bauen.mjs: WoW Forever basiert auf Classic
        static readonly HashSet<int> LaterZones = new HashSet<int> { 65, 66, 67, 206, 210, 394, 495, 1196, 2817, 4298, -372, -241, -1002 };
        static readonly Regex laterPlaces = new Regex(@"\b(Eversong|Silvermoon|Ghostlands|Tranquillien|Sunstrider|Azuremyst|Bloodmyst|Exodar|Ammen Vale|Outland|Hellfire|Honor Hold|Thrallmar|Zangarmarsh|Terokkar|Shattrath|Nagrand|Blade's Edge|Netherstorm|Shadowmoon Valley|Isle of Quel'Danas|Northrend|Borean Tundra|Howling Fjord|Dragonblight|Grizzly Hills|Zul'Drak|Sholazar|Storm Peaks|Icecrown|Crystalsong|Wintergrasp|Acherus|Ebon Hold|Valgarde|Warsong Hold|Valiance Keep|Argent Tournament)\b", RegexOptions.Compiled);
        static bool IsClassic(SqlTable qt, string[] q, string reward, string request)
        {
            int lvl = ToInt(qt.Get(q, "QuestLevel")), min = ToInt(qt.Get(q, "MinLevel")), sort = ToInt(qt.Get(q, "QuestSortID")), races = ToInt(qt.Get(q, "AllowableRaces"));
            if (lvl > 60 || min > 60) return false;
            if (LaterZones.Contains(sort)) return false;
            if (sort >= 3430 && sort != 3456 && sort != 3478) return false;
            if (races != 0 && (races & ~(512 | 1024)) == 0) return false;
            string text = string.Join(" ", qt.Get(q, "LogTitle"), qt.Get(q, "LogDescription"), qt.Get(q, "QuestDescription"), qt.Get(q, "AreaDescription"), qt.Get(q, "QuestCompletionLog"), reward, request);
            return !laterPlaces.IsMatch(text);
        }
        static int ToInt(string s) { int v; return int.TryParse(s, out v) ? v : 0; }
    }

    /// <summary>Minimaler Leser für MySQL-Dumps: Spalten aus CREATE TABLE, Werte aus INSERT … VALUES.</summary>
    class SqlTable
    {
        public readonly List<string> Cols = new List<string>();
        public readonly List<string[]> Rows = new List<string[]>();
        readonly Dictionary<string, int> idx = new Dictionary<string, int>();

        public string Get(string[] row, string col) { int i; return idx.TryGetValue(col, out i) && i < row.Length ? row[i] ?? "" : ""; }

        public Dictionary<string, string[]> ByKey(string col)
        {
            var d = new Dictionary<string, string[]>();
            foreach (var r in Rows) { var k = Get(r, col); if (!d.ContainsKey(k)) d[k] = r; }
            return d;
        }

        public static SqlTable Parse(string sql)
        {
            var t = new SqlTable();
            int create = sql.IndexOf("CREATE TABLE", StringComparison.Ordinal);
            int end = create >= 0 ? sql.IndexOf(") ENGINE", create, StringComparison.Ordinal) : -1;
            if (create >= 0 && end > create)
                foreach (var line in sql.Substring(create, end - create).Split('\n'))
                {
                    var m = Regex.Match(line, @"^\s+`([^`]+)`\s");
                    if (m.Success) { t.idx[m.Groups[1].Value] = t.Cols.Count; t.Cols.Add(m.Groups[1].Value); }
                }
            int i = 0;
            var val = new StringBuilder();
            while ((i = sql.IndexOf("VALUES", i, StringComparison.Ordinal)) >= 0)
            {
                i += 6;
                List<string> row = null; bool inStr = false, isStr = false; int depth = 0;
                val.Clear();
                for (; i < sql.Length; i++)
                {
                    char c = sql[i];
                    if (inStr)
                    {
                        if (c == '\\' && i + 1 < sql.Length) { char n = sql[++i]; val.Append(n == 'n' ? '\n' : n == 'r' ? '\0' : n == 't' ? '\t' : n); }
                        else if (c == '\'') { if (i + 1 < sql.Length && sql[i + 1] == '\'') { val.Append('\''); i++; } else inStr = false; }
                        else val.Append(c);
                        continue;
                    }
                    if (c == '\'') { inStr = true; isStr = true; continue; }
                    if (c == '(' && depth == 0) { depth = 1; row = new List<string>(); val.Clear(); isStr = false; continue; }
                    if (depth == 0) { if (c == ';') break; continue; }
                    if (c == ',' || c == ')')
                    {
                        string v = isStr ? val.ToString().Replace("\0", "") : val.ToString().Trim();
                        row.Add(!isStr && v == "NULL" ? null : v);
                        val.Clear(); isStr = false;
                        if (c == ')') { depth = 0; t.Rows.Add(row.ToArray()); }
                        continue;
                    }
                    val.Append(c);
                }
            }
            return t;
        }
    }
}
