// Picture: how the strip turns screen colors into light. Brightness, gamma, saturation and white
// balance, the power limit, presets, a before / after strip from the current frame and a reset.
// ▲▼ move between rows, ◀▶ change the focused value at once; the change is sent to every
// controller 300 ms after the last key. Several controllers: the first answering one's values,
// "Mixed" where they differ.

import { useEffect, useRef, useState } from 'preact/hooks';
import '@ambilight/ui/tokens.css';
import { Button, Card, CheckIcon, KeyHint, SliderBar, cssRgb, parseEdgeColors, type Rgb } from '@ambilight/ui';
import { type CaptureStatus, type Esp32Config, limits } from '@ambilight/protocol';
import { Footer } from '../components/Footer';
import { type Controller, unreachable } from '../controllers';
import { EDGE_LEFT, type FocusGrid, step } from '../focus';
import { live } from '../status';
import type { ScreenProps } from './types';
import './picture.css';

type Field = 'brightness' | 'gamma' | 'saturation' | 'wb_r' | 'wb_g' | 'wb_b' | 'power_limit_w' | 'pixel_watts';
type Values = Pick<Esp32Config, Field>;
type Correction = Pick<Esp32Config, 'gamma' | 'saturation' | 'wb_r' | 'wb_g' | 'wb_b'>;

const FIELDS: Field[] = ['brightness', 'gamma', 'saturation', 'wb_r', 'wb_g', 'wb_b', 'power_limit_w', 'pixel_watts'];
const CORRECTION: (keyof Correction)[] = ['gamma', 'saturation', 'wb_r', 'wb_g', 'wb_b'];

/** One ◀▶ press, in the field's unit (brightness: percent). */
const STEP: Record<Field, number> = {
  brightness: 5, gamma: 0.1, saturation: 0.1, wb_r: 5, wb_g: 5, wb_b: 5, power_limit_w: 5, pixel_watts: 0.01,
};
const DECIMALS: Record<Field, number> = {
  brightness: 0, gamma: 1, saturation: 1, wb_r: 0, wb_g: 0, wb_b: 0, power_limit_w: 0, pixel_watts: 2,
};
/** Long ranges move ten steps at a time while the key repeats. */
const FAST = new Set<Field>(['power_limit_w', 'pixel_watts']);
const FAST_AFTER = 6;
const REPEAT_MS = 300;

const SEND_DEBOUNCE_MS = 300;
const RESET_CONFIRM_MS = 3000;
const PRESET_KEY = 'ambilight.picture.preset';

const NAMES: Record<Field, string> = {
  brightness: 'brightness', gamma: 'gamma', saturation: 'saturation', wb_r: 'red', wb_g: 'green', wb_b: 'blue',
  power_limit_w: 'the power limit', pixel_watts: 'LED power',
};

const DEFAULTS: Correction = {
  gamma: limits.gamma.default, saturation: limits.saturation.default,
  wb_r: limits.wb_r.default, wb_g: limits.wb_g.default, wb_b: limits.wb_b.default,
};

interface Preset { id: string; name: string; desc: string; values: Correction }

const PRESETS: Preset[] = [
  { id: 'preset-natural', name: 'Natural', desc: 'Gamma 2.2 · saturation 1.0×', values: DEFAULTS },
  { id: 'preset-cinema', name: 'Cinema', desc: 'Warm white · saturation 1.2×', values: { gamma: 2.4, saturation: 1.2, wb_r: 255, wb_g: 236, wb_b: 214 } },
  { id: 'preset-vivid', name: 'Vivid', desc: 'Games and animation · 1.6×', values: { gamma: 2.2, saturation: 1.6, wb_r: 255, wb_g: 255, wb_b: 255 } },
];

