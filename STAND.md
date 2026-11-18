# WoW Lexikon – Stand (18.11.2026)

Projektordner: `wow-woerterbuch` (beim Entwickler in OneDrive). GitHub: `Baougam/wow-lexikon` (geplant).

Zuerst lesen, wer am Projekt weiterarbeitet (Mensch oder KI). Nach jeder größeren Änderung aktualisieren.

## Worum es geht

Hilfe beim Englischlernen in **WoW Forever** (Blizzards offizielles „Classic+“, gestartet am 4.11.2026, Stufe 60,
Classic-Welt plus neue Gebiete wie Riverglades, Hyjal, Zephras Isle). Der Spieler spielt auf **Englisch** und will
englische Wörter und Texte im Spiel sofort verstehen: deutsche Übersetzung, Lautschrift, Aussprache zum Anhören,
Beispielsätze – ohne aus dem Spiel herausgerissen zu werden. WoW läuft auf einem **anderen PC** als dem Entwicklungs-PC.

## Die zwei Teile

### 1. Addon `addon/WowLexikon` (im Spiel)
- Pergament-Fenster neben Questfenster, Questlog, NPC-Gesprächen, Büchern/Briefen: englischer Text mit anklickbaren
  Wörtern, darunter der **offizielle deutsche Questtext** (über die Quest-ID, keine Lesefehler).
- **Eine Übersetzen-Taste für alles** (Bindung `WOWLEXIKON_UNDERMOUSE`, vom Spieler im Begrüßungsfenster festgelegt,
  `Welcome.lua`; Alt allein war dem Spieler zu sehr vorbelegt): Maus auf Wort → Wortkarte (Übersetzung, IPA,
  „gesprochen etwa: bridsch“, Beispiel; deutsches Wort leuchtet im deutschen Text auf); Maus auf Gegenstand/Zauber/
  Questziele/Fenster → Text im Wörterbuch-Fenster; Spielwelt → Hinweis auf Kasten im Begleit-Programm.
  Halten (Alt/Strg/Umschalt/immer) bleibt als Option (`hoverMode`, Standard `taste`). Klick = Vorlesen.
- Übersetzen-Taste: beim ersten Login automatisch die erste **freie** aus Alt+T, Strg+T, Alt+Strg+T, Alt+Umschalt+T,
  Strg+Umschalt+T (`WL.AutoKey`, nie überschreiben); ändern über Begrüßung/Buch („Andere Taste …“, warnt bei belegten).
- **Lexikon-Buch** (`Book.lua`) als Spielmenü: Reiter Suchen, Vokabeln (Liste + Lernkarten „Gewusst/Nochmal“), Chat,
  Hilfe. Öffnen: Buch-Knopf an der Minikarte, Knopf „WoW Lexikon“ unter dem Esc-Menü, `/lex`.
- Fenster im Stil des Spiels: `WL.MakeWindow` nutzt `ButtonFrameTemplate` (Porträt, Titel, roter Schließen-Knopf),
  Pergament innen; Rückfall auf eigenes Pergament, wenn die Vorlage fehlt. Beide Wege getestet (`WOW_TEMPLATE=1`).
- WoW Forever = Produkt `wowf`, Version **1.60.1 → Interface 16001** (wago.tools), fest in den TOCs.
- **Im Spiel bestätigt (16.11.)**: Addon lädt, Wörterbuch-Fenster neben dem Questgeber, Namen erkannt.
- Wörterbuch-Fenster **standardmäßig eingeklappt**: rotes Lesezeichen („DE“, Buch-Symbol) am rechten Rand von
  Quest-/Gesprächs-/Buchfenster; Klick oder Übersetzen-Taste über dem Fenster → gleitet heraus (0,2 s). Über einem
  Gegenstand im Questfenster zeigt die Taste stattdessen die Wortkarte. Beim Schließen des Spielfensters wieder zu.
