// Home: lighting on/off, brightness, mode, live preview and a compact status list. When
// something is wrong a problem banner with the fix (if there is one) comes first.

import { useEffect, useState } from 'preact/hooks';
import { Preview } from '../components/Preview';
import { Footer, Hint, Kbd } from '../components/Keys';
import { CircleIcon, ErrorIcon, ScreenIcon, WarnIcon } from '../components/icons';
import { type Controller, type ControllerActions, unreachable } from '../controllers';
import { type Esp32Config, LedMode, type ServiceStatus, limits } from '@ambilight/protocol';
import type { Backend, ServiceLink } from '../data';
import { EDGE_LEFT, type FocusGrid, type Key, step } from '../focus';
import {
  type Problem, type Row, captureRow, controllerRow, controllerTitle, fps, hyperHdrRow, live, summary,
} from '../status';

export const SWATCHES = [
  { name: 'Warm white', c: '#ffd9a8' },
  { name: 'Cool white', c: '#eaf2ff' },
  { name: 'Amber', c: '#f2a13b' },
  { name: 'Red', c: '#e5484d' },
  { name: 'Blue', c: '#3e7fd9' },
  { name: 'Purple', c: '#8e6ad8' },
];

/** The screen handles a key: true = consumed, 'rail' = move the focus to the rail. */
export type KeyHandler = (key: Key) => boolean | 'rail';

interface Props {
  backend: Backend;
  link: ServiceLink;
  prev: ServiceStatus | null;
  controllers: Controller[];
  actions: ControllerActions;
  problems: Problem[];
  model: string | null;
  /** Focus this element on mount instead of the default. */
  initialFocus: string | null;
  /** The focus is on the screen (not on the rail). */
  active: boolean;
  bindKeys: (h: KeyHandler) => void;
  onTestPattern: () => void;
  onControllers: () => void;
}

const BRIGHTNESS_MAX = limits.brightness.max;
const pctOf = (b: number) => Math.round((b * 100) / BRIGHTNESS_MAX);
const byteOf = (pct: number) => Math.round((pct * BRIGHTNESS_MAX) / 100);

