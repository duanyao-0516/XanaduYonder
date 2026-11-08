// One-time, deterministic extraction from the reviewed shipped H5 sources.
// Requires their pre-removal checkout; generated data remains independently hash-verifiable.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';
const root = path.resolve(process.argv[2] || '.');
const source = path.resolve(process.argv[3] || path.join(root, 'artifacts/gold-miner-source-reference'));
if (!fs.existsSync(path.join(source,'solo/game.js'))) throw new Error('Pass the reviewed source web directory as the second argument; it is not a build dependency.');
const output = path.join(root, 'Assets/Resources/GoldMiner/Source');
fs.mkdirSync(output, { recursive: true });
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const solo = fs.readFileSync(path.join(source, 'solo/game.js'), 'utf8');
const first = solo.indexOf('return a.levels = [') + 'return a.levels = '.length;
const last = solo.indexOf('], a', first) + 1;
const maps = vm.runInNewContext('(' + solo.slice(first, last) + ')');
const start = solo.indexOf('BoulderData = function()');
const stop = solo.indexOf('    Bonuses = function()', start);
const context = { Game: { bonuses: {} } };
vm.runInNewContext(solo.slice(start, stop).trim().replace(/,$/, ';') + 'BoulderData.init();', context);
const duo = await import(pathToFileURL(path.join(source, 'duo/js/game-data.mjs')));
const atlas = JSON.parse(fs.readFileSync(path.join(source, 'solo/assets/atlas.json'), 'utf8'));
const frames = Object.fromEntries(atlas.frames.map(frame => [frame.filename, frame]));
const soloKinds = [...new Set(maps.flatMap(map => map.data.map(item => item.name)))];
const soloData = { schemaVersion: 1, mode: 'solo', width: 640, height: 480, originY: 46,
  maps, types: context.BoulderData.data, frames: Object.fromEntries(soloKinds.map(key => [key, frames[key]])) };
const sizes = {};
for (const key of Object.keys(duo.ORE_TYPES)) {
  const png = fs.readFileSync(path.join(source, 'duo/resource/assets/ui', key + '.png'));
  sizes[key] = { width: png.readUInt32BE(16), height: png.readUInt32BE(20) };
  fs.copyFileSync(path.join(source, 'duo/resource/assets/ui', key + '.png'), path.join(output, 'duo-' + key + '.png'));
}

const duoData = { schemaVersion: 1, mode: 'coop', width: 550, height: 400, originY: 55, maps: duo.MAPS, types: duo.ORE_TYPES, sizes };
const entries = [];
for (const [name, data, count] of [['solo', soloData, 611], ['coop', duoData, 594]]) {
  const json = JSON.stringify(data) + '\n'; fs.writeFileSync(path.join(output, name + '.json'), json);
  const actual = name === 'solo' ? data.maps.reduce((n,m)=>n+m.data.length,0) : Object.values(data.maps).reduce((n,m)=>n+m.length,0);
  if (actual !== count) throw new Error(name + ' entity count mismatch');
  entries.push({ mode: name, file: name + '.json', sha256: hash(json), mapCount: 30, entityCount: count });
}
const sourceSprites = JSON.parse(execFileSync('python3', [path.join(root, 'scripts/gold-miner/extract-source-sprites.py'), path.join(source, 'solo/assets/atlas.png'), path.join(output, 'solo.json'), output], {encoding:'utf8'}));
const sourceFiles = ['solo/game.js','solo/adapter.js','solo/layout.js','solo/state.js','solo/assets/atlas.json','solo/assets/atlas.png','duo/js/game-data.mjs','duo/js/game.mjs','duo/js/layout.mjs','duo/js/state-core.mjs'];
const manifest = { schemaVersion: 1, rulesVersion: 5, campaign: 'source-v1', unitsPerPixel: 0.01,
  sourceSprites, sourceFiles: sourceFiles.map(file=>({file,sha256:hash(fs.readFileSync(path.join(source,file)))})), files: entries,
  solo: {source:'https://www.4399.com/flash/230816.htm',selection:'n<=10 ? 3*(n-1)+random(3) : 3*((n-6)%10)+random(3)',target:'5*(27*n*n+28*n+75)',durationTicks:3720,coordinate:'x=(source.x+35)/100; y=(source.y+50-46)/100; preserve scale and flip',shopDebt:true},
  coop: {source:'https://www.4399.com/flash/232532.htm',selection:'map=n; n=1..30',target:'500+150*n',durationTicks:2400,coordinate:'x=source.x/100; y=(source.y-55)/100',shopDebt:false},
  extractionVerification: {soloMaps:30,soloObjects:611,coopMaps:30,coopObjects:594,exactRawFieldComparison:true} };
fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify(manifest,null,2)+'\n');
// Compare the persisted JSON against the evaluated original definitions, not counts alone.
if (JSON.stringify(JSON.parse(fs.readFileSync(path.join(output,'solo.json'))).maps)!==JSON.stringify(maps) ||
    JSON.stringify(JSON.parse(fs.readFileSync(path.join(output,'coop.json'))).maps)!==JSON.stringify(duo.MAPS)) throw new Error('Source field mismatch');
console.log(JSON.stringify({verified:entries,totalObjects:1205,sourceFiles:manifest.sourceFiles},null,2));
