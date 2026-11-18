WowLexikon.Try("Einstellungen", function()
-- WoW Lexikon – Einstellungen im Spiel (Esc → Optionen → AddOns → WoW Lexikon, oder /lex optionen)
local WL = WowLexikon

local panel = CreateFrame("Frame", "WowLexikonOptions")
panel.name = "WoW Lexikon"
panel:Hide()

local function text(parent, str, fontObj, x, y)
  local fs = parent:CreateFontString(nil, "ARTWORK", fontObj or "GameFontHighlight")
  fs:SetPoint("TOPLEFT", x, y); fs:SetJustifyH("LEFT"); fs:SetText(str)
  return fs
end

text(panel, "WoW Lexikon", "GameFontNormalLarge", 16, -16)
text(panel, "Englische Texte im Spiel verstehen – Übersetzung, Lautschrift und Aussprache.", "GameFontHighlightSmall", 16, -40)

-- Wann erscheint die Wortkarte beim Darüberfahren?
text(panel, "Wortkarte beim Darüberfahren zeigen:", "GameFontNormal", 16, -76)
local MODES = { { "taste", "mit der Übersetzen-Taste (empfohlen)" }, { "alt", "nur solange Alt gehalten wird" }, { "strg", "nur solange Strg gehalten wird" },
                { "umschalt", "nur solange Umschalt gehalten wird" }, { "immer", "immer" }, { "klick", "nie – nur beim Anklicken" } }
local radios = {}
for i, m in ipairs(MODES) do
  local cb = CreateFrame("CheckButton", nil, panel, "UICheckButtonTemplate")
  cb:SetPoint("TOPLEFT", 20, -96 - (i - 1) * 26)
  local label = cb.Text or cb.text or _G[(cb:GetName() or "") .. "Text"]
  if type(label) ~= "table" then label = cb:CreateFontString(nil, "ARTWORK", "GameFontHighlight"); label:SetPoint("LEFT", cb, "RIGHT", 2, 0) end
  label:SetText(m[2])
  cb.mode = m[1]
  cb:SetScript("OnClick", function(self)
    WowLexikonDB.options.hoverMode = self.mode
    for _, r in ipairs(radios) do r:SetChecked(r.mode == self.mode) end
  end)
  radios[#radios + 1] = cb
end

-- Schalter
local SWITCHES = {
  { "autoPanel", "Wörterbuch-Fenster automatisch neben Quests, NPC-Gesprächen und Büchern öffnen" },
  { "tooltipNames", "Deutsche Namen in Tooltips anzeigen (Gegenstände, NPCs)" },
  { "hoverTip", "Beim Darüberfahren kleine Übersetzung zeigen" },
  { "showGerman", "Deutschen Text im Wörterbuch aufgeklappt zeigen" },
  { "speakOnClick", "Wort beim Anklicken vorlesen" },
}
local checks = {}
local y = -96 - #MODES * 26 - 16
for _, s in ipairs(SWITCHES) do
  local cb = CreateFrame("CheckButton", nil, panel, "UICheckButtonTemplate")
  cb:SetPoint("TOPLEFT", 16, y)
  local label = cb.Text or cb.text
  if type(label) ~= "table" then label = cb:CreateFontString(nil, "ARTWORK", "GameFontHighlight"); label:SetPoint("LEFT", cb, "RIGHT", 2, 0) end
  label:SetText(s[2])
  cb.key = s[1]
  cb:SetScript("OnClick", function(self) WowLexikonDB.options[self.key] = self:GetChecked() and true or false end)
  checks[#checks + 1] = cb
  y = y - 28
end

-- Tasten
text(panel, "Tasten", "GameFontNormal", 16, y - 10)
local keyInfo = text(panel, "", "GameFontHighlightSmall", 16, y - 30)
keyInfo:SetWidth(560)
local function keyName(action) local k = GetBindingKey and GetBindingKey(action); return k and WL.KeyLabel(k) or "nicht belegt" end
local capture = CreateFrame("Button", nil, panel, "UIPanelButtonTemplate")
capture:SetSize(220, 24); capture:SetPoint("TOPLEFT", 16, y - 104); capture:SetText("Übersetzen-Taste festlegen …")
capture:SetScript("OnClick", function() if WL.CaptureKey then WL.CaptureKey() end end)
local bind = CreateFrame("Button", nil, panel, "UIPanelButtonTemplate")
bind:SetSize(200, 24); bind:SetPoint("LEFT", capture, "RIGHT", 8, 0); bind:SetText("Tastenbelegung öffnen")
bind:SetScript("OnClick", function()
  if SettingsPanel and SettingsPanel.Open then pcall(SettingsPanel.Open, SettingsPanel)
  elseif KeyBindingFrame_LoadUI then KeyBindingFrame_LoadUI(); if KeyBindingFrame then ShowUIPanel(KeyBindingFrame) end end
end)

local function refresh()
  local o = WowLexikonDB and WowLexikonDB.options
  if not o then return end
  for _, r in ipairs(radios) do r:SetChecked(r.mode == (o.hoverMode or "taste")) end
  for _, c in ipairs(checks) do c:SetChecked(o[c.key] and true or false) end
  keyInfo:SetText(("Übersetzen-Taste: %s   ·   Chat: %s\nFenster ein/aus: %s   ·   Wort suchen: %s\nDie übrigen Funktionen gibt es auch über das Buch an der Minikarte.")
    :format(keyName("WOWLEXIKON_UNDERMOUSE"), keyName("WOWLEXIKON_CHAT"), keyName("WOWLEXIKON_PANEL"), keyName("WOWLEXIKON_SEARCH")))
end
panel:SetScript("OnShow", refresh)
WL.RefreshOptions = refresh

-- Im Spielmenü eintragen (neues und altes Einstellungssystem)
local category
if Settings and Settings.RegisterCanvasLayoutCategory then
  category = Settings.RegisterCanvasLayoutCategory(panel, panel.name)
  Settings.RegisterAddOnCategory(category)
elseif InterfaceOptions_AddCategory then
  InterfaceOptions_AddCategory(panel)
end

function WL.OpenOptions()
  if category and Settings and Settings.OpenToCategory then
    Settings.OpenToCategory(category.ID or category:GetID())
  elseif InterfaceOptionsFrame_OpenToCategory then
    InterfaceOptionsFrame_OpenToCategory(panel); InterfaceOptionsFrame_OpenToCategory(panel)
  else
    panel:SetParent(UIParent); panel:SetPoint("CENTER"); panel:SetSize(600, 480); panel:Show()
  end
end

end)
