-- WoW Lexikon – Fenster im Stil des Spiels und das „Lexikon-Buch“ (Hauptmenü mit Reitern)
WowLexikon.Try("Lexikon-Buch", function()
local WL = WowLexikon
local BACKDROP_TEMPLATE = BackdropTemplateMixin and "BackdropTemplate" or nil
local INK = { 0.16, 0.10, 0.04 }

local function isTable(x) return type(x) == "table" end

---------------------------------------------------------------------------
-- Pergament: die Grafik aus dem Questfenster des Spiels übernehmen (gleiche Farbe, gleicher gerissener Rand).
-- Gibt es sie nicht, ein Ersatz in Pergamentfarbe mit dunkleren Rändern.
---------------------------------------------------------------------------
WL.papers = {}
WL.parchmentSource = "Ersatz (Farbe)"

local function gradient(tex, orient, r, g, b, a1, a2)
  tex:SetColorTexture(1, 1, 1, 1)
  if CreateColor and pcall(tex.SetGradient, tex, orient, CreateColor(r, g, b, a1), CreateColor(r, g, b, a2)) then return end
  if tex.SetGradientAlpha then pcall(tex.SetGradientAlpha, tex, orient, r, g, b, a1, r, g, b, a2) end
end

local function findGameParchment()
  local best, bestArea = nil, 0
  local function scan(frame, depth)
    if not frame or depth > 5 or not frame.GetRegions then return end
    for _, r in ipairs({ frame:GetRegions() }) do
      if r.GetObjectType and r:GetObjectType() == "Texture" then
        local atlas = r.GetAtlas and r:GetAtlas()
        local tex = r.GetTexture and r:GetTexture()
        local name = (r.GetName and r:GetName()) or (r.GetDebugName and r:GetDebugName()) or ""
        local s = (tostring(atlas or "") .. " " .. tostring(tex or "") .. " " .. tostring(name)):lower()
        local w, h = (r.GetWidth and r:GetWidth() or 0), (r.GetHeight and r:GetHeight() or 0)
        local looks = s:find("parchment") or s:find("questbg") or s:find("questdetail") or s:find("questbackground")
        if looks and w * h > bestArea and (atlas or tex) then
          best = { atlas = atlas, tex = tex, coords = { r:GetTexCoord() }, from = s }
          bestArea = w * h
        end
      end
    end
    if frame.GetChildren then for _, c in ipairs({ frame:GetChildren() }) do scan(c, depth + 1) end end
  end
  scan(QuestFrame, 0)
  if not best then scan(GossipFrame, 0) end
  if not best then scan(QuestLogFrame, 0) end
  return best
end

local function paint(p)
  local src = WL.gameParchment
  if src then
    local ok = false
    if src.atlas and p.paper.SetAtlas then ok = pcall(p.paper.SetAtlas, p.paper, src.atlas) end
    if not ok and src.tex then ok = pcall(p.paper.SetTexture, p.paper, src.tex) end
    if ok then
      if not src.atlas and #src.coords >= 4 then pcall(p.paper.SetTexCoord, p.paper, unpack(src.coords)) end
      p.paper:SetVertexColor(1, 1, 1, 1)
      for _, e in ipairs(p.edges) do e:Hide() end
      return
    end
  end
  p.paper:SetColorTexture(0.91, 0.80, 0.57, 1)
  for _, e in ipairs(p.edges) do e:Show() end
end

function WL.ApplyParchment(parent)
  local p = { paper = parent:CreateTexture(nil, "BACKGROUND", nil, 1), edges = {} }
  p.paper:SetPoint("TOPLEFT", 3, -3); p.paper:SetPoint("BOTTOMRIGHT", -3, 3)
  -- dunklere Ränder wie bei altem Pergament
  local function edge(point1, point2, w, h, orient, a1, a2)
    local e = parent:CreateTexture(nil, "BACKGROUND", nil, 2)
    e:SetPoint(point1, 3 * (point1:find("LEFT") and 1 or -1), 3 * (point1:find("TOP") and -1 or 1))
    e:SetPoint(point2, 3 * (point2:find("LEFT") and 1 or -1), 3 * (point2:find("TOP") and -1 or 1))
    if w then e:SetWidth(w) end; if h then e:SetHeight(h) end
    gradient(e, orient, 0.42, 0.26, 0.08, a1, a2)
    p.edges[#p.edges + 1] = e
  end
  edge("TOPLEFT", "BOTTOMLEFT", 26, nil, "HORIZONTAL", 0.35, 0)
  edge("TOPRIGHT", "BOTTOMRIGHT", 26, nil, "HORIZONTAL", 0, 0.35)
  edge("TOPLEFT", "TOPRIGHT", nil, 22, "VERTICAL", 0, 0.30)
  edge("BOTTOMLEFT", "BOTTOMRIGHT", nil, 26, "VERTICAL", 0.35, 0)
  WL.papers[#WL.papers + 1] = p
  paint(p)
  return p
end

-- Beim Einloggen gibt es das Questfenster sicher: Grafik suchen und auf alle Fenster übertragen
local pev = CreateFrame("Frame")
pev:RegisterEvent("PLAYER_LOGIN")
pev:SetScript("OnEvent", function()
  local ok, found = pcall(findGameParchment)
  if ok and found then
    WL.gameParchment = found
    WL.parchmentSource = "aus dem Spiel: " .. tostring(found.atlas or found.tex)
  end
  for _, p in ipairs(WL.papers) do pcall(paint, p) end
end)

---------------------------------------------------------------------------
-- Fenster wie die Spielfenster (Porträt, Titelleiste, roter Schließen-Knopf). Fällt auf Pergament zurück,
-- wenn es die Vorlage in dieser Spielversion nicht gibt.
---------------------------------------------------------------------------
function WL.MakeWindow(name, width, height, titleText, icon)
  local ok, f = pcall(CreateFrame, "Frame", name, UIParent, "ButtonFrameTemplate")
  local styled = ok and f and isTable(f.Inset) and (isTable(f.TitleText) or isTable(f.TitleContainer)) and true or false
  if not styled then
    if ok and f then f:Hide() end
    f = CreateFrame("Frame", name, UIParent, BACKDROP_TEMPLATE)
    if f.SetBackdrop then
      f:SetBackdrop({ bgFile = "Interface\\Buttons\\WHITE8x8", edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
                      tile = false, edgeSize = 24, insets = { left = 6, right = 6, top = 6, bottom = 6 } })
      f:SetBackdropColor(0.90, 0.80, 0.60, 0.98)
    end
    local bar = f:CreateTexture(nil, "ARTWORK"); bar:SetPoint("TOPLEFT", 8, -8); bar:SetPoint("TOPRIGHT", -8, -8); bar:SetHeight(22)
    bar:SetColorTexture(0.08, 0.08, 0.08, 0.92)
    local t = f:CreateFontString(nil, "OVERLAY", "GameFontNormal"); t:SetPoint("CENTER", bar, "CENTER"); t:SetText(titleText)
    t:SetTextColor(0.92, 0.88, 0.78)
    local c = CreateFrame("Button", nil, f, "UIPanelCloseButton"); c:SetPoint("TOPRIGHT", 0, 0)
    c:SetScript("OnClick", function() f:Hide() end)
    f.body = CreateFrame("Frame", nil, f); f.body:SetPoint("TOPLEFT", 14, -34); f.body:SetPoint("BOTTOMRIGHT", -14, 12)
  else
    if f.SetTitle then f:SetTitle(titleText) elseif isTable(f.TitleText) then f.TitleText:SetText(titleText) end
    local portrait = (isTable(f.PortraitContainer) and f.PortraitContainer.portrait) or f.portrait or (name and _G[name .. "Portrait"])
    if isTable(portrait) then
      if SetPortraitToTexture then pcall(SetPortraitToTexture, portrait, icon or "Interface\\Icons\\INV_Misc_Book_09")
      else portrait:SetTexture(icon or "Interface\\Icons\\INV_Misc_Book_09") end
    end
    -- Pergament in den Innenbereich – dasselbe wie im Questfenster des Spiels
    WL.ApplyParchment(f.Inset)
    f.body = CreateFrame("Frame", nil, f.Inset); f.body:SetPoint("TOPLEFT", 10, -10); f.body:SetPoint("BOTTOMRIGHT", -10, 10)
  end
  f.styled = styled
  f:SetSize(width, height)
  f:SetFrameStrata("HIGH"); f:SetClampedToScreen(true); f:Hide()
  f:EnableMouse(true); f:SetMovable(true); f:RegisterForDrag("LeftButton")
  f:SetScript("OnDragStart", f.StartMoving); f:SetScript("OnDragStop", f.StopMovingOrSizing)
  if name then tinsert(UISpecialFrames, f:GetName()) end
  return f
end

---------------------------------------------------------------------------
-- Lexikon-Buch
---------------------------------------------------------------------------
local book = WL.MakeWindow("WowLexikonBook", 420, 470, "WoW Lexikon")
book:SetPoint("CENTER", -120, 40)
WL.book = book
local body = book.body
local pages, tabs = {}, {}

local function ink(fs) fs:SetTextColor(unpack(INK)); fs:SetShadowColor(0, 0, 0, 0); return fs end
local function text(parent, obj, x, y, w)
  local fs = ink(parent:CreateFontString(nil, "OVERLAY", obj or "GameFontHighlight"))
  fs:SetPoint("TOPLEFT", x, y); if w then fs:SetWidth(w) end; fs:SetJustifyH("LEFT"); fs:SetJustifyV("TOP")
  return fs
end

local function showPage(key)
  for k, p in pairs(pages) do if k == key then p:Show() else p:Hide() end end
  for k, t in pairs(tabs) do t:SetEnabled(k ~= key) end
  book.current = key
  if type(pages[key].OnOpen) == "function" then pages[key].OnOpen() end
end

local TABS = { { "suche", "Suchen" }, { "vokabeln", "Vokabeln" }, { "chat", "Chat" }, { "hilfe", "Hilfe" } }
for i, t in ipairs(TABS) do
  local b = CreateFrame("Button", nil, book, "UIPanelButtonTemplate")
  b:SetSize(92, 22); b:SetPoint("BOTTOMLEFT", book, "BOTTOMLEFT", 10 + (i - 1) * 98, -24); b:SetText(t[2])
  b:SetScript("OnClick", function() showPage(t[1]) end)
  tabs[t[1]] = b
  local p = CreateFrame("Frame", nil, body); p:SetAllPoints(); p:Hide()
  pages[t[1]] = p
end

-- Suchen -------------------------------------------------------------------
do
  local p = pages.suche
  text(p, "QuestTitleFont", 0, 0):SetText("Wort nachschlagen")
  local box = CreateFrame("EditBox", nil, p, "InputBoxTemplate")
  box:SetSize(300, 24); box:SetPoint("TOPLEFT", 6, -32); box:SetAutoFocus(false)
  local hint = text(p, "GameFontHighlightSmall", 0, -64, 360); hint:SetTextColor(0.30, 0.21, 0.10)
  hint:SetText("Englisches Wort oder Satz eintippen – das Ergebnis erscheint sofort. Enter = vorlesen.")
  local res = { word = text(p, "QuestTitleFont", 0, -96, 360), ipa = text(p, nil, 0, -122, 360), tr = text(p, nil, 0, -146, 360),
                note = text(p, "GameFontHighlightSmall", 0, -176, 360), ex = text(p, "GameFontHighlightSmall", 0, -214, 360) }
  pcall(res.ipa.SetFont, res.ipa, WL.IPA_FONT, 14, "")
  pcall(res.tr.SetFont, res.tr, "Fonts\\FRIZQT__.TTF", 16, ""); res.tr:SetTextColor(0.43, 0.10, 0.03)
  local speak = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); speak:SetSize(90, 22); speak:SetPoint("BOTTOMLEFT", 0, 0); speak:SetText("Vorlesen")
  local slow = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); slow:SetSize(80, 22); slow:SetPoint("LEFT", speak, "RIGHT", 4, 0); slow:SetText("Langsam")
  local save = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); save:SetSize(80, 22); save:SetPoint("LEFT", slow, "RIGHT", 4, 0); save:SetText("Merken")
  local cur
  local function show(t)
    t = (t or ""):gsub("^%s+", ""):gsub("%s+$", "")
    cur = (#t >= 2) and (WL.LookupName(t) or WL.Lookup(t) or { word = t }) or nil
    if cur and WL.LookupName(t) and WL.Lookup(t) and not t:find(" ") then cur = WL.Lookup(t) end
    res.word:SetText(cur and t or "")
    res.ipa:SetText(cur and cur.ipa and ("/" .. cur.ipa .. "/   ·   gesprochen etwa: " .. (WL.GermanReading(cur.ipa) or "")) or "")
    res.tr:SetText(cur and (cur.tr or "Noch nicht im Wörterbuch") or "")
    res.note:SetText(cur and cur.note or "")
    res.ex:SetText(cur and cur.ex and (cur.ex .. (cur.exTr and ("\n" .. cur.exTr) or "")) or "")
    save:SetText("Merken")
  end
  box:SetScript("OnTextChanged", function(self) show(self:GetText()) end)
  box:SetScript("OnEnterPressed", function(self) WL.Speak(self:GetText(), 0) end)
  box:SetScript("OnEscapePressed", function(self) self:ClearFocus() end)
  speak:SetScript("OnClick", function() if cur then WL.Speak(cur.word, 0) end end)
  slow:SetScript("OnClick", function() if cur then WL.Speak(cur.word, -4) end end)
  save:SetScript("OnClick", function() if cur and cur.tr then WL.AddVocab(cur); save:SetText("Gemerkt") end end)
  p.OnOpen = function() box:SetText(""); box:SetFocus(); show("") end
  WL.bookSearchBox = box
end

-- Vokabeln (Liste + Lernen) -----------------------------------------------
do
  local p = pages.vokabeln
  local head = text(p, "QuestTitleFont", 0, 0); head:SetText("Meine Vokabeln")
  local count = text(p, "GameFontHighlightSmall", 0, -26, 360); count:SetTextColor(0.30, 0.21, 0.10)
  local list = text(p, nil, 0, -50, 360)
  local learn = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); learn:SetSize(130, 22); learn:SetPoint("BOTTOMLEFT", 0, 0); learn:SetText("Lernen")
  -- Lernkarte
  local quiz = CreateFrame("Frame", nil, p); quiz:SetAllPoints(); quiz:Hide()
  local qWord = text(quiz, "QuestTitleFont", 0, -40, 360)
  local qIpa = text(quiz, nil, 0, -70, 360); pcall(qIpa.SetFont, qIpa, WL.IPA_FONT, 14, "")
  local qAns = text(quiz, nil, 0, -110, 360); pcall(qAns.SetFont, qAns, "Fonts\\FRIZQT__.TTF", 16, ""); qAns:SetTextColor(0.43, 0.10, 0.03)
  local qInfo = text(quiz, "GameFontHighlightSmall", 0, 0, 360); qInfo:SetTextColor(0.30, 0.21, 0.10)
  local bShow = CreateFrame("Button", nil, quiz, "UIPanelButtonTemplate"); bShow:SetSize(110, 22); bShow:SetPoint("BOTTOMLEFT", 0, 0); bShow:SetText("Aufdecken")
  local bOk = CreateFrame("Button", nil, quiz, "UIPanelButtonTemplate"); bOk:SetSize(90, 22); bOk:SetPoint("LEFT", bShow, "RIGHT", 4, 0); bOk:SetText("Gewusst")
  local bAgain = CreateFrame("Button", nil, quiz, "UIPanelButtonTemplate"); bAgain:SetSize(90, 22); bAgain:SetPoint("LEFT", bOk, "RIGHT", 4, 0); bAgain:SetText("Nochmal")
  local bEnd = CreateFrame("Button", nil, quiz, "UIPanelButtonTemplate"); bEnd:SetSize(70, 22); bEnd:SetPoint("LEFT", bAgain, "RIGHT", 4, 0); bEnd:SetText("Ende")
  local deck, idx = {}, 0

  local function refreshList()
    local items = {}
    for k, v in pairs(WowLexikonDB.vocab) do items[#items + 1] = v end
    table.sort(items, function(a, b) return (a.t or 0) > (b.t or 0) end)
    local lines = {}
    for i = 1, math.min(14, #items) do lines[#lines + 1] = items[i].w .. "  –  " .. (items[i].tr or "?") end
    list:SetText(#lines > 0 and table.concat(lines, "\n") or "Noch keine Wörter gemerkt.\nIm Wörterbuch-Fenster: Umschalt + Klick auf ein Wort, oder „Merken“ in der Karte.")
    count:SetText(#items .. " Wörter" .. (#items > 14 and " (die neuesten 14 angezeigt)" or ""))
    learn:SetEnabled(#items > 0)
  end
  local function card()
    local v = deck[idx]
    if not v then quiz:Hide(); head:Show(); count:Show(); list:Show(); learn:Show(); refreshList(); return end
    qInfo:SetText("Karte " .. idx .. " von " .. #deck .. " – weißt du, was das heißt?")
    qWord:SetText(v.w); qIpa:SetText(v.ipa and ("/" .. v.ipa .. "/") or ""); qAns:SetText("")
    bShow:Show(); bOk:Hide(); bAgain:Hide()
    WL.Speak(v.w, 0)
  end
  learn:SetScript("OnClick", function()
    deck = {}
    for _, v in pairs(WowLexikonDB.vocab) do deck[#deck + 1] = v end
    for i = #deck, 2, -1 do local j = math.random(i); deck[i], deck[j] = deck[j], deck[i] end
    idx = 1; head:Hide(); count:Hide(); list:Hide(); learn:Hide(); quiz:Show(); card()
  end)
  bShow:SetScript("OnClick", function() qAns:SetText(deck[idx].tr or "?"); bShow:Hide(); bOk:Show(); bAgain:Show() end)
  bOk:SetScript("OnClick", function() local v = deck[idx]; v.known = (v.known or 0) + 1; idx = idx + 1; card() end)
  bAgain:SetScript("OnClick", function() deck[#deck + 1] = deck[idx]; idx = idx + 1; card() end)
  bEnd:SetScript("OnClick", function() deck = {}; idx = 1; card() end)
  p.OnOpen = function() quiz:Hide(); head:Show(); count:Show(); list:Show(); learn:Show(); refreshList() end
end

-- Chat ----------------------------------------------------------------------
do
  local p = pages.chat
  text(p, "QuestTitleFont", 0, 0):SetText("Chat übersetzen")
  local info = text(p, "GameFontHighlightSmall", 0, -26, 360); info:SetTextColor(0.30, 0.21, 0.10)
  info:SetText("Öffnet die letzten Nachrichten – auch was NPCs sagen – im Wörterbuch-Fenster. Dort jedes Wort anklickbar.")
  local b = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); b:SetSize(200, 24); b:SetPoint("TOPLEFT", 0, -70); b:SetText("Letzte Nachrichten zeigen")
  b:SetScript("OnClick", function() WL.ShowChat() end)
end

-- Hilfe ----------------------------------------------------------------------
do
  local p = pages.hilfe
  text(p, "QuestTitleFont", 0, 0):SetText("So geht's")
  local t = text(p, nil, 0, -30, 360)
  local keyLine = text(p, "GameFontNormal", 0, -250, 360)
  local change = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); change:SetSize(170, 22); change:SetPoint("BOTTOMLEFT", 0, 0); change:SetText("Taste ändern …")
  change:SetScript("OnClick", function() if WL.CaptureKey then WL.CaptureKey() end end)
  local opts = CreateFrame("Button", nil, p, "UIPanelButtonTemplate"); opts:SetSize(140, 22); opts:SetPoint("LEFT", change, "RIGHT", 6, 0); opts:SetText("Einstellungen")
  opts:SetScript("OnClick", function() book:Hide(); WL.OpenOptions() end)
  p.OnOpen = function()
    local k = WL.TranslateKey and WL.TranslateKey()
    t:SetText("• Maus auf ein Wort, einen Gegenstand, einen Zauber oder die Questziele – dann die Übersetzen-Taste.\n\n" ..
              "• Neben Quests, NPC-Gesprächen und Büchern öffnet sich das Wörterbuch-Fenster. Wort anklicken = vorlesen, " ..
              "Rechtsklick = langsam, Umschalt + Klick = merken.\n\n" ..
              "• Gegenstände und NPCs zeigen ihren deutschen Namen im Tooltip.\n\n" ..
              "• Dieses Buch: Buch-Knopf an der Minikarte, Esc-Menü oder /lex.\n\n" ..
              "• Text in der Spielwelt: im Begleit-Programm Strg + Umschalt halten und einen Kasten ziehen.")
    keyLine:SetText("Deine Übersetzen-Taste: |cff6e1a08" .. (WL.KeyLabel and WL.KeyLabel(k) or "?") .. "|r")
  end
end

function WL.OpenBook(page)
  book:Show()
  local key = (type(page) == "string" and pages[page] and page) or (type(book.current) == "string" and pages[book.current] and book.current) or "suche"
  showPage(key)
end
function WL.ToggleBook() if book:IsShown() then book:Hide() else WL.OpenBook() end end

---------------------------------------------------------------------------
-- Knopf im Esc-Menü (unterhalb, ohne die Knöpfe des Spiels zu verschieben)
---------------------------------------------------------------------------
if GameMenuFrame then
  local gm = CreateFrame("Button", "WowLexikonGameMenuButton", GameMenuFrame, "UIPanelButtonTemplate")
  gm:SetSize(144, 24); gm:SetPoint("TOP", GameMenuFrame, "BOTTOM", 0, -6); gm:SetText("WoW Lexikon")
  gm:SetScript("OnClick", function()
    if HideUIPanel then HideUIPanel(GameMenuFrame) else GameMenuFrame:Hide() end
    WL.OpenBook()
  end)
end
end)