- Keine Bedienungs-Hinweise mehr im Fenster; Bildlaufleiste nur bei Bedarf.
- Pergament: Grafik wird zur Laufzeit aus dem Questfenster übernommen (`findGameParchment`), sonst Farbe + dunkle Ränder;
  `/lex textur` zeigt, was gefunden wurde.
- Wortkarte knapp: bei mehreren Bedeutungen nur die im (deutschen) Satz gemeinte (`WL.MatchSense`), Hinweis
  „so ist es hier im Satz gemeint“; **Mehr** zeigt alle Bedeutungen, Beispiel, Quelle (Zustand gemerkt).
- Geplant (Daten gefunden, noch nicht gebaut): wago.tools liefert Forever-Spieldateien auf Deutsch – AreaTable
  (Gebiete, auch neue: Riverglades → Flusslande), ItemSparse (19.300 Gegenstände inkl. Beschreibung), SpellName/Spell,
  GlobalStrings (27.500 Oberflächentexte), QuestV2 (Quest-IDs). Dazu AzerothCore broadcast_text_locale, npc_text_locale,
  page_text_locale, gossip_menu_option_locale. Ideen: Gebietsnamen, Inventar-Tooltips komplett deutsch, Systemmeldungen.
- **Darüberfahren = kleines Fenster** (`WowLexikonTip`, `WL.ShowTip`): nur die direkte Übersetzung (passende Bedeutung
  bzw. höchstens zwei), dunkel wie ein Spiel-Tooltip; **Klick = Wortkarte** mit Aussprache, Vorlesen, Merken, Mehr.
  Option `hoverTip` (Standard an, `/lex tipp`). Die alten Halte-Modi (`hoverMode`) zeigen weiterhin die ganze Karte.
- **Deutscher Text im Wörterbuch zugeklappt** (Zeile „<Questtitel> · anzeigen ›“, `WowLexikonGermanToggle`), Zustand
  in `options.showGerman` gemerkt (`/lex deutsch`). Die Bedeutungs-Erkennung nutzt den Text auch zugeklappt. Kurze
  Namen (Übersetzen-Taste auf Gegenstand) stehen immer offen (`deAlways`). Ohne deutschen Text: nur ein Hinweis.
- **Immersiv** (`Immersive.lua`): Knopf „Deutsch“/„English“ unten im Questfenster tauscht die Texte im Questfenster
  selbst (QuestInfo*-Felder), nur wenn es deutschen Text gibt; jeder neue Questschritt beginnt auf Englisch.
- **Vorlesen repariert**: Seit WoW 12.0 (Jan. 2026) ist `C_VoiceChat.SpeakText(stimme, text, tempo, lautstärke)` – der alte
  Aufruf mit Ziel-Argument lief dort mit Lautstärke 0 (stumm, ohne Fehler). Jetzt: neue Form zuerst, ohne Meldung
  `VOICE_CHAT_TTS_PLAYBACK_STARTED` binnen 1,5 s die alte; was klappt, steht in `WowLexikonDB.tts` (je Build).
  Lautstärke aus `C_TTSSettings`. `/lex vorlesen test` (Stimmen + Probesatz), `/lex stimme` (nächste Stimme).
  Im echten Spiel noch zu bestätigen.
- **Mehrsprachigkeit (Grundausstattung)**: `Locale.lua` – `WL.T("deutscher Text")` für Oberflächentexte (Tabelle je
  Sprache in `WL.locales`, Englisch als Vorlage), `WL.LANGS` mit Zielsprachen (de fertig; fr/es/it/pt/ru/ko/zh vorbereitet,
  je mit Blizzard-Sprachversion), `options.uiLang`/`targetLang`, `WL.WordTable()` (Wörter je Zielsprache in
  `WowLexikon_Words_<kürzel>`, gelernte in `Learned.words_<kürzel>`). Fehlt etwas, gilt Deutsch. Noch zu tun: alle
  Oberflächentexte über `WL.T` führen, Wörterlisten und Spieldaten je Sprache im Begleit-Programm erzeugen.
