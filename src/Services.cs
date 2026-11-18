using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WowLexikon
{
    public class DictInfo
    {
        public string Ipa, Audio, PartOfSpeech, Definition, Example;
    }

    public class AiInfo
    {
        public string Translation, Ipa, PartOfSpeech, Meaning, Example, ExampleTr, ContextTr;
        public List<string> Alternatives = new List<string>();
    }

    public class TrResult
    {
        public string Text; public List<string> Alternatives = new List<string>();
    }

    /// <summary>Kleiner Zwischenspeicher auf der Festplatte: gleiche Wörter kosten kein Kontingent und gehen offline.</summary>
    static class Cache
    {
        static Dictionary<string, string> data;
        static readonly object gate = new object();
        static bool dirty;
        static string FilePath { get { return Path.Combine(Settings.Dir, "zwischenspeicher.json"); } }

        static void Ensure()
        {
            if (data != null) return;
            data = new Dictionary<string, string>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var d = Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                    if (d != null) foreach (var kv in d) data[kv.Key] = Convert.ToString(kv.Value);
                }
            }
            catch { }
        }
        public static string Get(string key)
        {
            lock (gate) { Ensure(); string v; return data.TryGetValue(key, out v) ? v : null; }
        }
        public static void Put(string key, string value)
        {
            if (value == null) return;
            lock (gate)
            {
                Ensure();
                if (data.Count > 8000) data.Clear();
                data[key] = value; dirty = true;
            }
        }
        public static void Flush()
        {
            lock (gate)
            {
                if (!dirty || data == null) return;
                try { File.WriteAllText(FilePath, Json.Write(data), new UTF8Encoding(false)); dirty = false; } catch { }
            }
        }
    }

    static class Http
    {
        public static readonly HttpClient Client;
        static Http()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("WowLexikon/1.0");
        }
    }

    // ---------------- MyMemory: kostenlos, ohne Konto ----------------
    static class MyMemory
    {
        public static async Task<TrResult> Translate(string text, string src, string tgt, string email)
        {
            string key = "mm|" + src + "|" + tgt + "|" + text;
            string raw = Cache.Get(key);
            if (raw == null)
            {
                string url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(text) +
                             "&langpair=" + Uri.EscapeDataString(src + "|" + tgt);
                if (!string.IsNullOrWhiteSpace(email)) url += "&de=" + Uri.EscapeDataString(email.Trim());
                // Bei kurzer Überlastung des kostenlosen Dienstes einmal nachfassen
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        raw = await Http.Client.GetStringAsync(url);
                        if (Json.Str(Json.Parse(raw), "responseStatus") == "200" || attempt >= 1) break;
                    }
                    catch (HttpRequestException) { if (attempt >= 1) throw; }
                    await Task.Delay(600);
                }
                if (Json.Str(Json.Parse(raw), "responseStatus") == "200") Cache.Put(key, raw);
            }
            var o = Json.Parse(raw);
            if (Json.Str(o, "quotaFinished") == "True")
                throw new Exception("Tageskontingent von MyMemory aufgebraucht – in den Einstellungen E-Mail eintragen (10× mehr) oder KI/DeepL nutzen.");
            string status = Json.Str(o, "responseStatus");
            if (status != "200") throw new Exception("MyMemory: " + (Json.Str(o, "responseDetails") ?? status));

            // Kandidaten: Hauptergebnis + Treffer aus dem Übersetzungsgedächtnis mit exakt gleichem Ausgangstext
            var cands = new List<string>();
            cands.Add(Fix(Json.Str(o, "responseData", "translatedText"), text));
            var matches = Json.Get(o, "matches") as object[];
            if (matches != null)
                foreach (var m in matches)
                {
                    string seg = Json.Str(m, "segment");
                    if (seg != null && seg.Trim().Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)) cands.Add(Fix(Json.Str(m, "translation"), text));
                }
            cands.RemoveAll(c => string.IsNullOrWhiteSpace(c) || c.Length > Math.Max(60, text.Length * 3));

            // Ein „Ergebnis“, das nur das Original wiederholt (sword → Sword), ist keine Übersetzung
            var r = new TrResult();
            r.Text = cands.Find(c => !c.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)) ?? (cands.Count > 0 ? cands[0] : null);
            // Grundbegriff bevorzugen, wenn er im Hauptergebnis steckt (Schwert statt Kettenschienenschwert)
            if (r.Text != null)
                foreach (var c in cands)
                    if (c.Length >= 3 && c.Length < r.Text.Length && !c.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)
                        && r.Text.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0) r.Text = c;
            foreach (var tr in cands)
            {
                if (tr.Equals(r.Text, StringComparison.OrdinalIgnoreCase) || tr.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (r.Alternatives.Exists(a => a.Equals(tr, StringComparison.OrdinalIgnoreCase))) continue;
                // Zusammensetzungen wie „Kettenschienenschwert“ für „sword“ sind keine echten Alternativen
                if (r.Text != null && tr.Length > r.Text.Length + 3 && tr.IndexOf(r.Text, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                r.Alternatives.Add(tr);
                if (r.Alternatives.Count >= 4) break;
            }
            return r;
        }

        static string Fix(string s, string original)
        {
            if (s == null) return null;
            s = WebUtility.HtmlDecode(s).Trim();
            // Satzzeichen am Ende nur behalten, wenn das Original auch eins hat („sword“ → „Schwert“, nicht „Schwert.“)
            string o2 = original.Trim();
            if (s.Length > 1 && ".!?".IndexOf(s[s.Length - 1]) >= 0 && (o2.Length == 0 || ".!?".IndexOf(o2[o2.Length - 1]) < 0))
                s = s.Substring(0, s.Length - 1).TrimEnd();
            // MyMemory liefert bei kurzen Wörtern manchmal GROSSBUCHSTABEN
            if (s.Length > 3 && s == s.ToUpperInvariant() && original != original.ToUpperInvariant())
                s = s.Substring(0, 1) + s.Substring(1).ToLowerInvariant();
            return s;
        }
    }

    // ---------------- DeepL Free ----------------
    static class DeepL
    {
        public static async Task<TrResult> Translate(string text, string src, string tgt, string key)
        {
            string ck = "dl|" + src + "|" + tgt + "|" + text;
            string cached = Cache.Get(ck);
            if (cached != null) return new TrResult { Text = cached };
            string host = key.Trim().EndsWith(":fx") ? "https://api-free.deepl.com" : "https://api.deepl.com";
            var req = new HttpRequestMessage(HttpMethod.Post, host + "/v2/translate");
            req.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + key.Trim());
            string srcCode = Lang.Get(src).DeepL.Split('-')[0];
            req.Content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("source_lang", srcCode),
                new KeyValuePair<string, string>("target_lang", Lang.Get(tgt).DeepL) });
            var resp = await Http.Client.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception("DeepL: " + (int)resp.StatusCode + " " + Short(body));
            string t = Json.Str(Json.Parse(body), "translations", 0, "text");
            Cache.Put(ck, t);
            return new TrResult { Text = t };
        }
        static string Short(string s) { return s == null ? "" : s.Length > 120 ? s.Substring(0, 120) : s; }
    }

    // ---------------- Freies englisches Wörterbuch: Lautschrift, Aufnahme, Beispiel ----------------
    static class DictApi
    {
        public static async Task<DictInfo> Lookup(string term)
        {
            if (term.Contains(" ") || term.Length > 40) return null;
            foreach (var cand in Lemma.Candidates(term))
            {
                if (cand.Contains(" ")) continue;
                string key = "dict|" + cand;
                string raw = Cache.Get(key);
                if (raw == null)
                {
                    var resp = await Http.Client.GetAsync("https://api.dictionaryapi.dev/api/v2/entries/en/" + Uri.EscapeDataString(cand));
                    if (resp.StatusCode == HttpStatusCode.NotFound) { Cache.Put(key, "[]"); continue; }
                    if (!resp.IsSuccessStatusCode) return null;
                    raw = await resp.Content.ReadAsStringAsync();
                    Cache.Put(key, raw);
                }
                var arr = Json.Parse(raw) as object[];
                if (arr == null || arr.Length == 0) continue;
                return Extract(arr, cand);
            }
            return null;
        }

        static DictInfo Extract(object[] arr, string word)
        {
            var d = new DictInfo();
            foreach (var entry in arr)
            {
                if (d.Ipa == null) { var ph = Json.Str(entry, "phonetic"); if (!string.IsNullOrEmpty(ph)) d.Ipa = ph; }
                var phs = Json.Get(entry, "phonetics") as object[];
                if (phs != null)
                    foreach (var p in phs)
                    {
                        string t = Json.Str(p, "text"), a = Json.Str(p, "audio");
                        if (d.Ipa == null && !string.IsNullOrEmpty(t)) d.Ipa = t;
                        if (!string.IsNullOrEmpty(a) && (d.Audio == null || a.Contains("-us."))) d.Audio = a;
                    }
                var means = Json.Get(entry, "meanings") as object[];
                if (means == null) continue;
                foreach (var m in means)
                {
                    var defs = Json.Get(m, "definitions") as object[];
                    if (defs == null) continue;
                    foreach (var df in defs)
                    {
                        if (d.Definition == null) { d.Definition = Json.Str(df, "definition"); d.PartOfSpeech = Json.Str(m, "partOfSpeech"); }
                        string ex = Json.Str(df, "example");
                        if (d.Example == null && !string.IsNullOrEmpty(ex) && ex.Length < 140 && !char.IsDigit(ex[0])) d.Example = ex;
                    }
                }
            }
            if (d.Ipa != null) d.Ipa = d.Ipa.Trim('/', '[', ']', ' ');
            return d;
        }

        public static string PosGerman(string pos)
        {
            switch (pos)
            {
                case "noun": return "Substantiv"; case "verb": return "Verb"; case "adjective": return "Adjektiv";
                case "adverb": return "Adverb"; case "pronoun": return "Pronomen"; case "preposition": return "Präposition";
                case "conjunction": return "Konjunktion"; case "interjection": return "Ausruf";
            }
            return pos;
        }
    }

    // ---------------- Freie KI (OpenAI-kompatible Schnittstelle) ----------------
    static class Ai
    {
        public static async Task<AiInfo> Ask(Settings cfg, string term, string context)
        {
            string src = Lang.English(cfg.SourceLang), tgt = Lang.English(cfg.TargetLang);
            string ck = "ai|" + cfg.AiModel + "|" + cfg.SourceLang + "|" + cfg.TargetLang + "|" + term + "|" + (context ?? "");
            string content = Cache.Get(ck);
            if (content == null)
            {
                string sys =
                    "You are a precise bilingual dictionary for a player of the online game World of Warcraft. " +
                    "The player reads game text in " + src + " and wants explanations in " + tgt + ". " +
                    "Reply with ONLY a JSON object (no markdown) with these keys: " +
                    "\"translation\" (best " + tgt + " translation of the term as used here; for WoW names, zones, items, classes and slang use the official " + tgt + " WoW client term if one exists), " +
                    "\"alternatives\" (array, up to 4 other common translations), " +
                    "\"ipa\" (IPA pronunciation of the term in " + src + ", without slashes), " +
                    "\"part_of_speech\" (in " + tgt + "), " +
                    "\"meaning\" (one short sentence in " + tgt + " explaining what the term means here; mention if it is WoW slang), " +
                    "\"example\" (one short natural example sentence in " + src + ", WoW-flavored if it fits), " +
                    "\"example_translation\" (" + tgt + "), " +
                    "\"context_translation\" (" + tgt + " translation of the given context, or empty string if there is none).";
                string user = "Term: \"" + term + "\"\nContext: \"" + (context ?? "") + "\"";
                var body = new Dictionary<string, object> {
                    { "model", cfg.AiModel }, { "temperature", 0.2 },
                    { "messages", new object[] {
                        new Dictionary<string, object> { { "role", "system" }, { "content", sys } },
                        new Dictionary<string, object> { { "role", "user" }, { "content", user } } } } };
                string resp = await Post(cfg, "/chat/completions", Json.Write(body));
                content = Json.Str(Json.Parse(resp), "choices", 0, "message", "content");
                if (string.IsNullOrEmpty(content)) throw new Exception("KI: leere Antwort");
                Cache.Put(ck, content);
            }
            int a = content.IndexOf('{'), b = content.LastIndexOf('}');
            if (a < 0 || b <= a) throw new Exception("KI: Antwort nicht lesbar");
            var o = Json.Parse(content.Substring(a, b - a + 1));
            var r = new AiInfo
            {
                Translation = Json.Str(o, "translation"), Ipa = Json.Str(o, "ipa"), PartOfSpeech = Json.Str(o, "part_of_speech"),
                Meaning = Json.Str(o, "meaning"), Example = Json.Str(o, "example"), ExampleTr = Json.Str(o, "example_translation"),
                ContextTr = Json.Str(o, "context_translation")
            };
            if (r.Ipa != null) r.Ipa = r.Ipa.Trim('/', '[', ']', ' ');
            var alts = Json.Get(o, "alternatives") as object[];
            if (alts != null) foreach (var x in alts) { string s = Convert.ToString(x); if (!string.IsNullOrWhiteSpace(s) && r.Alternatives.Count < 4) r.Alternatives.Add(s); }
            return r;
        }

        /// <summary>Ganzen Text (z. B. Questtext) übersetzen – Namen und Spielbegriffe wie im deutschen Client.</summary>
        public static async Task<string> TranslateText(Settings cfg, string text)
        {
            string ck = "ait|" + cfg.AiModel + "|" + cfg.TargetLang + "|" + text;
            string cached = Cache.Get(ck);
            if (cached != null) return cached;
            string sys = "Translate the following World of Warcraft text from " + Lang.English(cfg.SourceLang) + " to " + Lang.English(cfg.TargetLang) +
                         ". Use the official " + Lang.English(cfg.TargetLang) + " WoW client terms for names, places, classes and items when they exist. " +
                         "Keep line breaks and placeholders like <Name>. Reply with the translation only.";
            var body = new Dictionary<string, object> {
                { "model", cfg.AiModel }, { "temperature", 0.2 },
                { "messages", new object[] {
                    new Dictionary<string, object> { { "role", "system" }, { "content", sys } },
                    new Dictionary<string, object> { { "role", "user" }, { "content", text } } } } };
            string resp = await Post(cfg, "/chat/completions", Json.Write(body));
            string content = Json.Str(Json.Parse(resp), "choices", 0, "message", "content");
            if (string.IsNullOrWhiteSpace(content)) throw new Exception("KI: leere Antwort");
            content = content.Trim();
            Cache.Put(ck, content);
            return content;
        }

        public static async Task<List<string>> Models(Settings cfg)
        {
            string resp = await Get(cfg, "/models");
            var list = new List<string>();
            var data = Json.Get(Json.Parse(resp), "data") as object[];
            if (data != null) foreach (var m in data) { string id = Json.Str(m, "id"); if (id != null) list.Add(id.Replace("models/", "")); }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        static HttpRequestMessage Req(Settings cfg, HttpMethod m, string path)
        {
            var req = new HttpRequestMessage(m, cfg.AiEndpoint.TrimEnd('/') + path);
            string key = cfg.GetAiKey();
            if (!string.IsNullOrEmpty(key)) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            req.Headers.TryAddWithoutValidation("X-Title", "WoW Lexikon");
            return req;
        }
        static async Task<string> Post(Settings cfg, string path, string json)
        {
            var req = Req(cfg, HttpMethod.Post, path);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return await Send(req);
        }
        static async Task<string> Get(Settings cfg, string path) { return await Send(Req(cfg, HttpMethod.Get, path)); }
        static async Task<string> Send(HttpRequestMessage req)
        {
            var resp = await Http.Client.SendAsync(req);
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                string msg = Json.Str(SafeParse(body), "error", "message") ?? Json.Str(SafeParse(body), 0, "error", "message") ?? body;
                if (msg != null && msg.Length > 160) msg = msg.Substring(0, 160) + "…";
                throw new Exception("KI " + (int)resp.StatusCode + ": " + msg);
            }
            return body;
        }
        static object SafeParse(string s) { try { return Json.Parse(s); } catch { return null; } }
    }

    // ---------------- Vokabelliste (CSV, öffnet sich in Excel) ----------------
    static class Vocab
    {
        public static string FilePath { get { return Path.Combine(Settings.Dir, "vokabeln.csv"); } }
        public static void Add(string term, string ipa, string tr, string ex, string exTr, string ctx)
        {
            bool isNew = !File.Exists(FilePath);
            using (var w = new StreamWriter(FilePath, true, new UTF8Encoding(true)))
            {
                if (isNew) w.WriteLine("Datum;Wort;Lautschrift;Übersetzung;Beispiel;Beispiel übersetzt;Kontext");
                var cells = new[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm"), term, ipa, tr, ex, exTr, ctx };
                w.WriteLine(string.Join(";", System.Linq.Enumerable.Select(cells, Csv)));
            }
        }
        static string Csv(string s)
        {
            s = s ?? "";
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }
    }
}
