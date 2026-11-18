# WoW Lexikon

Zwei Teile, die zusammenarbeiten:

- **Addon** (`addon/WowLexikon`) – im Spiel: neben jedem Questfenster ein Pergament-Fenster mit dem englischen
  Questtext, jedes Wort anklickbar (Übersetzung, Lautschrift, Vorlesen), darunter der offizielle deutsche Questtext.
  Fährt man über ein englisches Wort, leuchtet das passende deutsche Wort im deutschen Text auf. Deutsche Namen in
  Tooltips. `/lex` öffnet die Suche. Alles offline und sofort (über 14.500 Wörter, 73.000 Namen, alle Classic-Quests).
- **Begleit-Programm** (`WowLexikon.exe`) – läuft im Hintergrund: installiert und aktualisiert das Addon,
  erzeugt die deutschen Spieldaten, übersetzt nach jedem Ausloggen online, was das Addon nicht kannte
  (Wörter und neue Forever-Quests), erkennt Spiel-Updates und geänderte Questtexte. Zusätzlich die Bildschirm-Übersetzung
  mit Strg + Umschalt (unten beschrieben).

## Addon einrichten (auf dem Spiele-PC)

1. `WowLexikon.exe` starten → Einstellungen → **Addon (im Spiel)**.
2. WoW-Ordner wird meist automatisch gefunden (sonst „Wählen …“).
3. **Addon installieren / aktualisieren** klicken. (Bei der weitergegebenen Version vorher **Spieldaten erzeugen** –
   lädt einmalig ~120 MB aus der öffentlichen Datenbank und baut die deutschen Texte auf diesem PC.)
4. WoW **komplett neu starten** (neue Addons erkennt WoW nur beim Spielstart, `/reload` reicht nicht). Falls WoW das Addon als „veraltet“ markiert: im Addon-Menü
   „Veraltete AddOns laden“ anhaken.

Im Spiel:

| Was | Wie |
|---|---|
| Questfenster, Questlog, NPC-Gespräche, Bücher/Briefe | Wörterbuch-Fenster erscheint automatisch daneben |
| Wort übersetzen | **Alt halten** und darüberfahren (oder anklicken) |
| Vorlesen | Wort anklicken · Rechtsklick = langsam · Umschalt+Klick = merken |
| Gegenstand im Beutel, Zauber, Questziele am Rand, jedes Fenster | Maus drauf, **Alt+T** |
| Chat und was NPCs sagen | **Alt+C** |
| Wort suchen / Fenster ein-aus | **Alt+F** / **Alt+W** |
| Deutscher Name | steht automatisch in jedem Tooltip (Gegenstände, NPCs) |

Alle Tasten lassen sich unter **Tastenbelegung → WoW Lexikon** ändern. Halte-Taste (Alt, Strg, Umschalt, immer oder
nur Klick) und Schalter: **Esc → Optionen → AddOns → WoW Lexikon** oder `/lex optionen`. `/lex hilfe` zeigt alle Befehle.
Text in der 3D-Spielwelt kann kein Addon lesen – dafür das Begleit-Programm mit Strg + Umschalt (Rahmen ziehen).

## Weitergeben und Updates

`.\tools\veroeffentlichen.ps1 -Version 1.0.1 -BasisUrl "https://raw.githubusercontent.com/DEINNAME/wow-lexikon/main/release"`
erzeugt den Ordner `release` (Programm ohne Blizzard-Texte, Addon-ZIP, `manifest.json`). Den Ordner hochladen,
z. B. in ein GitHub-Projekt. Wer in den Einstellungen als **Update-Quelle** die Adresse der `manifest.json` eingetragen
hat, bekommt neue Versionen automatisch (beim Start und alle 6 Stunden; das Addon wirkt nach `/reload`, das Programm
beim nächsten Start). Questtexte und Namen von Blizzard werden nie verteilt – jeder PC erzeugt sie selbst.

Übersetzt englische Wörter aus World of Warcraft (Questtexte, Chat, Tooltips, Menüs) per Bildschirmauswahl:
Übersetzung, Lautschrift, Aussprache und Beispielsatz in einer schwebenden Karte neben dem Wort.

