-- WoW Lexikon – Kern: Nachschlagen, Grundformen, Vorlesen, Aufzeichnen für das Begleit-Programm
local ADDON_NAME = ...

WowLexikon = WowLexikon or {}
local WL = WowLexikon
WL.version = "1.0.0"

-- Datentabellen (werden von den Data-Dateien gefüllt)
WowLexikon_Words   = WowLexikon_Words   or {}   -- [wort] = "lautschrift|übersetzung|hinweis|beispiel|beispiel übersetzt|quelle"
WowLexikon_Learned = WowLexikon_Learned or {}   -- vom Begleit-Programm: { words = {...}, quests = {...}, build = "...", generated = "..." }
WowLexikon_Names   = WowLexikon_Names   or {}   -- [name] = "typ|übersetzung|titel"          (WowLexikon_Data, lokal erzeugt)
WowLexikon_Quests  = WowLexikon_Quests  or {}   -- [questID] = { T=, D=, O=, Q=, R=, C= }      (WowLexikon_Data, lokal erzeugt)

WL.IPA_FONT = "Interface\\AddOns\\WowLexikon\\Fonts\\GentiumBookPlus-Regular.ttf"

---------------------------------------------------------------------------
-- Mehrsprachigkeit (Grundausstattung; Texte und Sprachliste in Locale.lua)
--   Oberflächen-Sprache: WL.T("deutscher Text", ...) → Text in der gewählten Sprache (fehlt er: Deutsch)
--   Zielsprache:         in welche Sprache übersetzt wird – jede hat ihre eigene Wörtertabelle
---------------------------------------------------------------------------
WL.locales = WL.locales or {}   -- [sprachkürzel] = { ["deutscher Text"] = "übersetzt", ... }
WL.LANGS = WL.LANGS or {}       -- [sprachkürzel] = { name =, words = "Tabellenname", gameLocale =, ready = true/false }

function WL.UiLang() return (WowLexikonDB and WowLexikonDB.options.uiLang) or "de" end
function WL.TargetLang() return (WowLexikonDB and WowLexikonDB.options.targetLang) or "de" end

function WL.T(s, ...)
  local t = WL.locales[WL.UiLang()]
  local r = (t and t[s]) or s
  if select("#", ...) > 0 then return r:format(...) end
  return r
end

--- Wörtertabelle der Zielsprache (Deutsch: WowLexikon_Words, andere: WowLexikon_Words_<kürzel>)
function WL.WordTable()
  local lang = WL.TargetLang()
  local info = WL.LANGS[lang]
  local t = info and info.words and _G[info.words]
  if lang ~= "de" and type(t) ~= "table" then t = _G["WowLexikon_Words_" .. lang] end
  return type(t) == "table" and t or WowLexikon_Words
end