const clamp = (f: Field, v: number) => Math.min(limits[f].max, Math.max(limits[f].min, v));
const round = (v: number, d: number) => Number(v.toFixed(d));
const pctOf = (b: number) => Math.round((b * 100) / limits.brightness.max);
const byteOf = (pct: number) => Math.round((pct * limits.brightness.max) / 100);
/** Position of a value on its slider, 0…1. */
const pos = (f: Field, v: number) => (v - limits[f].min) / (limits[f].max - limits[f].min);
const same = (a: Correction, b: Correction) =>
  Math.abs(a.gamma - b.gamma) < 0.05 && Math.abs(a.saturation - b.saturation) < 0.05
  && a.wb_r === b.wb_r && a.wb_g === b.wb_g && a.wb_b === b.wb_b;
const pick = (v: Values): Correction => ({ gamma: v.gamma, saturation: v.saturation, wb_r: v.wb_r, wb_g: v.wb_g, wb_b: v.wb_b });

/** Shown with one decimal where the value sits on the 0.1 grid, else two (set elsewhere). */
const dec = (v: number, d: number) => (round(v, d) === round(v, 2) ? v.toFixed(d) : v.toFixed(2));

function nudge(f: Field, cur: number, dir: 1 | -1, fast: boolean): number {
  if (f === 'brightness') {
    const pct = Math.max(0, Math.min(100, Math.round(pctOf(cur) / 5) * 5 + dir * 5));
    return byteOf(pct);
  }
  const base = STEP[f];
  const n = Math.round(cur / base) + dir * (fast ? 10 : 1);
  return round(clamp(f, n * base), DECIMALS[f]);
}

// ---------------------------------------------------------------- saved preset (this TV only)

function readMine(): Correction | null {
  try {
    const raw = localStorage.getItem(PRESET_KEY);
    if (!raw) return null;
    const o = JSON.parse(raw) as Record<string, unknown>;
    const out = {} as Correction;
    for (const k of CORRECTION) {
      const v = o[k];
      if (typeof v !== 'number' || !Number.isFinite(v)) return null;
      out[k] = clamp(k, v);
    }
    return out;
  } catch {
    return null;
  }
}

function writeMine(v: Correction): boolean {
  try {
    localStorage.setItem(PRESET_KEY, JSON.stringify(v));
    return true;
  } catch {
    return false;
  }
}

// ---------------------------------------------------------------- before / after

/**
 * Roughly what the controller does to a frame before gamma: saturation scaled in HSV (hue and
 * value kept), then white balance. Gamma is left out: it shapes the LEDs' response, and the
 * screen already has its own.
 */
function correct(c: Rgb, v: Correction): Rgb {
  const max = Math.max(c[0], c[1], c[2]);
  const min = Math.min(c[0], c[1], c[2]);
  let out: Rgb = c;
  if (max > 0 && max > min && v.saturation !== 1) {
    const s = (max - min) / max;
    const f = Math.min(1, s * v.saturation) / s;
    out = [max - (max - c[0]) * f, max - (max - c[1]) * f, max - (max - c[2]) * f];
  }
  return [out[0] * v.wb_r / 255, out[1] * v.wb_g / 255, out[2] * v.wb_b / 255];
}

// ---------------------------------------------------------------- screen

export function Picture(p: ScreenProps) {
  const { controllers, link } = p;
  const usable = controllers.filter((c) => c.present && c.config && !unreachable(c));
  const reading = !usable.length && controllers.some((c) => c.present && !c.config && c.failures === 0);
  if (!usable.length) return <Empty {...p} reading={reading} />;
  return <Settings {...p} usable={usable} capture={link.kind === 'up' ? link.status.capture : null} />;
}

