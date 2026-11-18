// Erzeugt die Daten-Dateien für das WoW-Addon:
//   addon/WowLexikon/Data/Words*.lua      – Wortschatz (Glossar, 3000 Questwörter, großer Wortschatz) → darf weitergegeben werden
//   addon/WowLexikon_Data/Names*.lua      – Namen aus dem Spiel   ┐ Texte von Blizzard: werden auf jedem PC lokal
//   addon/WowLexikon_Data/Quests*.lua     – deutsche Questtexte    ┘ erzeugt und NICHT mit dem Addon verteilt
//
// Aufruf: node tools/addon-daten.mjs [--sprache de]

import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const lang = (() => { const i = args.indexOf('--sprache'); return i >= 0 ? args[i + 1] : 'de'; })();
const coreDir = path.join(root, 'addon', 'WowLexikon', 'Data');
const dataDir = path.join(root, 'addon', 'WowLexikon_Data');
fs.mkdirSync(coreDir, { recursive: true });
fs.mkdirSync(dataDir, { recursive: true });

// Lua-Zeichenkette sicher schreiben
const L = s => '"' + String(s ?? '').replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\r/g, '').replace(/\n/g, '\\n') + '"';

// Große Tabellen in Stücke teilen (Lua erlaubt je Datei nur begrenzt viele Konstanten)
function writeChunks(dir, base, globalName, entries, perFile, header) {
  for (const f of fs.readdirSync(dir)) if (f.startsWith(base) && f.endsWith('.lua')) fs.unlinkSync(path.join(dir, f));
  const files = [];
  for (let i = 0, n = 1; i < entries.length; i += perFile, n++) {
    const name = `${base}${n}.lua`;
    const body = entries.slice(i, i + perFile).map(([k, v]) => `[${typeof k === 'number' ? k : L(k)}]=${v}`).join(',\n');
    fs.writeFileSync(path.join(dir, name), `${header}\nlocal T = ${globalName}\nlocal add = {\n${body}\n}\nfor k, v in pairs(add) do T[k] = v end\n`);
    files.push(name);
  }
  return files;
}

// ---------------- Wortschatz (Rangfolge: auto < Questwörter < Glossar) ----------------
const words = new Map();   // wort → "lautschrift|deutsch|hinweis|beispiel|beispiel_de|quelle"
const put = (w, ipa, de, note, ex, exDe, src) => { if (w && de) words.set(w.toLowerCase(), [ipa, de, note, ex, exDe, src].map(x => (x ?? '').replace(/\|/g, '/')).join('|')); };
for (const l of fs.readFileSync(path.join(root, 'data', 'woerter-auto.txt'), 'utf8').split('\n')) { const [w, ipa, de] = l.split('|'); put(w, ipa, de, '', '', '', 'w'); }
for (const l of fs.readFileSync(path.join(root, 'data', 'questwoerter.txt'), 'utf8').split('\n')) { const [w, ipa, de] = l.split('|'); put(w, ipa, de, '', '', '', 'q'); }
for (const e of JSON.parse(fs.readFileSync(path.join(root, 'data', 'glossar-wow.json'), 'utf8'))) put(e.en, e.ipa, e[lang], lang === 'de' ? e.note : '', e.ex, e['ex_' + lang], 'g');
const wordEntries = [...words].sort((a, b) => a[0] < b[0] ? -1 : 1).map(([k, v]) => [k, L(v)]);
const wordFiles = writeChunks(coreDir, 'Words', 'WowLexikon_Words', wordEntries, 6000,
  '-- Wortschatz für WoW Lexikon (automatisch erzeugt mit tools/addon-daten.mjs)\n-- Format: "lautschrift|übersetzung|hinweis|beispiel|beispiel übersetzt|quelle (g=Glossar, q=Questwort, w=Wörterbuch)"');

// ---------------- Namen + Questtexte (nur lokal) ----------------
const gz = f => zlib.gunzipSync(fs.readFileSync(path.join(root, 'data', f))).toString('utf8').split('\n').filter(Boolean);
const names = new Map();
const rank = { n: 0, i: 1, q: 2, o: 3 };
for (const l of gz(`namen-${lang}.txt.gz`)) {
  const [t, en, tr, , subTr] = l.split('\t');
  const k = en.toLowerCase();
  const old = names.get(k);
  if (old && rank[old.t] <= rank[t]) continue;
  names.set(k, { t, v: `${t}|${tr}|${subTr || ''}` });
}
const nameEntries = [...names].map(([k, v]) => [k, L(v.v)]);
const nameFiles = writeChunks(dataDir, 'Names', 'WowLexikon_Names', nameEntries, 15000, '-- Namen aus dem Spiel (lokal erzeugt, nicht weitergeben). Format: "typ|übersetzung|titel"');

const quests = new Map();   // id → {T,D,O,Q,R}
for (const l of gz(`quests-${lang}.txt.gz`)) {
  const [id, kind, , tr] = l.split('\t');
  if (!'TDOQRC'.includes(kind)) continue;
  const q = quests.get(+id) || {};
  q[kind] = tr.replace(/\$B/gi, '\n');
  quests.set(+id, q);
}
const questEntries = [...quests].sort((a, b) => a[0] - b[0]).map(([id, q]) => [id, '{' + ['T', 'D', 'O', 'Q', 'R', 'C'].filter(k => q[k]).map(k => `${k}=${L(q[k])}`).join(',') + '}']);
const questFiles = writeChunks(dataDir, 'Quests', 'WowLexikon_Quests', questEntries, 1500, '-- Deutsche Questtexte aus dem Spiel (lokal erzeugt, nicht weitergeben)');

// ---------------- TOC-Dateien ----------------
const interfaces = '16001, 11509, 11508, 11507';   // 16001 = WoW Forever 1.60.1
fs.writeFileSync(path.join(dataDir, 'WowLexikon_Data.toc'),
`## Interface: ${interfaces}
## Title: WoW Lexikon – Spieldaten
## Notes: Deutsche Questtexte und Namen (lokal erzeugt vom Begleit-Programm)
## Author: WoW Lexikon
## Dependencies: WowLexikon
## LoadOnDemand: 1
## Version: 1.0

${nameFiles.join('\n')}
${questFiles.join('\n')}
`);

console.log(`Wörter: ${words.size} in ${wordFiles.length} Dateien`);
console.log(`Namen: ${names.size} in ${nameFiles.length} Dateien, Quests: ${quests.size} in ${questFiles.length} Dateien`);
const size = d => fs.readdirSync(d).reduce((s, f) => s + fs.statSync(path.join(d, f)).size, 0) / 1048576;
console.log(`Größe: Addon-Wörter ${size(coreDir).toFixed(1)} MB, Spieldaten ${size(dataDir).toFixed(1)} MB`);
export { wordFiles };
