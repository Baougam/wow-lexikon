using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WowLexikon
{
    /// <summary>Ein Treffer in der Quest-Datenbank: der Originalsatz und der Satz aus dem Spielclient in der Zielsprache.</summary>
    public class QuestMatch
    {
        public int QuestId;
        public string En, Tr;           // Satz (oder Absatz, wenn sich die Sätze nicht eindeutig zuordnen ließen)
        public string TitleEn, TitleTr;
        public double Score;
        public bool SentenceAligned;
    }

    public class NameEntry
    {
        public char Type; public string En, Tr, SubEn, SubTr;
        public string TypeLabel
        {
            get { switch (Type) { case 'n': return "NPC"; case 'i': return "Gegenstand"; case 'o': return "Objekt"; case 'q': return "Quest"; } return ""; }
        }
    }

    /// <summary>
    /// Alle Questtexte englisch + Zielsprache aus der öffentlichen AzerothCore-Datenbank.
    /// Findet zu einer gelesenen Zeile den Originalsatz – auch wenn die Texterkennung Fehler macht oder nur ein Stück der Zeile liest.
    /// </summary>
    public class QuestCorpus
    {
        struct Unit { public int Text; public int EnStart, EnLen, TrStart, TrLen; public bool Aligned; }

        readonly List<int> ids = new List<int>();
        readonly List<string> en = new List<string>(), tr = new List<string>();
        readonly List<Unit> units = new List<Unit>();
        readonly Dictionary<string, List<int>> index = new Dictionary<string, List<int>>();
        readonly Dictionary<int, int> titles = new Dictionary<int, int>();   // Quest-ID → Textnummer des Titels
        public bool Ready { get; private set; }
        public string Language { get; private set; }
        public int TextCount { get { return en.Count; } }
        public int SentenceCount { get { return units.Count; } }

        public static bool Available(string lang) { return Resource("quests-" + lang + ".txt.gz") != null; }

        /// <summary>Spieldaten: eingebaut (eigener PC) oder auf diesem PC vom Begleit-Programm erzeugt.</summary>
        public static Stream Resource(string name)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("WowLexikon." + name);
            if (s != null) return s;
            string f = Path.Combine(GameDataBuilder.Dir, name);
            return File.Exists(f) ? File.OpenRead(f) : null;
        }

        readonly List<string> kinds = new List<string>();
        readonly Dictionary<string, int> byQuestKind = new Dictionary<string, int>();

        /// <summary>Englischer Originaltext einer Quest (z. B. Feld "D" = Beschreibung), um Änderungen in WoW Forever zu erkennen.</summary>
        public string EnglishText(int questId, string kind)
        {
            int t;
            return Ready && byQuestKind.TryGetValue(questId + "|" + kind, out t) ? en[t] : null;
        }

        public Task LoadAsync(string lang) { return Task.Run(() => Load(lang)); }

        void Load(string lang)
        {
            Language = lang;
            var s = Resource("quests-" + lang + ".txt.gz");
            if (s == null) return;
            using (var gz = new GZipStream(s, CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    var p = line.Split('\t');
                    if (p.Length != 4) continue;
                    int id; int.TryParse(p[0], out id);
                    int t = en.Count;
                    ids.Add(id); en.Add(p[2]); tr.Add(p[3]); kinds.Add(p[1]);
                    byQuestKind[id + "|" + p[1]] = t;
                    if (p[1] == "T") titles[id] = t;
                    else AddUnits(t);
                }
            }
            Ready = true;
        }

        static readonly Regex paraSplit = new Regex(@"\$[Bb]", RegexOptions.Compiled);
        static readonly Regex sentSplit = new Regex(@"(?<=[.!?…])\s+(?=[\p{Lu}""'„«¡¿<$])", RegexOptions.Compiled);

        /// <summary>Zerlegt Absätze und Sätze; ordnet Sätze 1:1 zu, wenn beide Fassungen gleich viele haben.</summary>
        void AddUnits(int t)
        {
            var enParas = Spans(en[t], paraSplit); var trParas = Spans(tr[t], paraSplit);
            bool parasAligned = enParas.Count == trParas.Count;
            for (int pi = 0; pi < enParas.Count; pi++)
            {
                var ep = enParas[pi];
                var tp = parasAligned ? trParas[pi] : new KeyValuePair<int, int>(0, tr[t].Length);
                var es = SubSpans(en[t], ep, sentSplit); var ts = SubSpans(tr[t], tp, sentSplit);
                bool sAligned = parasAligned && es.Count == ts.Count;
                for (int si = 0; si < es.Count; si++)
                {
                    var u = new Unit { Text = t, EnStart = es[si].Key, EnLen = es[si].Value, Aligned = sAligned };
                    if (sAligned) { u.TrStart = ts[si].Key; u.TrLen = ts[si].Value; } else { u.TrStart = tp.Key; u.TrLen = tp.Value; }
                    int ui = units.Count;
                    units.Add(u);
                    foreach (var tok in new HashSet<string>(Tokens(en[t].Substring(u.EnStart, u.EnLen))))
                    {
                        List<int> l;
                        if (!index.TryGetValue(tok, out l)) index[tok] = l = new List<int>(4);
                        l.Add(ui);
                    }
                }
            }
        }

        static List<KeyValuePair<int, int>> Spans(string s, Regex sep)
        {
            var r = new List<KeyValuePair<int, int>>();
            int last = 0;
            foreach (Match m in sep.Matches(s)) { AddSpan(r, s, last, m.Index); last = m.Index + m.Length; }
            AddSpan(r, s, last, s.Length);
            return r;
        }
        static List<KeyValuePair<int, int>> SubSpans(string s, KeyValuePair<int, int> span, Regex sep)
        {
            var r = new List<KeyValuePair<int, int>>();
            string sub = s.Substring(span.Key, span.Value);
            int last = 0;
            foreach (Match m in sep.Matches(sub)) { AddSpan(r, s, span.Key + last, span.Key + m.Index); last = m.Index + m.Length; }
            AddSpan(r, s, span.Key + last, span.Key + sub.Length);
            return r;
        }
        static void AddSpan(List<KeyValuePair<int, int>> r, string s, int a, int b)
        {
            while (a < b && char.IsWhiteSpace(s[a])) a++;
            while (b > a && char.IsWhiteSpace(s[b - 1])) b--;
            if (b - a >= 2) r.Add(new KeyValuePair<int, int>(a, b - a));
        }

        static readonly Regex tokenRe = new Regex(@"[a-z][a-z']*[a-z]|[a-z]", RegexOptions.Compiled);
        static readonly Regex placeholder = new Regex(@"\$[a-zA-Z](?:[^;:\s]*[:;][^;]*;)?", RegexOptions.Compiled);
        public static IEnumerable<string> Tokens(string text)
        {
            text = placeholder.Replace(text, " ").ToLowerInvariant().Replace('’', '\'');
            foreach (Match m in tokenRe.Matches(text))
            {
                string w = m.Value;
                if (w.EndsWith("'s")) w = w.Substring(0, w.Length - 2);
                if (w.Length >= 2) yield return w;
            }
        }

        /// <summary>Bester Originalsatz zur gelesenen Zeile. <paramref name="mustContain"/> bevorzugt Sätze mit dem angeklickten Wort.</summary>
        public QuestMatch Find(string ocrText, string mustContain)
        {
            if (!Ready || string.IsNullOrWhiteSpace(ocrText)) return null;
            // Typische Lesefehler: Ziffern mitten im Wort (Th0mas, he1p)
            ocrText = Regex.Replace(ocrText, @"(?<=\p{L})[0-9]|[0-9](?=\p{L})", m => m.Value == "0" ? "o" : m.Value == "1" ? "l" : m.Value == "5" ? "s" : m.Value == "8" ? "b" : m.Value);
            var q = new HashSet<string>();
            foreach (var t in Tokens(ocrText)) q.Add(index.ContainsKey(t) ? t : (Correct(t) ?? t));
            if (q.Count < 3) return null;   // zu wenig Text, um eine Quest sicher zu erkennen
            double total = 0;
            var scores = new Dictionary<int, double>();
            int n = units.Count;
            foreach (var tok in q)
            {
                List<int> l;
                if (!index.TryGetValue(tok, out l)) { total += 0.5 * Math.Log(n); continue; }   // unbekanntes Wort zählt nur halb
                double idf = Math.Log((double)n / l.Count);
                total += idf;
                if (l.Count > n / 4) continue;   // Allerweltswörter („the“, „you“) nicht einzeln durchsuchen
                foreach (var u in l) { double sc; scores.TryGetValue(u, out sc); scores[u] = sc + idf; }
            }
            if (total <= 0) return null;
            string want = string.IsNullOrEmpty(mustContain) ? null : mustContain.ToLowerInvariant();
            int best = -1; double bestScore = 0;
            foreach (var kv in scores)
            {
                double sc = kv.Value / total;
                if (want != null && EnText(kv.Key).ToLowerInvariant().Contains(want)) sc += 0.05;
                if (sc > bestScore) { bestScore = sc; best = kv.Key; }
            }
            if (best < 0 || bestScore < 0.55) return null;
            var un = units[best];
            int qid = ids[un.Text];
            var m2 = new QuestMatch
            {
                QuestId = qid, Score = bestScore, SentenceAligned = un.Aligned,
                En = Pretty(en[un.Text].Substring(un.EnStart, un.EnLen)),
                Tr = Pretty(tr[un.Text].Substring(un.TrStart, un.TrLen))
            };
            int ti;
            if (titles.TryGetValue(qid, out ti)) { m2.TitleEn = en[ti]; m2.TitleTr = tr[ti]; }
            return m2;
        }

        Dictionary<char, List<string>> vocab;

        /// <summary>Korrigiert ein falsch gelesenes Wort auf das ähnlichste Wort aus den Questtexten (1–2 Buchstaben Abstand).</summary>
        string Correct(string w)
        {
            if (w.Length < 4) return null;
            if (vocab == null)
            {
                var v = new Dictionary<char, List<string>>();
                foreach (var k in index.Keys) { List<string> l; if (!v.TryGetValue(k[0], out l)) v[k[0]] = l = new List<string>(); l.Add(k); }
                vocab = v;
            }
            int maxD = w.Length >= 7 ? 2 : 1;
            string best = null; int bestD = maxD + 1, bestDf = 0;
            List<string> cands;
            if (!vocab.TryGetValue(w[0], out cands)) return null;
            foreach (var c in cands)
            {
                if (Math.Abs(c.Length - w.Length) > maxD) continue;
                int d = Distance(w, c, maxD);
                int df = index[c].Count;
                if (d < bestD || (d == bestD && df > bestDf)) { bestD = d; best = c; bestDf = df; }
            }
            return bestD <= maxD ? best : null;
        }

        static int Distance(string a, string b, int max)
        {
            var prev = new int[b.Length + 1]; var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i; int rowMin = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                    if (cur[j] < rowMin) rowMin = cur[j];
                }
                if (rowMin > max) return max + 1;
                var t = prev; prev = cur; cur = t;
            }
            return prev[b.Length];
        }

        string EnText(int unit) { var u = units[unit]; return en[u.Text].Substring(u.EnStart, u.EnLen); }

        /// <summary>Platzhalter des Spiels lesbar machen ($N = Name, $C = Klasse, $G männlich:weiblich;).</summary>
        public static string Pretty(string s)
        {
            s = Regex.Replace(s, @"\$[Gg]\s*([^:;]*):([^;]*);", "$1/$2");
            s = Regex.Replace(s, @"\$[Nn]", "<Name>");
            s = Regex.Replace(s, @"\$[Cc]", "<Klasse>");
            s = Regex.Replace(s, @"\$[Rr]", "<Volk>");
            s = Regex.Replace(s, @"\$[Bb]", " ");
            return Regex.Replace(s, @"\s{2,}", " ").Trim();
        }
    }

    /// <summary>Namen aus dem Spielclient: NPCs, Gegenstände, Objekte, Questtitel.</summary>
    public class NameIndex
    {
        readonly Dictionary<string, NameEntry> map = new Dictionary<string, NameEntry>();
        public int Count { get { return map.Count; } }
        public bool Ready { get; private set; }

        public Task LoadAsync(string lang) { return Task.Run(() => Load(lang)); }

        void Load(string lang)
        {
            var s = QuestCorpus.Resource("namen-" + lang + ".txt.gz");
            if (s == null) return;
            using (var gz = new GZipStream(s, CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    var p = line.Split('\t');
                    if (p.Length < 3 || p[0].Length != 1) continue;
                    var e = new NameEntry { Type = p[0][0], En = p[1], Tr = p[2], SubEn = p.Length > 3 ? p[3] : "", SubTr = p.Length > 4 ? p[4] : "" };
                    string key = Glossary.Normalize(e.En);
                    NameEntry old;
                    // Bei gleichen Namen: NPC vor Gegenstand vor Quest vor Objekt
                    if (map.TryGetValue(key, out old) && Rank(old.Type) <= Rank(e.Type)) continue;
                    map[key] = e;
                }
            }
            Ready = true;
        }
        static int Rank(char t) { return t == 'n' ? 0 : t == 'i' ? 1 : t == 'q' ? 2 : 3; }

        public NameEntry Find(string term)
        {
            if (!Ready) return null;
            NameEntry e;
            if (map.TryGetValue(Glossary.Normalize(term), out e)) return e;   // „The Fargodeep Mine“ samt Artikel
            foreach (var c in Lemma.Candidates(term))
                if (map.TryGetValue(c, out e)) return e;
            return null;
        }
    }
}
