// Home: lighting on/off, brightness, mode, live preview and a compact status list. When
// something is wrong a problem banner with the fix (if there is one) comes first.

import { useEffect, useState } from 'preact/hooks';
import {
  Button, Card, CircleIcon, ColorSwatches, Kbd, KeyHint, LivePreview, Option, ProblemBanner, SWATCHES, ScreenIcon,
  SliderBar, StatusLine, StatusList, StatusPill, Switch,
} from '@ambilight/ui';
import { Footer } from '../components/Footer';
import { type Controller, type ControllerActions, unreachable } from '../controllers';
import { LedMode, type ServiceStatus, limits } from '@ambilight/protocol';
import type { Backend, ServiceLink } from '../data';
import { EDGE_LEFT, type FocusGrid, type Key, step } from '../focus';
import {
  type Problem, type Row, captureRow, controllerRow, controllerTitle, fps, hyperHdrRow, live, summary,
} from '../status';

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
      actions.setColor(SWATCHES[+id.slice(7)].color);
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

  const f = (id: string) => p.active && current === id;

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
  const swatches = (
    <ColorSwatches selected={lead?.led_mode === LedMode.plain ? lead.color : null}
      focusedIndex={SWATCHES.findIndex((_, i) => f(`swatch-${i}`))} small={compact} />
  );

  return (
    <main class="main">
      <header class="header">
        <div class="header-titles">
          <h1>Home</h1>
          <p class="subtitle">{p.model ? `Samsung ${p.model}` : 'Samsung TV'}{capture?.hw ? ` · ${capture.hw.fullW}×${capture.hw.fullH}` : ''}</p>
        </div>
        <StatusPill tone={pill.tone}>{pill.text}</StatusPill>
      </header>

      {problem && (
        <ProblemBanner tone={problem.tone} title={problem.title} text={problem.text} detail={problem.detail} hints={problem.hints}
          actions={(problem.action || problem.controller) && (
            <>
              {/* Real buttons, never focused natively: the focus manager drives `focused`. */}
              {problem.action && (
                <Button variant="primary" tabIndex={-1} focused={f('banner-primary')}>
                  {busy ?? (problem.action === 'start' ? 'Start' : 'Search now')}
                </Button>
              )}
              {problem.controller && <Button tabIndex={-1} focused={f('banner-secondary')}>Controller details</Button>}
            </>
          )} />
      )}

      {compact ? (
        <section class="tiles tiles-compact" aria-label="Main controls">
          <Card class="tile-row" focused={f('lighting')}>
            <Switch on={on} />
            <span class="stack"><span class="ui-value">{cfgs.length ? (on ? 'On' : 'Off') : '—'}</span><span class="ui-muted">{lightingText}</span></span>
          </Card>
          <Card class="tile-center" focused={f('brightness')} adjusting={adjusting}>
            <div class="spread"><span class="ui-label">Brightness</span><span class="ui-value ui-num">{pct == null ? '—' : `${pct}%`}</span></div>
            <SliderBar value={(pct ?? 0) / 100} muted={!adjusting} />
          </Card>
          <Card class="tile-center" focused={f('mode')}>
            <div class="spread"><span class="ui-label">Mode</span><span class="ui-muted">OK switches</span></div>
            <span class="ui-value-s">{modeText}</span>
            {swatches}
          </Card>
        </section>
      ) : (
        <section class="tiles" aria-label="Main controls">
          <Card class="tile-big" focused={f('lighting')}>
            <div class="spread"><span class="ui-label">Lighting</span><Switch on={on} accent /></div>
            <span class="ui-value-xl">{cfgs.length ? (on ? 'On' : 'Off') : '—'}</span>
            <span class="ui-desc">{lightingText}</span>
            <span class="tile-hint"><Kbd>OK</Kbd>{on ? 'Turn off' : 'Turn on'}</span>
          </Card>
          <Card class="tile-big" focused={f('brightness')} adjusting={adjusting}>
            <span class="ui-label">Brightness</span>
            <span class="ui-value-xl ui-num">{pct == null ? '—' : pct}<span class="ui-unit">%</span></span>
            <SliderBar value={(pct ?? 0) / 100} knob />
            {mixed((c) => c.brightness) && <span class="ui-muted">Controllers differ · ◀▶ sets all</span>}
            <span class="tile-hint">{adjusting ? <><Kbd>◀▶</Kbd>5% steps · OK done</> : <><Kbd>OK</Kbd>Adjust in 5% steps</>}</span>
          </Card>
          <Card class="tile-big tile-mode">
            <span class="ui-label">Mode</span>
            <div class="modes" role="radiogroup" aria-label="Lighting mode">
              <div role="radio" aria-checked={lead?.led_mode === LedMode.dynamic}>
                <Option checked={lead?.led_mode === LedMode.dynamic} focused={f('mode-follow')} icon={<ScreenIcon size={26} />}>Follow the picture</Option>
              </div>
              <div role="radio" aria-checked={lead?.led_mode === LedMode.plain}>
                <Option checked={lead?.led_mode === LedMode.plain} focused={f('mode-static')} icon={<CircleIcon size={26} />}>Static color</Option>
              </div>
            </div>
            {swatches}
          </Card>
        </section>
      )}

      {compact ? (
        <section class="status-cards" aria-label="Status">
          {rows.map((r) => <StatusLine key={r.title} tone={r.tone} title={r.title} detail={r.detail} value={r.value} card />)}
          <Button tabIndex={-1} focused={f('test-pattern')}>Test pattern</Button>
        </section>
      ) : (
        <div class="lower">
          <Card class="preview-panel" label="Live preview">
            <LivePreview rgb={capture?.rgb} h={capture?.h} v={capture?.v} live={capturing} config={lead} width={520} />
            <div class="preview-text">
              <h2>Live preview</h2>
              <p class="ui-muted">Inner bands: what the TV samples. Dots: what the {lead ? `${lead.num_leds} ` : ''}LEDs show.</p>
              <span class="ui-num fps">{capture && capturing ? `${fps(capture)} fps` : '—'}</span>
              <Button tabIndex={-1} focused={f('test-pattern')}>Test pattern</Button>
            </div>
          </Card>
          <Card class="status-panel" label="Status">
            <StatusList>
              {rows.map((r) => <StatusLine key={r.title} tone={r.tone} title={r.title} detail={r.detail} value={r.value} />)}
            </StatusList>
          </Card>
        </div>
      )}

      <Footer>
        {problem?.action ? (
          <>
            <KeyHint k="OK">{problem.action === 'start' ? 'Start the service' : 'Search now'}</KeyHint>
            <KeyHint k="▼">Lighting controls</KeyHint>
            <KeyHint k="↩">Back to TV</KeyHint>
            <span class="footer-note">The banner leaves by itself once the problem is gone</span>
          </>
        ) : (
          <>
            <KeyHint k="OK">Lighting on / off</KeyHint>
            <KeyHint k="▶">Brightness, mode</KeyHint>
            <KeyHint k="◀">Menu</KeyHint>
            <KeyHint k="↩">Back to TV</KeyHint>
          </>
        )}
      </Footer>
    </main>
  );
}
