USE_GAME_TEMPLATE = os.getenv and os.getenv('WOW_TEMPLATE') == '1' or false
-- Minimale Nachbildung der WoW-Oberfläche für Tests außerhalb des Spiels
local frames, events = {}, {}
local spoken = {}
_G.SPOKEN = spoken
_G.PRINTED = {}
print_orig = print
print = function(...) local t = {} for i = 1, select("#", ...) do t[#t+1] = tostring(select(i, ...)) end PRINTED[#PRINTED+1] = table.concat(t, " ") end

function strsplit(sep, s)
  local out, pos = {}, 1
  while true do
    local a, b = string.find(s, sep, pos, true)
    if not a then out[#out+1] = s:sub(pos); break end
    out[#out+1] = s:sub(pos, a - 1); pos = b + 1
  end
  return table.unpack(out)
end
unpack = table.unpack
function wipe(t) for k in pairs(t) do t[k] = nil end return t end
tinsert = table.insert
UISpecialFrames = {}
function hooksecurefunc(name, fn) local old = _G[name]; _G[name] = function(...) local r = old and old(...) ; fn(...) ; return r end end
function GetBuildInfo() return "1.15.8", "99999", "Nov 1 2026", 11508 end
function GetLocale() return "enUS" end
function time() return 1700000000 end
function GetCursorPosition() return 500, 400 end
function IsShiftKeyDown() return false end
SlashCmdList = {}

local function newObj(kind)
  local o = { kind = kind, shown = (kind ~= "Frame"), scripts = {}, text = "", points = {} }
  return setmetatable(o, { __index = function(t, k)
    local m = rawget(_G, "MOCK_" .. k)
    if m then return m end
    return function() end
  end })
end
function MOCK_SetText(self, t) self.text = t or "" end
function MOCK_GetText(self) return self.text end
function MOCK_GetStringHeight(self) return 14 * (1 + select(2, (self.text or ""):gsub("\n", ""))) end
function MOCK_Show(self) self.shown = true end
function MOCK_Hide(self) self.shown = false end
function MOCK_IsShown(self) return self.shown end
function MOCK_SetScript(self, n, f) self.scripts[n] = f end
function MOCK_HookScript(self, n, f) self.scripts["hook_" .. n] = f end
function MOCK_GetScript(self, n) return self.scripts[n] end
function MOCK_RegisterEvent(self, e) events[e] = events[e] or {}; table.insert(events[e], self) end
function MOCK_CreateFontString(self) return newObj("FontString") end
function MOCK_CreateTexture(self) return newObj("Texture") end
function MOCK_GetEffectiveScale() return 1 end
function CreateFrame(kind, name, parent, template)
  local f = newObj(kind); f.name = name; f.shown = false
  if template == "ButtonFrameTemplate" and USE_GAME_TEMPLATE then
    f.Inset = newObj("Frame"); f.Inset.shown = true
    f.TitleText = newObj("FontString")
    f.PortraitContainer = { portrait = newObj("Texture") }
    f.NineSlice = {}
  end
  if name then _G[name] = f end
  frames[#frames+1] = f; ALLFRAMES = frames
  return f
end
UIParent = newObj("Frame"); UIParent.shown = true
GameTooltip = newObj("GameTooltip"); GameTooltip.name = "GameTooltip"; GameTooltip.shown = false
GossipFrame = newObj("Frame"); GossipFrame.shown = true
ItemTextFrame = newObj("Frame"); ItemTextFrame.shown = true
WorldFrame = newObj("Frame")
QuestFrame = newObj("Frame"); QuestFrame.shown = true
QuestLogFrame = newObj("Frame"); QuestLogFrame.shown = false
BackdropTemplateMixin = {}

-- Sprachausgabe
C_VoiceChat = {
  GetTtsVoices = function() return { { voiceID = 0, name = "Microsoft Hedda - German" }, { voiceID = 1, name = "Microsoft Zira Desktop - English (United States)" } } end,
  -- Neuer Unterbau (ab 12.0): SpeakText(stimme, text, tempo, lautstärke). TTS_OLD = true spielt den alten nach.
  SpeakText = function(voice, text, a, b, c)
    local e
    if TTS_OLD then e = { voice = voice, text = text, dest = a, rate = b, vol = c, audible = (a == 1 and (c or 0) > 0) }
    else e = { voice = voice, text = text, rate = a, vol = b, audible = ((b or 0) > 0) } end
    spoken[#spoken+1] = e
    if e.audible then FireEvent("VOICE_CHAT_TTS_PLAYBACK_STARTED", 1, #spoken, 800, 1) end
  end,
  StopSpeakingText = function() end,
}
Enum = { VoiceTtsDestination = { LocalPlayback = 1 } }

function FireEvent(e, ...) for _, f in ipairs(events[e] or {}) do if f.scripts.OnEvent then f.scripts.OnEvent(f, e, ...) end end end

-- Questfenster-Daten
QUEST = {}
function GetQuestID() return QUEST.id end
function GetTitleText() return QUEST.title end
function GetQuestText() return QUEST.desc end
function GetObjectiveText() return QUEST.obj end
function GetProgressText() return QUEST.progress end
function GetRewardText() return QUEST.reward end

-- Halte-Tasten
KEYS = { alt = false, ctrl = false, shift = false }
function IsAltKeyDown() return KEYS.alt end
function IsControlKeyDown() return KEYS.ctrl end
function IsShiftKeyDown() return KEYS.shift end
function MOCK_GetObjectType(self) return self.kind end
function MOCK_IsVisible(self) return self.shown end
function MOCK_GetRegions(self) return table.unpack(rawget(self, "regions") or {}) end
function MOCK_GetChildren(self) return table.unpack(rawget(self, "children") or {}) end
function MOCK_NumLines(self) return rawget(self, "lines") or 0 end
function MOCK_GetName(self) return self.name end
function MOCK_IsMouseOver(self) return false end
function MOCK_GetVerticalScrollRange() return 0 end
function MOCK_GetChecked(self) return self.checked end
function MOCK_SetChecked(self, v) self.checked = v end
C_Timer = { After = function(_, f) f() end }
function UnitName(u) return u == "npc" and "Innkeeper Farley" or "Spieler" end

-- Verhalten des neueren Spiel-Unterbaus (Classic Era 1.15 / WoW Forever):
function MOCK_SetFont(self, path, size, flags)
  if flags == nil then error("Usage: FontString:SetFont(fontFile, height, flags)", 2) end
end
function MOCK_HookScript(self, n, f)
  if n:find("^OnTooltipSet") then error(tostring(self.name) .. " doesn't have a \"" .. n .. "\" script", 2) end
  local old = self.scripts["hook_" .. n]
  self.scripts["hook_" .. n] = old and function(...) old(...); f(...) end or f
end
TOOLTIP_POSTCALLS = {}
TooltipDataProcessor = { AddTooltipPostCall = function(kind, f) TOOLTIP_POSTCALLS[kind] = f end }
Enum.TooltipDataType = { Item = 0, Unit = 2 }
-- Minikarte und Tastenbelegung
Minimap = newObj("Frame"); Minimap.shown = true
function MOCK_GetCenter() return 100, 100 end
BINDINGS = { W = "MOVEFORWARD" }
function GetBindingKey(action) for k, a in pairs(BINDINGS) do if a == action then return k end end return nil end
function GetBindingAction(key) return BINDINGS[key] or "" end
function SetBinding(key, action) BINDINGS[key] = action end
function SaveBindings() SAVED_BINDINGS = true end
function GetCurrentBindingSet() return 1 end
function InCombatLockdown() return false end
BINDING_NAME_MOVEFORWARD = "Vorwärts bewegen"
-- Questfenster-Textfelder (für den Deutsch/English-Knopf)
for _, n in ipairs({ "QuestInfoTitleHeader", "QuestInfoDescriptionText", "QuestInfoObjectivesText", "QuestProgressText", "QuestInfoRewardText" }) do
  local f = newObj("FontString"); f.shown = true; _G[n] = f
end

-- Neues Questlog in der Karte („Map & Quest Log“, WoW Forever)
WorldMapFrame = newObj("Frame"); WorldMapFrame.shown = true; WorldMapFrame.name = "WorldMapFrame"
QuestMapFrame = newObj("Frame"); QuestMapFrame.shown = true
QuestMapFrame.DetailsFrame = newObj("Frame"); QuestMapFrame.DetailsFrame.shown = false
function QuestMapFrame_ShowQuestDetails(id) QuestMapFrame.DetailsFrame.shown = true end
MAPQUEST = { [35] = { t = "Further Concerns", d = "If you are concerned that the rumors are true, speak with Guard Thomas.", o = "Speak with Guard Thomas." } }
C_QuestLog = {
  GetSelectedQuest = function() return 35 end,
  GetTitleForQuestID = function(id) return MAPQUEST[id] and MAPQUEST[id].t end,
  GetLogIndexForQuestID = function(id) return id end,
}
function GetQuestLogQuestText(idx) local q = MAPQUEST[idx]; if q then return q.d, q.o end end
