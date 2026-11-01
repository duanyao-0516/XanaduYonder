#!/usr/bin/env node
// Use the decoder in the hash-verified original; do not maintain a second Hx codec.
const fs = require('node:fs'), vm = require('node:vm'), path = require('node:path');
const [sourcePath, inputDirectory, outputDirectory] = process.argv.slice(2);
let source = fs.readFileSync(sourcePath, 'utf8');
const anchor = '  m.yP = [';
if (source.split(anchor).length !== 2) throw new Error('Hx extraction anchor changed');
source = source.replace(anchor, '  globalThis.decodeHx = a => new bi().read(Ha.Xg(a)).b.uv;\n' + anchor);
const context = vm.createContext({ console, TextDecoder, TextEncoder, window: {}, navigator: {}, performance: { now: () => 0 } });
vm.runInContext(source, context);
function walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const sourceFile = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(sourceFile);
    else if (entry.name.endsWith('.dds')) {
      const output = path.join(outputDirectory, path.relative(inputDirectory, sourceFile));
      fs.mkdirSync(path.dirname(output), { recursive: true });
      context.input = Array.from(fs.readFileSync(sourceFile));
      vm.runInContext('result = decodeHx(new Uint8Array(input).buffer)', context);
      fs.writeFileSync(output, Buffer.from(context.result));
    }
  }
}
walk(inputDirectory);