export function Home(p: Props) {
  const { link, controllers, actions, problems } = p;
  const status = link.kind === 'up' ? link.status : null;
  const problem = problems[0] as Problem | undefined;

  // The controllers the controls apply to: listed by the service and answering.
  const usable = controllers.filter((c) => c.present && c.config && !unreachable(c));
  const canControl = usable.length > 0;
  // Shown values: the answering controllers'; while none answers, the last ones read.
  const cfgs = (canControl ? usable : controllers.filter((c) => c.config)).map((c) => c.config!);
  const lead = cfgs[0];
  const on = cfgs.some((c) => c.power);
  const mixed = (f: (c: (typeof cfgs)[number]) => unknown) => cfgs.some((c) => f(c) !== f(lead));

  const [focus, setFocus] = useState<string>(p.initialFocus ?? (problem?.action ? 'banner-primary' : 'lighting'));
  const [adjusting, setAdjusting] = useState(false);
  const [busy, setBusy] = useState<string | null>(null); // label of a running banner action

  // ---------------------------------------------------------------- focus grid
  const compact = !!problem;
  const grid: FocusGrid = [];
  if (problem?.action) grid.push([{ id: 'banner-primary', col: 2, nav: problem.controller ? {} : { down: 'lighting' } }]);
  if (problem?.controller) grid.push([{ id: 'banner-secondary', col: 2, nav: { down: 'lighting' } }]);
  if (compact) {
    grid.push([
      { id: 'lighting', col: 0, nav: { down: 'test-pattern' } },
      { id: 'brightness', col: 1, nav: { down: 'test-pattern' } },
      { id: 'mode', col: 2 },
    ]);
    grid.push(SWATCHES.map((_, i) => ({ id: `swatch-${i}`, col: 2 + i / 100, nav: i === 0 ? { left: 'brightness' } : {} })));
    grid.push([{ id: 'test-pattern', col: 3 }]);
  } else {
    grid.push([
      { id: 'lighting', col: 0, nav: { down: 'test-pattern' } },
      { id: 'brightness', col: 1, nav: { down: 'test-pattern' } },
      { id: 'mode-follow', col: 2 },
    ]);
    grid.push([{ id: 'mode-static', col: 2, nav: { left: 'brightness' } }]);
    grid.push(SWATCHES.map((_, i) => ({ id: `swatch-${i}`, col: 2 + i / 100, nav: i === 0 ? { left: 'brightness' } : {} })));
    grid.push([{ id: 'test-pattern', col: 0, nav: { up: 'lighting' } }]);
  }
  const ids = grid.flat().map((it) => it.id);
  const current = ids.includes(focus) ? focus : problem?.action ? 'banner-primary' : 'lighting';
  useEffect(() => { if (current !== focus) setFocus(current); }, [current]);
  useEffect(() => { if (current !== 'brightness') setAdjusting(false); }, [current]);
  // A new problem with a fix takes the focus (not while the user adjusts the brightness).
  const fixable = problem?.action ? problem.title : null;
  useEffect(() => { if (fixable && !adjusting) setFocus('banner-primary'); }, [fixable]);

  // ---------------------------------------------------------------- actions
  const toggleLighting = () => {
    if (!canControl) return;
    // Only the strips go dark (their `power` flag). Capture keeps running: other outputs
    // (HyperHDR) still need it, and a strip switched back on elsewhere (its web app) must
    // get frames right away.
    actions.setPower(!on);
  };
  const setMode = (mode: LedMode) => { if (canControl) actions.setMode(mode); };
  const nudgeBrightness = (d: number) => {
    if (!canControl) return;
    const pct = Math.max(0, Math.min(100, Math.round(pctOf(lead.brightness) / 5) * 5 + d));
    actions.setBrightness(byteOf(pct));
  };
  const runBanner = async () => {
    if (!problem?.action || busy) return;
    if (problem.action === 'start') {
      setBusy('Starting…');
      try { await p.backend.service.start(); } catch (e) { console.warn('Start failed', e); }
      setTimeout(() => setBusy(null), 3000);
    } else {
      setBusy('Searching…');
      const r = await p.backend.service.command('rediscover');
      if (!r.ok) console.warn('rediscover:', r.error);
      setTimeout(() => setBusy(null), 3000);
    }
  };

  const ok = (id: string) => {
    if (id === 'lighting') toggleLighting();
    else if (id === 'brightness') setAdjusting((a) => !a);
    else if (id === 'mode') setMode(lead?.led_mode === LedMode.dynamic ? LedMode.plain : LedMode.dynamic);
    else if (id === 'mode-follow') setMode(LedMode.dynamic);
    else if (id === 'mode-static') setMode(LedMode.plain);
    else if (id.startsWith('swatch-')) {
      if (!canControl) return;
      actions.setColor(SWATCHES[+id.slice(7)].c);
      if (lead?.led_mode !== LedMode.plain) setMode(LedMode.plain);
    } else if (id === 'test-pattern') p.onTestPattern();
    else if (id === 'banner-primary') runBanner();
    else if (id === 'banner-secondary') p.onControllers();
  };

  p.bindKeys((key) => {
    if (adjusting) {
      if (key === 'left' || key === 'right') { nudgeBrightness(key === 'left' ? -5 : 5); return true; }
      if (key === 'ok' || key === 'back') { setAdjusting(false); return true; }
      setAdjusting(false);
    }
    if (key === 'ok') { ok(current); return true; }
    if (key === 'back') return false;
    const next = step(grid, current, key);
    if (next === EDGE_LEFT) return 'rail';
    if (next) setFocus(next);
    return true;
  });

  const f = (id: string) => (p.active && current === id ? ' focused' : '');

  // ---------------------------------------------------------------- view
  const pill = summary(link, problems);
  const rows: Row[] = [captureRow(link, compact)];
  // While the service lists hosts: those. When it lists none: the ones it lost in this session.
  const esp = status?.esp32;
  const listed = !esp?.enabled ? [] : controllers.filter((c) => c.present === esp.hosts.length > 0);
  listed.forEach((c, i) => rows.push(controllerRow(c, controllerTitle(i, listed.length), status, p.prev)));
  if (!esp) rows.push({ title: 'LED controller', detail: '—', value: '—', tone: 'off' });
  else if (!esp.enabled) rows.push({ title: 'LED controllers', detail: 'Turned off in ambilight.json', value: 'Off', tone: 'off' });
  else if (!listed.length) rows.push({ title: 'LED controller', detail: 'Searching the network', value: 'Not found', tone: 'warn' });
  rows.push(hyperHdrRow(link));

  const capture = status?.capture;
  const capturing = !!capture && live(capture);
  const pct = lead ? pctOf(lead.brightness) : null;

  const lightingText = !cfgs.length ? 'No controller found'
    : !canControl ? 'Waiting for the strip'
      : !on ? 'The strip is dark'
        : lead.led_mode === LedMode.plain ? 'Showing a static color'
          : capture && !capturing ? 'No picture to follow' : 'Following the picture';
  const modeText = !lead ? '—' : mixed((c) => c.led_mode) ? 'Mixed' : lead.led_mode === LedMode.dynamic ? 'Follow the picture' : 'Static color';

  return (
    <main class="main">
      <header class="header">
        <div class="header-titles">
          <h1>Home</h1>
          <p class="subtitle">{p.model ? `Samsung ${p.model}` : 'Samsung TV'}{capture?.hw ? ` · ${capture.hw.fullW}×${capture.hw.fullH}` : ''}</p>
        </div>
        <span class={`pill tone-${pill.tone}`}><span class="pill-dot" />{pill.text}</span>
      </header>

      {problem && (
        <section class={`banner banner-${problem.tone}`} role="alert">
          <span class="banner-icon">{problem.tone === 'bad' ? <ErrorIcon /> : <WarnIcon />}</span>
          <div class="banner-body">
            <h2>{problem.title}</h2>
            <p>{problem.text}</p>
            {problem.detail && <p class="banner-detail">{problem.detail}</p>}
            {problem.hints && <ul class="banner-hints">{problem.hints.map((h) => <li key={h}>{h}</li>)}</ul>}
          </div>
          {(problem.action || problem.controller) && (
            <div class="banner-actions">
              {problem.action && (
                <div class={`btn btn-primary${f('banner-primary')}`}>
                  {busy ?? (problem.action === 'start' ? 'Start' : 'Search now')}
                </div>
              )}
              {problem.controller && <div class={`btn btn-secondary${f('banner-secondary')}`}>Controller details</div>}
            </div>
          )}
        </section>
      )}

      {compact ? (
        <section class="tiles tiles-compact" aria-label="Main controls">
          <div class={`tile tile-row${f('lighting')}`}>
            <Switch on={on} accent={false} />
            <span class="stack"><span class="value-m">{cfgs.length ? (on ? 'On' : 'Off') : '—'}</span><span class="muted-s">{lightingText}</span></span>
          </div>
          <div class={`tile tile-center${f('brightness')}${adjusting ? ' adjusting' : ''}`}>
            <div class="spread"><span class="label">Brightness</span><span class="value-m num">{pct == null ? '—' : `${pct}%`}</span></div>
            <Bar pct={pct ?? 0} muted={!adjusting} />
          </div>
          <div class={`tile tile-center${f('mode')}`}>
            <div class="spread"><span class="label">Mode</span><span class="muted-s">OK switches</span></div>
            <span class="value-s">{modeText}</span>
            <Swatches lead={lead} f={f} small />
          </div>
        </section>
      ) : (
        <section class="tiles" aria-label="Main controls">
          <div class={`tile tile-big tile-lighting${f('lighting')}`}>
            <div class="spread"><span class="label">Lighting</span><Switch on={on} accent /></div>
            <span class="value-xl">{cfgs.length ? (on ? 'On' : 'Off') : '—'}</span>
            <span class="desc">{lightingText}</span>
            <span class="tile-hint"><Kbd>OK</Kbd>{on ? 'Turn off' : 'Turn on'}</span>
          </div>
          <div class={`tile tile-big${f('brightness')}${adjusting ? ' adjusting' : ''}`}>
            <span class="label">Brightness</span>
            <span class="value-xl num">{pct == null ? '—' : pct}<span class="unit">%</span></span>
            <Bar pct={pct ?? 0} knob />
            {mixed((c) => c.brightness) && <span class="muted-s">Controllers differ · ◀▶ sets all</span>}
            <span class="tile-hint">{adjusting ? <><Kbd>◀▶</Kbd>5% steps · OK done</> : <><Kbd>OK</Kbd>Adjust in 5% steps</>}</span>
          </div>
          <div class="tile tile-big tile-mode">
            <span class="label">Mode</span>
            <div class="modes" role="radiogroup" aria-label="Lighting mode">
              <div class={`mode${lead?.led_mode === LedMode.dynamic ? ' checked' : ''}${f('mode-follow')}`} role="radio" aria-checked={lead?.led_mode === LedMode.dynamic}><ScreenIcon />Follow the picture</div>
              <div class={`mode${lead?.led_mode === LedMode.plain ? ' checked' : ''}${f('mode-static')}`} role="radio" aria-checked={lead?.led_mode === LedMode.plain}><CircleIcon />Static color</div>
            </div>
            <Swatches lead={lead} f={f} />
          </div>
        </section>
      )}

      {compact ? (
        <section class="status-cards" aria-label="Status">
          {rows.map((r) => <StatusLine key={r.title} row={r} card />)}
          <div class={`btn btn-secondary btn-card${f('test-pattern')}`}>Test pattern</div>
        </section>
      ) : (
        <div class="lower">
          <section class="panel preview-panel" aria-label="Live preview">
            <Preview rgb={capture?.rgb} h={capture?.h} v={capture?.v} live={capturing} config={lead} />
            <div class="preview-text">
              <h2>Live preview</h2>
              <p class="muted-s">Inner bands: what the TV samples. Dots: what the {lead ? `${lead.num_leds} ` : ''}LEDs show.</p>
              <span class="num fps">{capture && capturing ? `${fps(capture)} fps` : '—'}</span>
              <div class={`btn btn-secondary${f('test-pattern')}`}>Test pattern</div>
            </div>
          </section>
          <section class="panel status-list" aria-label="Status">
            {rows.map((r) => <StatusLine key={r.title} row={r} />)}
          </section>
        </div>
      )}

      <Footer>
        {problem?.action ? (
          <>
            <Hint k="OK">{problem.action === 'start' ? 'Start the service' : 'Search now'}</Hint>
            <Hint k="▼">Lighting controls</Hint>
            <Hint k="↩">Back to TV</Hint>
            <span class="footer-note">The banner leaves by itself once the problem is gone</span>
          </>
        ) : (
          <>
            <Hint k="OK">Lighting on / off</Hint>
            <Hint k="▶">Brightness, mode</Hint>
            <Hint k="◀">Menu</Hint>
            <Hint k="↩">Back to TV</Hint>
          </>
        )}
      </Footer>
    </main>
  );
}

