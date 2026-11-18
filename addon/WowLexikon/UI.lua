-- WoW Lexikon – Oberfläche (abgesichert geladen: ein Fehler hier stoppt nicht das ganze Addon)
WowLexikon.Try("Oberfläche", function()
-- WoW Lexikon – Oberfläche: Pergament-Fenster am Questfenster, Wortkarte, Suche, deutsche Namen in Tooltips
local WL = WowLexikon

local BACKDROP_TEMPLATE = BackdropTemplateMixin and "BackdropTemplate" or nil
local FRIZ = "Fonts\\FRIZQT__.TTF"
local INK = { 0.16, 0.10, 0.04 }        -- Tinte auf Pergament
local INK_SOFT = { 0.30, 0.21, 0.10 }
local RED = "ff6e1a08"                  -- Hervorhebung (gedämpftes Dunkelrot)
local NAME = "ff4a1a6a"                 -- Namen aus dem Spiel (dunkles Violett, gut unterscheidbar)

local function parchment(f)
  if f.SetBackdrop then
    f:SetBackdrop({ bgFile = "Interface\\Buttons\\WHITE8x8", edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
                    tile = false, edgeSize = 24, insets = { left = 6, right = 6, top = 6, bottom = 6 } })
    f:SetBackdropColor(0.90, 0.80, 0.60, 0.98)
  end
  -- dunklere Ränder wie beim Pergament im Questfenster
  local shade = f:CreateTexture(nil, "BORDER")
  shade:SetPoint("TOPLEFT", 6, -6); shade:SetPoint("BOTTOMRIGHT", -6, 6)
  shade:SetColorTexture(0.45, 0.30, 0.10, 0.10)
end

local function font(parent, size, obj)
  local fs = parent:CreateFontString(nil, "OVERLAY", obj or "QuestFont")
  if size then fs:SetFont(FRIZ, size, "") end
  fs:SetJustifyH("LEFT"); fs:SetJustifyV("TOP")
  fs:SetTextColor(unpack(INK))
  if fs.SetWordWrap then fs:SetWordWrap(true) end
  fs:SetShadowColor(0, 0, 0, 0)
  return fs
end

local function titleBar(f, text)
  local bar = f:CreateTexture(nil, "ARTWORK")
  bar:SetPoint("TOPLEFT", 8, -8); bar:SetPoint("TOPRIGHT", -8, -8); bar:SetHeight(22)
  bar:SetColorTexture(0.08, 0.08, 0.08, 0.92)
  local t = f:CreateFontString(nil, "OVERLAY", "GameFontNormal")
  t:SetPoint("CENTER", bar, "CENTER", 0, 0); t:SetText(text)
  t:SetTextColor(0.92, 0.88, 0.78)
  local close = CreateFrame("Button", nil, f, "UIPanelCloseButton")
  close:SetPoint("TOPRIGHT", 0, 0)
  return bar, t, close
end

---------------------------------------------------------------------------
-- Wortkarte
---------------------------------------------------------------------------
local card = CreateFrame("Frame", "WowLexikonCard", UIParent, BACKDROP_TEMPLATE)
card:SetSize(300, 160); card:SetFrameStrata("TOOLTIP"); card:SetClampedToScreen(true); card:Hide()
card:EnableMouse(true); card:SetMovable(true); card:RegisterForDrag("LeftButton")
card:SetScript("OnDragStart", card.StartMoving); card:SetScript("OnDragStop", card.StopMovingOrSizing)
parchment(card)

card.word = card:CreateFontString(nil, "OVERLAY", "QuestTitleFont")
card.word:SetPoint("TOPLEFT", 16, -14); card.word:SetPoint("RIGHT", -34, 0); card.word:SetJustifyH("LEFT")
card.word:SetTextColor(unpack(INK)); card.word:SetShadowColor(0, 0, 0, 0)
card.close = CreateFrame("Button", nil, card, "UIPanelCloseButton"); card.close:SetPoint("TOPRIGHT", -2, -2)
card.close:SetScript("OnClick", function() card.pinned = false; card:Hide() end)

card.ipa = card:CreateFontString(nil, "OVERLAY")
if not pcall(card.ipa.SetFont, card.ipa, WL.IPA_FONT, 14, "") then card.ipa:SetFontObject("GameFontHighlight") end; card.ipa:SetJustifyH("LEFT"); card.ipa:SetTextColor(unpack(INK_SOFT))
card.ipa:SetPoint("TOPLEFT", card.word, "BOTTOMLEFT", 0, -4); card.ipa:SetPoint("RIGHT", -16, 0)

card.tr = font(card, 15); card.tr:SetTextColor(0.43, 0.10, 0.03)
card.tr:SetPoint("TOPLEFT", card.ipa, "BOTTOMLEFT", 0, -8); card.tr:SetPoint("RIGHT", -16, 0)
card.ctx = font(card, 10); card.ctx:SetTextColor(0.45, 0.36, 0.22)
card.ctx:SetPoint("TOPLEFT", card.tr, "BOTTOMLEFT", 0, -2); card.ctx:SetPoint("RIGHT", -16, 0)
card.note = font(card, 11.5); card.note:SetTextColor(unpack(INK_SOFT))
card.note:SetPoint("TOPLEFT", card.ctx, "BOTTOMLEFT", 0, -6); card.note:SetPoint("RIGHT", -16, 0)
card.all = font(card, 11.5); card.all:SetTextColor(unpack(INK))
card.all:SetPoint("TOPLEFT", card.note, "BOTTOMLEFT", 0, -6); card.all:SetPoint("RIGHT", -16, 0)
card.ex = font(card, 12)
card.ex:SetPoint("TOPLEFT", card.all, "BOTTOMLEFT", 0, -8); card.ex:SetPoint("RIGHT", -16, 0)
card.src = font(card, 9.5); card.src:SetTextColor(0.45, 0.36, 0.22)
card.src:SetPoint("TOPLEFT", card.ex, "BOTTOMLEFT", 0, -8); card.src:SetPoint("RIGHT", -16, 0)
card:SetWidth(320)

local function button(parent, text, w)
  local b = CreateFrame("Button", nil, parent, "UIPanelButtonTemplate")
  b:SetSize(w or 80, 22); b:SetText(text)
  return b
end
card.bSpeak = button(card, "Vorlesen", 76); card.bSlow = button(card, "Langsam", 70); card.bSave = button(card, "Merken", 66); card.bMore = button(card, "Mehr", 62)
card.bSpeak:SetPoint("BOTTOMLEFT", 14, 12); card.bSlow:SetPoint("LEFT", card.bSpeak, "RIGHT", 4, 0)
card.bSave:SetPoint("LEFT", card.bSlow, "RIGHT", 4, 0); card.bMore:SetPoint("LEFT", card.bSave, "RIGHT", 4, 0)
card.bSpeak:SetScript("OnClick", function() WL.Speak(card.entry and card.entry.word, 0) end)
card.bSlow:SetScript("OnClick", function() WL.Speak(card.entry and card.entry.word, -4) end)
card.bSave:SetScript("OnClick", function() if card.entry and card.entry.tr then WL.AddVocab(card.entry); card.bSave:SetText("Gemerkt") end end)
card.bMore:SetScript("OnClick", function()
  WowLexikonDB.options.cardMore = not WowLexikonDB.options.cardMore
  if card.args then WL.ShowCard(card.args[1], card.args[2], true, card.args[4], true) end
end)

local function setText(fs, text)
  fs:SetText(text or "")
  if text and text ~= "" then fs:Show() else fs:Hide() end
end

--- Bedeutungen eines Eintrags („Haut (eines Tieres); verstecken“ → { "Haut", "verstecken" })
function WL.Senses(tr)
  local t = {}
  for s in (tr or ""):gmatch("[^;,/]+") do
    s = s:gsub("%b()", ""):gsub("^%s+", ""):gsub("%s+$", "")
    if s ~= "" then t[#t + 1] = s end
  end
  return t
end

--- Zeigt die Karte für ein Wort oder einen Namen.
--- anchor: "cursor" | Frame | nil (Mitte) · sense: die im Satz gemeinte Bedeutung · keep: Position behalten
function WL.ShowCard(phrase, anchor, pinned, sense, keep)
  local e = WL.LookupName(phrase)
  local word = WL.Lookup(phrase)
  if e and word and not phrase:find(" ") then
    -- Einzelwort ist auch ein Name (z. B. „Wolf“): Wort zeigen, Name als Hinweis
    word.note = (word.note and word.note .. "  ·  " or "") .. "Im Spiel: " .. e.tr .. " (" .. e.note .. ")"
    e = word
  elseif not e then
    e = word
  end
  card.entry = e or { word = phrase }
  card.args = { phrase, anchor, pinned, sense }
  card.pinned = pinned and true or false
  card.word:SetText(phrase)
  local more = WowLexikonDB and WowLexikonDB.options.cardMore
  if e and e.ipa then
    local de = WL.GermanReading(e.ipa)
    card.ipa:SetText("/" .. e.ipa .. "/" .. (de and ("   ·   gesprochen etwa: " .. de) or ""))
    card.ipa:Show()
  else card.ipa:SetText(""); card.ipa:Hide() end
  local extra = false
  if e and e.tr then
    -- Knapp: nur die im Satz gemeinte Bedeutung (oder die ersten beiden). Alles andere unter „Mehr“.
    local list = WL.Senses(e.tr)
    local main
    if sense then main = sense
    elseif #list > 2 and not more then main = list[1] .. "; " .. list[2] .. " …"
    else main = e.tr end
    setText(card.tr, main)
    setText(card.ctx, sense and "so ist es hier im Satz gemeint" or nil)
    local others = {}
    for _, s in ipairs(list) do if s ~= sense then others[#others + 1] = s end end
    local hasAll = #list > 1 and (sense or #list > 2)
    setText(card.all, (more and hasAll) and ((sense and "Weitere Bedeutungen: " or "Alle Bedeutungen: ") .. table.concat(sense and others or list, "; ")) or nil)
    local shortNote = e.src == "g" or e.isName
    setText(card.note, (more or shortNote) and e.note or nil)
    setText(card.ex, more and e.ex and ("|cff3a2810" .. e.ex .. "|r" .. (e.exTr and ("\n|cff5c4422" .. e.exTr .. "|r") or "")) or nil)
    local src = ({ g = "WoW-Glossar", q = "Questwörterbuch", w = "Wörterbuch (Ding, TU Chemnitz)", l = "Vom Begleit-Programm gelernt", n = "Name aus dem Spiel" })[e.src or ""]
    setText(card.src, (more and src) and ("Quelle: " .. src) or nil)
    extra = hasAll or e.ex or (e.note and not shortNote) or src
    card.bSave:SetText("Merken"); card.bSave:Enable()
  else
    setText(card.tr, "Noch nicht im Wörterbuch")
    setText(card.ctx, nil); setText(card.all, nil)
    setText(card.note, WL.HasCompanion() and "Das Begleit-Programm schlägt es nach dem Ausloggen nach – beim nächsten Einloggen ist es da."
                                       or "Mit dem kostenlosen Begleit-Programm „WoW Lexikon“ werden unbekannte Wörter automatisch nachgeschlagen.")
    setText(card.ex, nil); setText(card.src, nil)
    card.bSave:Disable()
    WL.RecordUnknown(phrase)
  end
  card.bMore:SetText(more and "Weniger" or "Mehr")
  if extra then card.bMore:Show() else card.bMore:Hide() end
  -- Höhe aus dem Inhalt berechnen
  local h = 14 + card.word:GetStringHeight() + 4
  for _, fs in ipairs({ card.ipa, card.tr, card.ctx, card.note, card.all, card.ex, card.src }) do
    if fs:IsShown() then h = h + fs:GetStringHeight() + 7 end
  end
  card:SetHeight(h + 46)
  if not (keep and card:IsShown()) then
    card:ClearAllPoints()
    if anchor == "cursor" then
      local x, y = GetCursorPosition(); local s = UIParent:GetEffectiveScale()
      card:SetPoint("TOPLEFT", UIParent, "BOTTOMLEFT", x / s + 18, y / s - 12)
    elseif anchor then
      card:SetPoint("TOPLEFT", anchor, "BOTTOMLEFT", 0, -4)
    else
      card:SetPoint("CENTER", UIParent, "CENTER", 0, 120)
    end
  end
  card:Show()
  return e
end

function WL.HideCard() if not card.pinned then card:Hide() end end

---------------------------------------------------------------------------
-- Kleines Fenster beim Darüberfahren: nur die direkte Übersetzung (wie ein Spiel-Tooltip)
---------------------------------------------------------------------------
local tip = CreateFrame("Frame", "WowLexikonTip", UIParent, BACKDROP_TEMPLATE)
tip:SetFrameStrata("TOOLTIP"); tip:SetClampedToScreen(true); tip:Hide()
if tip.SetBackdrop then
  tip:SetBackdrop({ bgFile = "Interface\\Tooltips\\UI-Tooltip-Background", edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border",
                    tile = true, tileSize = 16, edgeSize = 14, insets = { left = 4, right = 4, top = 4, bottom = 4 } })
  tip:SetBackdropColor(0.03, 0.03, 0.06, 0.94)
  tip:SetBackdropBorderColor(0.6, 0.6, 0.65, 1)
end
tip.text = tip:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
tip.text:SetPoint("TOPLEFT", 9, -7); tip.text:SetJustifyH("LEFT")
tip.sub = tip:CreateFontString(nil, "OVERLAY", "GameFontDisableSmall")
tip.sub:SetPoint("TOPLEFT", tip.text, "BOTTOMLEFT", 0, -2); tip.sub:SetJustifyH("LEFT")

--- Kurzform: die im Satz gemeinte Bedeutung, sonst höchstens zwei
local function shortMeaning(e, sense)
  if not (e and e.tr) then return nil end
  if sense then return sense end
  local list = WL.Senses(e.tr)
  if #list == 0 then return e.tr end
  if #list == 1 then return list[1] end
  return list[1] .. ", " .. list[2]
end

--- Zeigt das kleine Fenster am Mauszeiger. Gibt false zurück, wenn es keine Übersetzung gibt.
function WL.ShowTip(phrase, sense)
  local e = WL.Lookup(phrase)
  local name = WL.LookupName(phrase)
  if name and (phrase:find(" ") or not e) then e = name end
  local text = shortMeaning(e, sense)
  if not text then tip:Hide(); return false end
  tip.text:SetText(text)
  tip.sub:SetText(e.isName and (e.note or "") or "")
  if e.isName and e.note ~= "" then tip.sub:Show() else tip.sub:Hide() end
  local w = math.max(tip.text:GetStringWidth() or 0, tip.sub:IsShown() and (tip.sub:GetStringWidth() or 0) or 0)
  tip:SetSize(math.min(260, w + 20), 14 + (tip.text:GetStringHeight() or 12) + (tip.sub:IsShown() and ((tip.sub:GetStringHeight() or 10) + 2) or 0))
  local x, y = GetCursorPosition(); local s = UIParent:GetEffectiveScale()
  tip:ClearAllPoints(); tip:SetPoint("BOTTOMLEFT", UIParent, "BOTTOMLEFT", x / s + 6, y / s + 10)
  tip:Show()
  return true
end
function WL.HideTip() tip:Hide() end

---------------------------------------------------------------------------
-- Text in anklickbare Wörter verwandeln (Namen aus dem Spiel als ganze Ausdrücke)
---------------------------------------------------------------------------
WL.links = {}

local function linkify(text)
  wipe(WL.links)
  text = (text or ""):gsub("|c%x%x%x%x%x%x%x%x", ""):gsub("|r", ""):gsub("|", "||")
  local toks, pos = {}, 1
  while pos <= #text do
    local s, e = text:find("%a[%a']*", pos)
    if not s then toks[#toks + 1] = { s = text:sub(pos) }; break end
    if s > pos then toks[#toks + 1] = { s = text:sub(pos, s - 1) } end
    toks[#toks + 1] = { w = text:sub(s, e) }
    pos = e + 1
  end
  local out, i = {}, 1
  local function link(phrase, color)
    WL.links[#WL.links + 1] = phrase
    return "|c" .. color .. "|Hwlx:" .. #WL.links .. "|h" .. phrase .. "|h|r"
  end
  while i <= #toks do
    local t = toks[i]
    if t.w then
      -- längsten Namen ab hier suchen (bis 4 Wörter, nur durch Leerzeichen getrennt)
      local best, bestJ
      local phrase, j = t.w, i
      for _ = 2, 4 do
        if toks[j + 1] and toks[j + 1].s == " " and toks[j + 2] and toks[j + 2].w then
          phrase = phrase .. " " .. toks[j + 2].w; j = j + 2
          if WowLexikon_Names[phrase:lower()] then best, bestJ = phrase, j end
        else break end
      end
      if best then
        out[#out + 1] = link(best, NAME); i = bestJ + 1
      else
        local known = WL.Lookup(t.w)
        local isName = not known and WowLexikon_Names[t.w:lower()]
        out[#out + 1] = link(t.w, isName and NAME or (known and "ff2b1c0a" or "ff7a6038"))
        i = i + 1
      end
    else
      out[#out + 1] = t.s; i = i + 1
    end
  end
  return table.concat(out)
end
WL.Linkify = linkify

-- Im deutschen Text das Wort hervorheben, das zur Übersetzung passt
local function utf8chars(s) local t = {}; for c in s:gmatch("[%z\1-\127\194-\244][\128-\191]*") do t[#t + 1] = c end; return t end
-- Kommt eine Bedeutung (Wortstamm, z. B. „Haut“ → „Häute“ nicht, aber „Haut“, „Fell“ → „Felle“) im deutschen Text vor?
local function findSense(german, sense)
  local lowG = german:lower()
  for w in sense:gmatch("[^%s]+") do
    local chars = utf8chars(w)
    if #chars >= 3 then
      local n = #chars <= 4 and #chars or math.max(4, #chars - 2)
      local stem = table.concat(chars, "", 1, n):lower()
      local s = lowG:find(stem, 1, true)
      -- nur am Wortanfang
      while s and s > 1 and german:sub(s - 1, s - 1):match("[%a\128-\255]") do s = lowG:find(stem, s + 1, true) end
      if s then
        local e = s + #stem - 1
        while e < #german and german:sub(e + 1, e + 1):match("[%a\128-\255]") do e = e + 1 end
        return s, e
      end
    end
  end
end

--- Welche der Bedeutungen ist im deutschen Text gemeint? (nur wenn es mehrere gibt)
function WL.MatchSense(german, tr)
  if not german or not tr then return nil end
  local list = WL.Senses(tr)
  if #list < 2 then return nil end
  for _, s in ipairs(list) do if findSense(german, s) then return s end end
end

local function highlight(german, tr)
  if not german or not tr then return german end
  for _, sense in ipairs(WL.Senses(tr)) do
    local s, e = findSense(german, sense)
    if s then return german:sub(1, s - 1) .. "|c" .. RED .. german:sub(s, e) .. "|r" .. german:sub(e + 1) end
  end
  return german
end

---------------------------------------------------------------------------
-- Wann erscheint die Wortkarte beim Darüberfahren? Standard: nur solange Alt gehalten wird.
-- (Klicken zeigt sie immer.)
---------------------------------------------------------------------------
local function hoverAllowed()
  local m = WowLexikonDB and WowLexikonDB.options.hoverMode or "taste"
  if m == "taste" or m == "klick" then return false end
  if m == "immer" then return true end
  if m == "alt" then return IsAltKeyDown() end
  if m == "strg" then return IsControlKeyDown() end
  if m == "umschalt" then return IsShiftKeyDown() end
  return false   -- „klick“
end
WL.HoverAllowed = hoverAllowed

local function modeHint()
  local m = WowLexikon and WowLexikonDB and WowLexikonDB.options.hoverMode or "taste"
  if m == "taste" then
    local k = WL.TranslateKey and WL.TranslateKey()
    return k and ("Maus auf ein Wort + " .. WL.KeyLabel(k) .. " = Übersetzung") or "Wort anklicken = Übersetzung"
  end
  if m == "immer" then return "Über ein Wort fahren = Übersetzung" end
  if m == "klick" then return "Wort anklicken = Übersetzung" end
  return ({ alt = "Alt", strg = "Strg", umschalt = "Umschalt" })[m] .. " halten + über ein Wort fahren = Übersetzung"
end

---------------------------------------------------------------------------
-- Pergament-Fenster (für Quests, NPC-Gespräche, Bücher, Tooltips, Chat …)
---------------------------------------------------------------------------
-- Gleiche Fenster-Art wie das Questfenster des Spiels (Porträt, Titelleiste, roter Schließen-Knopf)
local panel = WL.MakeWindow("WowLexikonPanel", 350, 470, "Wörterbuch", "Interface\\Icons\\INV_Misc_Book_09")

local scroll = CreateFrame("ScrollFrame", "WowLexikonPanelScroll", panel.body, "UIPanelScrollFrameTemplate")
scroll:SetPoint("TOPLEFT", 0, 0); scroll:SetPoint("BOTTOMRIGHT", -22, 0)
local content = CreateFrame("Frame", nil, scroll)
content:SetSize(286, 400); scroll:SetScrollChild(content)
WL.panelContent = content
if content.SetHyperlinksEnabled then content:SetHyperlinksEnabled(true) end

local title = content:CreateFontString(nil, "OVERLAY", "QuestTitleFont")
title:SetPoint("TOPLEFT", 0, -2); title:SetWidth(286); title:SetJustifyH("LEFT"); title:SetTextColor(unpack(INK)); title:SetShadowColor(0, 0, 0, 0)
local en = font(content); en:SetPoint("TOPLEFT", title, "BOTTOMLEFT", 0, -8); en:SetWidth(286)
-- Deutscher Text: zugeklappt, ein Klick auf die Zeile klappt ihn auf (Zustand wird gemerkt)
local deToggle = CreateFrame("Button", "WowLexikonGermanToggle", content)
deToggle:SetPoint("TOPLEFT", en, "BOTTOMLEFT", 0, -14); deToggle:SetSize(286, 24)
local deLine = deToggle:CreateTexture(nil, "ARTWORK"); deLine:SetPoint("TOPLEFT", 0, 0); deLine:SetPoint("TOPRIGHT", 0, 0); deLine:SetHeight(1)
deLine:SetColorTexture(0.43, 0.27, 0.08, 0.35)
local deHead = deToggle:CreateFontString(nil, "OVERLAY", "QuestTitleFont")
deHead:SetPoint("LEFT", 0, -3); deHead:SetPoint("RIGHT", -84, -3); deHead:SetJustifyH("LEFT"); deHead:SetTextColor(unpack(INK)); deHead:SetShadowColor(0, 0, 0, 0)
local deArrow = font(deToggle, 11); deArrow:SetPoint("RIGHT", 0, -3); deArrow:SetJustifyH("RIGHT"); deArrow:SetTextColor(0.43, 0.10, 0.03)
local deGlow = deToggle:CreateTexture(nil, "HIGHLIGHT"); deGlow:SetAllPoints(); deGlow:SetColorTexture(0.55, 0.30, 0.05, 0.10)
local de = font(content); de:SetPoint("TOPLEFT", deToggle, "BOTTOMLEFT", 0, -4); de:SetWidth(286); de:SetTextColor(unpack(INK_SOFT))
WL.panelGerman = de
WL.panelGermanToggle = deToggle
local hint = font(content, 10); hint:SetPoint("TOPLEFT", de, "BOTTOMLEFT", 0, -12); hint:SetWidth(286); hint:SetTextColor(0.45, 0.36, 0.22)
WL.panelHint = hint

local current = { germanPlain = nil, hovered = nil, last = nil, hint = nil, always = false }

--- Ist der deutsche Text gerade sichtbar?
local function germanOpen()
  return current.germanPlain ~= nil and (current.always or (WowLexikonDB and WowLexikonDB.options.showGerman))
end
local function setGerman(text) if germanOpen() then de:SetText(text or "") end end

local layout
local function renderGerman()
  if not current.germanPlain then
    deToggle:Hide(); de:SetText(""); hint:SetText(current.hint or "")
  else
    local open = germanOpen()
    deToggle:Show()
    deHead:SetText(current.deTitle or WL.T("Deutscher Text"))
    deArrow:SetText(current.always and "" or (open and WL.T("‹ ausblenden") or WL.T("anzeigen ›")))
    if deToggle.SetEnabled then deToggle:SetEnabled(not current.always) end
    de:SetText(open and current.germanPlain or "")
    hint:SetText(open and (current.hint or "") or "")
  end
  if layout then layout() end
end
deToggle:SetScript("OnClick", function()
  if current.always then return end
  WowLexikonDB.options.showGerman = not WowLexikonDB.options.showGerman
  renderGerman()
end)

function layout()
  local h = title:GetStringHeight() + en:GetStringHeight() + (deToggle:IsShown() and 28 or 0) + de:GetStringHeight() + hint:GetStringHeight() + 60
  content:SetHeight(h)
  -- Hoch/Runter-Pfeile nur, wenn der Text wirklich nicht passt
  local sb = scroll.ScrollBar or _G["WowLexikonPanelScrollScrollBar"]
  if type(sb) == "table" and sb.SetShown then sb:SetShown(h > (scroll:GetHeight() or 0) + 4) end
end

local drawer = { anchor = nil, open = false }
WL.drawer = drawer
local tab   -- Lesezeichen (weiter unten)

local function modernFrame(a)
  return a == WorldMapFrame or type(a.NineSlice) == "table" or type(a.Inset) == "table" or type(a.BorderFrame) == "table"
end
local function anchorX(a) return modernFrame(a) and 4 or -32 end
local function anchorY(a) return modernFrame(a) and 0 or -18 end

--- Fenster füllen. o = { title, en, deTitle, de, hint, anchor, x, y, source, auto, bottom }
function WL.ShowPanel(o)
  if o.auto and WowLexikonDB and not WowLexikonDB.options.autoPanel then return end
  current.last = o
  title:SetText(o.title or "")
  en:SetText(linkify(o.en))
  current.germanPlain = o.de
  current.deTitle = o.deTitle
  current.hint = o.hint
  current.always = o.deAlways and true or false
  renderGerman()
  panel.source = o.source
  scroll:SetVerticalScroll(0)
  if o.auto and o.anchor and o.anchor.IsShown and o.anchor:IsShown() then
    -- Neben Quest, Gespräch, Buch: zuerst nur das Lesezeichen – das Fenster kommt auf Klick (oder Übersetzen-Taste)
    if drawer.anchor ~= o.anchor then drawer.open = false end
    drawer.anchor = o.anchor
    if drawer.open then
      panel:ClearAllPoints(); panel:SetPoint("TOPLEFT", o.anchor, "TOPRIGHT", anchorX(o.anchor), anchorY(o.anchor)); panel:SetAlpha(1); panel:Show()
    else panel:Hide() end
    if tab then tab.update() end
  else
    drawer.anchor = nil
    if tab then tab:Hide() end
    panel:ClearAllPoints(); panel:SetPoint("CENTER", UIParent, "CENTER", 300, 60); panel:SetAlpha(1)
    panel:Show()
  end
  if o.bottom and C_Timer then C_Timer.After(0, function() scroll:SetVerticalScroll(scroll:GetVerticalScrollRange()) end) end
end

---------------------------------------------------------------------------
-- Ausklappen mit kurzer Animation + Lesezeichen am Rand des Spielfensters
---------------------------------------------------------------------------
local function slide(opening)
  local a = drawer.anchor
  if not a then return end
  local x0, y0 = anchorX(a), anchorY(a)
  local startX = x0 - 140
  local t = 0
  panel:Show()
  panel:SetScript("OnUpdate", function(self, elapsed)
    t = t + (elapsed or 0.02)
    local p = math.min(1, t / 0.2)
    local ease = 1 - (1 - p) ^ 3
    local k = opening and ease or (1 - ease)
    self:ClearAllPoints(); self:SetPoint("TOPLEFT", a, "TOPRIGHT", startX + (x0 - startX) * k, y0)
    self:SetAlpha(k)
    if p >= 1 then
      self:SetScript("OnUpdate", nil)
      if not opening then self:Hide() end
      if tab then tab.update() end
    end
  end)
end

function WL.OpenDrawer() if drawer.anchor and not drawer.open then drawer.open = true; slide(true); if tab then tab.update() end end end
function WL.CloseDrawer(animate)
  if drawer.open and animate and drawer.anchor and drawer.anchor:IsShown() then drawer.open = false; slide(false)
  else drawer.open = false; panel:SetScript("OnUpdate", nil); panel:Hide() end
  if tab then tab.update() end
end
local function toggleDrawer() if drawer.open then WL.CloseDrawer(true) else WL.OpenDrawer() end end

--- Wenn das Spielfenster schließt: alles einklappen und das Lesezeichen weg
function WL.DetachDrawer(src)
  if panel.source == src or (src == "quest" and panel.source == "greeting") or src == nil then
    drawer.open = false; drawer.anchor = nil
    panel:SetScript("OnUpdate", nil); panel:Hide()
    if tab then tab:Hide() end
  end
end

tab = CreateFrame("Button", "WowLexikonTab", UIParent, BACKDROP_TEMPLATE)
tab:SetSize(30, 92); tab:SetFrameStrata("HIGH"); tab:Hide()
if tab.SetBackdrop then
  tab:SetBackdrop({ bgFile = "Interface\\Buttons\\WHITE8x8", edgeFile = "Interface\\Tooltips\\UI-Tooltip-Border", edgeSize = 12,
                    insets = { left = 3, right = 3, top = 3, bottom = 3 } })
  tab:SetBackdropColor(0.40, 0.07, 0.03, 0.97)
  tab:SetBackdropBorderColor(0.86, 0.69, 0.32, 1)
end
local tIcon = tab:CreateTexture(nil, "ARTWORK"); tIcon:SetSize(20, 20); tIcon:SetPoint("TOP", 0, -7)
tIcon:SetTexture("Interface\\Icons\\INV_Misc_Book_09"); tIcon:SetTexCoord(0.08, 0.92, 0.08, 0.92)
local tLabel = tab:CreateFontString(nil, "OVERLAY", "GameFontNormal"); tLabel:SetPoint("TOP", tIcon, "BOTTOM", 0, -6); tLabel:SetText("D\nE")
local tArrow = tab:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge"); tArrow:SetPoint("BOTTOM", 0, 7)
local tGlow = tab:CreateTexture(nil, "HIGHLIGHT"); tGlow:SetPoint("TOPLEFT", 3, -3); tGlow:SetPoint("BOTTOMRIGHT", -3, 3)
tGlow:SetColorTexture(1, 0.85, 0.4, 0.18)
tab:SetScript("OnClick", toggleDrawer)
tab:SetScript("OnEnter", function(self)
  GameTooltip:SetOwner(self, "ANCHOR_RIGHT")
  GameTooltip:AddLine(drawer.open and "Wörterbuch einklappen" or "Wörterbuch ausklappen")
  GameTooltip:AddLine("Englischer Text zum Anklicken, deutscher Text darunter", 1, 1, 1, true)
  GameTooltip:Show()
end)
tab:SetScript("OnLeave", function() GameTooltip:Hide() end)
tab.update = function()
  local a = drawer.anchor
  if not (a and a:IsShown()) then tab:Hide(); return end
  tab:ClearAllPoints()
  if drawer.open and panel:IsShown() then tab:SetPoint("TOPLEFT", panel, "TOPRIGHT", -3, -64); tArrow:SetText("«")
  else tab:SetPoint("TOPLEFT", a, "TOPRIGHT", anchorX(a) - 6, -64); tArrow:SetText("»") end
  tab:Show()
end
WL.tab = tab

function WL.TogglePanel()
  if drawer.anchor and drawer.anchor:IsShown() then toggleDrawer(); return end
  if panel:IsShown() then panel:Hide()
  elseif current.last then local o = current.last; o.auto = false; WL.ShowPanel(o)
  else WL.ShowChat() end
end

--- Questtext (deutsch: offiziell, vom Begleit-Programm übersetzt oder noch unbekannt)
function WL.ShowQuest(id, enTitle, enText, fields, anchorFrame)
  local q, official, oldOfficial = WL.QuestText(id)
  local parts = {}
  if q then for _, k in ipairs(fields) do if q[k] and q[k] ~= "" then parts[#parts + 1] = q[k] end end end
  local o = { title = enTitle, en = enText, anchor = anchorFrame, source = "quest", auto = true,
              x = anchorFrame == QuestFrame and -32 or -2, y = anchorFrame == QuestFrame and -18 or -12 }
  if #parts > 0 then
    o.deTitle = (q.T and q.T ~= "" and q.T) or (oldOfficial and oldOfficial.T) or "Deutsch"
    o.de = table.concat(parts, "\n\n")
    if oldOfficial then
      local old = {}
      for _, k in ipairs(fields) do if oldOfficial[k] and oldOfficial[k] ~= "" then old[#old + 1] = oldOfficial[k] end end
      o.hint = "In WoW Forever geändert – oben die neue Übersetzung vom Begleit-Programm." ..
        (#old > 0 and ("\n\nFrüherer offizieller Text:\n" .. table.concat(old, "\n\n")) or "")
    else
      o.hint = (not official) and "Übersetzt vom Begleit-Programm – noch kein offizieller deutscher Text." or nil
    end
  elseif WL.HasCompanion() then
    o.hint = "Für diese Quest ist noch kein deutscher Text bekannt – das Begleit-Programm übersetzt sie nach dem Ausloggen."
  else
    o.hint = WL.COMPANION_HINT .. " Einzelne Wörter funktionieren auch ohne."
  end
  WL.ShowPanel(o)
end

local function showFor(phrase, pinned)
  local probe = WL.Lookup(phrase) or WL.LookupName(phrase)
  local sense = current.germanPlain and probe and WL.MatchSense(current.germanPlain, probe.tr)
  tip:Hide()
  local e = WL.ShowCard(phrase, "cursor", pinned, sense)
  if current.germanPlain and e and e.tr then setGerman(highlight(current.germanPlain, sense or e.tr)) end
end
local function linkPhrase(link)
  local n = tonumber(link:match("^wlx:(%d+)"))
  return n and WL.links[n]
end

-- Darüberfahren: kleines Fenster mit der Übersetzung · Klick (oder Übersetzen-Taste): Wortkarte mit Aussprache
local function showTipFor(phrase)
  if not (WowLexikonDB and WowLexikonDB.options.hoverTip ~= false) then return end
  if card:IsShown() and card.pinned and card.args and card.args[1] == phrase then return end
  local probe = WL.Lookup(phrase) or WL.LookupName(phrase)
  local sense = current.germanPlain and probe and WL.MatchSense(current.germanPlain, probe.tr)
  if WL.ShowTip(phrase, sense) and probe and probe.tr and current.germanPlain then
    setGerman(highlight(current.germanPlain, sense or probe.tr))
  end
end

content:SetScript("OnHyperlinkEnter", function(_, link)
  current.hovered = linkPhrase(link)
  if not current.hovered then return end
  if hoverAllowed() then showFor(current.hovered, false) else showTipFor(current.hovered) end
end)
content:SetScript("OnHyperlinkLeave", function()
  current.hovered = nil
  tip:Hide()
  WL.HideCard()
  if current.germanPlain then setGerman(current.germanPlain) end
end)
content:SetScript("OnHyperlinkClick", function(_, link, _, mouse)
  local phrase = linkPhrase(link)
  if not phrase then return end
  showFor(phrase, true)
  local e = card.entry
  if IsShiftKeyDown() and e and e.tr then WL.AddVocab(e) end
  if WowLexikonDB.options.speakOnClick then WL.Speak(phrase, mouse == "RightButton" and -4 or 0) end
end)

-- Taste gedrückt/losgelassen, während die Maus schon auf einem Wort ist
local mod = CreateFrame("Frame")
mod:RegisterEvent("MODIFIER_STATE_CHANGED")
mod:SetScript("OnEvent", function()
  if not current.hovered then return end
  if hoverAllowed() then showFor(current.hovered, false)
  elseif not card.pinned and card:IsShown() then
    card:Hide()
    showTipFor(current.hovered)
  end
end)

---------------------------------------------------------------------------
-- Questfenster, Questlog, NPC-Gespräche, Bücher/Briefe/Schilder
---------------------------------------------------------------------------
local function questID() return (GetQuestID and GetQuestID()) or 0 end
local function npcName() return UnitName and UnitName("npc") or "" end

local ev = CreateFrame("Frame")
for _, e in ipairs({ "QUEST_DETAIL", "QUEST_PROGRESS", "QUEST_COMPLETE", "QUEST_FINISHED", "QUEST_GREETING",
                     "GOSSIP_SHOW", "GOSSIP_CLOSED", "ITEM_TEXT_READY", "ITEM_TEXT_CLOSED" }) do pcall(ev.RegisterEvent, ev, e) end
ev:SetScript("OnEvent", function(_, event)
  if event == "QUEST_FINISHED" or event == "GOSSIP_CLOSED" or event == "ITEM_TEXT_CLOSED" then
    WL.DetachDrawer(({ QUEST_FINISHED = "quest", GOSSIP_CLOSED = "gossip", ITEM_TEXT_CLOSED = "book" })[event])
    return
  end
  if event == "QUEST_GREETING" then
    WL.ShowPanel({ title = npcName(), en = GetGreetingText and GetGreetingText() or "", anchor = QuestFrame, x = -32, y = -18, source = "greeting", auto = true })
    return
  end
  if event == "GOSSIP_SHOW" then
    local text = (C_GossipInfo and C_GossipInfo.GetText and C_GossipInfo.GetText()) or (GetGossipText and GetGossipText()) or ""
    if text ~= "" then WL.ShowPanel({ title = npcName(), en = text, anchor = GossipFrame, x = -32, y = -18, source = "gossip", auto = true }) end
    return
  end
  if event == "ITEM_TEXT_READY" then
    local item, text = ItemTextGetItem and ItemTextGetItem() or "", ItemTextGetText and ItemTextGetText() or ""
    if text ~= "" then WL.ShowPanel({ title = item, en = text, anchor = ItemTextFrame, x = -32, y = -18, source = "book", auto = true }) end
    return
  end
  local id, t = questID(), GetTitleText()
  local text, fields
  if event == "QUEST_DETAIL" then
    local d, o = GetQuestText(), GetObjectiveText()
    WL.RecordQuest(id, "T", t); WL.RecordQuest(id, "D", d); WL.RecordQuest(id, "O", o)
    text = (d or "") .. "\n\n" .. (o or ""); fields = { "D", "O" }
  elseif event == "QUEST_PROGRESS" then
    text = GetProgressText(); WL.RecordQuest(id, "Q", text); fields = { "Q" }
  else
    text = GetRewardText(); WL.RecordQuest(id, "R", text); fields = { "R" }
  end
  WL.ShowQuest(id, t, text, fields, QuestFrame)
end)

local function showLogSelection()
  if not (QuestLogFrame and QuestLogFrame:IsShown()) or not GetQuestLogSelection then return end
  local idx = GetQuestLogSelection()
  if not idx or idx == 0 then return end
  local t, _, _, isHeader, _, _, _, id = GetQuestLogTitle(idx)
  if isHeader or not t then return end
  local d, o = GetQuestLogQuestText()
  WL.RecordQuest(id, "T", t); WL.RecordQuest(id, "D", d); WL.RecordQuest(id, "O", o)
  WL.ShowQuest(id, t, (d or "") .. "\n\n" .. (o or ""), { "D", "O" }, QuestLogFrame)
  panel.source = "log"
end

---------------------------------------------------------------------------
-- Neues Questlog in der Karte („Map & Quest Log“, wie in WoW Forever): Questdetails rechts neben der Karte
---------------------------------------------------------------------------
local function mapDetails() return QuestMapFrame and type(QuestMapFrame.DetailsFrame) == "table" and QuestMapFrame.DetailsFrame end

local function showMapQuest(id)
  local df = mapDetails()
  if not (df and df:IsShown()) then return end
  local CQ = C_QuestLog or {}
  id = id or (CQ.GetSelectedQuest and CQ.GetSelectedQuest()) or 0
  if not id or id == 0 then return end
  local t = (CQ.GetTitleForQuestID and CQ.GetTitleForQuestID(id)) or ""
  local idx = CQ.GetLogIndexForQuestID and CQ.GetLogIndexForQuestID(id)
  local d, o = "", ""
  if GetQuestLogQuestText then d, o = GetQuestLogQuestText(idx) end
  d, o = d or "", o or ""
  if d == "" and o == "" then return end
  WL.RecordQuest(id, "T", t); WL.RecordQuest(id, "D", d); WL.RecordQuest(id, "O", o)
  local anchor = (WorldMapFrame and WorldMapFrame:IsShown() and WorldMapFrame) or df
  WL.ShowQuest(id, t, d .. "\n\n" .. o, { "D", "O" }, anchor)
  panel.source = "log"
  if WL.MapQuestShown then WL.MapQuestShown(id) end
end
WL.ShowMapQuest = showMapQuest

--- Einhaken, sobald es das Karten-Questlog gibt (mehrfach aufrufbar)
function WL.HookQuestMap()
  if WL.questMapHooked then return true end
  local df = mapDetails()
  if not (df and QuestMapFrame_ShowQuestDetails) then return false end
  WL.questMapHooked = true
  hooksecurefunc("QuestMapFrame_ShowQuestDetails", function(id)
    -- erst nachdem das Spiel die Texte eingesetzt hat
    if C_Timer then C_Timer.After(0, function() WL.Try("Karten-Questlog", function() showMapQuest(id) end) end)
    else showMapQuest(id) end
  end)
  df:HookScript("OnHide", function() WL.DetachDrawer("log") end)
  if WorldMapFrame then WorldMapFrame:HookScript("OnHide", function() WL.DetachDrawer("log") end) end
  return true
end
local mapEv = CreateFrame("Frame")
mapEv:RegisterEvent("ADDON_LOADED")
mapEv:SetScript("OnEvent", function(self) if WL.HookQuestMap() then self:UnregisterAllEvents() end end)

---------------------------------------------------------------------------
-- Alles andere: Text unter der Maus (Tooltips von Gegenständen/Zaubern, Questziele am Rand, Fenster, Menüs)
---------------------------------------------------------------------------
local function collect(frame, out, depth)
  if not frame or depth > 3 then return end
  if frame.GetRegions then
    for _, r in ipairs({ frame:GetRegions() }) do
      if r.GetObjectType and r:GetObjectType() == "FontString" and r:IsVisible() then
        local t = r:GetText()
        if t and t:find("%a%a") then out[#out + 1] = t end
      end
    end
  end
  if frame.GetChildren then
    for _, c in ipairs({ frame:GetChildren() }) do if c.IsVisible and c:IsVisible() then collect(c, out, depth + 1) end end
  end
end

local function tooltipLines(tt)
  local lines = {}
  local name = tt:GetName()
  for i = 1, (tt:NumLines() or 0) do
    local l, r = _G[name .. "TextLeft" .. i], _G[name .. "TextRight" .. i]
    local lt, rt = l and l:GetText(), r and r:IsShown() and r:GetText()
    if lt and lt ~= "" and not lt:find("^|cffffd100DE:") then lines[#lines + 1] = lt .. ((rt and rt ~= "") and ("   " .. rt) or "") end
  end
  return lines
end

function WL.TranslateUnderMouse()
  -- Maus auf einem Wort im Wörterbuch-Fenster? Dann einfach die Wortkarte zeigen.
  if current.hovered then showFor(current.hovered, true); return end
  -- Maus über dem Questfenster (oder Gespräch/Buch): auf einem Gegenstand → Karte dazu, sonst Wörterbuch ausklappen
  local a = drawer.anchor
  if a and a:IsShown() and a.IsMouseOver and a:IsMouseOver() then
    if GameTooltip:IsShown() and GameTooltipTextLeft1 then
      local name = (GameTooltipTextLeft1:GetText() or ""):gsub("|c%x%x%x%x%x%x%x%x", ""):gsub("|r", "")
      if name ~= "" then showFor(name, true); return end
    end
    WL.OpenDrawer()
    return
  end
  local lines, titleText, kind = {}, nil, nil
  local tt = (ItemRefTooltip and ItemRefTooltip:IsShown() and ItemRefTooltip:IsMouseOver() and ItemRefTooltip) or (GameTooltip:IsShown() and GameTooltip)
  if tt then
    lines = tooltipLines(tt); kind = "Tooltip"
    titleText = table.remove(lines, 1)
  else
    local focus = (GetMouseFoci and GetMouseFoci()[1]) or (GetMouseFocus and GetMouseFocus())
    if not focus or focus == WorldFrame then
      print("|cffffd100WoW Lexikon:|r Hier gibt es keinen Text, den das Addon lesen kann. Für Text in der Spielwelt: im Begleit-Programm Strg + Umschalt halten und einen Kasten ziehen.")
      return
    end
    collect(focus, lines, 0)
    if #lines == 0 and focus.GetParent and focus:GetParent() then collect(focus:GetParent(), lines, 1) end
    kind = "Text unter der Maus"
    titleText = kind
  end
  if #lines == 0 and not titleText then
    print("|cffffd100WoW Lexikon:|r Hier wurde kein Text gefunden.")
    return
  end
  local o = { title = titleText or kind, en = table.concat(lines, "\n"), source = "mouse" }
  -- Deutscher Name (Gegenstand, NPC, Objekt) für die erste Zeile
  local nm = titleText and WL.LookupName((titleText:gsub("|c%x%x%x%x%x%x%x%x", ""):gsub("|r", "")))
  if nm then o.deTitle = "Deutscher Name"; o.de = nm.tr .. "  (" .. nm.note .. ")"; o.deAlways = true end
  if o.en == "" then o.en = titleText end
  WL.ShowPanel(o)
end

---------------------------------------------------------------------------
-- Chat und NPC-Sprechblasen: die letzten Nachrichten merken (ohne den Chat zu verändern)
---------------------------------------------------------------------------
local chat = {}
WL.chat = chat
local chatEv = CreateFrame("Frame")
for _, e in ipairs({ "CHAT_MSG_SAY", "CHAT_MSG_YELL", "CHAT_MSG_PARTY", "CHAT_MSG_PARTY_LEADER", "CHAT_MSG_RAID", "CHAT_MSG_RAID_LEADER",
                     "CHAT_MSG_GUILD", "CHAT_MSG_OFFICER", "CHAT_MSG_WHISPER", "CHAT_MSG_CHANNEL", "CHAT_MSG_EMOTE", "CHAT_MSG_TEXT_EMOTE",
                     "CHAT_MSG_MONSTER_SAY", "CHAT_MSG_MONSTER_YELL", "CHAT_MSG_MONSTER_WHISPER", "CHAT_MSG_MONSTER_EMOTE", "CHAT_MSG_RAID_BOSS_EMOTE" }) do
  pcall(chatEv.RegisterEvent, chatEv, e)
end
chatEv:SetScript("OnEvent", function(_, _, msg, author)
  if type(msg) ~= "string" or msg == "" then return end
  chat[#chat + 1] = { a = (author or ""):gsub("%-.*$", ""), m = msg }
  if #chat > 40 then table.remove(chat, 1) end
end)

function WL.ShowChat()
  local lines = {}
  for i = math.max(1, #chat - 19), #chat do lines[#lines + 1] = (chat[i].a ~= "" and (chat[i].a .. ": ") or "") .. chat[i].m end
  WL.ShowPanel({ title = "Chat – letzte Nachrichten", en = #lines > 0 and table.concat(lines, "\n\n") or "Noch keine Nachrichten.", source = "chat", bottom = true,
                 hint = "Neueste unten. Enthält auch, was NPCs sagen und rufen." })
end

---------------------------------------------------------------------------
-- Start: Fenster beobachten, Tastenbelegung vorschlagen
---------------------------------------------------------------------------
BINDING_HEADER_WOWLEXIKON = "WoW Lexikon"
BINDING_NAME_WOWLEXIKON_UNDERMOUSE = "Übersetzen, was unter der Maus ist (Tooltip, Questziele, Fenster)"
BINDING_NAME_WOWLEXIKON_PANEL = "Wörterbuch-Fenster ein/aus"
BINDING_NAME_WOWLEXIKON_SEARCH = "Wort suchen"
BINDING_NAME_WOWLEXIKON_CHAT = "Chat-Nachrichten übersetzen"

local DEFAULT_KEYS = { { "ALT-T", "WOWLEXIKON_UNDERMOUSE" }, { "ALT-W", "WOWLEXIKON_PANEL" }, { "ALT-F", "WOWLEXIKON_SEARCH" }, { "ALT-C", "WOWLEXIKON_CHAT" } }

local function setDefaultBindings()
  if not (SetBinding and GetBindingKey and GetBindingAction) or (InCombatLockdown and InCombatLockdown()) then return end
  if WowLexikonDB.bindingsSet then return end
  local set = {}
  for _, kb in ipairs(DEFAULT_KEYS) do
    local key, action = kb[1], kb[2]
    if not GetBindingKey(action) and (GetBindingAction(key) or "") == "" then
      SetBinding(key, action); set[#set + 1] = key:gsub("ALT%-", "Alt+")
    end
  end
  if #set > 0 and SaveBindings and GetCurrentBindingSet then SaveBindings(GetCurrentBindingSet()) end
  WowLexikonDB.bindingsSet = true
  if #set > 0 then print("|cffffd100WoW Lexikon:|r Tasten belegt: " .. table.concat(set, ", ") .. " (änderbar unter Tastenbelegung → WoW Lexikon).") end
end

function WL.OnLogin()
  if QuestFrame then QuestFrame:HookScript("OnHide", function() WL.DetachDrawer("quest") end) end
  if QuestLogFrame then QuestLogFrame:HookScript("OnHide", function() WL.DetachDrawer("log") end) end
  if GossipFrame then GossipFrame:HookScript("OnHide", function() WL.DetachDrawer("gossip") end) end
  if ItemTextFrame then ItemTextFrame:HookScript("OnHide", function() WL.DetachDrawer("book") end) end
  if QuestLog_SetSelection then hooksecurefunc("QuestLog_SetSelection", showLogSelection) end
  if QuestLog_UpdateQuestDetails then hooksecurefunc("QuestLog_UpdateQuestDetails", showLogSelection) end
  WL.HookQuestMap()
  local n = 0; for _ in pairs(WL.WordTable()) do n = n + 1 end
  local k = WL.TranslateKey and WL.TranslateKey()
  print("|cffffd100WoW Lexikon|r geladen – " .. n .. " Wörter" .. (WL.dataLoaded and ", deutsche Questtexte und Namen" or "") .. ". " ..
        (k and ("Übersetzen-Taste: |cffffd100" .. WL.KeyLabel(k) .. "|r") or "Übersetzen-Taste noch nicht festgelegt – |cffffd100Buch an der Minikarte → Hilfe|r") ..
        " · |cffffd100/lex hilfe|r")
end

---------------------------------------------------------------------------
-- Deutsche Namen in Tooltips (NPCs, Gegenstände – auch im Beutel)
---------------------------------------------------------------------------
local function addName(tt, name)
  if not (WowLexikonDB and WowLexikonDB.options.tooltipNames) or not name then return end
  local s = WowLexikon_Names[name:lower()]
  if not s then return end
  local _, tr = strsplit("|", s)
  if tr and tr ~= "" and tr:lower() ~= name:lower() then tt:AddLine("|cffffd100DE:|r " .. tr, 0.95, 0.95, 0.95); tt:Show() end
end
if TooltipDataProcessor and TooltipDataProcessor.AddTooltipPostCall and Enum and Enum.TooltipDataType then
  TooltipDataProcessor.AddTooltipPostCall(Enum.TooltipDataType.Unit, function(tt) if tt.GetUnit then addName(tt, (tt:GetUnit())) end end)
  TooltipDataProcessor.AddTooltipPostCall(Enum.TooltipDataType.Item, function(tt) if tt.GetItem then addName(tt, (tt:GetItem())) end end)
else
  pcall(GameTooltip.HookScript, GameTooltip, "OnTooltipSetUnit", function(tt) addName(tt, (tt:GetUnit())) end)
  pcall(GameTooltip.HookScript, GameTooltip, "OnTooltipSetItem", function(tt) addName(tt, (tt:GetItem())) end)
  if ItemRefTooltip then pcall(ItemRefTooltip.HookScript, ItemRefTooltip, "OnTooltipSetItem", function(tt) addName(tt, (tt:GetItem())) end) end
end

---------------------------------------------------------------------------
-- Suche: /lex [wort]
---------------------------------------------------------------------------
local search = CreateFrame("Frame", "WowLexikonSearch", UIParent, BACKDROP_TEMPLATE)
search:SetSize(300, 78); search:SetPoint("CENTER", 0, 220); search:SetFrameStrata("DIALOG"); search:Hide()
search:EnableMouse(true); search:SetMovable(true); search:RegisterForDrag("LeftButton")
search:SetScript("OnDragStart", search.StartMoving); search:SetScript("OnDragStop", search.StopMovingOrSizing)
parchment(search)
local _, _, sclose = titleBar(search, "Wort suchen")
sclose:SetScript("OnClick", function() search:Hide(); card.pinned = false; card:Hide() end)
local box = CreateFrame("EditBox", nil, search, "InputBoxTemplate")
box:SetSize(250, 22); box:SetPoint("TOPLEFT", 24, -40); box:SetAutoFocus(true)
box:SetScript("OnTextChanged", function(self)
  local t = self:GetText():gsub("^%s+", ""):gsub("%s+$", "")
  if #t >= 2 then WL.ShowCard(t, search, true) else card.pinned = false; card:Hide() end
end)
box:SetScript("OnEnterPressed", function(self) WL.Speak(self:GetText(), 0) end)
box:SetScript("OnEscapePressed", function() search:Hide(); card.pinned = false; card:Hide() end)
tinsert(UISpecialFrames, "WowLexikonSearch")
tinsert(UISpecialFrames, "WowLexikonCard")

function WL.OpenSearch() search:Show(); box:SetText(""); box:SetFocus() end

local MODE_NAMES = { taste = "mit der Übersetzen-Taste", immer = "immer beim Darüberfahren", alt = "solange Alt gehalten wird", strg = "solange Strg gehalten wird", umschalt = "solange Umschalt gehalten wird", klick = "nur per Klick" }

WL.HandleSlash = function(msg)
  msg = (msg or ""):gsub("^%s+", ""):gsub("%s+$", "")
  local cmd, arg = msg:match("^(%S+)%s*(.*)$")
  cmd = cmd and cmd:lower()
  local o = WowLexikonDB.options
  if cmd == "hilfe" or cmd == "help" then
    local k = WL.TranslateKey and WL.TranslateKey()
    print("|cffffd100WoW Lexikon|r – so geht's:")
    print("  Übersetzen-Taste (" .. (k and WL.KeyLabel(k) or "noch nicht festgelegt") .. "): übersetzt, was unter der Maus ist – Wort, Gegenstand, Zauber, Questziele")
    print("  Wort im Wörterbuch-Fenster anklicken = vorlesen · Buch an der Minikarte = Menü (Suche, Chat, Einstellungen)")
    print("  /lex taste – Übersetzen-Taste festlegen · /lex willkommen – Kurzanleitung")
    print("Befehle:")
    print("  /lex <wort> – nachschlagen · /lex chat · /lex maus (wie Alt+T)")
    print("  /lex modus taste|alt|strg|umschalt|immer|klick – wann die Wortkarte erscheint (jetzt: " .. MODE_NAMES[o.hoverMode or "taste"] .. ")")
    print("  /lex fenster – Fenster automatisch bei Quests, Gesprächen, Büchern (jetzt: " .. (o.autoPanel and "an" or "aus") .. ")")
    print("  /lex namen – deutsche Namen in Tooltips (jetzt: " .. (o.tooltipNames and "an" or "aus") .. ")")
    print("  /lex vorlesen – beim Anklicken vorlesen (jetzt: " .. (o.speakOnClick and "an" or "aus") .. ") · /lex vorlesen test · /lex stimme – andere Stimme")
    print("  /lex tipp – kleine Übersetzung beim Darüberfahren (jetzt: " .. (o.hoverTip ~= false and "an" or "aus") .. ") · /lex deutsch – deutschen Text auf-/zuklappen")
    print("  /lex vokabeln – gemerkte Wörter · /lex optionen – Einstellungen und Tasten")
  elseif cmd == "modus" then
    local m = (arg or ""):lower()
    if MODE_NAMES[m] then o.hoverMode = m; print("Wortkarte erscheint jetzt " .. MODE_NAMES[m] .. ".")
    else print("Mögliche Werte: taste, alt, strg, umschalt, immer, klick") end
  elseif cmd == "fenster" then o.autoPanel = not o.autoPanel; print("Fenster automatisch: " .. (o.autoPanel and "an" or "aus"))
  elseif cmd == "namen" then o.tooltipNames = not o.tooltipNames; print("Deutsche Namen in Tooltips: " .. (o.tooltipNames and "an" or "aus"))
  elseif cmd == "vorlesen" and (arg or ""):lower() == "test" then WL.SpeechTest()
  elseif cmd == "stimme" then WL.NextVoice()
  elseif cmd == "vorlesen" then o.speakOnClick = not o.speakOnClick; print("Vorlesen beim Anklicken: " .. (o.speakOnClick and "an" or "aus"))
  elseif cmd == "deutsch" then o.showGerman = not o.showGerman; print("Deutscher Text im Wörterbuch: " .. (o.showGerman and "aufgeklappt" or "zugeklappt"))
  elseif cmd == "tipp" then o.hoverTip = not o.hoverTip; print("Kleine Übersetzung beim Darüberfahren: " .. (o.hoverTip and "an" or "aus"))
  elseif cmd == "optionen" or cmd == "einstellungen" or cmd == "tasten" then WL.OpenOptions()
  elseif cmd == "textur" then print("|cffffd100WoW Lexikon:|r Pergament " .. tostring(WL.parchmentSource))
  elseif cmd == "taste" then WL.CaptureKey()
  elseif cmd == "willkommen" or cmd == "anleitung" then WL.ShowWelcome()
  elseif cmd == "minimap" or cmd == "knopf" then o.minimapHidden = not o.minimapHidden; if WL.minimapButton then if o.minimapHidden then WL.minimapButton:Hide() else WL.minimapButton:Show() end end
  elseif cmd == "chat" then WL.ShowChat()
  elseif cmd == "maus" then WL.TranslateUnderMouse()
  elseif cmd == "vokabeln" then
    local n = 0
    for _, v in pairs(WowLexikonDB.vocab) do n = n + 1; print(("  %s – %s"):format(v.w, v.tr or "?")) end
    if n == 0 then print("Noch keine Wörter gemerkt. Umschalt+Klick auf ein Wort oder „Merken“ in der Karte.") end
  elseif msg ~= "" then
    WL.ShowCard(msg, nil, true)
  else
    WL.OpenBook()
  end
end

end)
