// Baut die Offline-Daten aus der öffentlichen AzerothCore-Datenbank:
//   data/quests-<sprache>.txt.gz  – jeder Questtext englisch + in der Zielsprache (so wie im Spielclient)
//   data/namen-<sprache>.txt.gz   – NPCs, Gegenstände, Objekte und Questtitel englisch → Zielsprache
//
// Aufruf:  node tools/daten-bauen.mjs [--sprachen de,fr,es,ru] [--cache <ordner>]
// Die SQL-Dateien (~120 MB) werden einmal heruntergeladen und im Cache-Ordner aufbewahrt.

import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : def; };
const sprachen = opt('--sprachen', 'de').split(',').map(s => s.trim()).filter(Boolean);
const cache = path.resolve(opt('--cache', path.join(root, 'tools', '.cache')));
// WoW Forever basiert auf Classic (Stufe 60): Quests aus Burning Crusade und Wrath of the Lich King weglassen,
// damit es keine Fehltreffer gibt. Mit --alle-erweiterungen bleiben sie drin.
const nurClassic = !args.includes('--alle-erweiterungen');

// Zonen (QuestSortID) aus Burning Crusade und Wrath of the Lich King; Classic-Zonen liegen fast alle unter 3430.
const LATER_ZONES = new Set([65, 66, 67, 206, 210, 394, 495, 1196, 2817, 4298]);
const CLASSIC_HIGH = new Set([3456, 3478]);   // Naxxramas (Classic) und Tore von Ahn'Qiraj
const LATER_SORTS = new Set([-372, -241, -1002]); // Todesritter, Argentumturnier, Kalu'ak-Angeln
// Orte, die es erst ab Burning Crusade / Lich King gibt – taucht einer im Text auf, ist es keine Classic-Quest
const LATER_PLACES = /\b(Eversong|Silvermoon|Ghostlands|Tranquillien|Sunstrider|Azuremyst|Bloodmyst|Exodar|Ammen Vale|Outland|Hellfire|Honor Hold|Thrallmar|Zangarmarsh|Terokkar|Shattrath|Nagrand|Blade's Edge|Netherstorm|Shadowmoon Valley|Isle of Quel'Danas|Northrend|Borean Tundra|Howling Fjord|Dragonblight|Grizzly Hills|Zul'Drak|Sholazar|Storm Peaks|Icecrown|Crystalsong|Wintergrasp|Acherus|Ebon Hold|Valgarde|Warsong Hold|Valiance Keep|Argent Tournament)\b/;
function isClassic(q) {
  const lvl = +q.QuestLevel, min = +q.MinLevel, sort = +q.QuestSortID;
  if (lvl > 60 || min > 60) return false;
  if (LATER_ZONES.has(sort) || LATER_SORTS.has(sort)) return false;
  if (sort >= 3430 && !CLASSIC_HIGH.has(sort)) return false;
  // Nur für Blutelfen (512) und/oder Draenei (1024) → Startgebiete aus Burning Crusade
  const races = +(q.AllowableRaces ?? q.RequiredRaces ?? 0);
  if (races && (races & ~(512 | 1024)) === 0) return false;
  const text = [q.LogTitle, q.LogDescription, q.QuestDescription, q.AreaDescription, q.QuestCompletionLog,
    enReward.get(q.ID), enRequest.get(q.ID)].join(' ');
  if (LATER_PLACES.test(text)) return false;
  return true;
}
const BASE = 'https://raw.githubusercontent.com/azerothcore/azerothcore-wotlk/master/data/sql/base/db_world/';
const LOCALE = { de: 'deDE', fr: 'frFR', es: 'esES', ru: 'ruRU', ko: 'koKR', 'zh-CN': 'zhCN' };

fs.mkdirSync(cache, { recursive: true });
fs.mkdirSync(path.join(root, 'data'), { recursive: true });

async function load(table) {
  const file = path.join(cache, table + '.sql');
  if (!fs.existsSync(file)) {
    process.stdout.write(`lade ${table} … `);
    const r = await fetch(BASE + table + '.sql');
    if (!r.ok) throw new Error(`${table}: HTTP ${r.status}`);
    fs.writeFileSync(file, Buffer.from(await r.arrayBuffer()));
    console.log('ok');
  }
  return parseSql(fs.readFileSync(file, 'utf8'));
}

// Minimaler Parser für MySQL-Dumps: Spaltennamen aus CREATE TABLE, Werte aus INSERT … VALUES
function parseSql(sql) {
  const create = sql.indexOf('CREATE TABLE');
  const end = sql.indexOf(') ENGINE', create);
  const cols = [];
  for (const line of sql.slice(create, end).split('\n')) {
    const m = line.match(/^\s+`([^`]+)`\s/);
    if (m) cols.push(m[1]);
  }
  const rows = [];
  let i = 0;
  while ((i = sql.indexOf('VALUES', i)) >= 0) {
    i += 6;
    let row = null, val = '', inStr = false, isStr = false, depth = 0;
    for (; i < sql.length; i++) {
      const c = sql[i];
      if (inStr) {
        if (c === '\\') { const n = sql[++i]; val += n === 'n' ? '\n' : n === 'r' ? '' : n === 't' ? '\t' : n; }
        else if (c === "'") { if (sql[i + 1] === "'") { val += "'"; i++; } else inStr = false; }
        else val += c;
        continue;
      }
      if (c === "'") { inStr = true; isStr = true; continue; }
      if (c === '(' && depth === 0) { depth = 1; row = []; val = ''; isStr = false; continue; }
      if (depth === 0) { if (c === ';') break; continue; }
      if (c === ',' || c === ')') {
        row.push(isStr ? val : (val.trim() === 'NULL' ? null : val.trim()));
        val = ''; isStr = false;
        if (c === ')') { depth = 0; rows.push(row); }
        continue;
      }
      val += c;
    }
  }
  return { cols, rows };
}

function table(t) {
  const idx = Object.fromEntries(t.cols.map((c, i) => [c, i]));
  return t.rows.map(r => new Proxy(r, { get: (o, k) => (k in idx ? o[idx[k]] : undefined) }));
}

const clean = s => (s ?? '').replace(/\r/g, '').replace(/\n/g, '$B').replace(/\t/g, ' ').trim();
// Interne Platzhalter-Einträge der Datenbank aussortieren (nur eindeutige Markierungen, damit „Grizzly“ oder „Phylactery“ bleiben)
const junk = s => !s || /\[(UNUSED|PH|DEPRECATED|DND|TEST|NOT USED|NYI)\]|\b(UNUSED|DEPRECATED|OLD)\b|^(zz|ZZ|Test |TEST |\[|\()/.test(s);

// ---------- Englische Grundtexte ----------
const qt = table(await load('quest_template'));
const qor = table(await load('quest_offer_reward'));
const qri = table(await load('quest_request_items'));
const ct = table(await load('creature_template'));
const it = table(await load('item_template'));
const gt = table(await load('gameobject_template'));

// Feldpaare englisch ↔ Übersetzungstabelle
const questFields = [
  ['T', 'LogTitle', 'Title'], ['D', 'QuestDescription', 'Details'], ['O', 'LogDescription', 'Objectives'],
  ['E', 'AreaDescription', 'EndText'], ['C', 'QuestCompletionLog', 'CompletedText'],
  ['1', 'ObjectiveText1', 'ObjectiveText1'], ['2', 'ObjectiveText2', 'ObjectiveText2'],
  ['3', 'ObjectiveText3', 'ObjectiveText3'], ['4', 'ObjectiveText4', 'ObjectiveText4'],
];
const enQuest = new Map(qt.map(r => [r.ID, r]));
const enReward = new Map(qor.map(r => [r.ID, r.RewardText]));
const enRequest = new Map(qri.map(r => [r.ID, r.CompletionText]));

const qtl = table(await load('quest_template_locale'));
const qorl = table(await load('quest_offer_reward_locale'));
const qril = table(await load('quest_request_items_locale'));
const ctl = table(await load('creature_template_locale'));
const itl = table(await load('item_template_locale'));
const gtl = table(await load('gameobject_template_locale'));

for (const lang of sprachen) {
  const loc = LOCALE[lang];
  if (!loc) { console.log(`Sprache ${lang}: keine Übersetzung in der Datenbank – übersprungen`); continue; }

  // ---------- Questtexte ----------
  const seen = new Set(); const lines = []; const skipped = new Set();
  const add = (id, kind, en, tr) => {
    en = clean(en); tr = clean(tr);
    if (!en || !tr || en.length < 3) return;
    const key = en + '\u0001' + tr;
    if (seen.has(key)) return;
    seen.add(key);
    lines.push(`${id}\t${kind}\t${en}\t${tr}`);
  };
  for (const r of qtl) {
    if (r.locale !== loc) continue;
    const en = enQuest.get(r.ID); if (!en) continue;
    if (nurClassic && !isClassic(en)) { skipped.add(r.ID); continue; }
    for (const [kind, enCol, trCol] of questFields) add(r.ID, kind, en[enCol], r[trCol]);
  }
  for (const r of qorl) if (r.locale === loc && !skipped.has(r.ID)) add(r.ID, 'R', enReward.get(r.ID), r.RewardText);
  for (const r of qril) if (r.locale === loc && !skipped.has(r.ID)) add(r.ID, 'Q', enRequest.get(r.ID), r.CompletionText);
  const qFile = path.join(root, 'data', `quests-${lang}.txt.gz`);
  fs.writeFileSync(qFile, zlib.gzipSync(lines.join('\n'), { level: 9 }));

  // ---------- Namen ----------
  const names = new Map();
  const addName = (type, en, tr, subEn, subTr) => {
    en = clean(en); tr = clean(tr);
    if (junk(en) || !tr || en.length < 2) return;
    const key = type + '|' + en.toLowerCase();
    if (names.has(key)) return;
    names.set(key, `${type}\t${en}\t${tr}\t${clean(subEn)}\t${clean(subTr)}`);
  };
  const enC = new Map(ct.map(r => [r.entry, r])), enI = new Map(it.map(r => [r.entry, r])), enG = new Map(gt.map(r => [r.entry, r]));
  for (const r of ctl) { if (r.locale !== loc) continue; const e = enC.get(r.entry); if (e) addName('n', e.name, r.Name, e.subname, r.Title); }
  for (const r of itl) { if (r.locale !== loc) continue; const e = enI.get(r.ID); if (e) addName('i', e.name, r.Name, '', ''); }
  for (const r of gtl) { if (r.locale !== loc) continue; const e = enG.get(r.entry); if (e) addName('o', e.name, r.name, '', ''); }
  for (const r of qtl) { if (r.locale !== loc) continue; const e = enQuest.get(r.ID); if (e) addName('q', e.LogTitle, r.Title, '', ''); }
  const nFile = path.join(root, 'data', `namen-${lang}.txt.gz`);
  fs.writeFileSync(nFile, zlib.gzipSync([...names.values()].join('\n'), { level: 9 }));

  const mb = f => (fs.statSync(f).size / 1048576).toFixed(1) + ' MB';
  console.log(`${lang}: ${lines.length} Questtexte (${mb(qFile)}), ${names.size} Namen (${mb(nFile)})`);
}