function Empty(p: ScreenProps & { reading: boolean }) {
  const down = p.link.kind !== 'up';
  const title = p.reading ? 'Reading the controller…' : 'No LED controller';
  const text = p.reading
    ? 'The settings come from the controller and show here in a moment.'
    : down
      ? 'The service is not running, so the launcher does not know of any controller. Picture settings live on the controller.'
      : 'Picture settings live on the controller. Outputs shows the controllers on the network and why one is missing.';
  p.bindKeys((key) => {
    if (key === 'ok') { p.open('outputs'); return true; }
    if (key === 'left') return 'rail';
    return key !== 'back';
  });
  return (
    <main class="main pic" data-density="tv">
      <Header saving={false} count={0} />
      <Card class="pic-empty" title={title}>
        <p class="pic-empty-text">{text}</p>
        <div><Button variant="primary" focused={p.active}>Open Outputs</Button></div>
      </Card>
      <Footer>
        <KeyHint k="OK">Open Outputs</KeyHint>
        <KeyHint k="◀">Menu</KeyHint>
        <KeyHint k="↩">Home</KeyHint>
      </Footer>
    </main>
  );
}

function Header({ saving, count }: { saving: boolean; count: number }) {
  return (
    <header class="header">
      <div class="header-titles">
        <h1>Picture</h1>
        <p class="subtitle">How the strip turns screen colors into light · changes show on the strip at once</p>
      </div>
      {count > 0 && (
        <span class="pic-saved" role="status">
          {saving ? <span class="pic-saving-dot" /> : <span class="pic-check"><CheckIcon size={20} /></span>}
          {saving ? 'Saving…' : count === 1 ? 'Saved on the controller' : `Saved on ${count} controllers`}
        </span>
      )}
    </header>
  );
}

interface SettingsProps extends ScreenProps {
  usable: Controller[];
  capture: CaptureStatus | null;
}

