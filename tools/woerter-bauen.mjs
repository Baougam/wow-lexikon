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

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : d; };
const N_QUEST = +opt('--quest', 20000), N_GENERAL = +opt('--allgemein', 12000);
const cache = path.resolve(opt('--cache', path.join(root, 'tools', '.cache')));
fs.mkdirSync(cache, { recursive: true });

const SOURCES = {
  'de-en.txt.gz': 'https://ftp.tu-chemnitz.de/pub/Local/urz/ding/de-en/de-en.txt.gz',
  'cmudict.dict': 'https://raw.githubusercontent.com/cmusphinx/cmudict/master/cmudict.dict',
  '20k.txt': 'https://raw.githubusercontent.com/first20hours/google-10000-english/master/20k.txt',
};
async function get(name) {
  const f = path.join(cache, name);
  if (!fs.existsSync(f)) {
    process.stdout.write(`lade ${name} … `);
    const r = await fetch(SOURCES[name]);
    if (!r.ok) throw new Error(name + ': HTTP ' + r.status);
    fs.writeFileSync(f, Buffer.from(await r.arrayBuffer()));
    console.log('ok');
  }
  const b = fs.readFileSync(f);
  return (name.endsWith('.gz') ? zlib.gunzipSync(b) : b).toString('utf8');
}

// ---------- Grundformen (wie im Programm) ----------
function forms(w) {
  const f = [w];
  if (w.endsWith("'s")) f.push(w.slice(0, -2));
  if (w.endsWith('ies')) f.push(w.slice(0, -3) + 'y');
  if (w.endsWith('ves')) f.push(w.slice(0, -3) + 'f', w.slice(0, -3) + 'fe');
  if (/(ches|shes|xes|sses|oes)$/.test(w)) f.push(w.slice(0, -2));
  if (w.endsWith('s') && !w.endsWith('ss')) f.push(w.slice(0, -1));
  if (w.endsWith('ied')) f.push(w.slice(0, -3) + 'y');
  if (w.endsWith('ed')) { const b = w.slice(0, -2); f.push(b, w.slice(0, -1)); if (b.length > 2 && b.at(-1) === b.at(-2)) f.push(b.slice(0, -1)); }
  if (w.endsWith('ing') && w.length > 5) { const b = w.slice(0, -3); f.push(b, b + 'e'); if (b.length > 2 && b.at(-1) === b.at(-2)) f.push(b.slice(0, -1)); }
  if (w.endsWith('er') && w.length > 5) f.push(w.slice(0, -2));
  if (w.endsWith('est') && w.length > 6) f.push(w.slice(0, -3));
  if (w.endsWith('ly') && w.length > 5) f.push(w.slice(0, -2));
  return f;
}

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

// ---------- Ding: Englisch → Deutsch ----------
// Fachgebiete und seltene Verwendungen abwerten (Schreibvarianten wie [Am.]/[Br.] sind dagegen ganz normal)
const SPECIAL = /\[(bot|zool|med|chem|phys|electr|comp|techn|econ|fin|jur|geol|biol|math|ling|aviat|auto|mus|sport|agr|pharm|statist|adm|pol|textil|min|print|photo|tv|telco|Ös|Schw|slang|vulg|obs|archaic|rare|dialect)\.?\]/;
const MILD = /\[(naut|mil|constr|cook|relig|poet|geh|ugs)\.?\]/;
const ding = new Map();   // englische Grundform → Map(deutsch → Punkte)
const cleanDe = s => s.replace(/\{[^}]*\}/g, '').replace(/\[[^\]]*\]/g, '').replace(/\([^)]*\)/g, '').replace(/\s+/g, ' ').replace(/^etw\.?\s+|^jdn\.?\s+|^jdm\.?\s+|^sich\s+(?=\w+en$)/, '').trim();
const cleanEn = s => s.replace(/\{[^}]*\}/g, '').replace(/\[[^\]]*\]/g, '').replace(/\([^)]*\)/g, '').replace(/^to\s+/, '').replace(/\s+/g, ' ').trim().toLowerCase();
for (const line of (await get('de-en.txt.gz')).split('\n')) {
  if (!line || line[0] === '#') continue;
  const [deSide, enSide] = line.split(' :: ');
  if (!enSide) continue;
  // Klammern zuerst entfernen – sie können Semikolons enthalten, die sonst falsch trennen
  const noParen = s => s.replace(/\([^)]*\)/g, '').replace(/\/[^/]*\//g, '');
  const deSubs = noParen(deSide).split(' | '), enSubs = noParen(enSide).split(' | ');
  const n = Math.min(deSubs.length, enSubs.length);
  for (let i = 0; i < n && i < 3; i++) {
    const deAlts = deSubs[i].split(';').map(s => s.trim()).filter(Boolean);
    const enAlts = enSubs[i].split(';').map(s => s.trim()).filter(Boolean);
    if (!deAlts.length) continue;
    const de = cleanDe(deAlts[0]);
    if (!de || de.length > 40 || /\.\.\.|…/.test(de)) continue;
    const special = SPECIAL.test(deSubs[i]) || SPECIAL.test(enSubs[i]);
    const mild = MILD.test(deSubs[i]) || MILD.test(enSubs[i]);
    // Britische/amerikanische Schreibweisen zählen als dieselbe Stelle (harbour; harbor → beide „an erster Stelle“)
    const am = s => cleanEn(s).replace(/our\b/g, 'or').replace(/re\b/g, 'er').replace(/is(e|ed|ing|ation)\b/g, 'iz$1').replace(/ll(ed|ing)\b/g, 'l$1');
    const firstAm = am(enAlts[0] || '');
    enAlts.forEach((ea, k0) => {
      const k = am(ea) === firstAm ? 0 : k0;
      const en = cleanEn(ea);
      if (!en || en.includes(' ') && !wantedSet.has(en)) return;
      if (!wantedSet.has(en) && !wantedSet.has(en.split(' ')[0])) return;
      // Hauptbedeutung = englisches Wort steht an erster Stelle des Eintrags
      let score = (i === 0 ? 3 : 0.5) + Math.min(deSubs.length, 8) * 0.4 + (k === 0 ? 2.5 : 0) - (special ? 2.5 : 0) - (mild ? 0.6 : 0)
                - (/\[(pej|slang|vulg|euphem|humor|iron)\.?\]/.test(deSubs[i] + enSubs[i]) ? 3 : 0)
                - (deAlts.length > 5 ? 0.5 : 0) - (enAlts.length > 6 ? 0.5 : 0);
      if (/^[A-ZÄÖÜ]/.test(de) && /\{[mfn]\}/.test(deAlts[0])) score += 0.3;   // Substantiv mit Genus
      let m = ding.get(en); if (!m) ding.set(en, m = new Map());
      m.set(de, (m.get(de) || 0) + score);
    });
  }
}
function translate(w) {
  for (const f of forms(w)) {
    const m = ding.get(f);
    if (!m) continue;
    const sorted = [...m].sort((a, b) => b[1] - a[1]);
    const top = sorted.length ? sorted[0][1] : 0;
    const best = sorted.filter(([, s]) => s > 1.5 && s >= top * 0.55);   // deutlich schwächere Nebenbedeutungen weglassen
    const out = [];
    for (const [de] of best) {
      if (out.some(o => o.toLowerCase() === de.toLowerCase())) continue;
      out.push(de);
      if (out.length >= 3) break;
    }
    if (out.length) return out.join('; ');
  }
  return null;
}

