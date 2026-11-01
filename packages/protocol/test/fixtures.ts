// Reads protocol/fixtures/<name>.json (validated against the schemas by `npm run protocol:check`).
import { readFileSync } from 'node:fs'

const dir = new URL('../../../protocol/fixtures/', import.meta.url)

export function fixture<T = unknown>(name: string): T {
  return JSON.parse(readFileSync(new URL(`${name}.json`, dir), 'utf8')) as T
}

export function fixtureText(name: string): string {
  return readFileSync(new URL(`${name}.json`, dir), 'utf8')
}
