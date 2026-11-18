// Baut den großen Wortschatz (data/woerter-auto.txt, Format: wort|lautschrift|deutsch):
//   - die häufigsten Wörter aus allen Classic-Questtexten (data/quests-de.txt.gz) und
//   - die häufigsten allgemeinen englischen Wörter (Liste „google-10000-english / 20k“),
//   - Übersetzungen aus dem freien Wörterbuch „Ding“ (TU Chemnitz, GPL),
//   - Lautschrift aus dem CMU-Aussprachewörterbuch (BSD), automatisch in IPA umgewandelt.
// Handübersetzte Questwörter (data/questwoerter.txt) und das WoW-Glossar haben Vorrang und werden hier übersprungen.
//
// Aufruf: node tools/woerter-bauen.mjs [--quest 20000] [--allgemein 12000] [--cache <ordner>]

import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { get as getSource, forms, loadDing, loadCmu } from './wortschatz.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const N_QUEST = +opt('--quest', 20000), N_GENERAL = +opt('--allgemein', 12000);
const cache = path.resolve(opt('--cache', path.join(root, 'tools', '.cache')));
fs.mkdirSync(cache, { recursive: true });
const get = name => getSource(cache, name);

// ---------- Vorhandene Einträge (haben Vorrang) ----------
const have = new Set();
for (const l of fs.readFileSync(path.join(root, 'data', 'questwoerter.txt'), 'utf8').split('\n')) { const w = l.split('|')[0].trim(); if (w) have.add(w.toLowerCase()); }
for (const e of JSON.parse(fs.readFileSync(path.join(root, 'data', 'glossar-wow.json'), 'utf8'))) have.add(e.en.toLowerCase());

// ---------- Wortliste: Classic-Questtexte ----------
const quests = zlib.gunzipSync(fs.readFileSync(path.join(root, 'data', 'quests-de.txt.gz'))).toString('utf8').split('\n');
const count = new Map(), capsMid = new Map(), seenMid = new Map();
for (const line of quests) {
  const en = (line.split('\t')[2] || '').replace(/\$[A-Za-z](?:[^;:\s]*[:;][^;]*;)?/g, ' ');
  for (const sent of en.split(/[.!?]+\s*/)) {
    const ws = sent.match(/[A-Za-z][A-Za-z'-]*[A-Za-z]|[A-Za-z]/g) || [];
    ws.forEach((w, i) => {
      const lw = w.toLowerCase().replace(/'s$/, '');
      count.set(lw, (count.get(lw) || 0) + 1);
      if (i > 0) { seenMid.set(lw, (seenMid.get(lw) || 0) + 1); if (/^[A-Z]/.test(w)) capsMid.set(lw, (capsMid.get(lw) || 0) + 1); }
    });
  }
}
// Gebeugte Formen der Grundform zuschlagen
const lemmaCount = new Map();
for (const [w, c] of count) {
  let t = w;
  for (const b of forms(w).slice(1)) if (b.length >= 3 && (count.get(b) || 0) >= 2) { t = b; break; }
  lemmaCount.set(t, (lemmaCount.get(t) || 0) + c);
}
const isName = w => { const n = seenMid.get(w) || 0; return n >= 2 ? (capsMid.get(w) || 0) / n > 0.6 : false; };
const questWords = [...lemmaCount].filter(([w]) => w.length >= 2 && !w.includes("'") && !isName(w)).sort((a, b) => b[1] - a[1]).map(([w]) => w).slice(0, N_QUEST);

// ---------- Wortliste: allgemeines Englisch ----------
const general = (await get('20k.txt')).split(/\r?\n/).map(s => s.trim().toLowerCase()).filter(s => /^[a-z]{2,}$/.test(s)).slice(0, N_GENERAL);

const wanted = [];
const wantedSet = new Set();
for (const w of [...questWords, ...general]) if (!have.has(w) && !wantedSet.has(w)) { wantedSet.add(w); wanted.push(w); }

// ---------- Ding (EN→DE) und CMU (Aussprache) aus tools/wortschatz.mjs ----------
const translate = await loadDing(cache, wantedSet);
const ipa = await loadCmu(cache);

// ---------- Ausgabe ----------
const lines = []; let noTr = 0;
for (const w of wanted) {
  const de = translate(w);
  if (!de) { noTr++; continue; }
  lines.push(`${w}|${ipa(w) || ''}|${de}`);
}
fs.writeFileSync(path.join(root, 'data', 'woerter-auto.txt'), lines.join('\n') + '\n');
console.log(`Gewünscht: ${wanted.length} (Quest ${questWords.length}, allgemein ${general.length}, ohne vorhandene)`);
console.log(`Geschrieben: ${lines.length} Wörter, ohne Übersetzung: ${noTr} (lernt das Begleit-Programm online nach)`);
console.log(`Mit Lautschrift: ${lines.filter(l => l.split('|')[1]).length}`);
