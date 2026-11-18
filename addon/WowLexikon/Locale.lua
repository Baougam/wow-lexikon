-- WoW Lexikon – Sprachen (Grundausstattung für spätere Übersetzungen in andere Sprachen)
--
-- Zwei Dinge sind getrennt:
--   1. Oberflächen-Sprache (uiLang): Knöpfe, Meldungen, Einstellungen. Geschrieben wird überall Deutsch,
--      WL.T("…") holt die Übersetzung aus WL.locales[uiLang]. Fehlt eine Zeile, bleibt sie deutsch.
--   2. Zielsprache (targetLang): in welche Sprache englische Wörter übersetzt werden.
--      Jede Sprache bringt eine eigene Wörtertabelle mit (Data\Words_<kürzel>*.lua → WowLexikon_Words_<kürzel>),
--      das Begleit-Programm erzeugt dazu die offiziellen Quest-/Namensdaten in dieser Sprache
--      (gameLocale = die Sprachversion der Blizzard-Daten).
--
-- Neue Sprache hinzufügen: Eintrag in WL.LANGS, Tabelle WL.locales.<kürzel>, Wörterdateien in die TOC.
WowLexikon.Try("Sprachen", function()
local WL = WowLexikon

WL.LANGS.de = { name = "Deutsch",    words = "WowLexikon_Words",    gameLocale = "deDE", ready = true }
WL.LANGS.fr = { name = "Français",   words = "WowLexikon_Words_fr", gameLocale = "frFR", ready = false }
WL.LANGS.es = { name = "Español",    words = "WowLexikon_Words_es", gameLocale = "esES", ready = false }
WL.LANGS.it = { name = "Italiano",   words = "WowLexikon_Words_it", gameLocale = "itIT", ready = false }
WL.LANGS.pt = { name = "Português",  words = "WowLexikon_Words_pt", gameLocale = "ptBR", ready = false }
WL.LANGS.ru = { name = "Русский",    words = "WowLexikon_Words_ru", gameLocale = "ruRU", ready = false }
WL.LANGS.ko = { name = "한국어",      words = "WowLexikon_Words_ko", gameLocale = "koKR", ready = false }
WL.LANGS.zh = { name = "简体中文",    words = "WowLexikon_Words_zh", gameLocale = "zhCN", ready = false }

--- Sprachen, für die es schon Wörter gibt (für Auswahllisten)
function WL.ReadyLangs()
  local t = {}
  for k, v in pairs(WL.LANGS) do
    if v.ready or type(_G[v.words or ""]) == "table" then t[#t + 1] = k end
  end
  table.sort(t)
  return t
end

-- Oberflächen-Texte. Deutsch ist die Grundlage und braucht keine Tabelle.
-- Englisch als Beispiel und Vorlage für weitere Sprachen:
WL.locales.en = {
  ["Vorlesen"] = "Speak",
  ["Langsam"] = "Slow",
  ["Merken"] = "Save",
  ["Gemerkt"] = "Saved",
  ["Mehr"] = "More",
  ["Weniger"] = "Less",
  ["Wörterbuch"] = "Dictionary",
  ["Deutscher Text"] = "Translation",
  ["anzeigen ›"] = "show ›",
  ["‹ ausblenden"] = "‹ hide",
  ["Noch nicht im Wörterbuch"] = "Not in the dictionary yet",
  ["so ist es hier im Satz gemeint"] = "meaning in this sentence",
  ["Stimmen gefunden"] = "voices found",
  ["Stimme"] = "Voice",
  ["Klappt"] = "Works",
  ["Keine Wiedergabe gemeldet."] = "No playback reported.",
  ["Keine Stimmen gefunden."] = "No voices found.",
  ["Diese Spielversion hat keine Sprachausgabe."] = "This game version has no text to speech.",
  ["Vorlesen hat nicht geklappt. Im Spiel: Einstellungen → Barrierefreiheit → Text-zu-Sprache (Lautstärke). Unter Windows muss eine englische Stimme installiert sein. Test: /lex vorlesen test"] =
    "Speaking failed. In game: Options → Accessibility → Text to Speech (volume). Windows needs an English voice installed. Test: /lex vorlesen test",
}
end)