- **Questlog in der Karte** („Map & Quest Log“ – so sieht das Questlog in WoW Forever aus, Screenshot 18.11.): Haken auf
  `QuestMapFrame_ShowQuestDetails` (`WL.HookQuestMap`, auch nachträglich per ADDON_LOADED), Texte über
  `C_QuestLog` + `GetQuestLogQuestText(index)`, Lesezeichen rechts an `WorldMapFrame`; zu beim Schließen der Details/Karte.
  Knopf „Deutsch“/„English“ oben rechts in `QuestMapFrame.DetailsFrame` (`WL.MapQuestShown`). Im Spiel noch zu prüfen
  (Lage des Knopfs neben „Back“, Karte im Vollbild).
- Deutsche Namen automatisch in Tooltips (NPCs, Gegenstände).
- Tasten änderbar unter Tastenbelegung → WoW Lexikon; Halte-Taste und Schalter unter Optionen → AddOns (`Options.lua`).
- Merkt sich unbekannte Wörter, gesehene Questtexte und die Spielversion in `WowLexikonDB` (SavedVariables).
- Offline: 14.513 Wörter (`Data/Words*.lua`), Namen + Questtexte im Lade-bei-Bedarf-Addon `WowLexikon_Data`.
- Freie Schrift mit Lautschrift-Zeichen: `Fonts/GentiumBookPlus-Regular.ttf` (SIL Open Font License) – die WoW-Schriften
  haben keine IPA-Zeichen.

### 2. Begleit-Programm `WowLexikon.exe` (Windows, C#/WPF)
- **Addon-Begleiter**: findet den WoW-Ordner (laufendes Spiel → Battle.net-Registry/`product.db` → Suche auf allen
  Laufwerken nach Ordnern mit „WoW/Warcraft“ im Namen, 2 Ebenen tief; der eigene Programmordner zählt nicht) und merkt
  ihn sich. Installiert/aktualisiert das Addon in **alle** gefundenen Spielversionen (Unterordner wie `_classic_era_`
  oder einfach `forever`), schreibt `WowLexikon_Data` aus den Spieldaten. Beim Spieler liegt WoW auf **D:** in einem
  eigenen Ordner; Forever-Ordner dort: `…\forever\Interface\AddOns`. Neue Addons erkennt WoW nur nach Spielneustart.
- **Lernt dazu**: Nach jedem Ausloggen (Dateiüberwachung auf `WTF\…\SavedVariables\WowLexikon.lua`) und alle 6 Stunden
  liest es, was das Addon nicht kannte, übersetzt es online (MyMemory / DeepL / freie KI) und schreibt
  `Interface\AddOns\WowLexikon\Data\Learned.lua`. Erkennt Spiel-Updates (Build-Nummer) und in Forever geänderte Questtexte.
- **Updates**: prüft eine `manifest.json` (Einstellung „Update-Quelle“), lädt neues Addon und neues Programm
  (Programm wird beim nächsten Start ersetzt).
- **Bildschirm-Übersetzung** (das Ursprungsprogramm): Strg + Umschalt halten, Wort einrahmen/anklicken, optional zweiter
  Rahmen = Kontext; Windows-Texterkennung, Karte im Questlog-/Tooltip-/Modern-Stil, erkennt Questsätze und zeigt den
  offiziellen deutschen Satz. Für Text, den kein Addon lesen kann (3D-Welt).
- Tray-Symbol: Doppelklick = Wort eintippen; Menü: Einstellungen, Abgleich, Vokabelliste, Glossar.

## Daten und Quellen