function Settings(p: SettingsProps) {
  const { usable, capture } = p;
  const cfgs = usable.map((c) => c.config!);
  const lead = cfgs[0];

  // ---------------------------------------------------------------- local edits, sent debounced
  const [draft, setDraft] = useState<Partial<Values>>({});
  const pending = useRef<Partial<Values>>({});
  const timer = useRef(0);
  const actions = useRef(p.actions);
  actions.current = p.actions;
  const esp32 = p.backend.esp32;
  const alive = useRef(true);

  const flush = () => {
    clearTimeout(timer.current);
    timer.current = 0;
    const body = pending.current;
    pending.current = {};
    if (!Object.keys(body).length) return;
    actions.current.update(() => body, (h) => esp32(h).saveLeds(body));
    if (alive.current) setDraft({});
  };
  const change = (patch: Partial<Values>, now = false) => {
    pending.current = { ...pending.current, ...patch };
    setDraft(pending.current);
    clearTimeout(timer.current);
    if (now) flush();
    else timer.current = window.setTimeout(flush, SEND_DEBOUNCE_MS);
  };
  // Leaving the screen sends what is still waiting.
  useEffect(() => () => { alive.current = false; flush(); }, []);

  const v = {} as Values;
  for (const f of FIELDS) v[f] = draft[f] ?? lead[f];
  const mixed = (f: Field) => draft[f] == null && cfgs.some((c) => c[f] !== lead[f]);
  const saving = Object.keys(draft).length > 0;

  // ---------------------------------------------------------------- presets
  const [mine, setMine] = useState<Correction | null>(readMine);
  const [saveNote, setSaveNote] = useState<string | null>(null);
  const [confirmReset, setConfirmReset] = useState(false);
  const resetTimer = useRef(0);
  useEffect(() => () => clearTimeout(resetTimer.current), []);

  const current = pick(v);
  const presets: Preset[] = mine
    ? [...PRESETS, { id: 'preset-mine', name: 'My preset', desc: 'Saved on this TV', values: mine }]
    : PRESETS;
  const anyMixed = CORRECTION.some(mixed);

  // ---------------------------------------------------------------- focus grid
  const tiles = [...presets.map((it) => it.id), 'preset-save'];
  const leftOf = ['brightness', 'gamma', 'saturation'];
  const grid: FocusGrid = FIELDS.map((f) => [{ id: f, col: 0 }]);
  for (let i = 0; i < tiles.length; i += 2) {
    grid.push(tiles.slice(i, i + 2).map((id, j) => ({ id, col: 1 + j, nav: j === 0 ? { left: leftOf[i / 2] ?? 'wb_r' } : {} })));
  }
  grid.push([{ id: 'reset', col: 1, nav: { left: 'pixel_watts' } }, { id: 'static', col: 2 }]);

  const [focus, setFocus] = useState<string>('brightness');
  const ids = grid.flat().map((it) => it.id);
  const cur = ids.includes(focus) ? focus : 'brightness';
  useEffect(() => { if (cur !== 'reset') setConfirmReset(false); }, [cur]);

  const repeat = useRef({ key: '', at: 0, n: 0 });
  const adjust = (f: Field, dir: 1 | -1) => {
    const now = Date.now();
    const r = repeat.current;
    const k = `${f}${dir}`;
    r.n = r.key === k && now - r.at < REPEAT_MS ? r.n + 1 : 0;
    r.key = k;
    r.at = now;
    const next = nudge(f, v[f], dir, FAST.has(f) && r.n >= FAST_AFTER);
    if (next !== v[f] || mixed(f)) change({ [f]: next });
  };

  const ok = (id: string) => {
    const preset = presets.find((it) => it.id === id);
    if (preset) change(preset.values, true);
    else if (id === 'preset-save') {
      const saved = writeMine(current);
      setMine(saved ? current : mine);
      setSaveNote(saved ? 'Saved on this TV as My preset' : 'This TV could not keep the preset');
    } else if (id === 'reset') {
      if (!confirmReset) {
        setConfirmReset(true);
        clearTimeout(resetTimer.current);
        resetTimer.current = window.setTimeout(() => setConfirmReset(false), RESET_CONFIRM_MS);
      } else {
        clearTimeout(resetTimer.current);
        setConfirmReset(false);
        change({ ...DEFAULTS, brightness: limits.brightness.default }, true);
      }
    } else if (id === 'static') p.open('home');
  };

  p.bindKeys((key) => {
    if (key === 'back') return false;
    if (key === 'ok') { ok(cur); return true; }
    if ((key === 'left' || key === 'right') && (FIELDS as string[]).includes(cur)) {
      adjust(cur as Field, key === 'left' ? -1 : 1);
      return true;
    }
    const next = step(grid, cur, key);
    if (next === EDGE_LEFT) return 'rail';
    if (next) { setFocus(next); setSaveNote(null); }
    return true;
  });

  const isF = (id: string) => p.active && cur === id;

  // ---------------------------------------------------------------- view
  const lc = usable[0];
  const m = lc.metrics;
  const fullW = lead.num_leds * v.pixel_watts * v.brightness / limits.brightness.max;
  const used = m ? m.power_w : null;
  const share = used == null ? 0 : v.power_limit_w > 0 ? used / v.power_limit_w : fullW > 0 ? used / fullW : 0;
  const meterText = used == null ? 'Waiting for the controller'
    : v.power_limit_w > 0
      ? `${Math.round(share * 100)}% of the limit · ${m!.power_limited ? 'brightness reduced' : 'not reduced'}`
      : `No limit · full white would draw ${fullW.toFixed(0)} W`;

  const h = capture?.h ?? 0;
  const vv = capture?.v ?? 0;
  const raw = capture && live(capture) ? parseEdgeColors(capture.rgb, h, vv) : null;
  const corrected = raw?.map((c) => correct(c, current)) ?? null;

  const row = (f: Field, name: string, desc: string, value: string) => (
    <div class={`pic-row${isF(f) ? ' focused' : ''}`}>
      <div class="pic-label"><span class="pic-name">{name}</span><Desc mixed={mixed(f)}>{desc}</Desc></div>
      <Track f={f} value={v[f]} focused={isF(f)} />
      <span class="pic-val num">{value}</span>
    </div>
  );
  const wbFocused = isF('wb_r') || isF('wb_g') || isF('wb_b');
  const wbMixed = mixed('wb_r') || mixed('wb_g') || mixed('wb_b');

  const focusedField = (FIELDS as string[]).includes(cur) ? (cur as Field) : null;

  return (
    <main class="main pic" data-density="tv">
      <Header saving={saving} count={usable.length} />

      <div class="pic-grid">
        <div class="pic-col">
          <Card class="pic-color" title="Color" aside={usable.length > 1 ? <span class="pic-aside">Values of {lc.host}</span> : undefined}>
            {row('brightness', 'Brightness', 'Overall strip level', `${pctOf(v.brightness)}%`)}
            {row('gamma', 'Gamma', 'Darker shadows, richer color', dec(v.gamma, 1))}
            {row('saturation', 'Saturation', '1.0× = as on screen', `${dec(v.saturation, 1)}×`)}
            <div class={`pic-row pic-wb${wbFocused ? ' focused' : ''}`}>
              <div class="pic-label"><span class="pic-name">White balance</span><Desc mixed={wbMixed}>Match the wall's white to the screen</Desc></div>
              <div class="pic-wb-lines">
                {(['wb_r', 'wb_g', 'wb_b'] as const).map((f) => (
                  <div key={f} class={`pic-wb-line pic-${f}`}>
                    <span class="pic-ch">{f.slice(3).toUpperCase()}</span>
                    <Track f={f} value={v[f]} focused={isF(f)} />
                    <span class={`pic-wb-val num${isF(f) ? ' on' : ''}`}>{v[f]}</span>
                  </div>
                ))}
              </div>
            </div>
          </Card>

          <Card class="pic-power" title="Power" aside={<span class="pic-aside">Keeps the strip within your power supply</span>}>
            <div class="pic-tiles">
              <Tile label="Limit" focused={isF('power_limit_w')} mixed={mixed('power_limit_w')}
                value={v.power_limit_w > 0 ? `${v.power_limit_w} W` : 'Off'} />
              <Tile label="One LED at full white" focused={isF('pixel_watts')} mixed={mixed('pixel_watts')}
                value={`${v.pixel_watts.toFixed(2)} W`} />
              <Tile label={usable.length > 1 ? 'Using now (first)' : 'Using now'} value={used == null ? '—' : `${used.toFixed(1)} W`} />
            </div>
            <div class="pic-meter">
              <SliderBar value={share} color={m?.power_limited ? 'var(--ui-warn)' : 'var(--ui-ok)'} />
              <span class={`pic-meter-text${m?.power_limited ? ' warn' : ''}`}>{meterText}</span>
            </div>
          </Card>
        </div>

        <div class="pic-col">
          <Card class="pic-presets-card" title="Presets" aside={anyMixed ? <span class="pic-aside warn">Controllers differ</span> : undefined}>
            <div class="pic-presets" role="radiogroup" aria-label="Preset">
              {presets.map((it) => {
                const checked = !anyMixed && same(it.values, current);
                return (
                  <div key={it.id} role="radio" aria-checked={checked} class={`pic-preset${checked ? ' checked' : ''}${isF(it.id) ? ' focused' : ''}`}>
                    <span class="pic-preset-name">{it.name}</span>
                    <span class="pic-preset-desc">{it.desc}</span>
                  </div>
                );
              })}
              <div class={`pic-preset pic-preset-save${isF('preset-save') ? ' focused' : ''}`}>
                <span class="pic-preset-name">{mine ? 'Replace My preset' : 'Save as preset'}</span>
                <span class="pic-preset-desc">{saveNote ?? 'Keep the values on the left'}</span>
              </div>
            </div>
          </Card>

          <Card class="pic-compare" title="Before and after">
            <div class="pic-strip-block">
              <span class="pic-strip-label">From the screen</span>
              <Strip colors={raw} />
            </div>
            <div class="pic-strip-block">
              <span class="pic-strip-label">On the strip, roughly: saturation and white balance, without gamma</span>
              <Strip colors={corrected} />
            </div>
            <p class="pic-note">
              {raw
                ? 'Live from the current frame, clockwise from the top-left corner.'
                : 'No picture right now: the strips fill in while the TV captures a frame.'}
            </p>
            <div class="pic-buttons">
              <Button class="pic-btn" focused={isF('reset')}>{confirmReset ? 'OK again to reset' : 'Reset to defaults'}</Button>
              <Button class="pic-btn" focused={isF('static')}>Static color…</Button>
            </div>
          </Card>
        </div>
      </div>

      <Footer>
        {focusedField ? (
          <>
            <KeyHint k="▲▼">Move</KeyHint>
            <KeyHint k="◀▶">Adjust {NAMES[focusedField]}</KeyHint>
            <KeyHint k="↩">Home</KeyHint>
            <span class="footer-note">{rangeNote(focusedField)}</span>
          </>
        ) : (
          <>
            <KeyHint k="OK">{cur === 'reset' ? 'Reset' : cur === 'static' ? 'Open Home' : cur === 'preset-save' ? 'Save' : 'Apply'}</KeyHint>
            <KeyHint k="◀▶▲▼">Move</KeyHint>
            <KeyHint k="↩">Home</KeyHint>
            <span class="footer-note">
              {cur === 'reset' ? 'Brightness, gamma, saturation and white balance; the power settings stay'
                : cur === 'static' ? 'Static colors are on Home'
                  : 'Presets set gamma, saturation and white balance'}
            </span>
          </>
        )}
      </Footer>
    </main>
  );
}

