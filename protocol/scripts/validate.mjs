#!/usr/bin/env node
// Validates JSON against the protocol schemas (ajv, strict), including JSON carried in strings
// (contentMediaType + contentSchema, e.g. the `status` value of a MessagePort reply). Used by
// check.mjs for the fixtures, and by CI for JSON produced by other code (the TV service's
// contract test, .github/tv-service-check/Tests):
//
//   node protocol/scripts/validate.mjs <schema> <file.json>...
//
// <schema>: `service/status`, `service/status.schema.json`, a path to the schema file, or its
// $id. Exits 1 if a file doesn't match (or can't be read). Needs `npm ci` at the repo root.
import { readFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { Ajv2020 } from 'ajv/dist/2020.js';
import { loadSchemas, schemasDir } from './schemas.mjs';

export function createValidator() {
  // Strict, except that "type": ["string", "null"] is how the schemas spell nullable fields.
  const ajv = new Ajv2020({ strict: true, allowUnionTypes: true, allErrors: true });
  ajv.addVocabulary(['x-unit']);
  const schemas = loadSchemas();
  for (const schema of schemas.values()) ajv.addSchema(schema);

  const compiled = (id) => {
    const validate = ajv.getSchema(id);
    if (!validate) throw new Error(`no schema ${id}`);
    return validate;
  };

  // The JSON-in-string properties of data under the schema at id: [{prop, id}] of the
  // content schemas. Follows $refs and the oneOf / anyOf branches that data matches.
  const jsonStrings = (id, data) => {
    const schema = compiled(id).schema;
    if (schema.$ref) return jsonStrings(new URL(schema.$ref, id).href, data);
    const found = [];
    for (const branch of [...(schema.oneOf ?? []), ...(schema.anyOf ?? [])]) {
      if (!branch.$ref) continue;
      const branchId = new URL(branch.$ref, id).href;
      if (compiled(branchId)(data)) found.push(...jsonStrings(branchId, data));
    }
    for (const [prop, ps] of Object.entries(schema.properties ?? {})) {
      if (ps.contentMediaType !== 'application/json' || !ps.contentSchema?.$ref) continue;
      if (typeof data?.[prop] === 'string') found.push({ prop, id: new URL(ps.contentSchema.$ref, id).href });
    }
    return found;
  };

  // Problems of data against the schema with this $id (empty if it is valid); label names
  // data in the messages.
  const check = (id, data, label) => {
    const validate = compiled(id);
    if (!validate(data)) return [ajv.errorsText(validate.errors, { dataVar: label })];
    const problems = [];
    for (const { prop, id: inner } of jsonStrings(id, data)) {
      let decoded;
      try {
        decoded = JSON.parse(data[prop]);
      } catch (e) {
        problems.push(`${label}: "${prop}" is not JSON: ${e.message}`);
        continue;
      }
      problems.push(...check(inner, decoded, `${label}.${prop}`));
    }
    return problems;
  };

  // `service/status`, `service/status.schema.json`, a file path or an $id → the $id.
  const resolveSchema = (arg) => {
    if (/^https?:\/\//.test(arg)) return arg;
    const candidates = [arg, `${arg}.schema.json`, relative(schemasDir, resolve(arg)).split('\\').join('/')];
    for (const key of candidates) if (schemas.has(key)) return schemas.get(key).$id;
    throw new Error(`unknown schema "${arg}" (expected e.g. service/status, or a file under protocol/schemas)`);
  };

  return { ajv, schemas, check, resolveSchema };
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [schemaArg, ...files] = process.argv.slice(2);
  if (!schemaArg || !files.length) {
    console.error('usage: node protocol/scripts/validate.mjs <schema> <file.json>...');
    process.exit(2);
  }
  const { check, resolveSchema } = createValidator();
  const id = resolveSchema(schemaArg);
  let failed = 0;
  for (const file of files) {
    let problems;
    try {
      problems = check(id, JSON.parse(readFileSync(file, 'utf8')), file);
    } catch (e) {
      problems = [`${file}: ${e.message}`];
    }
    if (problems.length) {
      failed++;
      for (const p of problems) console.error(`✗ ${p}`);
    } else {
      console.log(`✓ ${file}`);
    }
  }
  console.log(`${files.length - failed}/${files.length} valid against ${schemaArg}`);
  process.exit(failed ? 1 : 0);
}
