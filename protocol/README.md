# Protocol definition

The single, language-neutral definition of every message the project's parts exchange. Code in
other languages is generated from it, and CI fails if the generated files are stale or an
example doesn't match its schema. [docs/NETWORKING.md](../docs/NETWORKING.md) is the
human-readable summary.

```
protocol/
├── constants.json            ports, discovery magic, frame layout, API paths, CORS, message port names, app IDs
├── constants.schema.json     format of constants.json
├── schemas/                  JSON Schema (draft 2020-12), one file per message
│   ├── esp32/                ESP32 HTTP API: GET responses, POST bodies, {"message"} reply
│   │   ├── settings.schema.json   every setting: range, default, unit (referenced by the others)
│   │   └── common.schema.json     shared value types
│   ├── service/              TV UI ↔ TV service over Tizen MessagePort
│   └── event-log/            TV service event log (HTTP POST JSON to port 9000)
├── fixtures/                 realistic example(s) of every message, mirroring schemas/
└── scripts/
    ├── gen.mjs               generator
    └── check.mjs             CI check
```

The binary UDP color frame and the discovery strings are not JSON; their layout is in
`constants.json` (`frame`, `discovery`) and NETWORKING.md.

## Generated code

| File | Contents |
|------|----------|
| `packages/protocol/src/generated/types.ts` | A TypeScript type per schema (`Esp32Config`, `ServiceStatus`, …), plus `LedMode` / `StartCorner` value objects and `CaptureStateValues`-style arrays for enums |
| `packages/protocol/src/generated/limits.ts` | `limits`: range, default and unit of every ESP32 setting (passwords left out) |
| `packages/protocol/src/generated/constants.ts` | `constants`: constants.json as an `as const` object |
| `esp32/src/generated/ProtocolLimits.h` | `constexpr` `_MIN` / `_MAX` / `_DEFAULT` of every ESP32 setting and the constants groups that have a `cppPrefix`, in `namespace protocol`. Header-only, only `<stdint.h>`, C++11 — usable from the native host tests |

Never edit them by hand. After changing anything in `protocol/`:

```sh
npm ci                    # once, at the repo root
npm run protocol:gen      # regenerate
npm run protocol:check    # what CI runs: generated files up to date, constants.json and every
                          # fixture valid (ajv, strict), C++ header compiles (if a compiler is found)
```

Commit the schema change, its fixture(s) and the regenerated files together. `protocol:gen`
takes an optional output directory (`node protocol/scripts/gen.mjs /tmp/out`).

## Writing schemas

`gen.mjs` understands a deliberately small subset of JSON Schema and stops on anything else
(e.g. `allOf`, `if`, `patternProperties`), so a schema change can't silently generate wrong code.

- **One message per file**, `<name>.schema.json`, with a `title` — the generated type name —
  and a `$id` under `https://raw.githubusercontent.com/exzos28/tizen-os-ambilight/main/protocol/schemas/`
  matching its path. Files with only `$defs` (`settings`, `common`) are definition libraries.
- **Field names are wire names.** Objects must set `"additionalProperties": false`.
- **Named types**: a `$defs` entry with a `title` becomes its own TypeScript type; one without is
  inlined where it is referenced.
- **Numeric enums with meaning** are written as `oneOf` of `const` branches whose `title` is the
  member name (`led_mode`: `plain` = 0, `dynamic` = 1). They generate `LedMode` (type and value
  object) and `LED_MODE_PLAIN` / `LED_MODE_DYNAMIC` in C++.
- **Settings** (`esp32/settings.schema.json` `$defs`) need `default`, and numeric ones `minimum`
  and `maximum`; `limits.ts` and the C++ header are generated from exactly these entries.
  Passwords are `"writeOnly": true`: never returned by the device, left out of `limits`.
- **Units** go in the `x-unit` annotation (`"W"`, `"ms"`, `"dBm"`, …).
- **POST bodies** are form-urlencoded on the wire; their schemas describe the values before
  encoding. Booleans in forms are `0` / `1` (`common.schema.json#/$defs/flag`), while GET /config
  returns real booleans.
- **MessagePort messages** are bundles of string key/values. Replies that carry JSON in a string
  say so with `contentMediaType: application/json` and `contentSchema`; `check.mjs` validates the
  decoded value too.
- **Fixtures**: `fixtures/<dir>/<name>.json` for `schemas/<dir>/<name>.schema.json`, more examples
  as `<name>.<variant>.json`. Every message schema needs at least one.

## Using `@ambilight/protocol`

`packages/protocol` is a private package with no runtime dependencies, consumed as TypeScript
source (`"exports": {".": "./src/index.ts"}`) by the Vite apps. It isn't versioned or tagged on
its own: it ships inside the apps that use it (and is listed in `ignore` in
`.changeset/config.json`).

```ts
import { createEsp32Client, limits, LedMode, buildStatusRequest, parseServiceReply, constants } from '@ambilight/protocol'

const esp = createEsp32Client('http://192.168.1.245', { timeoutMs: 3000 })   // '' = same origin
await esp.saveLeds({ brightness: limits.brightness.default })
await esp.saveMode({ led_mode: LedMode.dynamic })
```

- `createEsp32Client(baseUrl, { fetch?, timeoutMs? })` — a typed method per endpoint. POST
  bodies are sent form-urlencoded (`undefined` fields are left out, so the device keeps them).
  Non-2xx replies, timeouts, network errors and unexpected bodies throw `Esp32Error`
  (`reason`, `status`, `path`; `message` is the firmware's own message when it sent one).
- `buildStatusRequest()` / `buildCommandRequest(command)` build the `[{key, value}]` array for
  `tizen.messageport`; `parseServiceReply(data)` reads a `status` or `result` reply with a light
  runtime check and throws `ServiceReplyError` on a malformed one; its `reply` says which kind
  (`status` / `result`) was malformed, so a bad `result` can still fail the pending command.

Hooking it into an app (the root has no npm workspaces — see below):

1. `package.json`: `"@ambilight/protocol": "file:../../packages/protocol"` (relative to the app),
   then `npm install` in the app; npm links the folder.
2. `tsconfig.json`: the source imports with `.ts` extensions, so `allowImportingTsExtensions`
   (with `noEmit`) and `moduleResolution: "bundler"` are needed — the Vite template defaults.
3. If the Vite dev server answers 403 for the package's files (they are outside the app), allow
   them in `vite.config.ts`: `server: { fs: { allow: ['.', '../../packages/protocol'] } }`.
   Builds don't need this.

Tests run with Node's built-in runner on the TypeScript source (Node 24 type stripping):
`npm run protocol:test` and `npm run protocol:type-check` at the repo root.

**Why no npm workspaces at the root:** with a `workspaces` field, changesets treats the repo as
a monorepo of the workspace packages and stops versioning the root package — which carries the
project version (`vX.Y.Z` tags, firmware version, TV manifests) — so `changeset status` fails
on the existing changesets. The package therefore has no dependencies to install and is
linked by path.