Läuft als eigenes kleines Windows-Programm neben dem Spiel. Es liest nur den Bildschirm (wie ein Screenshot-Tool),
greift nicht ins Spiel ein und automatisiert nichts.

## Starten

`bin\WowLexikon.exe` doppelklicken. Das Programm sitzt dann unten rechts im Infobereich der Taskleiste
(Doppelklick auf das Symbol = Einstellungen, Rechtsklick = Menü). Beim ersten Start öffnen sich die Einstellungen.

WoW im Modus **„Fenster (Vollbild)“** spielen – im exklusiven Vollbild kann nichts über dem Spiel angezeigt werden.

## Benutzen

1. In WoW **Strg + Umschalt gedrückt halten** (in den Einstellungen änderbar, z. B. Maus-Seitentaste).
   Der Bildschirm friert ein, eine Lupe hilft beim Zielen.
2. **Rahmen 1 (gold):** Rahmen um das Wort ziehen – oder einfach auf das Wort klicken.
3. **Rahmen 2 (türkis, optional):** Rahmen um den ganzen Satz ziehen – oder auf die Zeile klicken. Dann wird das Wort
   im Zusammenhang erklärt und der Satz mit übersetzt.
4. **Tasten loslassen** → die Karte erscheint.

Rechtsklick während der Auswahl = letzten Rahmen zurücknehmen, Esc = abbrechen.

**Wort eintippen statt einrahmen:** Doppelklick auf das Symbol in der Taskleiste (oder Rechtsklick → „Wort suchen …“),
Wort oder Satz eintippen, Enter.

In der Karte:
- 🔊 vorlesen, **0,6×** langsam vorlesen, 🎧 echte Aufnahme eines Muttersprachlers (wenn vorhanden)
- Wort falsch erkannt? Draufklicken, korrigieren, Enter.
- ☆ speichert das Wort in die Vokabelliste (`vokabeln.csv`, öffnet sich in Excel)
- Pin = Karte bleibt offen; Karte lässt sich mit der Maus verschieben

## Aussehen

Drei Stile (Einstellungen → Aussehen, mit Sofort-Vorschau):

- **Questlog** (Standard): Pergament mit Papierstruktur, dunkler Rahmen mit Goldlinie und Eckverzierungen,
  dunkelbraune Schrift, Übersetzung und hervorgehobene Wörter in Dunkelrot.
- **Tooltip**: dunkelblau mit silbernem Rahmen, Gold/Weiß/Grün wie Gegenstands-Tooltips.
- **Modern**: schlicht und dunkel.

Die **Spielschrift** wird aus deiner eigenen WoW-Installation geladen und nicht mitgeliefert: `Fonts\FRIZQT__.TTF`
(Text) und, falls vorhanden, `Fonts\MORPHEUS.TTF` (Questtitel). Das Programm sucht sie automatisch im Ordner des
laufenden WoW. Liegen die Schriften dort nicht als Datei vor (bei manchen Versionen stecken sie in den
Spielarchiven), kann man in den Einstellungen eine Schriftdatei auswählen. Ohne Spielschrift wird eine ähnliche
Windows-Schrift genutzt.

## Übersetzungsdienste

Zuerst wird immer das eingebaute Offline-Wörterbuch gefragt (über 3.500 Einträge, sofort und ohne Internet):

- **3.000 Questwörter** (`data/questwoerter.txt`): die häufigsten Wörter aus rund 45.000 echten Questtexten
  (öffentliche Datenbank des Open-Source-Projekts AzerothCore), ohne Namen und Füllwörter, mit Lautschrift und den
  Bedeutungen, die in Quests vorkommen. Gebeugte Formen werden erkannt (slaughtered → slaughter, Ravagers → ravager).
- **WoW-Glossar** (`data/glossar-wow.json`): Chat-Abkürzungen wie LFG/WTS/OOM, Spielbegriffe, Klassen, Werte,
  Kreaturen, Orte und Instanzen mit den deutschen Client-Namen – mit Erklärungen und Beispielsätzen.

