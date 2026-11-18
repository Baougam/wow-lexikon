// Neue Wörter aus WoW Forever – ohne dass jemand spielen muss.
//
// Holt die englischen Spieldaten der neuesten Forever-Version von wago.tools (Produkt „wowf“):
// Gegenstände + Beschreibungen, Zauber + Beschreibungen, Gebiete, Fraktionen, Oberflächentexte.
// Sucht darin alle Wörter, die das Addon noch nicht kennt, und schlägt sie in echten Wörterbüchern nach
// (Ding für Deutsch, CMU für die Aussprache). Was dort nicht steht, wird NICHT geraten – das sind fast immer
// Eigennamen, und die kommen aus den lokal erzeugten Namensdaten.
//
// Ergebnis: data/woerter-forever.txt (wort|lautschrift|deutsch) + data/forever-stand.json.
// Weitergeben ist in Ordnung: einzelne Wörter mit Wörterbuch-Übersetzung, keine Blizzard-Texte.
//
// Aufruf: node tools/forever-woerter.mjs [--build 1.60.1.70959] [--cache <ordner>] [--min 2]
// Läuft automatisch jede Woche auf GitHub (.github/workflows/forever-woerter.yml).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { forms, loadDing, loadCmu, existingWords } from './wortschatz.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const cache = path.resolve(opt('--cache', path.join(root, 'tools', '.cache')));
const MIN = +opt('--min', 2);
const UA = { 'User-Agent': 'wow-lexikon-wortliste (github.com/Baougam/wow-lexikon)' };

// Tabellen und Spalten mit englischem Text
const TABLES = {
  ItemSparse: ['Display_lang', 'Description_lang'],
  SpellName: ['Name_lang'],
  Spell: ['NameSubtext_lang', 'Description_lang', 'AuraDescription_lang'],
  AreaTable: ['AreaName_lang'],
  Faction: ['Name_lang', 'Description_lang'],
  GlobalStrings: ['TagText_lang'],
  CreatureFamily: ['Name_lang'],
  ChrClasses: ['Name_lang', 'Description_lang'],
};

async function fetchText(url) {
  for (let i = 0; i < 3; i++) {
    try {
      const r = await fetch(url, { headers: UA });
      if (r.ok) return await r.text();
      if (r.status < 500) throw new Error(url + ': HTTP ' + r.status);
    } catch (e) { if (i === 2) throw e; }
    await new Promise(r => setTimeout(r, 3000 * (i + 1)));
  }
}

// ---------- 1. Neueste Forever-Version ----------
let build = opt('--build', null);
if (!build) {
  const all = JSON.parse(await fetchText('https://wago.tools/api/builds'));
  const list = (all.wowf || []).filter(b => !b.is_bgdl).sort((a, b) => a.created_at < b.created_at ? 1 : -1);
  if (!list.length) throw new Error('Keine WoW-Forever-Version (wowf) bei wago.tools gefunden.');
  build = list[0].version;
}
console.log('WoW Forever, Version ' + build);

// ---------- 2. Tabellen laden (mit Zwischenspeicher je Version) ----------
function parseCsv(text) {
  const rows = []; let row = [], field = '', q = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) {
      if (c === '"') { if (text[i + 1] === '"') { field += '"'; i++; } else q = false; }
      else field += c;
    } else if (c === '"') q = true;
    else if (c === ',') { row.push(field); field = ''; }
    else if (c === '\n') { row.push(field.replace(/\r$/, '')); rows.push(row); row = []; field = ''; }
    else field += c;
  }
  if (field || row.length) { row.push(field); rows.push(row); }
  return rows;
}

const dir = path.join(cache, 'wago-' + build);
fs.mkdirSync(dir, { recursive: true });
const texts = [];
for (const [table, cols] of Object.entries(TABLES)) {
  const f = path.join(dir, table + '.csv');
  if (!fs.existsSync(f)) {
    process.stdout.write(`lade ${table} … `);
    fs.writeFileSync(f, await fetchText(`https://wago.tools/db2/${table}/csv?build=${build}&locale=enUS`));
    console.log('ok');
  }
  const rows = parseCsv(fs.readFileSync(f, 'utf8'));
  const head = rows.shift() || [];
  const idx = cols.map(c => head.indexOf(c)).filter(i => i >= 0);
  let n = 0;
  for (const r of rows) for (const i of idx) if (r[i]) { texts.push(r[i]); n++; }
  console.log(`  ${table}: ${rows.length} Zeilen, ${n} Texte`);
}

