-- WoW Lexikon – Questtext direkt im Questfenster zwischen Englisch und Deutsch umschalten (Knopf „Deutsch“ / „English“)
WowLexikon.Try("Questfenster Deutsch", function()
local WL = WowLexikon

-- Welche Textfelder des Spiels gehören zu welchem Teil der Quest (Namen je nach Spielversion verschieden)
local FIELDS = {
  T = { "QuestInfoTitleHeader", "QuestTitleText", "QuestProgressTitleText" },
  D = { "QuestInfoDescriptionText", "QuestDescription" },
  O = { "QuestInfoObjectivesText", "QuestObjectiveText" },
  Q = { "QuestProgressText" },
  R = { "QuestInfoRewardText", "QuestRewardText" },
}

local state = { id = 0, kinds = {}, german = false, saved = {} }
WL.questLang = state

local function anyGerman(q, kinds)
  if not q then return false end
  for _, k in ipairs(kinds) do if q[k] and q[k] ~= "" then return true end end
  return false
end

local btn, mapBtn
local function setLabel()
  for _, b in ipairs({ btn, mapBtn }) do if b then b:SetText(state.german and "English" or "Deutsch") end end
end

--- Englisch ↔ Deutsch im Questfenster selbst. Gibt false zurück, wenn es keinen deutschen Text gibt.
function WL.SwapQuestLanguage(toGerman)
  if toGerman then
    local q = WL.QuestText(state.id)
    if not anyGerman(q, state.kinds) then return false end
    local kinds = { "T" }
    for _, k in ipairs(state.kinds) do kinds[#kinds + 1] = k end
    local done = false
    for _, k in ipairs(kinds) do
      if q[k] and q[k] ~= "" then
        for _, name in ipairs(FIELDS[k]) do
          local fs = _G[name]
          if type(fs) == "table" and fs.SetText and fs.GetText and fs:IsVisible() then
            if state.saved[fs] == nil then state.saved[fs] = fs:GetText() or "" end
            fs:SetText(q[k])
            done = true
          end
        end
      end
    end
    state.german = done
  else
    for fs, txt in pairs(state.saved) do pcall(fs.SetText, fs, txt) end
    wipe(state.saved)
    state.german = false
  end
  setLabel()
  return state.german
end

local function makeButton(name, parent)
  local b = CreateFrame("Button", name, parent, "UIPanelButtonTemplate")
  b:SetSize(84, 22); b:Hide()
  b:SetFrameLevel((parent:GetFrameLevel() or 1) + 10)
  b:SetScript("OnClick", function() WL.SwapQuestLanguage(not state.german) end)
  b:SetScript("OnEnter", function(self)
    local _, official = WL.QuestText(state.id)
    GameTooltip:SetOwner(self, "ANCHOR_TOP")
    GameTooltip:AddLine(state.german and "Zurück zum englischen Text" or "Questtext auf Deutsch")
    GameTooltip:AddLine(official and "Offizieller deutscher Text aus dem Spiel." or "Übersetzt vom Begleit-Programm.", 1, 1, 1, true)
    GameTooltip:Show()
  end)
  b:SetScript("OnLeave", function() GameTooltip:Hide() end)
  return b
end

if QuestFrame then
  btn = makeButton("WowLexikonQuestLang", QuestFrame)
  btn:SetPoint("BOTTOM", QuestFrame, "BOTTOM", 0, 4)
end

-- Questlog in der Karte („Map & Quest Log“): Knopf oben rechts in den Questdetails (neben „Back“)
--- Wird vom Wörterbuch aufgerufen, sobald dort eine Quest angezeigt wird
function WL.MapQuestShown(id)
  local df = QuestMapFrame and type(QuestMapFrame.DetailsFrame) == "table" and QuestMapFrame.DetailsFrame
  if not df then return end
  if not mapBtn then
    mapBtn = makeButton("WowLexikonMapQuestLang", df)
    mapBtn:SetPoint("TOPRIGHT", df, "TOPRIGHT", -6, -2)
    df:HookScript("OnHide", function() wipe(state.saved); state.german = false; mapBtn:Hide() end)
  end
  wipe(state.saved); state.german = false
  state.id = id or 0
  state.kinds = { "D", "O" }
  setLabel()
  if anyGerman(WL.QuestText(state.id), state.kinds) then mapBtn:Show() else mapBtn:Hide() end
end

local ev = CreateFrame("Frame")
for _, e in ipairs({ "QUEST_DETAIL", "QUEST_PROGRESS", "QUEST_COMPLETE", "QUEST_FINISHED", "QUEST_GREETING" }) do pcall(ev.RegisterEvent, ev, e) end
ev:SetScript("OnEvent", function(_, event)
  -- Das Spiel schreibt die Texte bei jedem Schritt neu – also immer wieder auf Englisch beginnen
  wipe(state.saved); state.german = false
  if event == "QUEST_FINISHED" or event == "QUEST_GREETING" then
    if btn then btn:Hide() end
    return
  end
  state.id = (GetQuestID and GetQuestID()) or 0
  state.kinds = event == "QUEST_DETAIL" and { "D", "O" } or event == "QUEST_PROGRESS" and { "Q" } or { "R" }
  setLabel()
  if btn then
    if anyGerman(WL.QuestText(state.id), state.kinds) then btn:Show() else btn:Hide() end
  end
end)
end)
