using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace WowLexikon
{
    public class Settings
    {
        public string SourceLang { get; set; }
        public string TargetLang { get; set; }
        public string Hotkey { get; set; }
        public bool OnlyInWow { get; set; }
        public bool Autostart { get; set; }
        public int AutoHideSeconds { get; set; }
        public string Provider { get; set; }       // auto | mymemory | deepl | ai
        public string MyMemoryEmail { get; set; }
        public string DeepLKeyEnc { get; set; }
        public string AiPreset { get; set; }
        public string AiEndpoint { get; set; }
        public string AiModel { get; set; }
        public string AiKeyEnc { get; set; }
        public string Voice { get; set; }
        public double SpeechRate { get; set; }
        public bool AutoSpeak { get; set; }
        public bool FirstRun { get; set; }
        public string Look { get; set; }           // quest | tooltip | modern
        public string GameFontPath { get; set; }
        public string WowFolder { get; set; }      // WoW-Ordner (für das Addon); leer = automatisch suchen
        public string UpdateUrl { get; set; }      // Update-Quelle (manifest.json), z. B. auf GitHub
        public bool AutoSync { get; set; }         // Addon automatisch abgleichen und Updates prüfen

        public Settings()
        {
            SourceLang = "en"; TargetLang = "de"; Hotkey = "CtrlShift"; OnlyInWow = true;
            AutoHideSeconds = 15; Provider = "auto"; MyMemoryEmail = ""; DeepLKeyEnc = "";
            AiPreset = "gemini"; AiEndpoint = AiPresets.Get("gemini").Endpoint; AiModel = AiPresets.Get("gemini").Model;
            AiKeyEnc = ""; Voice = ""; SpeechRate = 1.0; AutoSpeak = true; FirstRun = true;
            Look = "quest"; GameFontPath = ""; WowFolder = ""; UpdateUrl = ""; AutoSync = true;
        }

        // ---- Schlüssel werden mit Windows-DPAPI nur für diesen Benutzer verschlüsselt ----
        public string GetDeepLKey() { return Unprotect(DeepLKeyEnc); }
        public void SetDeepLKey(string v) { DeepLKeyEnc = Protect(v); }
        public string GetAiKey() { return Unprotect(AiKeyEnc); }
        public void SetAiKey(string v) { AiKeyEnc = Protect(v); }

        static string Protect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));
        }
        static string Unprotect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }

        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WowLexikon");
                Directory.CreateDirectory(d);
                return d;
            }
        }
        static string FilePath { get { return Path.Combine(Dir, "einstellungen.json"); } }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (s != null) return s;
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            File.WriteAllText(FilePath, Json.Pretty(new JavaScriptSerializer().Serialize(this)), new UTF8Encoding(false));
        }

        public Settings Clone()
        {
            var js = new JavaScriptSerializer();
            return js.Deserialize<Settings>(js.Serialize(this));
        }
    }

    public class HotkeyDef
    {
        public string Id, Label; public int[] Keys;
        public HotkeyDef(string id, string label, params int[] keys) { Id = id; Label = label; Keys = keys; }

        public static readonly HotkeyDef[] All = new[]
        {
            new HotkeyDef("CtrlShift", "Strg + Umschalt", 0x11, 0x10),
            new HotkeyDef("CtrlAlt", "Strg + Alt", 0x11, 0x12),
            new HotkeyDef("AltShift", "Alt + Umschalt", 0x12, 0x10),
            new HotkeyDef("Mouse4", "Maus-Seitentaste 1", 0x05),
            new HotkeyDef("Mouse5", "Maus-Seitentaste 2", 0x06),
        };
        public static HotkeyDef Get(string id)
        {
            foreach (var h in All) if (h.Id == id) return h;
            return All[0];
        }
    }

    public class AiPreset
    {
        public string Id, Label, Endpoint, Model, KeyUrl; public bool NeedsKey;
        public AiPreset(string id, string label, string ep, string model, string keyUrl, bool needsKey)
        { Id = id; Label = label; Endpoint = ep; Model = model; KeyUrl = keyUrl; NeedsKey = needsKey; }
    }

    public static class AiPresets
    {
        public static readonly AiPreset[] All = new[]
        {
            new AiPreset("gemini", "Google Gemini (kostenloses Kontingent)", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-2.5-flash", "https://aistudio.google.com/apikey", true),
            new AiPreset("groq", "Groq (kostenloses Kontingent, sehr schnell)", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", "https://console.groq.com/keys", true),
            new AiPreset("openrouter", "OpenRouter (kostenlose Modelle)", "https://openrouter.ai/api/v1", "meta-llama/llama-3.3-70b-instruct:free", "https://openrouter.ai/keys", true),
            new AiPreset("ollama", "Ollama (lokal auf diesem PC)", "http://localhost:11434/v1", "llama3.2", "https://ollama.com/download", false),
            new AiPreset("custom", "Eigener Dienst (OpenAI-kompatibel)", "", "", "", true),
        };
        public static AiPreset Get(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return All[0];
        }
    }

    public class Lang
    {
        public string Code, Name, DeepL;
        public Lang(string code, string name, string deepl) { Code = code; Name = name; DeepL = deepl; }

        public static readonly Lang[] All = new[]
        {
            new Lang("de", "Deutsch", "DE"), new Lang("en", "Englisch", "EN-US"), new Lang("fr", "Französisch", "FR"),
            new Lang("es", "Spanisch", "ES"), new Lang("it", "Italienisch", "IT"), new Lang("pt", "Portugiesisch", "PT-PT"),
            new Lang("nl", "Niederländisch", "NL"), new Lang("pl", "Polnisch", "PL"), new Lang("ru", "Russisch", "RU"),
            new Lang("uk", "Ukrainisch", "UK"), new Lang("tr", "Türkisch", "TR"), new Lang("cs", "Tschechisch", "CS"),
            new Lang("sv", "Schwedisch", "SV"), new Lang("da", "Dänisch", "DA"), new Lang("el", "Griechisch", "EL"),
            new Lang("ar", "Arabisch", "AR"), new Lang("zh-CN", "Chinesisch", "ZH"), new Lang("ja", "Japanisch", "JA"),
            new Lang("ko", "Koreanisch", "KO"),
        };
        public static Lang Get(string code)
        {
            foreach (var l in All) if (l.Code == code) return l;
            return new Lang(code, code, code.ToUpperInvariant());
        }
        /// <summary>Englischer Name für KI-Prompts.</summary>
        public static string English(string code)
        {
            switch (code)
            {
                case "de": return "German"; case "en": return "English"; case "fr": return "French"; case "es": return "Spanish";
                case "it": return "Italian"; case "pt": return "Portuguese"; case "nl": return "Dutch"; case "pl": return "Polish";
                case "ru": return "Russian"; case "uk": return "Ukrainian"; case "tr": return "Turkish"; case "cs": return "Czech";
                case "sv": return "Swedish"; case "da": return "Danish"; case "el": return "Greek"; case "ar": return "Arabic";
                case "zh-CN": return "Chinese (Simplified)"; case "ja": return "Japanese"; case "ko": return "Korean";
            }
            return code;
        }
    }

    static class Json
    {
        static readonly JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static object Parse(string s) { return js.DeserializeObject(s); }
        public static string Write(object o) { return js.Serialize(o); }

        public static object Get(object o, params object[] path)
        {
            foreach (var p in path)
            {
                if (o == null) return null;
                var d = o as Dictionary<string, object>;
                var a = o as object[];
                if (p is string && d != null) { object v; o = d.TryGetValue((string)p, out v) ? v : null; }
                else if (p is int && a != null) { int i = (int)p; o = i < a.Length ? a[i] : null; }
                else return null;
            }
            return o;
        }
        public static string Str(object o, params object[] path)
        {
            var v = Get(o, path);
            return v == null ? null : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Einfaches Einrücken, damit die Einstellungsdatei lesbar bleibt.</summary>
        public static string Pretty(string json)
        {
            var sb = new StringBuilder(); int ind = 0; bool q = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\')) q = !q;
                if (q) { sb.Append(c); continue; }
                if (c == '{' || c == '[') { sb.Append(c).Append('\n').Append(' ', (++ind) * 2); }
                else if (c == '}' || c == ']') { sb.Append('\n').Append(' ', (--ind) * 2).Append(c); }
                else if (c == ',') { sb.Append(c).Append('\n').Append(' ', ind * 2); }
                else if (c == ':') { sb.Append(": "); }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