function rangeNote(f: Field): string {
  const L = limits[f];
  if (f === 'brightness') return `Default ${pctOf(L.default)}% · 5% steps`;
  if (f === 'power_limit_w') return `0 = no limit · range ${L.min}–${L.max} W · hold to move faster`;
  const d = DECIMALS[f];
  const unit = f === 'pixel_watts' ? ' W' : '';
  return `Default ${L.default.toFixed(d)}${unit} · range ${L.min.toFixed(d)}–${L.max.toFixed(d)}${unit}`;
}

function Desc({ mixed, children }: { mixed: boolean; children: string }) {
  return mixed
    ? <span class="pic-desc warn">Mixed · ◀▶ sets all controllers</span>
    : <span class="pic-desc">{children}</span>;
}

function Track({ f, value, focused }: { f: Field; value: number; focused: boolean }) {
  const fill = f === 'wb_r' ? 'var(--ui-side-top)' : f === 'wb_g' ? '#30a46c' : f === 'wb_b' ? '#3e7fd9' : focused ? 'var(--ui-accent)' : undefined;
  const wb = f.startsWith('wb_');
  return (
    <div class="pic-track">
      <span class={`pic-arrow${focused ? ' on' : ''}`} aria-hidden="true">◀</span>
      <div class="pic-bar">
        <SliderBar value={pos(f, value)} knob={!wb || focused} muted={!focused && !wb} color={fill} />
        {focused && !wb && <span class="pic-default" style={`left:${(pos(f, limits[f].default) * 100).toFixed(2)}%`}>default</span>}
      </div>
      <span class={`pic-arrow${focused ? ' on' : ''}`} aria-hidden="true">▶</span>
    </div>
  );
}

function Tile({ label, value, focused, mixed }: { label: string; value: string; focused?: boolean; mixed?: boolean }) {
  const adjustable = focused !== undefined;
  return (
    <div class={`pic-tile${focused ? ' focused' : ''}`}>
      <span class="pic-tile-label">{mixed ? <span class="warn">{label} · Mixed</span> : label}</span>
      <span class="pic-tile-value num">
        {adjustable && <span class={`pic-arrow${focused ? ' on' : ''}`} aria-hidden="true">◀</span>}
        {value}
        {adjustable && <span class={`pic-arrow${focused ? ' on' : ''}`} aria-hidden="true">▶</span>}
      </span>
    </div>
  );
}

function Strip({ colors }: { colors: Rgb[] | null }) {
  return (
    <div class="pic-strip" aria-hidden="true">
      {colors?.map((c, i) => <span key={i} style={`background:${cssRgb(c)}`} />)}
    </div>
  );
}
