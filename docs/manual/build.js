// Builds docs/manual/materials-manual.html from template.html and the game's material and reaction tables.
// Usage (from the repo root): node docs/manual/build.js
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const root = path.resolve(__dirname, '..', '..');
const json = path.join(os.tmpdir(), 'strata-materials.json');
execFileSync('dotnet', ['run', '--project', path.join(root, 'tools', 'Strata.Sim.Bench'), '--', '--materials-json', json], { stdio: 'inherit' });
const data = fs.readFileSync(json, 'utf8');
const template = fs.readFileSync(path.join(__dirname, 'template.html'), 'utf8');
const out = template.replace('/*DATA*/null', data.replace(/<\/script/gi, '<\\/script'));
fs.writeFileSync(path.join(__dirname, 'materials-manual.html'), out);
console.log('wrote docs/manual/materials-manual.html');