// ---------- 3. Wörter zählen ----------
// Spiel-Platzhalter und Farbcodes entfernen: $s1, $d, ${…}, $<…>, $lWort:Wörter;, |cff…|r, |n, %s
const clean = s => s
  .replace(/\\[nrt]/g, ' ')
  .replace(/\|cn[A-Z0-9_]+:|\|c[0-9a-fA-F]{8}|\|r|\|n|\|T[^|]*\|t|\|A[^|]*\|a|\|H[^|]*\|h|\|h|\|K[^|]*\|k/g, ' ')
  .replace(/\$@[A-Za-z]+\d*/g, ' ')
  .replace(/https?:\/\/\S+|www\.\S+/g, ' ')
  .replace(/\$\{[^}]*\}|\$<[^>]*>/g, ' ')
  .replace(/\$[lLgG]([^:;]*):([^;]*);/g, ' $1 $2 ')
  .replace(/\$[\/?@]?[A-Za-z]{0,2}\d*[A-Za-z]?\d*/g, ' ')
  .replace(/%\d?\$?[sdif]/g, ' ');

const count = new Map(), lower = new Map();
for (const t of texts) {
  for (const m of clean(t).matchAll(/[A-Za-z]+(?:'[a-z]+)?/g)) {
    const w = m[0];
    const lw = w.toLowerCase().replace(/'s$/, '');
    if (lw.length < 2 || lw.includes("'")) continue;
    count.set(lw, (count.get(lw) || 0) + 1);
    if (/^[a-z]/.test(w)) lower.set(lw, (lower.get(lw) || 0) + 1);
  }
}

// ---------- 4. Was fehlt dem Addon? ----------
const have = existingWords(root);
const known = w => forms(w).some(f => have.has(f));
const wanted = [...count].filter(([w, c]) => c >= MIN && !known(w)).sort((a, b) => b[1] - a[1]).map(([w]) => w);
const wantedSet = new Set(wanted);
console.log(`Verschiedene Wörter in den Spieldaten: ${count.size}, davon fehlen dem Addon: ${wanted.length}`);

// ---------- 5. In Wörterbüchern nachschlagen (nichts raten) ----------
const translate = await loadDing(cache, wantedSet);
const ipa = await loadCmu(cache);
const out = [], missing = [];
for (const w of wanted) {
  const de = translate(w);
  if (de) out.push(`${w}|${ipa(w) || ''}|${de}`);
  else missing.push(`${w}\t${count.get(w)}\t${lower.get(w) || 0}`);
}
out.sort();
const file = path.join(root, 'data', 'woerter-forever.txt');
const before = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
const now = out.join('\n') + '\n';
fs.writeFileSync(file, now);
// Nicht gefundene Wörter nur im Zwischenspeicher (meist Eigennamen) – zum Ansehen, nicht zum Weitergeben
fs.writeFileSync(path.join(cache, 'forever-ohne-uebersetzung.tsv'), 'wort\tvorkommen\tklein geschrieben\n' + missing.join('\n') + '\n');

const oldWords = new Set(before.split('\n').map(l => l.split('|')[0]).filter(Boolean));
const added = out.filter(l => !oldWords.has(l.split('|')[0])).length;
fs.writeFileSync(path.join(root, 'data', 'forever-stand.json'), JSON.stringify({
  build, woerter: out.length, neu: added, ohneUebersetzung: missing.length,
  quelle: 'wago.tools (enUS) → Ding (TU Chemnitz, GPL) + CMU', tabellen: Object.keys(TABLES),
}, null, 2) + '\n');
console.log(`Geschrieben: ${out.length} Wörter (${added} neu), ohne Wörterbuch-Eintrag: ${missing.length} (meist Namen, werden nicht geraten)`);
console.log(`Mit Lautschrift: ${out.filter(l => l.split('|')[1]).length}`);