| Datei | Inhalt | Quelle / Lizenz | weitergeben? |
|---|---|---|---|
| `data/questwoerter.txt` | 3.000 häufigste Questwörter, von Hand übersetzt, mit IPA | eigene Arbeit | ja |
| `data/glossar-wow.json` | ~580 WoW-Begriffe, Chat-Slang, Orte mit dt. Namen, Beispiele | eigene Arbeit | ja |
| `data/woerter-auto.txt` | ~10.900 weitere Wörter, automatisch | Ding (TU Chemnitz, GPL) + CMU-Aussprachewörterbuch (BSD) | ja (GPL beachten) |
| `data/quests-de.txt.gz` | 4.725 Classic-Quests, 18.747 Texte EN↔DE | AzerothCore-Datenbank; **Texte gehören Blizzard** | **nein** |
| `data/namen-de.txt.gz` | 75.375 Namen EN↔DE | AzerothCore; **Blizzard** | **nein** |

Blizzard-Texte werden nie verteilt: Die weitergegebene Programmversion erzeugt sie auf jedem PC selbst
(„Spieldaten erzeugen“, `GameDataBuilder.cs`, ~120 MB Download, 15 s).
Quests aus Burning Crusade / Lich King sind herausgefiltert (WoW Forever basiert auf Classic).
Die **neuen Forever-Quests** gibt es in keiner öffentlichen Datenbank → werden vom Begleit-Programm online übersetzt.
Googles inoffizielle Übersetzungs-Schnittstelle ist von diesem Anschluss aus gesperrt; MyMemory funktioniert ohne Schlüssel.

## Ordner

```
addon/WowLexikon/        Addon (Core.lua, UI.lua, Options.lua, Bindings.xml, Data/, Fonts/)
addon/WowLexikon_Data/   lokal erzeugt (git-ignoriert)
src/                     Begleit-Programm (C# 5, .NET Framework 4.8)
data/                    Wörter, Glossar, Spieldaten
tools/daten-bauen.mjs    Spieldaten aus AzerothCore (node)
tools/woerter-bauen.mjs  großer Wortschatz aus Ding + CMU
tools/addon-daten.mjs    Lua-Datendateien fürs Addon
tools/veroeffentlichen.ps1  Release (Programm ohne Blizzard-Texte, Addon-ZIP, manifest.json)
tools/addon-test/        Addon-Test in nachgebauter WoW-Umgebung (fengari + luaparse)
```

## Bauen und testen

- **Programm bauen**: `.\build.ps1` (nur Windows-Bordmittel: eingebauter C#-Compiler; das .NET SDK zu installieren war
  auf dem Entwicklungs-PC nicht erlaubt). `-OhneSpieldaten` für die weitergebbare Version.
- **Programm testen**: `bin\WowLexikon.exe --selftest <ordner>` → `selbsttest.txt` + Vorschaubilder.
  Stand: **58/58 OK** (56 mit Spieldaten-Erzeugung, wenn `<ordner>\gdb\sql` die SQL-Dateien enthält).
- **Addon testen**: `cd tools\addon-test; node run.mjs ..\..\addon` → **85/85 OK**, auch mit `WOW_TEMPLATE=1` (Syntax Lua 5.1 + Abläufe:
  Mini-Übersetzung, Karte, Deutsch zuklappen, Deutsch-Knopf im Questfenster, Vorlesen alt/neu, Sprachen, Hervorhebung, Gespräch, Buch, Alt+T, Alt+C, Namen-Tooltip, Aufzeichnen).
- **Daten neu bauen**: `node tools/daten-bauen.mjs` → `node tools/woerter-bauen.mjs` → `node tools/addon-daten.mjs` → `.\build.ps1`.

## Stand

**Fertig und getestet (außerhalb des Spiels):** alles oben Beschriebene.

**Noch nie im echten Spiel gelaufen.** WoW ist auf dem Entwicklungs-PC nicht installiert. Unsicher sind besonders:
- `## Interface:`-Nummer: wird jetzt **automatisch** vom Begleit-Programm gesetzt (`AddonSync.UpdateTocVersions`):
  aus `.build.info` im WoW-Ordner (1.15.8 → 11508) und aus der Nummer, die das Addon im Spiel meldet
  (`WowLexikonDB.build.toc`). Gemeldete Nummer steht vorne. Bei Installation, Spieldaten-Schreiben und jedem Abgleich.
