# Anleitung

## Teil A – Bei dir zum Laufen bringen

Du brauchst nur **eine Datei**: `C:\Projects\wow-woerterbuch\bin\WowLexikon.exe` (5 MB). Darin stecken das Addon,
alle Wörter und die deutschen Questtexte und Namen.

**1. Auf den Spiele-PC kopieren**
USB-Stick, Cloud oder Netzwerk. Dort in einen festen Ordner legen, z. B. `C:\WowLexikon\`.

**2. Starten**
Doppelklick. Kommt „Der Computer wurde durch Windows geschützt“: **Weitere Informationen → Trotzdem ausführen**
(das Programm hat keine gekaufte digitale Unterschrift). Beim ersten Start öffnen sich die Einstellungen.

**3. Addon installieren**
Einstellungen → Abschnitt **ADDON (IM SPIEL)**:
- Das Addon wird in **alle** gefundenen WoW-Versionen installiert (Battle.net legt jede Version in einen eigenen
  Unterordner wie `_classic_era_`); die Meldung zeigt, wohin. Der WoW-Ordner wird meist automatisch gefunden. Sonst **Wählen …** und den WoW-Ordner auswählen
  (der Ordner mit `Interface` und `WTF`, oder der Hauptordner „World of Warcraft“).
- **Addon installieren / aktualisieren** klicken → Meldung „Addon installiert, mit 4.725 deutschen Questtexten …“.
- Häkchen bei **Automatisch abgleichen** lassen. Optional unten **Mit Windows starten** anhaken.
- **Speichern**. Das Programm läuft jetzt unten rechts in der Taskleiste (Buch-Symbol).

**4. WoW (neu) starten**
- Läuft WoW schon: **komplett beenden** (zurück zum Desktop) und neu starten. Neue Addons erkennt WoW nur beim
  Spielstart – `/reload` oder Neu-Einloggen reicht dafür nicht.
- In der Charakterauswahl unten links **AddOns**: „WoW Lexikon“ und „WoW Lexikon – Spieldaten“ müssen angehakt sein.
  Steht dort „veraltet“: oben **Veraltete AddOns laden** anhaken.
- Einloggen. Das Addon wählt selbst eine **freie** Übersetzen-Taste (zuerst Alt+T, sonst Strg+T …) und überschreibt
  nie eine belegte. Das **Begrüßungsfenster** zeigt sie; mit **Andere Taste …** kannst du eine eigene wählen.

**5. Benutzen – eine Taste für alles**

| Was | Wie |
|---|---|
| Quest, NPC-Gespräch, Buch | Wörterbuch-Fenster erscheint daneben |
| Wort übersetzen | Maus auf das Wort, **Übersetzen-Taste** |
| Gegenstand im Beutel, Zauber, Questziele am Rand | Maus drauf, **Übersetzen-Taste** |
| Vorlesen | Wort anklicken (Rechtsklick = langsam) · Umschalt+Klick = merken |
| Lexikon-Buch: Suchen, Vokabeln lernen, Chat, Hilfe | **Buch-Knopf an der Minikarte**, **Esc-Menü → WoW Lexikon** oder `/lex` |
| Taste ändern | Lexikon-Buch → Hilfe → **Taste ändern …** (oder `/lex taste`) |
| Lieber halten statt drücken (Alt/Strg/Umschalt) | Esc → Optionen → AddOns → **WoW Lexikon** |
| Text in der 3D-Welt | Begleit-Programm: **Strg + Umschalt** halten, Kasten ziehen (WoW im Modus „Fenster (Vollbild)“) |

**6. Dazulernen**
Nach jedem **Ausloggen** übersetzt das Programm automatisch, was das Addon nicht kannte (Wörter, neue Forever-Quests).
Beim nächsten Einloggen (oder `/reload`) ist es da. Kurze Meldung unten rechts: „WoW Lexikon hat dazugelernt“.

**Addon taucht nicht auf?** WoW komplett neu starten. Hilft das nicht: im Battle.net-Launcher WoW Forever wählen →
Zahnrad neben „Spielen“ → **Im Explorer anzeigen**. Diesen Ordner in den Einstellungen bei „WoW-Ordner“ eintragen
(„Wählen …“), erneut **Addon installieren** und WoW neu starten.

**Rot markiert („veraltet“)?** Einmal ein- und wieder ausloggen – das Begleit-Programm trägt danach die passende
Spielversion ein. Beim nächsten Spielstart ist es weg.

---

## Teil B – An andere weitergeben

Empfehlung: **Addon über CurseForge** (jeder findet es, Updates kommen automatisch über die CurseForge-App) und das
**Begleit-Programm als optionales Extra** über GitHub (aktualisiert sich selbst – inklusive Addon).

### B1. GitHub einmalig einrichten (ca. 5 Minuten)
1. Kostenloses Konto auf **github.com** anlegen.
2. **New repository** → Name `wow-lexikon`, **Public**, Haken bei **Add a README file** → Create.
3. Zugangsschlüssel: Profilbild → **Settings → Developer settings → Personal access tokens → Fine-grained tokens →
   Generate new token**. „Repository access: Only select repositories“ → `wow-lexikon`. Bei „Permissions“
   → **Contents: Read and write**. Erzeugen und den Schlüssel kopieren.
4. Den Schlüssel in eine neue Textdatei **`C:\Projects\wow-woerterbuch\tools\.github-token`** einfügen und speichern.
   (Die Datei wird nie weitergegeben.)

### B2. Version veröffentlichen – ein Befehl
PowerShell im Projektordner öffnen (`C:\Projects\wow-woerterbuch`):
```
.\tools\veroeffentlichen.ps1 -Version 1.0.0 -GitHubRepo DEINNAME/wow-lexikon -Hochladen
```
Ab dem zweiten Mal reicht: `.\tools\veroeffentlichen.ps1 -Version 1.0.1 -Hochladen`

Das baut alles, baut die Update-Adresse fest ins Programm ein und legt auf GitHub eine neue Version an. Danach:
- **Download-Link zum Weitergeben** (ändert sich nie):
  `https://github.com/DEINNAME/wow-lexikon/releases/latest/download/WowLexikon.exe`
