using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace WowLexikon
{
    public class GlossEntry
    {
        public string Term;
        public Dictionary<string, object> Raw;
        public string Tr(string lang) { return Json.Str(Raw, lang); }
        public string ExTr(string lang) { return Json.Str(Raw, "ex_" + lang); }
        public string Ipa { get { return Json.Str(Raw, "ipa"); } }
        public string Example { get { return Json.Str(Raw, "ex"); } }
        public string Note(string lang) { return Json.Str(Raw, "note_" + lang) ?? (lang == "de" ? Json.Str(Raw, "note") : null); }
        /// <summary>Stammt aus der Liste der 3000 häufigsten Questwörter (nicht aus dem WoW-Glossar).</summary>
        public bool IsQuestWord, IsAuto;
    }

    public class Glossary
    {
        readonly Dictionary<string, GlossEntry> map = new Dictionary<string, GlossEntry>();
        public int Count { get { return map.Count; } }
        public int QuestWords;
        public string UserFile { get { return Path.Combine(Settings.Dir, "eigenes-glossar.json"); } }
        public string LoadError;

        public void Load()
        {
            map.Clear(); LoadError = null; QuestWords = 0;
            // 0) Großer Wortschatz (automatisch aus Ding + CMU) – niedrigste Priorität
            AddLines("WowLexikon.woerter-auto.txt", true);
            // 1) Die 3000 häufigsten Wörter aus echten Questtexten (Format: wort|lautschrift|deutsch)
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("WowLexikon.questwoerter.txt"))
            using (var r = new StreamReader(s, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    var p = line.Split('|');
                    if (p.Length != 3 || p[0].Trim().Length == 0) continue;
                    var d = new Dictionary<string, object> { { "en", p[0].Trim() }, { "ipa", p[1].Trim() }, { "de", p[2].Trim() } };
                    map[Normalize(p[0])] = new GlossEntry { Term = p[0].Trim(), Raw = d, IsQuestWord = true };
                    QuestWords++;
                }
            }
            // 2) WoW-Glossar (überschreibt), 3) eigenes Glossar (überschreibt alles)
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("WowLexikon.glossar.json"))
            using (var r = new StreamReader(s, Encoding.UTF8)) Add(r.ReadToEnd());

            if (!File.Exists(UserFile))
            {
                File.WriteAllText(UserFile,
                    "[\n" +
                    "  {\"en\": \"mrglglgl\", \"de\": \"(Murloc-Kampfschrei)\", \"ex\": \"Mrglglgl! The murlocs are attacking!\", \"ex_de\": \"Mrglglgl! Die Murlocs greifen an!\", \"note\": \"Beispiel-Eintrag. Eigene Einträge überschreiben das eingebaute Glossar. Felder: en, de (oder fr, es …), ipa, ex, ex_de, note\"}\n" +
                    "]\n", new UTF8Encoding(false));
            }
            try { Add(File.ReadAllText(UserFile, Encoding.UTF8)); }
            catch (Exception e) { LoadError = "Eigenes Glossar fehlerhaft: " + e.Message; }
        }

        public int AutoWords;
        void AddLines(string resource, bool auto)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (s == null) return;
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        var p = line.Split('|');
                        if (p.Length != 3 || p[0].Trim().Length == 0) continue;
                        var d = new Dictionary<string, object> { { "en", p[0].Trim() }, { "ipa", p[1].Trim() }, { "de", p[2].Trim() } };
                        map[Normalize(p[0])] = new GlossEntry { Term = p[0].Trim(), Raw = d, IsQuestWord = true, IsAuto = auto };
                        if (auto) AutoWords++;
                    }
                }
            }
        }

        void Add(string json)
        {
            var arr = Json.Parse(json) as object[];
            if (arr == null) return;
            foreach (var o in arr)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                string t = Json.Str(d, "en");
                if (string.IsNullOrWhiteSpace(t)) continue;
                map[Normalize(t)] = new GlossEntry { Term = t, Raw = d };
            }
        }

        public static string Normalize(string s)
        {
            s = (s ?? "").ToLowerInvariant().Replace('’', '\'').Trim();
            return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        }

        public GlossEntry Find(string term)
        {
            foreach (var c in Lemma.Candidates(term))
            {
                GlossEntry e;
                if (map.TryGetValue(c, out e)) return e;
                if (map.TryGetValue("the " + c, out e)) return e;
            }
            return null;
        }
    }

    /// <summary>Einfache englische Grundformen-Bildung (murlocs → murloc, wolves → wolf, slaying → slay).</summary>
    public static class Lemma
    {
        public static List<string> Candidates(string term)
        {
            var r = new List<string>();
            string t = Glossary.Normalize(term);
            if (t.StartsWith("the ")) t = t.Substring(4);
            AddC(r, t);
            if (t.EndsWith("'s")) { t = t.Substring(0, t.Length - 2); AddC(r, t); }
            if (t.EndsWith("s'")) { t = t.Substring(0, t.Length - 1); AddC(r, t); }

            // Bei Mehrwort-Ausdrücken nur das letzte Wort beugen
            int sp = t.LastIndexOf(' ');
            string head = sp >= 0 ? t.Substring(0, sp + 1) : "", w = sp >= 0 ? t.Substring(sp + 1) : t;
            foreach (var f in Forms(w)) AddC(r, head + f);
            return r;
        }

        static void AddC(List<string> r, string s) { if (s.Length > 0 && !r.Contains(s)) r.Add(s); }

        static IEnumerable<string> Forms(string w)
        {
            var f = new List<string>();
            if (w.Length < 3) return f;
            if (w.EndsWith("ies")) f.Add(w.Substring(0, w.Length - 3) + "y");
            if (w.EndsWith("ves")) { f.Add(w.Substring(0, w.Length - 3) + "f"); f.Add(w.Substring(0, w.Length - 3) + "fe"); }
            if (w.EndsWith("men")) f.Add(w.Substring(0, w.Length - 3) + "man");
            if (w.EndsWith("ches") || w.EndsWith("shes") || w.EndsWith("xes") || w.EndsWith("sses") || w.EndsWith("oes")) f.Add(w.Substring(0, w.Length - 2));
            if (w.EndsWith("s") && !w.EndsWith("ss")) f.Add(w.Substring(0, w.Length - 1));
            if (w.EndsWith("ied")) f.Add(w.Substring(0, w.Length - 3) + "y");
            if (w.EndsWith("ed"))
            {
                string b = w.Substring(0, w.Length - 2);
                f.Add(b); f.Add(w.Substring(0, w.Length - 1));
                if (b.Length > 2 && b[b.Length - 1] == b[b.Length - 2]) f.Add(b.Substring(0, b.Length - 1));
            }
            if (w.EndsWith("ing") && w.Length > 5)
            {
                string b = w.Substring(0, w.Length - 3);
                f.Add(b); f.Add(b + "e");
                if (b.Length > 2 && b[b.Length - 1] == b[b.Length - 2]) f.Add(b.Substring(0, b.Length - 1));
            }
            if (w.EndsWith("er") && w.Length > 5) f.Add(w.Substring(0, w.Length - 2));
            if (w.EndsWith("est") && w.Length > 6) f.Add(w.Substring(0, w.Length - 3));
            switch (w)
            {
                case "slew": f.Add("slay"); break;
                case "men": f.Add("man"); break;
                case "teeth": f.Add("tooth"); break;
                case "feet": f.Add("foot"); break;
                case "mice": f.Add("mouse"); break;
                case "geese": f.Add("goose"); break;
                case "children": f.Add("child"); break;
                case "fought": f.Add("fight"); break;
                case "brought": f.Add("bring"); break;
                case "sought": f.Add("seek"); break;
                case "stole": case "stolen": f.Add("steal"); break;
                case "drew": case "drawn": f.Add("draw"); break;
                case "found": f.Add("find"); break;
                case "took": case "taken": f.Add("take"); break;
                case "gave": case "given": f.Add("give"); break;
            }
            return f;
        }
    }
}
