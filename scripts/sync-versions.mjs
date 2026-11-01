#!/usr/bin/env node
// Copies the project version from the root package.json (bumped by changesets) into the TV
// app manifests, so both apps of the TV package carry the release version:
//
//   tv/ui/config.xml              <widget version="...">
//   tv/service/tizen-manifest.xml <manifest version="...">
//
// Runs as part of `npm run version-packages`, so the "chore: release" PR bumps them too.
//   node scripts/sync-versions.mjs           update the manifests
//   node scripts/sync-versions.mjs --check   exit 1 if they differ (CI)
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const check = process.argv.includes('--check');
const { version } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

// Tizen accepts only numeric versions: major and minor 0–255, patch 0–65535.
const m = /^(\d+)\.(\d+)\.(\d+)$/.exec(version);
if (!m || +m[1] > 255 || +m[2] > 255 || +m[3] > 65535) {
  console.error(`package.json version "${version}" is not a valid Tizen version (X.Y.Z, X,Y ≤ 255, Z ≤ 65535)`);
  process.exit(1);
}

const targets = [
  ['tv/ui/config.xml', 'widget'],
  ['tv/service/tizen-manifest.xml', 'manifest'],
];

let stale = 0;
for (const [file, element] of targets) {
  const path = join(root, file);
  const xml = readFileSync(path, 'utf8');
  // \s before "version" so api-version="..." on the same element doesn't match.
  const re = new RegExp(`(<${element}\\b[^>]*?\\sversion=")([^"]*)(")`);
  const found = re.exec(xml);
  if (!found) {
    console.error(`${file}: no version attribute on <${element}>`);
    process.exit(1);
  }
  if (found[2] === version) continue;
  if (check) {
    console.error(`${file}: version ${found[2]}, package.json has ${version} — run: node scripts/sync-versions.mjs`);
    stale++;
  } else {
    writeFileSync(path, xml.replace(re, `$1${version}$3`));
    console.log(`${file}: ${found[2]} → ${version}`);
  }
}
process.exit(stale ? 1 : 0);
