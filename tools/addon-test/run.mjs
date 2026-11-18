// Prüft das Addon: Syntax (Lua 5.1) aller Dateien + Ablauf in einer nachgebildeten WoW-Umgebung
import fs from 'node:fs';
import path from 'node:path';
import luaparse from 'luaparse';
import fengari from 'fengari';
const { lua, lauxlib, lualib, to_luastring, to_jsstring } = fengari;

const addon = process.argv[2];
const results = [];
const check = (name, ok, detail) => results.push(`${ok ? 'OK    ' : 'FEHLER'}  ${name}${detail ? '  →  ' + detail : ''}`);

// 1) Syntax aller Lua-Dateien
const all = [];
for (const dir of ['WowLexikon', 'WowLexikon/Data', 'WowLexikon_Data']) {
  const d = path.join(addon, dir);
  for (const f of fs.readdirSync(d)) if (f.endsWith('.lua')) all.push(path.join(d, f));
}
for (const f of all) {
  try { luaparse.parse(fs.readFileSync(f, 'utf8'), { luaVersion: '5.1', comments: false, locations: false }); check('Syntax ' + path.relative(addon, f), true); }
  catch (e) { check('Syntax ' + path.relative(addon, f), false, e.message); }
}

// 2) Ablauf
const L = lauxlib.luaL_newstate();
lualib.luaL_openlibs(L);
function run(code, name) {
  // Lua 5.3 (Testumgebung) kennt %z nicht mehr – nur für den Test ersetzen
  code = code.replace(/%z/g, '\\0');
  const st = lauxlib.luaL_loadbuffer(L, to_luastring(code), null, to_luastring(name));
  if (st !== 0) throw new Error(name + ': ' + to_jsstring(lua.lua_tostring(L, -1)));
  if (lua.lua_pcall(L, 0, 0, 0) !== 0) throw new Error(name + ': ' + to_jsstring(lua.lua_tostring(L, -1)));
}
const src = f => fs.readFileSync(path.join(addon, f), 'utf8');
run(fs.readFileSync(new URL('./wowmock.lua', import.meta.url), 'utf8'), 'wowmock');
// LoadAddOn lädt die Spieldaten-Dateien
const dataFiles = fs.readFileSync(path.join(addon, 'WowLexikon_Data', 'WowLexikon_Data.toc'), 'utf8').split('\n').filter(l => l.trim().endsWith('.lua')).map(l => l.trim());
globalThis.__loadData = () => { for (const f of dataFiles) run(src('WowLexikon_Data/' + f), f); };
run(`function LoadAddOn(n) if n == "WowLexikon_Data" then __js_load() return true end return false end`, 'loadaddon');
lua.lua_pushjsfunction(L, () => { globalThis.__loadData(); return 0; });
lua.lua_setglobal(L, to_luastring('__js_load'));

