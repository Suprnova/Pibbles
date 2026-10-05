// Fails if a scope the grammar assigns is asserted by no grammar test, so no part of the grammar goes untested.
const fs = require('node:fs');
const path = require('node:path');
const yaml = require('js-yaml');

const root = path.join(__dirname, '..');
const grammar = yaml.load(fs.readFileSync(path.join(root, 'syntaxes', 'pibbles.tmLanguage.yaml'), 'utf8'));

const defined = new Set();
(function collect(node) {
  if (Array.isArray(node)) {
    node.forEach(collect);
  } else if (node && typeof node === 'object') {
    for (const [key, value] of Object.entries(node)) {
      if ((key === 'name' || key === 'contentName') && typeof value === 'string' && value.endsWith('.pibbles')) {
        value.split(/\s+/).forEach(scope => defined.add(scope));
      } else {
        collect(value);
      }
    }
  }
})(grammar.repository);

const testsFolder = path.join(root, 'tests');
const asserted = new Set(fs.readdirSync(testsFolder)
  .filter(file => file.endsWith('.pib'))
  .flatMap(file => fs.readFileSync(path.join(testsFolder, file), 'utf8').split(/\r?\n/))
  .filter(line => /^\/\/\s*(\^|<~*-+)/.test(line))
  .flatMap(line => line.replace(/^\/\/\s*[\^<~-]+/, '').split(/\s+/).filter(scope => scope.endsWith('.pibbles'))));

const missing = [...defined].filter(scope => !asserted.has(scope)).sort();
if (missing.length > 0) {
  console.error(`Scopes no grammar test asserts:\n${missing.map(scope => `  ${scope}`).join('\n')}`);
  process.exit(1);
}

console.log(`All ${defined.size} scopes are asserted.`);