- **Spieldaten** (`data/quests-de.txt.gz`, `data/namen-de.txt.gz`): **alle Classic-Quests** (4.725 Quests,
  18.747 Texte) englisch und so, wie sie im deutschen Spielclient stehen, plus **73.000 Namen** von NPCs,
  Gegenständen, Objekten und Quests (Defias Bandit → Bandit der Defias, Murloc Eye → Murlocauge).
  WoW Forever basiert auf Classic; Quests aus Burning Crusade und Lich King sind deshalb herausgefiltert
  (`node tools/daten-bauen.mjs --alle-erweiterungen` nimmt sie wieder mit). Die **neuen Forever-Quests**
  (Riverglades, Hyjal, Zephras Isle …) gibt es noch in keiner öffentlichen Datenbank – sie werden normal übersetzt.

**Questtexte werden wiedererkannt:** Klickst du in einem Questfenster auf ein Wort, liest das Programm die Zeile
mit, sucht den Satz in allen Questtexten (verträgt Lesefehler wie „bridqe“ oder „Th0mas“) und zeigt den
Originalsatz mit der **offiziellen deutschen Übersetzung** – das Wort ist darin hervorgehoben, dazu der Questtitel.
Rahmst du einen ganzen Questsatz als Wort ein, kommt direkt die offizielle Übersetzung. Chat und andere Texte, die
nicht aus Quests stammen, werden normal übersetzt.

Die Spieldaten erzeugt `node tools/daten-bauen.mjs` aus der öffentlichen AzerothCore-Datenbank (lädt einmal
ca. 120 MB). Andere Zielsprachen: `node tools/daten-bauen.mjs --sprachen de,fr,es,ru`, danach `build.ps1`.
Die Texte stammen aus dem Spiel und gehören Blizzard – nur für den eigenen Gebrauch, nicht weitergeben.

**Kontext braucht keine KI:** Ziehst du den zweiten Rahmen um den Satz, wird der Satz übersetzt, und hat das Wort
mehrere Bedeutungen („hide“ = Haut; verstecken), zeigt die Karte an, welche davon im Satz gemeint ist.
Die KI ist nur ein optionales Extra für Redewendungen, Slang und Erklärungen.

Für Wörter, die nicht im Offline-Wörterbuch stehen, und für Sätze, je nach Einstellung:

| Dienst | Kosten | Stärke |
|---|---|---|
| MyMemory | kostenlos, ohne Konto (ca. 5.000 Wörter/Tag, mit E-Mail 10×) | sofort startklar |
| DeepL Free | kostenloses Konto, 500.000 Zeichen/Monat | sehr gute Übersetzungen |
| Freie KI (Gemini, Groq, OpenRouter, Ollama) | kostenloses Kontingent mit Anmeldung, Ollama lokal | versteht Kontext und WoW-Slang, erklärt die Bedeutung im Satz |

Lautschrift, echte Aufnahmen und englische Beispielsätze kommen von dictionaryapi.dev (kostenlos).
Ergebnisse werden zwischengespeichert – gleiche Wörter kosten kein Kontingent und funktionieren auch offline.

## Andere Sprachen

In den Einstellungen sind Spieltext- und Zielsprache frei wählbar (19 Sprachen). Für die Spieltext-Sprache braucht
Windows die passende Texterkennung und für die Aussprache eine Stimme: Windows-Einstellungen → Zeit und Sprache →
Sprache und Region → Sprache hinzufügen. Das Einstellungsfenster zeigt an, ob beides vorhanden ist.

## Eigene Wörter

`eigenes-glossar.json` im Datenordner (`%APPDATA%\WowLexikon`) – Menü „Eigenes Glossar bearbeiten“, danach
„Glossar neu laden“. Eigene Einträge überschreiben das eingebaute Glossar:

```json
{"en": "pug", "de": "Zufallsgruppe", "ipa": "pʌɡ", "ex": "We pugged the last tank.", "ex_de": "Den letzten Tank haben wir zufällig gefunden.", "note": "Slang: pick-up group"}
```

## Bauen

`build.ps1` ausführen. Nutzt nur den in Windows eingebauten C#-Compiler (.NET Framework 4.8) – nichts zu installieren.
Selbsttest: `bin\WowLexikon.exe --selftest <Ordner>` schreibt `selbsttest.txt` und Vorschaubilder.

## Daten

Alles liegt in `%APPDATA%\WowLexikon`: Einstellungen (API-Schlüssel mit Windows verschlüsselt), eigenes Glossar,
Vokabelliste, Zwischenspeicher, `fehler.log`.
