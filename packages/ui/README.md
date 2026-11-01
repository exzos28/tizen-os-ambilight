# @ambilight/ui

The shared look of the project's two web apps: the TV launcher (`tv/app`, a 1920×1080 canvas
driven by the remote) and the controller's phone web app (`esp32/web`, served from the
ESP32's flash). Design tokens, an inline-SVG icon set and presentational Preact components,
with the values of the TV launcher's design.

Private package, consumed as TypeScript source like [`@ambilight/protocol`](../../protocol/README.md#using-ambilightprotocol):
not versioned on its own, it ships inside the apps. `preact` is a peer dependency and the
only runtime one; no CSS-in-JS, no webfonts.

```
src/
  tokens.css            colors, status colors, strip side colors, type scale, spacing, radii;
                        density presets [data-density="tv"] and [data-density="phone"] (default);
                        the TV keeps the browser's normal line height, body copy sets its own
  base.css              page base (background, font) and text classes: ui-label, ui-value-xl,
                        ui-value, ui-unit, ui-desc, ui-muted, ui-num, ui-sr (visually hidden)
  icons.tsx             Home, Picture, Layout, Chip, Pulse, Screen, Circle, Power, Warn, Error,
                        Check, Refresh, Wifi, Blink icons and the Logo
  Card.tsx              panel / tile; title + aside; tone warn|bad; focused, adjusting
  Switch.tsx            on/off switch, visual only (accent = the big blue one)
  SliderBar.tsx         level bar with optional knob; `inset` lines the knob up with a native range thumb
  Button.tsx            <button> primary | secondary | danger; focused, block, icon
  Status.tsx            StatusDot, StatusPill, StatusLine (title, detail, value; card), StatusList
  ProblemBanner.tsx     icon, title, text, detail, hints, actions
  ColorSwatches.tsx     the six static colors (SWATCHES); buttons with onSelect, else plain + focusedIndex
  Option.tsx            one choice of a radio group (Follow the picture / Static color), visual only
  KeyHint.tsx           TV only: Kbd, KeyHint (remote key caps)
  LivePreview.tsx       edge-color ring from a status `rgb` string (EdgeFrame order) inside a 2 px
                        screen frame + LED dots, SVG
  StripLayoutDiagram.tsx  the strip around the screen: sides red/green/yellow/blue as the
                        calibration preview lights them, LED 1 at the start corner, direction arrows
  geometry.ts           pure helpers: parseEdgeColors, interpolate, sideOrder, sideArrows (tested)
```

## Rules

- **Presentational only.** Components take state through props (`selected`, `focused`,
  `disabled`, values, callbacks) and handle no platform input: no key handling, no focus
  manager. Focus rings come from a `focused` prop (the TV's focus manager) or `:focus-visible`
  (real focus on a phone). Where a phone needs a native control, the kit draws and the app puts
  the native element next to it (a transparent `<input type="range">` over a `SliderBar`, a
  hidden radio before an `Option`, a `<button role="switch">` around a `Switch`).
- **Each component imports its own small stylesheet**, so a bundle contains only the CSS of
  what it renders: the ESP32 build keeps no `KeyHint` or `LivePreview` CSS. `sideEffects` lists
  only `*.css`, so unused components drop out of the barrel import.
- **Status is never color alone:** pills and lines always carry text. Text colors keep ≥ 4.5:1
  on the panels (`--ui-muted` #a3aebd on `--ui-panel` is 7.8:1).
- Cheap for the TV (Chromium 94, little RAM): no blur, no shadows, no `color-mix()`, no
  transitions in the kit.

## Using it in an app

The root has no npm workspaces (see [protocol/README.md](../../protocol/README.md#using-ambilightprotocol)),
so the package is linked by path:

1. `package.json`: `"@ambilight/ui": "file:../../packages/ui"` and `"preact"` as a dependency.
2. Entry: `import '@ambilight/ui/tokens.css'` and `import '@ambilight/ui/base.css'` first, then
   the app (components bring their CSS), then the app's own stylesheet so it can adjust the kit.
3. Root element: `<html data-density="tv">` or `data-density="phone"`.
4. `vite.config.ts`: `resolve: { dedupe: ['preact'] }` (the kit has its own `node_modules/preact`
   for its type-check; the bundle must use the app's single copy) and, for the dev server,
   `server.fs.allow` with `'../../packages/ui'`.
5. `tsconfig.json`: `jsxImportSource: "preact"`, `allowImportingTsExtensions`, and `paths` so the
   kit's files resolve `preact` to the app's copy too:
   `"preact": ["./node_modules/preact/src/index.d.ts"], "preact/*": ["./node_modules/preact/*/src/index.d.ts"]`.

Both `tv/app` and `esp32/web` are set up this way.

## Checks

```sh
cd packages/ui
npm ci
npm run type-check     # tsc on src and test
npm test               # node --test on geometry.ts (Node 24, type stripping)
```

## Where the apps use it

Both apps are built on the kit and keep only their shell in their own stylesheet: the TV
launcher (`tv/app`, `<html data-density="tv">`, focus from its focus manager through
`focused` props) and the phone web app (`esp32/web`, `data-density="phone"`, native focus and
inputs). The TV density is the launcher's design to the pixel; check a change to the kit with
screenshots of the launcher's mock (`tv/app`, `?state=ok|lost|none|off|unsupported`) against
`docs/media/tv-home*.png`, and of `esp32/web`'s `npm run build:mock`.

| Kit | TV launcher (`tv/app`) | Phone (`esp32/web`) |
|---|---|---|
| `tokens.css`, `base.css` | Entry imports them; `style.css` has the rail, header, footer and the screens' grids | Same; `app.css` has the top bar, tab bar and forms |
| Icons, `Logo` | Rail (`size` 28), mode options (26) | Tab bar, setup |
| `Card` | Home tiles (`focused`, `adjusting`), live preview and status panels, placeholder screens | Every section |
| `Switch` | Lighting tile (`accent`), compact tile (plain) | Inside a `<button role="switch">` |
| `SliderBar` | Brightness (`knob`; `muted` in the compact tile); no `inset` | Under a transparent `<input type="range">` (`inset`) |
| `Option` | *Follow the picture* / *Static color* (`checked`, `focused`) inside `role="radio"` wrappers | After a hidden radio input |
| `ColorSwatches`, `SWATCHES` | Plain elements with `selected`, `focusedIndex`, `small` | Buttons with `onSelect` |
| `StatusPill` | Header summary | Top bar, unsaved layout |
| `StatusLine`, `StatusList` | Status panel; compact `card` row while a problem is shown | Home and Device status |
| `ProblemBanner` | Top of Home, `Button`s as `actions` | Home, Wi-Fi setup |
| `Button` | Banner and *Test pattern* (`focused`, `tabIndex={-1}`: never focused natively) | Forms and actions |
| `Kbd`, `KeyHint` | Footer and tile hints, test pattern | — |
| `LivePreview` | Home (`width={520}`, 1:1) | — |
| `StripLayoutDiagram` | *Strip layout* screen (to build): `focused` = `corner-0`…`corner-3` or a side name, no callbacks | Layout screen with `onCorner`, `onSide` |
