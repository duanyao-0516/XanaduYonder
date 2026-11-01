#!/usr/bin/env node
/* Inspect the pinned bundle's actual registrations without starting the game or a browser. */
'use strict';
const fs = require('fs');
const vm = require('vm');
const source = fs.readFileSync(process.argv[2] === '-' ? 0 : process.argv[2], 'utf8');
const marker = 'O.init();new class';
const end = source.indexOf(marker);
if (end < 0 || end !== source.lastIndexOf(marker)) throw Error('Unrecognized game bootstrap');
const registrations = new Map();
const Laya = {
  View: class {}, Scene: class {}, Script: class {}, Script3D: class {},
  ClassUtils: { regClass(name, constructor) { registrations.set(name, constructor); } }
};
vm.runInNewContext(source.slice(0, end) + 'O.init();}();', { Laya }, { timeout: 1000 });
const views = {};
for (const [name, constructor] of registrations) if (constructor.uiView) views[name] = constructor.uiView;
process.stdout.write(JSON.stringify({ registrations: [...registrations.keys()].sort(), views }));