- Ladefehler: Oberfläche und Einstellungen laufen abgesichert (`WL.Try`), `/lex` gibt es immer, `/lex fehler` zeigt Ursachen.
  Behoben: `SetFont` braucht im neuen Unterbau 3 Argumente; `OnTooltipSet*`-Hooks gibt es dort nicht mehr.
- Spiel-Schnittstellen, die je nach Client abweichen können: `C_VoiceChat.SpeakText` (jetzt automatisch erkannt), Tooltip-Hooks
  (`TooltipDataProcessor` vs. `OnTooltipSetItem`), `GetMouseFoci`/`GetMouseFocus`, Hyperlinks in eigenen Frames,
  Einstellungs-API (`Settings` vs. `InterfaceOptions_AddCategory`), Ankerpositionen neben QuestFrame/GossipFrame.
- Ob die Spielschrift `Fonts\FRIZQT__.TTF` als Datei im WoW-Ordner liegt (für den Spiel-Look des Programms).
- Ordnername von WoW Forever in der Blizzard-Installation (Suche nimmt `_*_`-Unterordner, bevorzugt „forever“).

## Weitergabe (vorbereitet)

- Addon für **CurseForge** vorbereitet: Lizenz GPL-3.0 (`addon/WowLexikon/LICENSE.txt`), Quellen (`CREDITS.txt`),
  Beschreibung/Änderungen/Logo in `curseforge/`, Paket `release/WowLexikon-<Version>.zip` (Pfade mit „/“, `tools/zip-ordner.ps1`).
- Ohne Begleit-Programm zeigt das Addon Hinweise auf das Programm statt „wird übersetzt“ (`WL.HasCompanion`).
- Begleit-Programm über **GitHub Releases**: `tools\veroeffentlichen.ps1 -Version x.y.z -Hochladen` baut, trägt die
  Update-Adresse `https://github.com/<repo>/releases/latest/download/manifest.json` fest ein (`Program.DefaultUpdateUrl`),
  legt per GitHub-API ein Release an und hängt exe, Addon-ZIPs und manifest.json an. Repo wird in
  `tools/release-config.json` gemerkt, Schlüssel in `tools/.github-token` (git-ignoriert). Probelauf ohne Schlüssel ok;
  echter Upload noch nie gelaufen (kein GitHub-Konto bisher).
- Nutzer: Updates automatisch (Start, 6 h) + Knopf **„Nach Updates suchen“** (Einstellungen, Tray-Menü); nach
  Programm-Update Frage „Jetzt neu starten?“.
- Noch offen: GitHub-Konto/Repo des Spielers anlegen; Download-Link in `curseforge/BESCHREIBUNG.md` eintragen.

## Nächste Schritte

1. Auf dem Spiele-PC installieren (Anleitung README), echtes Verhalten prüfen, Interface-Nummer eintragen.
2. Fehler aus dem echten Spiel beheben (Screenshots vom Spieler).
3. Update-Quelle einrichten (GitHub-Projekt des Spielers), `tools/veroeffentlichen.ps1` nutzen.
4. Ideen: offizielle deutsche Texte auch für NPC-Gespräche und Bücher (AzerothCore `npc_text_locale`,
   `page_text_locale`, `broadcast_text_locale`); Qualität von `woerter-auto.txt` verbessern; Vokabeltrainer im Addon.

## Entscheidungen

- Addon ohne Internet (WoW erlaubt das nicht) + Begleit-Programm für alles Online; kein „Pixel-Bridge“-Trick (Grauzone).
- Darüberfahren zeigt nur ein kleines Übersetzungsfenster (stört nicht); die große Wortkarte kommt per Klick oder Übersetzen-Taste.
- Design: Questlog-Pergament wie im Spiel, Original-Bausteine des Spiels (Schrift, rote Knöpfe, Dialograhmen).
- Keine Blizzard-Texte verteilen; jeder PC erzeugt sie selbst.
