// Loads protocol/schemas and resolves $refs between them. Shared by gen.mjs and check.mjs.
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join, posix, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
export const protocolDir = join(repoRoot, 'protocol');
export const schemasDir = join(protocolDir, 'schemas');
export const fixturesDir = join(protocolDir, 'fixtures');

const toPosix = (p) => p.split(sep).join('/');

export function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8'));
}

// Sorted relative posix paths of every file under dir whose name ends with suffix.
export function listFiles(dir, suffix) {
  const out = [];
  const walk = (d) => {
    for (const entry of readdirSync(d, { withFileTypes: true })) {
      const full = join(d, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith(suffix)) out.push(toPosix(relative(dir, full)));
    }
  };
  walk(dir);
  return out.sort();
}

// Map of 'esp32/settings.schema.json' → parsed schema, sorted by path.
export function loadSchemas() {
  const schemas = new Map();
  for (const file of listFiles(schemasDir, '.schema.json')) schemas.set(file, readJson(join(schemasDir, file)));
  return schemas;
}

// A schema with a type of its own (not just a $defs library) describes a message and needs
// a title and at least one fixture.
export function isMessageSchema(schema) {
  return ['type', 'oneOf', 'anyOf', 'enum', 'const', '$ref'].some((k) => k in schema);
}

export class SchemaSet {
  constructor(schemas) {
    this.schemas = schemas;
  }

  // ref: 'other.schema.json#/$defs/x', '#/$defs/x' or 'other.schema.json', relative to file.
  resolve(file, ref) {
    const [target, pointer = ''] = ref.split('#');
    const targetFile = target ? posix.normalize(posix.join(posix.dirname(file), target)) : file;
    const root = this.schemas.get(targetFile);
    if (!root) throw new Error(`${file}: $ref "${ref}" points to a missing file ${targetFile}`);
    let schema = root;
    for (const raw of pointer.split('/').slice(1)) {
      const key = raw.replace(/~1/g, '/').replace(/~0/g, '~');
      schema = schema?.[key];
      if (schema === undefined) throw new Error(`${file}: $ref "${ref}" does not resolve`);
    }
    return { file: targetFile, pointer, key: `${targetFile}#${pointer}`, schema };
  }
}