// ---------- CMU: Aussprache → IPA ----------
const cmu = new Map();
for (const l of (await get('cmudict.dict')).split('\n')) {
  const m = l.match(/^([a-z'.-]+)(\(\d+\))?\s+(.+?)(\s+#.*)?$/);
  if (m && !m[2] && !cmu.has(m[1])) cmu.set(m[1], m[3].trim().split(' '));
}
const V = { AA: 'ɑː', AE: 'æ', AH: 'ʌ', AO: 'ɔː', AW: 'aʊ', AY: 'aɪ', EH: 'ɛ', ER: 'ɜːr', EY: 'eɪ', IH: 'ɪ', IY: 'iː', OW: 'oʊ', OY: 'ɔɪ', UH: 'ʊ', UW: 'uː' };
const C = { B: 'b', CH: 'tʃ', D: 'd', DH: 'ð', F: 'f', G: 'ɡ', HH: 'h', JH: 'dʒ', K: 'k', L: 'l', M: 'm', N: 'n', NG: 'ŋ', P: 'p', R: 'r', S: 's', SH: 'ʃ', T: 't', TH: 'θ', V: 'v', W: 'w', Y: 'j', Z: 'z', ZH: 'ʒ' };
const ONSET2 = new Set(['pr', 'pl', 'br', 'bl', 'tr', 'dr', 'kr', 'kl', 'ɡr', 'ɡl', 'fr', 'fl', 'θr', 'ʃr', 'sp', 'st', 'sk', 'sm', 'sn', 'sl', 'sw', 'tw', 'kw', 'dw']);
function ipa(w) {
  let ph = null;
  for (const f of forms(w)) if (cmu.has(f)) { ph = cmu.get(f); if (f !== w) return null; break; }   // nur exakte Formen, sonst falsche Endung
  if (!ph) return null;
  const segs = ph.map(p => {
    const base = p.replace(/\d/, ''), stress = p.match(/\d/)?.[0];
    if (V[base]) {
      let v = V[base];
      if (stress === '0') v = base === 'AH' ? 'ə' : base === 'ER' ? 'ər' : base === 'IY' ? 'i' : base === 'UW' ? 'u' : v.replace('ː', '');
      return { v, stress };
    }
    return { c: C[base] || '' };
  });
  const vowels = segs.filter(s => s.v).length;
  const out = [];
  for (let i = 0; i < segs.length; i++) {
    const s = segs[i];
    if (s.v && s.stress === '1' && vowels > 1) {
      // Betonungszeichen vor den Silbenanlaut setzen (ein Konsonant oder ein üblicher Zweierkonsonant)
      let k = out.length, back = 0;
      if (k >= 1 && !out[k - 1].v) back = 1;
      if (k >= 2 && !out[k - 1].v && !out[k - 2].v && ONSET2.has(out[k - 2].c + out[k - 1].c)) back = 2;
      out.splice(k - back, 0, { c: 'ˈ' });
    }
    out.push(s);
  }
  return out.map(s => s.v || s.c).join('');
}

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