- Alle Begleit-Programme holen sich neue Versionen von Programm und Addon **automatisch** – beim Start, alle 6 Stunden
  oder sofort mit **„Nach Updates suchen“** (Einstellungen oder Rechtsklick auf das Taskleisten-Symbol). Nach einem
  Programm-Update fragt es „Jetzt neu starten?“.
- Auch deine eigene `bin\WowLexikon.exe` wird mit der Update-Adresse neu gebaut.

Vorher `curseforge\CHANGELOG.md` um einen Abschnitt `## 1.0.1` ergänzen – der Text erscheint auch auf GitHub.

### B3. Addon auf CurseForge (einmalig einrichten)
1. Auf curseforge.com anmelden → **Create a Project** → Spiel „World of Warcraft“, Kategorie z. B. „Quests & Leveling“.
2. Name „WoW Lexikon“, Zusammenfassung „Englische Quests verstehen: Übersetzung, Lautschrift, Aussprache“.
3. Beschreibung: Inhalt aus `curseforge\BESCHREIBUNG.md` einfügen, Download-Link aus B2 eintragen.
4. Logo: `curseforge\logo.png`. Lizenz: **GPL-3.0**.
5. Datei hochladen: `release\WowLexikon-1.0.0.zip`, Spielversion **WoW Forever** (bzw. passende Classic-Version)
   auswählen, Änderungen aus `curseforge\CHANGELOG.md`. CurseForge prüft neue Projekte kurz von Hand (1–2 Tage).
6. Bei jeder neuen Version: nach B2 die Datei `release\WowLexikon-<Version>.zip` dort als neue Datei hochladen –
   die CurseForge-App aktualisiert das Addon bei allen Nutzern.

