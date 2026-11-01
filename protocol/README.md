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
    ├── check.mjs             CI check
    └── validate.mjs          validates JSON files against a schema (also used by check.mjs)
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

## Validating JSON from other code

```sh
node protocol/scripts/validate.mjs service/status out/*.json    # schema: service/status,
                                                                 # a schema file path or its $id
```

Validates each file like `protocol:check` validates the fixtures (ajv, strict, JSON carried in
strings included — through `oneOf` branches too) and exits 1 if one doesn't match. CI uses it
for the **TV service contract check**: `.github/tv-service-check/Tests` builds the service's
real status, command result and command reply JSON with the service's own C# code (several
capture states, `version` null, outputs from the package / saved / after a bad saved file,
command successes and errors, with and without a request id) into `Tests/out/contract/`, and
the `tv-service` job validates `status.*.json` against `service/status`,
`command-result.*.json` against `service/command-result` and `command-reply.*.json` against
`service/command-reply`. The C# tests also feed the `outputs` fixtures to the service's
`set-outputs` parser, so both directions are covered. A service change that breaks the
contract fails CI; so does a schema change the service doesn't follow.

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
- **MessagePort messages** are bundles of string key/values. Values that carry JSON in a string
  (the `status` / `result` replies, the `outputs` of `set-outputs`) say so with
  `contentMediaType: application/json` and `contentSchema`; `check.mjs` validates the decoded
  value too. Requests that differ by command are a root `oneOf` of titled `$defs`
  (`ServiceCommandRequest` = `ServiceSimpleCommandRequest | ServiceSetOutputsRequest`).
- **Strings** may set `maxLength` (generated as `@maxLength`), e.g. the optional command `id`.
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
- `configChanged(readAt, metrics)` — whether GET /config should be read again: `metrics.config_rev`
  differs from the revision the config in hand was read at (`null`: none known → true).
  Revisions are compared for equality only. Firmware without `config_rev` → false.
- `buildStatusRequest()` / `buildCommandRequest(command, { id? })` /
  `buildSetOutputsRequest(outputs, { id? })` build the `[{key, value}]` array for
  `tizen.messageport`. `id` (up to `MAX_COMMAND_ID_LENGTH` = 64 characters) is echoed by the
  service in the reply, so a late reply can be matched to its request instead of being taken
  for the answer to the next one; without it the reply has none (older UIs and services).
  `buildSetOutputsRequest` sends only the `ServiceOutputs` keys; the service validates the
  values and answers `ok: false` with the reason if one is invalid.
- `parseServiceReply(data)` reads a `status` or `result` reply with a light runtime check —
  `{kind: 'result', id, result}` with `id` null when the reply has none — and throws
  `ServiceReplyError` on a malformed one; its `reply` says which kind (`status` / `result`) was
  malformed, so a bad `result` can still fail the pending command.

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