function Switch({ on, accent }: { on: boolean; accent: boolean }) {
  return <span class={`switch${on ? ' on' : ''}${accent ? ' accent' : ''}`} aria-hidden="true"><span class="knob" /></span>;
}

function Bar({ pct, knob, muted }: { pct: number; knob?: boolean; muted?: boolean }) {
  return (
    <div class={`bar${muted ? ' muted' : ''}`}>
      <div class="bar-fill" style={`width:${pct}%`} />
      {knob && <div class="bar-knob" style={`left:calc(${pct}% - 16px)`} />}
    </div>
  );
}

function Swatches({ lead, f, small }: { lead?: Esp32Config; f: (id: string) => string; small?: boolean }) {
  return (
    <div class={`swatches${small ? ' small' : ''}`} aria-label="Static colors">
      {SWATCHES.map((s, i) => (
        <div key={s.c} aria-label={s.name} style={`background:${s.c}`}
          class={`swatch${lead?.led_mode === LedMode.plain && lead.color.toLowerCase() === s.c ? ' selected' : ''}${f(`swatch-${i}`)}`} />
      ))}
    </div>
  );
}

function StatusLine({ row, card }: { row: Row; card?: boolean }) {
  return (
    <div class={`status-line${card ? ' card' : ''} tone-${row.tone}`}>
      <span class="dot-s" />
      <span class="stack"><span class="status-title">{row.title}</span><span class="status-detail">{row.detail}</span></span>
      <span class="status-value">{row.value}</span>
    </div>
  );
}
