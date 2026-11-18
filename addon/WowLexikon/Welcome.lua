-- WoW Lexikon – Begrüßung, Übersetzen-Taste festlegen, Knopf an der Minikarte mit Menü
WowLexikon.Try("Begrüßung", function()
local WL = WowLexikon
local BACKDROP_TEMPLATE = BackdropTemplateMixin and "BackdropTemplate" or nil
local ACTION = "WOWLEXIKON_UNDERMOUSE"

local function parchment(f)
  if f.SetBackdrop then
    f:SetBackdrop({ bgFile = "Interface\\Buttons\\WHITE8x8", edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
                    tile = false, edgeSize = 24, insets = { left = 6, right = 6, top = 6, bottom = 6 } })
    f:SetBackdropColor(0.90, 0.80, 0.60, 0.98)
  end
end
local function ink(fs) fs:SetTextColor(0.16, 0.10, 0.04); fs:SetShadowColor(0, 0, 0, 0); return fs end

--- Lesbarer Tastenname: „ALT-T“ → „Alt+T“
function WL.KeyLabel(key)
  if not key or key == "" then return "nicht festgelegt" end
  return (key:gsub("ALT%-", "Alt+"):gsub("CTRL%-", "Strg+"):gsub("SHIFT%-", "Umschalt+"):gsub("BUTTON(%d)", "Maustaste %1"))
end
function WL.TranslateKey() return GetBindingKey and GetBindingKey(ACTION) end

---------------------------------------------------------------------------
-- Begrüßungsfenster
---------------------------------------------------------------------------
local w = CreateFrame("Frame", "WowLexikonWelcome", UIParent, BACKDROP_TEMPLATE)
w:SetSize(440, 380); w:SetPoint("CENTER", 0, 40); w:SetFrameStrata("DIALOG"); w:Hide()
w:EnableMouse(true); w:SetMovable(true); w:RegisterForDrag("LeftButton")
w:SetScript("OnDragStart", w.StartMoving); w:SetScript("OnDragStop", w.StopMovingOrSizing)
parchment(w)
tinsert(UISpecialFrames, "WowLexikonWelcome")
local close = CreateFrame("Button", nil, w, "UIPanelCloseButton"); close:SetPoint("TOPRIGHT", -2, -2)

local title = ink(w:CreateFontString(nil, "OVERLAY", "QuestTitleFont")); title:SetPoint("TOP", 0, -18); title:SetText("Willkommen bei WoW Lexikon")
local steps = {
  { "1", "Übersetzen-Taste drücken", "Maus auf ein Wort, einen Gegenstand, einen Zauber oder die Questziele – dann deine Übersetzen-Taste drücken." },
  { "2", "Wort anklicken = vorlesen", "Im Wörterbuch-Fenster neben Quests: Klick liest vor, Rechtsklick langsam, Umschalt+Klick merkt das Wort." },
  { "3", "Lexikon-Buch", "Buch-Knopf an der Minikarte, Esc-Menü oder /lex: Wörter suchen, Vokabeln lernen, Chat übersetzen, Hilfe." },
}
local y = -56
for _, s in ipairs(steps) do
  local n = ink(w:CreateFontString(nil, "OVERLAY", "QuestTitleFont")); n:SetPoint("TOPLEFT", 22, y); n:SetText(s[1] .. ".")
  local h = ink(w:CreateFontString(nil, "OVERLAY", "GameFontNormal")); h:SetPoint("TOPLEFT", 48, y); h:SetText(s[2])
  local t = ink(w:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")); t:SetPoint("TOPLEFT", 48, y - 18); t:SetWidth(370); t:SetJustifyH("LEFT"); t:SetText(s[3])
  t:SetTextColor(0.30, 0.21, 0.10)
  y = y - 66
end

local keyText = ink(w:CreateFontString(nil, "OVERLAY", "GameFontNormal")); keyText:SetPoint("TOPLEFT", 22, y - 6); keyText:SetWidth(396); keyText:SetJustifyH("LEFT")
local keyHint = ink(w:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")); keyHint:SetPoint("TOPLEFT", keyText, "BOTTOMLEFT", 0, -6); keyHint:SetWidth(396); keyHint:SetJustifyH("LEFT")
keyHint:SetTextColor(0.43, 0.10, 0.03)

local setKey = CreateFrame("Button", nil, w, "UIPanelButtonTemplate"); setKey:SetSize(170, 24); setKey:SetPoint("BOTTOMLEFT", 20, 18); setKey:SetText("Andere Taste …")
local go = CreateFrame("Button", nil, w, "UIPanelButtonTemplate"); go:SetSize(130, 24); go:SetPoint("BOTTOMRIGHT", -20, 18); go:SetText("Los geht's")
go:SetScript("OnClick", function() w:Hide() end)

local function refresh()
  local k = WL.TranslateKey()
  keyText:SetText("Deine Übersetzen-Taste: |cff6e1a08" .. WL.KeyLabel(k) .. "|r" ..
    ((WowLexikonDB and WowLexikonDB.keyAuto == k and k) and "  (automatisch gewählt – war frei)" or ""))
end
w:SetScript("OnShow", function() refresh(); keyHint:SetText("") end)

---------------------------------------------------------------------------
-- Taste festlegen: nächsten Tastendruck (oder Maus-Seitentaste) übernehmen
---------------------------------------------------------------------------
local capture = CreateFrame("Frame", nil, w)
capture:SetAllPoints(); capture:Hide(); capture:EnableKeyboard(true)
if capture.SetPropagateKeyboardInput then capture:SetPropagateKeyboardInput(false) end
local pending

local MODIFIERS = { LALT = true, RALT = true, LCTRL = true, RCTRL = true, LSHIFT = true, RSHIFT = true, LMETA = true, RMETA = true }
local function combo(key)
  return (IsAltKeyDown() and "ALT-" or "") .. (IsControlKeyDown() and "CTRL-" or "") .. (IsShiftKeyDown() and "SHIFT-" or "") .. key
end

local function apply(key)
  if InCombatLockdown and InCombatLockdown() then keyHint:SetText("Im Kampf geht das nicht – bitte danach noch einmal."); return end
  local old = WL.TranslateKey()
  if old then SetBinding(old) end
  SetBinding(key, ACTION)
  if SaveBindings and GetCurrentBindingSet then SaveBindings(GetCurrentBindingSet()) end
  keyHint:SetText("Gespeichert. Ändern kannst du sie jederzeit hier oder unter Tastenbelegung → WoW Lexikon.")
  refresh()
end

local function take(key)
  if key == "ESCAPE" then capture:Hide(); setKey:SetText("Andere Taste …"); keyHint:SetText(""); pending = nil; return end
  if MODIFIERS[key] then return end
  local k = combo(key)
  local used = GetBindingAction and GetBindingAction(k) or ""
  if used ~= "" and used ~= ACTION and pending ~= k then
    pending = k
    local name = _G["BINDING_NAME_" .. used] or used
    keyHint:SetText(WL.KeyLabel(k) .. " ist schon belegt mit „" .. name .. "“. Noch einmal drücken = trotzdem nehmen, oder andere Taste drücken.")
    return
  end
  pending = nil
  capture:Hide(); setKey:SetText("Andere Taste …")
  apply(k)
end
capture:SetScript("OnKeyDown", function(_, key) take(key) end)
capture:SetScript("OnMouseDown", function(_, button)
  local map = { Button4 = "BUTTON4", Button5 = "BUTTON5", MiddleButton = "BUTTON3" }
  if map[button] then take(map[button]) end
end)
function WL.CaptureKey()
  w:Show()
  capture:Show()
  setKey:SetText("Taste drücken …")
  keyHint:SetText("Drück jetzt die Taste (auch mit Alt/Strg/Umschalt oder Maus-Seitentaste). Esc = abbrechen.")
end
setKey:SetScript("OnClick", function() WL.CaptureKey() end)

function WL.ShowWelcome() w:Show() end

---------------------------------------------------------------------------
-- Knopf an der Minikarte (verschiebbar) + kleines Menü
---------------------------------------------------------------------------
local menu = CreateFrame("Frame", "WowLexikonMenu", UIParent, BACKDROP_TEMPLATE)
menu:SetSize(210, 200); menu:SetFrameStrata("DIALOG"); menu:Hide(); menu:EnableMouse(true)
parchment(menu)
tinsert(UISpecialFrames, "WowLexikonMenu")
local mtitle = ink(menu:CreateFontString(nil, "OVERLAY", "GameFontNormal")); mtitle:SetPoint("TOP", 0, -14); mtitle:SetText("WoW Lexikon")
local ENTRIES = {
  { "Wort suchen", function() WL.OpenSearch() end },
  { "Chat übersetzen", function() WL.ShowChat() end },
  { "Wörterbuch-Fenster", function() WL.TogglePanel() end },
  { "Einstellungen", function() WL.OpenOptions() end },
  { "Hilfe / Übersetzen-Taste", function() WL.ShowWelcome() end },
}
for i, e in ipairs(ENTRIES) do
  local b = CreateFrame("Button", nil, menu, "UIPanelButtonTemplate")
  b:SetSize(176, 24); b:SetPoint("TOP", 0, -14 - i * 28); b:SetText(e[1])
  b:SetScript("OnClick", function() menu:Hide(); e[2]() end)
end
menu:SetHeight(28 + #ENTRIES * 28 + 18)

local mm = CreateFrame("Button", "WowLexikonMinimapButton", Minimap)
mm:SetSize(31, 31); mm:SetFrameStrata("MEDIUM"); mm:SetFrameLevel(8)
mm:RegisterForClicks("LeftButtonUp", "RightButtonUp"); mm:RegisterForDrag("LeftButton")
local icon = mm:CreateTexture(nil, "BACKGROUND"); icon:SetSize(20, 20); icon:SetPoint("CENTER", 0, 1)
icon:SetTexture("Interface\\Icons\\INV_Misc_Book_09")
local border = mm:CreateTexture(nil, "OVERLAY"); border:SetSize(53, 53); border:SetPoint("TOPLEFT")
border:SetTexture("Interface\\Minimap\\MiniMap-TrackingBorder")
mm:SetHighlightTexture("Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight")

local function placeButton()
  local a = math.rad((WowLexikonDB and WowLexikonDB.options.minimapAngle) or 200)
  mm:ClearAllPoints()
  mm:SetPoint("CENTER", Minimap, "CENTER", math.cos(a) * 80, math.sin(a) * 80)
end
mm:SetScript("OnDragStart", function(self)
  self:SetScript("OnUpdate", function()
    local mx, my = Minimap:GetCenter(); local x, y = GetCursorPosition(); local s = Minimap:GetEffectiveScale()
    WowLexikonDB.options.minimapAngle = math.deg(math.atan2(y / s - my, x / s - mx))
    placeButton()
  end)
end)
mm:SetScript("OnDragStop", function(self) self:SetScript("OnUpdate", nil) end)
mm:SetScript("OnClick", function(self, button)
  if button == "RightButton" then WL.OpenOptions(); return end
  WL.ToggleBook()
end)
mm:SetScript("OnEnter", function(self)
  GameTooltip:SetOwner(self, "ANCHOR_LEFT")
  GameTooltip:AddLine("WoW Lexikon")
  GameTooltip:AddLine("Übersetzen-Taste: " .. WL.KeyLabel(WL.TranslateKey()), 1, 1, 1)
  GameTooltip:AddLine("Klick: Lexikon-Buch · Rechtsklick: Einstellungen · Ziehen: verschieben", 0.8, 0.8, 0.8)
  GameTooltip:Show()
end)
mm:SetScript("OnLeave", function() GameTooltip:Hide() end)
WL.minimapButton = mm

-- Übersetzen-Taste ohne Einrichtung: die erste freie aus dieser Liste – nie eine schon belegte überschreiben
local CANDIDATES = { "ALT-T", "CTRL-T", "ALT-CTRL-T", "ALT-SHIFT-T", "CTRL-SHIFT-T" }
function WL.AutoKey()
  if not (SetBinding and GetBindingAction) or WL.TranslateKey() then return end
  if (InCombatLockdown and InCombatLockdown()) or (WowLexikonDB and WowLexikonDB.keyAuto) then return end
  for _, k in ipairs(CANDIDATES) do
    if (GetBindingAction(k) or "") == "" then
      SetBinding(k, ACTION)
      if SaveBindings and GetCurrentBindingSet then SaveBindings(GetCurrentBindingSet()) end
      WowLexikonDB.keyAuto = k
      return k
    end
  end
end

-- Beim Einloggen: Knopf platzieren, Taste wählen, beim ersten Mal begrüßen
local ev = CreateFrame("Frame")
ev:RegisterEvent("PLAYER_LOGIN")
ev:SetScript("OnEvent", function()
  placeButton()
  if WowLexikonDB and WowLexikonDB.options.minimapHidden then mm:Hide() end
  local auto = WL.AutoKey()
  if auto then print("|cffffd100WoW Lexikon:|r Übersetzen-Taste ist |cffffd100" .. WL.KeyLabel(auto) .. "|r (war frei). Ändern: Buch an der Minikarte → Hilfe.") end
  if WowLexikonDB and not WowLexikonDB.welcomed then
    WowLexikonDB.welcomed = true
    WL.ShowWelcome()
  end
end)
end)