---------------------------------------------------------------------------
-- Fehler beim Laden abfangen und melden (statt dass still ein Teil fehlt)
---------------------------------------------------------------------------
WL.errors = {}
function WL.Try(name, f)
  local ok, err = xpcall(f, function(e) return tostring(e) .. ((debugstack and ("\n" .. (debugstack(2, 4, 0) or ""))) or "") end)
  if not ok then
    WL.errors[#WL.errors + 1] = name .. ": " .. tostring(err)
    print("|cffff5555WoW Lexikon – Fehler beim Laden (" .. name .. "):|r " .. tostring(err):sub(1, 220) .. "  |cffffd100/lex fehler|r zeigt mehr.")
  end
  return ok
end

-- /lex gibt es immer – auch wenn die Oberfläche nicht geladen werden konnte
SLASH_WOWLEXIKON1 = "/lex"
SLASH_WOWLEXIKON2 = "/lexikon"
SlashCmdList.WOWLEXIKON = function(msg)
  local cmd = (msg or ""):lower():match("^%s*(%S*)")
  if cmd == "fehler" or not WL.HandleSlash then
    if #WL.errors == 0 then print("|cffffd100WoW Lexikon:|r keine Fehler.") end
    for _, e in ipairs(WL.errors) do print("|cffff5555WoW Lexikon – Fehler:|r " .. e) end
    if not WL.HandleSlash then print("Bitte einen Screenshot dieser Meldung schicken.") end
    return
  end
  WL.HandleSlash(msg)
end

---------------------------------------------------------------------------
-- Grundformen: murlocs → murloc, wolves → wolf, slaughtered → slaughter
---------------------------------------------------------------------------
local IRREGULAR = {
  slew = "slay", slain = "slay", men = "man", teeth = "tooth", feet = "foot", mice = "mouse", geese = "goose",
  children = "child", fought = "fight", brought = "bring", sought = "seek", stole = "steal", stolen = "steal",
  drew = "draw", drawn = "draw", found = "find", took = "take", taken = "take", gave = "give", given = "give",
  went = "go", gone = "go", saw = "see", seen = "see", knew = "know", known = "know", told = "tell", sold = "sell",
  held = "hold", kept = "keep", left = "leave", lost = "lose", made = "make", paid = "pay", said = "say", sent = "send",
  spent = "spend", stood = "stand", thought = "think", understood = "understand", won = "win", wrote = "write",
  written = "write", ate = "eat", eaten = "eat", began = "begin", begun = "begin", broke = "break", broken = "break",
  chose = "choose", chosen = "choose", fell = "fall", fallen = "fall", flew = "fly", flown = "fly", forgot = "forget",
  forgotten = "forget", froze = "freeze", frozen = "freeze", grew = "grow", grown = "grow", hid = "hide", hidden = "hide",
  rode = "ride", ridden = "ride", rose = "rise", risen = "rise", shook = "shake", spoke = "speak", spoken = "speak",
  swore = "swear", sworn = "swear", tore = "tear", torn = "tear", woke = "wake", wore = "wear", worn = "wear",
  better = "good", best = "good", worse = "bad", worst = "bad", wolves = "wolf", elves = "elf", dwarves = "dwarf",
}

local function forms(w)
  local f, seen = {}, {}
  local function add(x) if x and #x >= 2 and not seen[x] then seen[x] = true; f[#f + 1] = x end end
  add(w)
  if w:sub(1, 4) == "the " then add(w:sub(5)) end
  if w:sub(-2) == "'s" then add(w:sub(1, -3)) end
  add(IRREGULAR[w])
  if w:sub(-3) == "ies" then add(w:sub(1, -4) .. "y") end
  if w:sub(-3) == "ves" then add(w:sub(1, -4) .. "f"); add(w:sub(1, -4) .. "fe") end
  if w:match("ches$") or w:match("shes$") or w:match("xes$") or w:match("sses$") or w:match("oes$") then add(w:sub(1, -3)) end
  if w:sub(-1) == "s" and w:sub(-2) ~= "ss" then add(w:sub(1, -2)) end
  if w:sub(-3) == "ied" then add(w:sub(1, -4) .. "y") end
  if w:sub(-2) == "ed" then
    local b = w:sub(1, -3); add(b); add(w:sub(1, -2))
    if #b > 2 and b:sub(-1) == b:sub(-2, -2) then add(b:sub(1, -2)) end
  end
  if w:sub(-3) == "ing" and #w > 5 then
    local b = w:sub(1, -4); add(b); add(b .. "e")
    if #b > 2 and b:sub(-1) == b:sub(-2, -2) then add(b:sub(1, -2)) end
  end
  if w:sub(-2) == "er" and #w > 5 then add(w:sub(1, -3)) end
  if w:sub(-3) == "est" and #w > 6 then add(w:sub(1, -4)) end
  if w:sub(-2) == "ly" and #w > 5 then add(w:sub(1, -3)) end
  return f
end
WL.Forms = forms

local function parse(word, base, s, origin)
  local ipa, tr, note, ex, exTr, src = strsplit("|", s)
  return { word = word, base = base, ipa = ipa ~= "" and ipa or nil, tr = tr, note = note ~= "" and note or nil,
           ex = ex ~= "" and ex or nil, exTr = exTr ~= "" and exTr or nil, src = origin or src }
end

--- Sucht ein Wort (beliebige Form). Rückgabe: Eintrag oder nil.
function WL.Lookup(word)
  if not word or word == "" then return nil end
  local w = word:lower():gsub("’", "'")
  -- Gelernte Wörter gibt es je Zielsprache (Deutsch: words, sonst words_<kürzel>)
  local lang = WL.TargetLang()
  local learned = (lang == "de" and WowLexikon_Learned.words or WowLexikon_Learned["words_" .. lang]) or {}
  local words = WL.WordTable()
  for _, f in ipairs(forms(w)) do
    local s = learned[f]
    if s then return parse(word, f, s, "l") end
    s = words[f]
    if s then return parse(word, f, s) end
  end
  return nil
end

--- Sucht einen Namen aus dem Spiel (NPC, Gegenstand, Objekt, Quest).
function WL.LookupName(phrase)
  if not phrase then return nil end
  local p = phrase:lower():gsub("’", "'")
  for _, f in ipairs(forms(p)) do
    local s = WowLexikon_Names[f]
    if s then
      local t, tr, title = strsplit("|", s)
      local label = ({ n = "NPC", i = "Gegenstand", o = "Objekt", q = "Quest" })[t] or ""
      return { word = phrase, base = f, tr = tr, note = label .. ((title and title ~= "") and (" · " .. title) or ""), isName = true, src = "n" }
    end
  end
  return nil
end

---------------------------------------------------------------------------
-- Aussprache „deutsch gelesen“ (falls die Lautschrift ungewohnt ist): /brɪdʒ/ → bridsch
---------------------------------------------------------------------------
local GERMAN_SOUND = {
  { "tʃ", "tsch" }, { "dʒ", "dsch" }, { "ʃ", "sch" }, { "ʒ", "sch" }, { "θ", "th" }, { "ð", "dh" }, { "ŋ", "ng" },
  { "aɪ", "ei" }, { "aʊ", "au" }, { "ɔɪ", "eu" }, { "eɪ", "eh" }, { "oʊ", "ou" },
  { "ɑː", "aa" }, { "ɔː", "oo" }, { "iː", "ii" }, { "uː", "uu" }, { "ɜːr", "öör" }, { "ɜː", "öö" },
  { "æ", "ä" }, { "ʌ", "a" }, { "ɛ", "e" }, { "ɪ", "i" }, { "ʊ", "u" }, { "ə", "e" }, { "ɒ", "o" }, { "ɑ", "a" }, { "ɔ", "o" },
  { "ɡ", "g" }, { "v", "w" }, { "j", "j" }, { "ˈ", "'" }, { "ˌ", "" }, { "ː", "" },
}
function WL.GermanReading(ipa)
  if not ipa or ipa == "" then return nil end
  local s = ipa
  for _, p in ipairs(GERMAN_SOUND) do s = s:gsub(p[1], p[2]) end
  return s
end

---------------------------------------------------------------------------
-- Vorlesen über die Sprachausgabe des Spiels
---------------------------------------------------------------------------
-- Seit Spielversion 12.0 (Jan. 2026): SpeakText(stimme, text, tempo, lautstärke)
-- Davor:                              SpeakText(stimme, text, ziel, tempo, lautstärke)
-- Wer die alte Form im neuen Spiel aufruft, bekommt Lautstärke 0 – also stumm. Darum: neue Form zuerst,
-- und nur wenn das Spiel kein „Wiedergabe gestartet“ meldet, die alte Form. Was klappt, wird gemerkt.
WL.SPEECH_LANG = "en"   -- Sprache, die vorgelesen wird (die Spielsprache, nicht die Zielsprache)
local VOICE_HINTS = { en = { "english", "en%-", "zira", "david", "mark", "hazel", "susan", "george" } }

function WL.TtsVoices() return (C_VoiceChat and C_VoiceChat.GetTtsVoices and C_VoiceChat.GetTtsVoices()) or {} end

local function pickVoice()
  local voices = WL.TtsVoices()
  local want = WowLexikonDB and WowLexikonDB.options.ttsVoice
  for _, v in ipairs(voices) do if want and v.voiceID == want then return v end end
  for _, hint in ipairs(VOICE_HINTS[WL.SPEECH_LANG] or {}) do
    for _, v in ipairs(voices) do if (v.name or ""):lower():find(hint) then return v end end
  end
  return voices[1]
end
WL.PickVoice = pickVoice

local tts = { waiting = nil, n = 0, warned = false }
WL.tts = tts

local function volume()
  local v = C_TTSSettings and C_TTSSettings.GetSpeechVolume and C_TTSSettings.GetSpeechVolume()
  return (type(v) == "number" and v > 0) and v or 100
end

local function speakWith(sig, voice, text, rate)
  if sig == "alt" then
    local dest = Enum and Enum.VoiceTtsDestination and Enum.VoiceTtsDestination.LocalPlayback or 1
    return pcall(C_VoiceChat.SpeakText, voice, text, dest, rate, volume())
  end
  return pcall(C_VoiceChat.SpeakText, voice, text, rate, volume())
end

local function knownSig()
  local s = WowLexikonDB and WowLexikonDB.tts
  local build = WowLexikonDB and WowLexikonDB.build and WowLexikonDB.build.build
  if s and s.sig and s.build == build then return s.sig end
end

local function speakFailed()
  if tts.warned then return end
  tts.warned = true
  print("|cffffd100WoW Lexikon:|r " .. WL.T("Vorlesen hat nicht geklappt. Im Spiel: Einstellungen → Barrierefreiheit → Text-zu-Sprache (Lautstärke). Unter Windows muss eine englische Stimme installiert sein. Test: /lex vorlesen test"))
end

--- rate: 0 = normal, negativ = langsamer (bis -10)
function WL.Speak(text, rate)
  if not text or text == "" or not (C_VoiceChat and C_VoiceChat.SpeakText) then return false end
  local v = pickVoice()
  if not v then speakFailed(); return false end
  if C_VoiceChat.StopSpeakingText then pcall(C_VoiceChat.StopSpeakingText) end
  rate = rate or 0
  local sig = knownSig()
  if sig then return (speakWith(sig, v.voiceID, text, rate)) end
  -- Noch unbekannt, welche Form dieses Spiel will: neue Form versuchen und auf die Rückmeldung warten
  tts.n = tts.n + 1
  local id = tts.n
  tts.waiting = { id = id, sig = "neu", voice = v.voiceID, text = text, rate = rate }
  local ok = speakWith("neu", v.voiceID, text, rate)
  local function fallback()
    local w = tts.waiting
    if not w or w.id ~= id then return end
    if w.sig == "neu" then
      w.sig = "alt"
      if C_VoiceChat.StopSpeakingText then pcall(C_VoiceChat.StopSpeakingText) end
      speakWith("alt", w.voice, w.text, w.rate)
      if C_Timer then C_Timer.After(1.5, fallback) end
    else
      tts.waiting = nil; speakFailed()
    end
  end
  if not ok then fallback()
  elseif C_Timer and tts.waiting then C_Timer.After(1.5, fallback) end
  return true
end

local ttsEv = CreateFrame("Frame")
pcall(ttsEv.RegisterEvent, ttsEv, "VOICE_CHAT_TTS_PLAYBACK_STARTED")
pcall(ttsEv.RegisterEvent, ttsEv, "VOICE_CHAT_TTS_PLAYBACK_FAILED")
ttsEv:SetScript("OnEvent", function(_, event)
  local w = tts.waiting
  if not w then return end
  if event == "VOICE_CHAT_TTS_PLAYBACK_STARTED" then
    tts.waiting = nil
    if WowLexikonDB then WowLexikonDB.tts = { sig = w.sig, build = WowLexikonDB.build and WowLexikonDB.build.build } end
  end
  -- FAILED: der Zeitgeber probiert gleich die andere Form
end)

--- /lex vorlesen test: zeigt Stimmen und liest einen Satz vor
function WL.SpeechTest()
  local voices = WL.TtsVoices()
  print("|cffffd100WoW Lexikon – Vorlesen:|r " .. #voices .. " " .. WL.T("Stimmen gefunden"))
  local v = pickVoice()
  for i, x in ipairs(voices) do print(("  %d. %s%s"):format(i, x.name or "?", (v and x.voiceID == v.voiceID) and "  ◄" or "")) end
  if not (C_VoiceChat and C_VoiceChat.SpeakText) then print("  " .. WL.T("Diese Spielversion hat keine Sprachausgabe.")); return end
  if WowLexikonDB then WowLexikonDB.tts = nil end
  tts.warned = false
  WL.Speak("Hello, adventurer. Welcome to Azeroth.", 0)
  if C_Timer then C_Timer.After(3.2, function()
    local s = WowLexikonDB and WowLexikonDB.tts
    print("  " .. (s and (WL.T("Klappt") .. " (" .. s.sig .. ")") or WL.T("Keine Wiedergabe gemeldet.")))
  end) end
end

--- /lex stimme: nächste Stimme wählen
function WL.NextVoice()
  local voices = WL.TtsVoices()
  if #voices == 0 then print("|cffffd100WoW Lexikon:|r " .. WL.T("Keine Stimmen gefunden.")); return end
  local cur, idx = pickVoice(), 1
  for i, x in ipairs(voices) do if cur and x.voiceID == cur.voiceID then idx = i % #voices + 1 end end
  WowLexikonDB.options.ttsVoice = voices[idx].voiceID
  print("|cffffd100WoW Lexikon:|r " .. WL.T("Stimme") .. ": " .. (voices[idx].name or "?"))
  WL.Speak("Hello, adventurer.", 0)
end

---------------------------------------------------------------------------
-- Gespeicherte Daten (fürs Begleit-Programm): gesehene Questtexte, unbekannte Wörter, Vokabelliste
---------------------------------------------------------------------------
local defaults = { seen = {}, unknown = {}, vocab = {}, options = { autoPanel = true, speakOnClick = true, tooltipNames = true, hoverMode = "taste",
                                                                hoverTip = true, showGerman = false, uiLang = "de", targetLang = "de" } }

local function initDB()
  WowLexikonDB = WowLexikonDB or {}
  for k, v in pairs(defaults) do if WowLexikonDB[k] == nil then WowLexikonDB[k] = v end end
  for k, v in pairs(defaults.options) do if WowLexikonDB.options[k] == nil then WowLexikonDB.options[k] = v end end
  local version, build, date, toc = GetBuildInfo()
  WowLexikonDB.build = { version = version, build = build, date = date, toc = toc, locale = GetLocale() }
  WowLexikonDB.addonVersion = WL.version
end

function WL.RecordUnknown(word)
  if not WowLexikonDB or not word then return end
  local k = word:lower()
  if #k < 2 or k:find("%d") then return end
  WowLexikonDB.unknown[k] = (WowLexikonDB.unknown[k] or 0) + 1
end

function WL.RecordQuest(id, field, text)
  if not WowLexikonDB or not id or id == 0 or not text or text == "" then return end
  local q = WowLexikonDB.seen[id] or {}
  q[field] = text
  q.t = time()
  q.b = WowLexikonDB.build and WowLexikonDB.build.build
  WowLexikonDB.seen[id] = q
end

function WL.AddVocab(entry)
  if not WowLexikonDB or not entry then return end
  WowLexikonDB.vocab[entry.base or entry.word:lower()] = { w = entry.word, tr = entry.tr, ipa = entry.ipa, t = time() }
end

--- Deutsche Questtexte: offiziell (lokale Spieldaten) oder vom Begleit-Programm übersetzt
function WL.QuestText(id)
  if not id or id == 0 then return nil end
  WL.EnsureData()
  local q = WowLexikon_Quests[id]
  local lq = WowLexikon_Learned.quests and WowLexikon_Learned.quests[id]
  -- In WoW Forever geändert: neue Übersetzung zuerst, alter offizieller Text als Ergänzung
  if lq and lq.changed then return lq, false, q end
  if q then return q, true end
  if lq then return lq, false end
  return nil
end

--- Läuft das Begleit-Programm (es schreibt Learned.lua bzw. erzeugt die Spieldaten)?
function WL.HasCompanion()
  return ((WowLexikon_Learned and WowLexikon_Learned.generated) or "") ~= "" or WL.dataLoaded and true or false
end
WL.COMPANION_HINT = "Deutsche Questtexte, deutsche Namen und automatisches Dazulernen gibt es mit dem kostenlosen Begleit-Programm „WoW Lexikon“ (Link in der Addon-Beschreibung)."

function WL.EnsureData()
  if WL.dataTried then return WL.dataLoaded end
  WL.dataTried = true
  local load = (C_AddOns and C_AddOns.LoadAddOn) or LoadAddOn
  local ok, loaded = pcall(load, "WowLexikon_Data")
  WL.dataLoaded = ok and loaded and true or false
  return WL.dataLoaded
end

---------------------------------------------------------------------------
-- Start
---------------------------------------------------------------------------
local ev = CreateFrame("Frame")
ev:RegisterEvent("ADDON_LOADED")
ev:RegisterEvent("PLAYER_LOGIN")
ev:SetScript("OnEvent", function(_, event, name)
  if event == "ADDON_LOADED" and name == ADDON_NAME then
    initDB()
  elseif event == "PLAYER_LOGIN" then
    WL.EnsureData()
    if WL.OnLogin then WL.Try("Start", WL.OnLogin) end
    if #WL.errors > 0 then print("|cffff5555WoW Lexikon:|r " .. #WL.errors .. " Fehler beim Laden – |cffffd100/lex fehler|r zeigt sie (bitte Screenshot schicken).") end
  end
end)