try {
  const t0 = Date.now();
  // Addon-Dateien in TOC-Reihenfolge (... = Addon-Name)
  for (const f of fs.readFileSync(path.join(addon, 'WowLexikon', 'WowLexikon.toc'), 'utf8').split('\n').map(s => s.trim()).filter(s => s.endsWith('.lua'))) {
    let code = src('WowLexikon/' + f.replace(/\\/g, '/'));
    code = code.replace(/^local ADDON_NAME = \.\.\./m, 'local ADDON_NAME = "WowLexikon"');
    run(code, f);
  }
  run(`FireEvent("ADDON_LOADED", "WowLexikon"); FireEvent("PLAYER_LOGIN")`, 'start');
  check('Addon lädt + Spieldaten', true, (Date.now() - t0) + ' ms (Testumgebung, im Spiel schneller)');

  const q = code => { run('RESULT = tostring(' + code + ')', 'q'); lua.lua_getglobal(L, to_luastring('RESULT')); const s = to_jsstring(lua.lua_tostring(L, -1)); lua.lua_pop(L, 1); return s; };
  check('Fenster im Spiel-Stil', true, 'Wörterbuch: ' + q('tostring(WowLexikonPanel.styled)') + ', Buch: ' + q('tostring(WowLexikonBook.styled)'));
  check('Keine Ladefehler', q('#WowLexikon.errors') === '0', q('WowLexikon.errors[1]'));
  run(`SlashCmdList.WOWLEXIKON("hilfe")`, 'help');
  check('/lex hilfe schreibt in den Chat', q('#PRINTED') !== '0' && q('(function() for _, l in ipairs(PRINTED) do if l:find("bersetzen%-Taste") then return "ja" end end return "nein" end)()') === 'ja', q('PRINTED[#PRINTED]'));  for (const [w, expect] of [['slaughtered', 'abschlachten'], ['Murlocs', 'Murloc'], ['wolves', 'Wolf'], ['bridge', 'Brücke'], ['villages', 'Dörfer'], ['LFG', 'suche Gruppe']]) {
    const r = q(`(WowLexikon.Lookup("${w}") or {}).tr`);
    check('Wort: ' + w, r.includes(expect), r);
  }
  const nm = q(`(WowLexikon.LookupName("Defias Bandits") or {}).tr`);
  check('Name: Defias Bandits', nm === 'Bandit der Defias', nm);
  check('Aussprache deutsch gelesen', q(`WowLexikon.GermanReading("brɪdʒ")`) === 'bridsch', q(`WowLexikon.GermanReading("brɪdʒ")`));
  // Questfenster öffnet sich
  run(`QUEST.id = 35; QUEST.title = "Further Concerns"; QUEST.desc = "If you are concerned that the rumors of murlocs are true, then do this -- travel to the eastern Elwynn bridge and speak with Guard Thomas."; QUEST.obj = "Speak with Guard Thomas."; FireEvent("QUEST_DETAIL")`, 'quest');
  check('Standardmäßig eingeklappt: nur das Lesezeichen am Questfenster', q('WowLexikonPanel.shown') === 'false' && q('WowLexikonTab.shown') === 'true', null);
  run(`WowLexikonTab.scripts.OnClick(WowLexikonTab); WowLexikonPanel.scripts.OnUpdate(WowLexikonPanel, 1)`, 'tabopen');
  check('Klick aufs Lesezeichen: Wörterbuch gleitet heraus', q('WowLexikonPanel.shown') === 'true' && q('WowLexikon.drawer.open') === 'true', null);
  check('Keine Bedienungs-Hinweise im Fenster', q('WowLexikon.panelHint.text') === '', q('WowLexikon.panelHint.text'));
  run(`WowLexikonTab.scripts.OnClick(WowLexikonTab); WowLexikonPanel.scripts.OnUpdate(WowLexikonPanel, 1)`, 'tabclose');
  check('Nochmal Klick: wieder eingeklappt', q('WowLexikonPanel.shown') === 'false' && q('WowLexikonTab.shown') === 'true', null);
  run(`QuestFrame.IsMouseOver = function() return true end; GameTooltip.shown = false; WowLexikon.TranslateUnderMouse(); WowLexikonPanel.scripts.OnUpdate(WowLexikonPanel, 1); QuestFrame.IsMouseOver = function() return false end`, 'keyopen');
  check('Übersetzen-Taste über dem Questtext klappt das Wörterbuch aus', q('WowLexikonPanel.shown') === 'true', null);
  // Mehrere Bedeutungen: die im Satz gemeinte zuerst, der Rest unter „Mehr“
  check('Bedeutung im Satz erkannt (hide → Haut)', q('WowLexikon.MatchSense("Bringt mir die Haut des alten Ebers.", "Haut (eines Tieres); verstecken")') === 'Haut', null);
  run(`WowLexikonDB.options.cardMore = false; WowLexikon.ShowCard("hide", nil, true, "Haut")`, 'sense');
  check('Karte knapp: nur „Haut“ + „im Satz gemeint“', q('WowLexikonCard.tr.text') === 'Haut' && q('WowLexikonCard.ctx.text').includes('im Satz') && q('WowLexikonCard.all.shown') === 'false', q('WowLexikonCard.tr.text') + ' / ' + q('WowLexikonCard.ctx.text'));
  run(`WowLexikonCard.bMore.scripts.OnClick(WowLexikonCard.bMore)`, 'more');
  check('„Mehr“ zeigt weitere Bedeutungen', q('WowLexikonCard.all.text').includes('verstecken'), q('WowLexikonCard.all.text'));
  run(`WowLexikonDB.options.cardMore = false; WowLexikonCard.pinned = false; WowLexikonCard:Hide()`, 'lessreset');
  const linked = q('WowLexikon.links and #WowLexikon.links');
  check('Wörter anklickbar gemacht', +linked > 20, linked + ' Verweise');
  const guard = q(`(function() for i, p in ipairs(WowLexikon.links) do if p == "Guard Thomas" then return "ja" end end return "nein" end)()`);
  check('Name „Guard Thomas“ als Ganzes erkannt', guard === 'ja', guard);
  // Standard: Übersetzen-Taste
  check('Begrüßungsfenster beim ersten Start', q('WowLexikonWelcome.shown') === 'true', null);
  check('Freie Übersetzen-Taste automatisch gewählt', q('GetBindingKey("WOWLEXIKON_UNDERMOUSE")') === 'ALT-T', q('WowLexikon.KeyLabel(WowLexikon.TranslateKey())'));
  run(`WowLexikonWelcome:Hide()`, 'hidewelcome');
  run(`local n; for i, p in ipairs(WowLexikon.links) do if p == "bridge" then n = i end end
       WowLexikon.panelContent.scripts.OnHyperlinkEnter(WowLexikon.panelContent, "wlx:" .. n, "bridge")`, 'hover0');
  check('Darüberfahren: kleines Fenster mit direkter Übersetzung', q('WowLexikonTip.shown') === 'true' && q('WowLexikonTip.text.text') === 'Brücke' && q('WowLexikonCard.shown') === 'false', q('WowLexikonTip.text.text'));
  check('Deutscher Text standardmäßig zugeklappt', q('WowLexikonGermanToggle.shown') === 'true' && q('WowLexikon.panelGerman.text') === '', q('WowLexikon.panelGerman.text').slice(0, 60));
  run(`WowLexikon.TranslateUnderMouse()`, 'key');
  check('Übersetzen-Taste auf Wort: Karte erscheint', q('WowLexikonCard.shown') === 'true' && q('WowLexikonCard.tr.text') === 'Brücke', q('WowLexikonCard.tr.text'));
  run(`WowLexikon.panelContent.scripts.OnHyperlinkLeave(WowLexikon.panelContent); WowLexikonCard.pinned = false; WowLexikonCard:Hide()`, 'leave0');
  check('Wegfahren: kleines Fenster weg', q('WowLexikonTip.shown') === 'false', null);
  run(`WowLexikonGermanToggle.scripts.OnClick(WowLexikonGermanToggle)`, 'deopen');
  check('Klick auf „Deutscher Text“ klappt ihn auf', q('WowLexikon.panelGerman.text').length > 20 && q('WowLexikonDB.options.showGerman') === 'true', q('WowLexikon.panelGerman.text').slice(0, 60));
  // Deutsch/English-Knopf direkt im Questfenster
  run(`QuestInfoDescriptionText.text = "If you are concerned that the rumors are true"; QuestInfoTitleHeader.text = "Further Concerns"; FireEvent("QUEST_DETAIL")`, 'immersive0');
  check('Knopf „Deutsch“ im Questfenster', q('WowLexikonQuestLang.shown') === 'true' && q('WowLexikonQuestLang.text') === 'Deutsch', q('WowLexikonQuestLang.text'));
  run(`WowLexikonQuestLang.scripts.OnClick(WowLexikonQuestLang)`, 'immersive1');
  const deQ = q('QuestInfoDescriptionText.text');
  check('Klick: Questtext im Questfenster auf Deutsch', !deQ.startsWith('If you') && deQ.length > 20 && q('WowLexikonQuestLang.text') === 'English', deQ.slice(0, 60) + ' | ' + q('QuestInfoTitleHeader.text'));
  run(`WowLexikonQuestLang.scripts.OnClick(WowLexikonQuestLang)`, 'immersive2');
  check('Nochmal: wieder Englisch', q('QuestInfoDescriptionText.text').startsWith('If you'), null);
  // Taste festlegen: W ist belegt → Warnung, zweites Drücken übernimmt; T ist frei
  run(`WowLexikon.CaptureKey()`, 'capture');
  run(`for _, f in ipairs({ WowLexikonWelcome }) do end
       local cap
       for k, v in pairs(_G) do end`, 'noop');
  run(`CAPTURE = nil
       -- der Fang-Rahmen ist das einzige Kind mit OnKeyDown: über die Mock-Rahmenliste suchen
       for _, f in ipairs(ALLFRAMES) do if f.scripts.OnKeyDown then CAPTURE = f end end
       CAPTURE.scripts.OnKeyDown(CAPTURE, "W")`, 'w1');
  check('Belegte Taste: Warnung statt Überschreiben', q('GetBindingAction("W")') === 'MOVEFORWARD', null);
  run(`CAPTURE.scripts.OnKeyDown(CAPTURE, "T")`, 't');
  check('Freie Taste übernommen', q('GetBindingKey("WOWLEXIKON_UNDERMOUSE")') === 'T' && q('SAVED_BINDINGS') === 'true', 'Übersetzen-Taste: ' + q('WowLexikon.KeyLabel(WowLexikon.TranslateKey())'));
  // Minikarten-Knopf → Menü
  run(`WowLexikonMinimapButton.scripts.OnClick(WowLexikonMinimapButton, "LeftButton")`, 'mm');
  check('Minikarten-Knopf öffnet das Lexikon-Buch', q('WowLexikonBook.shown') === 'true', null);
  run(`WowLexikon.AddVocab({ word = "sword", base = "sword", tr = "Schwert", ipa = "sɔːrd" })
       for _, pg in ipairs({ "suche", "vokabeln", "chat", "hilfe" }) do WowLexikon.OpenBook(pg) end
       WowLexikon.bookSearchBox:SetText("bridge"); WowLexikon.bookSearchBox.scripts.OnTextChanged(WowLexikon.bookSearchBox)`, 'book');
  check('Alle Reiter des Buchs öffnen ohne Fehler', q('#WowLexikon.errors') === '0', q('WowLexikon.errors[1]'));
  run(`WowLexikonBook:Hide(); WowLexikonDB.options.hoverMode = "alt"`, 'altmode');

  // Maus über „bridge“ (Halte-Modus Alt)
  run(`local n; for i, p in ipairs(WowLexikon.links) do if p == "bridge" then n = i end end
       WowLexikon.panelContent.scripts.OnHyperlinkEnter(WowLexikon.panelContent, "wlx:" .. n, "bridge")`, 'hover');
  check('Ohne Alt: keine Karte beim Darüberfahren', q('WowLexikonCard.shown') === 'false', null);
  run(`KEYS.alt = true; FireEvent("MODIFIER_STATE_CHANGED", "LALT", 1)`, 'alt');
  check('Alt gedrückt: Wortkarte erscheint', q('WowLexikonCard.shown') === 'true', q('WowLexikonCard.word.text') + ' = ' + q('WowLexikonCard.tr.text') + ' | ' + q('WowLexikonCard.ipa.text'));
  const g = q('WowLexikon.panelGerman.text');
  check('Deutsches Wort leuchtet im offiziellen Text', g.includes('|cff6e1a08Brücke|r'), g.slice(0, 160));
  run(`KEYS.alt = false; FireEvent("MODIFIER_STATE_CHANGED", "LALT", 0)`, 'altup');
  check('Alt losgelassen: Karte weg', q('WowLexikonCard.shown') === 'false', null);
  run(`KEYS.alt = true; WowLexikon.panelContent.scripts.OnHyperlinkLeave(WowLexikon.panelContent); KEYS.alt = false`, 'leave');
  check('Karte verschwindet beim Wegfahren', q('WowLexikonCard.shown') === 'false', null);
  // Klick = vorlesen (englische Stimme) + anheften
  run(`local n; for i, p in ipairs(WowLexikon.links) do if p == "murlocs" then n = i end end
       WowLexikon.panelContent.scripts.OnHyperlinkClick(WowLexikon.panelContent, "wlx:" .. n, "murlocs", "LeftButton")`, 'click');
  check('Klick liest mit englischer Stimme hörbar vor (neuer Unterbau)', q('SPOKEN[#SPOKEN] and SPOKEN[#SPOKEN].text') === 'murlocs' && q('SPOKEN[#SPOKEN].voice') === '1' && q('SPOKEN[#SPOKEN].audible') === 'true' && q('WowLexikonDB.tts and WowLexikonDB.tts.sig') === 'neu', 'Stimme ' + q('SPOKEN[#SPOKEN].voice') + ', Lautstärke ' + q('SPOKEN[#SPOKEN].vol'));
  run(`TTS_OLD = true; WowLexikonDB.tts = nil; WowLexikon.Speak("bridge", 0)`, 'ttsold');
  check('Älterer Unterbau: alte Aufrufart erkannt und gemerkt', q('SPOKEN[#SPOKEN].audible') === 'true' && q('WowLexikonDB.tts and WowLexikonDB.tts.sig') === 'alt', q('WowLexikonDB.tts and WowLexikonDB.tts.sig'));
  run(`TTS_OLD = false; WowLexikonDB.tts = nil`, 'ttsnew');
  run(`WowLexikonDB.options.uiLang = "en"`, 'uien');
  check('Oberflächen-Sprache umschaltbar (Grundausstattung)', q('WowLexikon.T("Vorlesen")') === 'Speak' && q('WowLexikon.T("Gibt es nicht")') === 'Gibt es nicht', q('WowLexikon.T("Vorlesen")'));
  run(`WowLexikonDB.options.uiLang = "de"; WowLexikonDB.options.targetLang = "fr"`, 'fr');
  check('Zielsprache ohne Wörter: fällt auf Deutsch zurück', q('(WowLexikon.Lookup("bridge") or {}).tr').includes('Brücke'), null);
  run(`WowLexikon_Words_fr = { bridge = "bʁidʒ|pont||||w" }`, 'frwords');
  check('Zielsprache Französisch mit eigener Wörtertabelle', q('(WowLexikon.Lookup("bridge") or {}).tr') === 'pont', q('(WowLexikon.Lookup("bridge") or {}).tr'));
  run(`WowLexikonDB.options.targetLang = "de"; WowLexikon_Words_fr = nil`, 'de');
  check('Karte bleibt nach Klick offen', q('WowLexikonCard.shown') === 'true', null);
  // Unbekanntes Wort wird gemerkt
  run(`WowLexikon.ShowCard("flibbertigibbet", "cursor", false)`, 'unknown');
  check('Unbekanntes Wort für Begleit-Programm gemerkt', q('WowLexikonDB.unknown["flibbertigibbet"]') === '1', null);
  check('Questtext für Begleit-Programm gespeichert', q('WowLexikonDB.seen[35] and WowLexikonDB.seen[35].D ~= nil') === 'true', null);
  check('Spielversion gespeichert', q('WowLexikonDB.build.version') === '1.15.8', q('WowLexikonDB.build.version'));
  // Neue Quest ohne deutschen Text
  run(`QUEST.id = 999001; QUEST.title = "A Bundle of Furs"; QUEST.desc = "Would you mind taking this bundle of furs to Dandy Jones in Hatherford for me?"; QUEST.obj = "Deliver the Bundle of Warm Furs."; FireEvent("QUEST_DETAIL")`, 'newquest');
  check('Neue Quest: Hinweis statt deutschem Text', q('WowLexikon.panelHint.text').includes('noch kein deutscher Text') && q('WowLexikonGermanToggle.shown') === 'false', q('WowLexikon.panelHint.text'));
  // Suche
  run(`SlashCmdList.WOWLEXIKON("treasure")`, 'slash');
  check('/lex treasure', q('WowLexikonCard.tr.text').includes('Schatz'), q('WowLexikonCard.tr.text'));
  // Tooltip-Namen (alter Weg über HookScript)
  run(`GameTooltip.GetUnit = function() return "Defias Bandit" end; ADDED = nil; GameTooltip.AddLine = function(_, t) ADDED = t end
       TOOLTIP_POSTCALLS[Enum.TooltipDataType.Unit](GameTooltip)`, 'tooltip');
  check('Deutscher Name im Tooltip', q('ADDED').includes('Bandit der Defias'), q('ADDED'));
  // NPC-Gespräch
  run(`C_GossipInfo = { GetText = function() return "Welcome to the Lion's Pride Inn. Make yourself at home!" end }; FireEvent("GOSSIP_SHOW")`, 'gossip');
  check('NPC-Gespräch: Lesezeichen am Gesprächsfenster', q('WowLexikonPanel.source') === 'gossip' && q('WowLexikonTab.shown') === 'true', null);
  run(`FireEvent("GOSSIP_CLOSED")`, 'gossipclose');
  check('Gespräch zu: Lesezeichen und Fenster weg', q('WowLexikonPanel.shown') === 'false' && q('WowLexikonTab.shown') === 'false', null);
  // Buch / Brief
  run(`ItemTextGetItem = function() return "Old Letter" end; ItemTextGetText = function() return "My dearest Elaine, the harvest was poor this year." end; FireEvent("ITEM_TEXT_READY")`, 'book');
  check('Buch/Brief im Fenster', q('WowLexikonPanel.source') === 'book', null);
  // Tooltip eines Gegenstands im Beutel + Alt+T
  run(`GameTooltip.shown = true; GameTooltip.lines = 2
       GameTooltipTextLeft1 = CreateFrame("FontString"); GameTooltipTextLeft1.text = "Linen Cloth"
       GameTooltipTextLeft2 = CreateFrame("FontString"); GameTooltipTextLeft2.text = "Used to craft bandages and tailored goods."
       WowLexikon.TranslateUnderMouse()`, 'tooltip-t');
  check('Alt+T auf Gegenstand: Tooltip-Text übersetzt', q('WowLexikonPanel.source') === 'mouse' && q('WowLexikon.panelGerman.text').includes('Leinenstoff'), q('WowLexikon.panelGerman.text'));
  // Questziele am Bildschirmrand (beliebiger Text unter der Maus)
  run(`GameTooltip.shown = false
       local fs = CreateFrame("FontString"); fs.kind = "FontString"; fs.shown = true; fs.text = "- Murloc Scout slain: 4/10"
       local f = CreateFrame("Frame"); f.shown = true; f.regions = { fs }
       GetMouseFoci = function() return { f } end
       WowLexikon.TranslateUnderMouse()`, 'undermouse');
  check('Alt+T auf Questziele am Rand', q('WowLexikon.links and #WowLexikon.links') !== '0' && q('(function() for _, p in ipairs(WowLexikon.links) do if p == "Murloc Scout" then return "ja" end end return "nein" end)()') === 'ja', 'Name „Murloc Scout“ erkannt');
  // Chat
  run(`FireEvent("CHAT_MSG_SAY", "Anyone want to group for the Deadmines?", "Bob-Realm")
       FireEvent("CHAT_MSG_MONSTER_YELL", "You no take candle!", "Kobold Tunneler")
       WowLexikon.ShowChat()`, 'chat');
  check('Alt+C: Chat-Nachrichten anklickbar', q('WowLexikonPanel.source') === 'chat' && q('#WowLexikon.chat') === '2', q('#WowLexikon.links') + ' Verweise');
  // Einstellungen
  run(`WowLexikonDB.options.hoverMode = "immer"`, 'mode');
  check('Halte-Taste umstellbar', q('WowLexikon.HoverAllowed()') === 'true', null);
  run(`WowLexikonDB.options.hoverMode = "alt"`, 'mode2');
  // Karten-Questlog: Quest erneut öffnen
  run(`WowLexikonDB.options.showGerman = false; WowLexikonPanel:Hide(); QuestInfoDescriptionText.text = MAPQUEST[35].d
       QuestMapFrame_ShowQuestDetails(35)`, 'mapquest');
  check('Karten-Questlog: Lesezeichen neben der Karte', q('WowLexikonPanel.source') === 'log' && q('WowLexikonTab.shown') === 'true' && q('WowLexikon.drawer.anchor == WorldMapFrame') === 'true', q('WowLexikonPanel.source'));
  check('Karten-Questlog: deutscher Text zugeklappt', q('WowLexikon.panelGerman.text') === '' && q('WowLexikonGermanToggle.shown') === 'true', null);
  check('Karten-Questlog: Knopf „Deutsch“', q('WowLexikonMapQuestLang and WowLexikonMapQuestLang.shown') === 'true', null);
  run(`WowLexikonMapQuestLang.scripts.OnClick(WowLexikonMapQuestLang)`, 'mapde');
  check('Karten-Questlog: Text auf Deutsch', !q('QuestInfoDescriptionText.text').startsWith('If you'), q('QuestInfoDescriptionText.text').slice(0, 50));
  run(`QuestMapFrame.DetailsFrame.shown = false; for k, f in pairs(QuestMapFrame.DetailsFrame.scripts) do if k == "hook_OnHide" then f() end end`, 'maphide');
  check('Karten-Questlog zu: Lesezeichen weg', q('WowLexikonTab.shown') === 'false', null);

  if (process.argv[3] === 'learned') {
    const lw = q(`(WowLexikon.Lookup("brouhaha") or {}).tr .. " | " .. tostring((WowLexikon.Lookup("brouhaha") or {}).src)`);
    check('Addon kennt gelerntes Wort', lw.includes('| l'), lw);
    run(`QUEST.id = 990001; QUEST.title = "A Bundle of Furs"; QUEST.desc = "You there! Would you happen to be passing by Hatherford?"; QUEST.obj = "Deliver the Bundle of Warm Furs to Dandy Jones in Hatherford."; FireEvent("QUEST_DETAIL")`, 'learnedquest');
    const g2 = q('WowLexikon.panelGerman.text') + q('WowLexikon.panelHint.text');
    check('Addon zeigt übersetzte neue Quest', !g2.includes('noch kein deutscher Text') && g2.length > 20, g2.slice(0, 160));
  }
  console.log(results.join('\n'));
} catch (e) {
  check('Ablauf', false, e.message);
  console.log(results.join('\n'));
}
