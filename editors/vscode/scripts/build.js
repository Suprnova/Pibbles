// Compiles the YAML grammar to the JSON that VS Code reads, and copies the repository's license in for packaging.
const fs = require('node:fs');
const path = require('node:path');
const yaml = require('js-yaml');

const root = path.join(__dirname, '..');
const grammar = yaml.load(fs.readFileSync(path.join(root, 'syntaxes', 'pibbles.tmLanguage.yaml'), 'utf8'));

fs.writeFileSync(path.join(root, 'syntaxes', 'pibbles.tmLanguage.json'), JSON.stringify(grammar, null, 2) + '\n');
fs.copyFileSync(path.join(root, '..', '..', 'LICENSE'), path.join(root, 'LICENSE'));
