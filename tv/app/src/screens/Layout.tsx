// Strip layout: the corner the strip starts at, its direction and the LEDs per side, for one
// controller (the layout is stored on each controller). Edits change a local draft and light
// the 10 s calibration preview on the strip 400 ms after the last change (POST
// /preview/calibration, not saved); Save layout stores it (POST /save/calibration, which also
// sets num_leds to the sum of the sides). Back with unsaved changes asks first.

import { useEffect, useRef, useState } from 'preact/hooks';
import { type CalibrationRequest, type Esp32Config, constants, limits } from '@ambilight/protocol';
import {
  Button, CORNER_NAMES, Card, CheckIcon, type Corner, KeyHint, SIDES, type Side, StatusPill, StripLayoutDiagram,
} from '@ambilight/ui';
import { Footer } from '../components/Footer';
import { type Controller, unreachable } from '../controllers';
import { EDGE_LEFT, type FocusGrid, step } from '../focus';
import { controllerTitle } from '../status';
import type { ScreenProps } from './types';
import './layout.css';

interface Draft {
  top: number;
  right: number;
  bottom: number;
  left: number;
  corner: Corner;
  clockwise: boolean;
}

const PREVIEW_DELAY_MS = 400;
/** Digits typed within this time make one number. */
const TYPE_MS = 1500;
const PREVIEW_S = constants.esp32.calibrationPreviewMs / 1000;
const SIDE_NAME: Record<Side, string> = { top: 'Top', right: 'Right', bottom: 'Bottom', left: 'Left' };
const STEPS = ['Start corner', 'Direction', 'LEDs per side', 'Check on the strip'];
const DIGITS = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

const draftOf = (c: Esp32Config): Draft => ({
  top: c.led_top, right: c.led_right, bottom: c.led_bottom, left: c.led_left, corner: c.start_corner, clockwise: c.clockwise,
});
const same = (a: Draft, b: Draft) =>
  a.top === b.top && a.right === b.right && a.bottom === b.bottom && a.left === b.left && a.corner === b.corner && a.clockwise === b.clockwise;
const body = (d: Draft): CalibrationRequest => ({
  led_top: d.top, led_right: d.right, led_bottom: d.bottom, led_left: d.left, start_corner: d.corner, clockwise: d.clockwise ? 1 : 0,
});
const clampSide = (s: Side, v: number) => Math.max(limits[`led_${s}`].min, Math.min(limits[`led_${s}`].max, v));
const errText = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** An unsaved draft survives a trip to another section through the rail (never saved by itself). */
let kept: { host: string; base: Draft; draft: Draft } | null = null;

export function Layout(p: ScreenProps) {
  // Controllers whose settings were read; the first answering one unless another is picked.
  const listed = p.controllers.filter((c) => c.present && c.config);
  const [host, setHost] = useState<string | null>(null);
  const chosen = listed.find((c) => c.host === host) ?? listed.find((c) => !unreachable(c)) ?? listed[0];
  useEffect(() => { if (chosen && chosen.host !== host) setHost(chosen.host); }, [chosen?.host]);

  if (!chosen) return <Empty {...p} />;
  return <Editor key={chosen.host} p={p} c={chosen} list={listed} onPick={setHost} />;
}

// ---------------------------------------------------------------- no controller

function Empty(p: ScreenProps) {
  const present = p.controllers.filter((c) => c.present);
  const silent = present.length > 0 && present.every(unreachable);
  const reading = p.link.kind === 'checking' || (present.length > 0 && !silent);
  const [title, text] = reading
    ? ['Reading the layout…', 'The layout is stored on the LED controller; the TV is asking it for the current one.']
    : silent
      ? ['The controller does not answer', `It is listed at ${present[0].host} but does not reply over the network. Outputs shows the controllers the TV found and can search again.`]
      : ['No LED controller found', 'The layout is stored on the LED controller, so this screen needs one on the network. Outputs shows the controllers the TV found and can search again.'];

  p.bindKeys((key) => {
    if (key === 'ok') { p.open('outputs'); return true; }
    if (key === 'back') return false;
    return key === 'left' ? 'rail' : true;
  });

  return (
    <main class="main lay">
      <Header subtitle="LEDs per side, the corner the strip starts at and its direction" step={null} />
      <Card class="lay-empty" title={title}>
        <p class="lay-text">{text}</p>
        <Button variant="primary" class="lay-empty-btn" focused={p.active}>Open Outputs</Button>
      </Card>
      <Footer>
        <KeyHint k="OK">Open Outputs</KeyHint>
        <KeyHint k="◀">Menu</KeyHint>
        <KeyHint k="↩">Menu</KeyHint>
      </Footer>
    </main>
  );
}

// ---------------------------------------------------------------- editor

interface EditorProps {
  p: ScreenProps;
  /** The controller being edited (present, config read). */
  c: Controller;
  /** Every present controller with a config, for the picker. */
  list: Controller[];
  onPick: (host: string) => void;
}

function Editor({ p, c, list, onPick }: EditorProps) {
  const fromCfg = draftOf(c.config!);
  /** What the controller has saved as far as this screen knows (ahead of /config right after a save). */
  const [base, setBase] = useState<Draft>(fromCfg);
  const [d, setD] = useState<Draft>(() => (kept && kept.host === c.host && same(kept.base, fromCfg) ? kept.draft : fromCfg));
  const dRef = useRef(d);
  dRef.current = d;
  const [focus, setFocus] = useState(`corner-${d.corner}`);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null);
  /** Back / another controller with unsaved changes: what to do on Discard. */
  const [confirm, setConfirm] = useState<{ then: () => void } | null>(null);
  const [confirmFocus, setConfirmFocus] = useState<'keep' | 'discard'>('keep');
  const [typing, setTyping] = useState<Side | null>(null);
  const typed = useRef<{ side: Side; at: number; text: string } | null>(null);
  const timer = useRef(0);
  const typeTimer = useRef(0);
  const discarded = useRef(false);
  const digits = useDigitKeys();

  const dirty = !same(d, base);
  const off = unreachable(c) || busy;
  const total = d.top + d.right + d.bottom + d.left;
  const index = list.indexOf(c);

  // /config read again with other values (saved elsewhere, or our save read back): a draft
  // without changes follows it.
  const cfgKey = `${fromCfg.top},${fromCfg.right},${fromCfg.bottom},${fromCfg.left},${fromCfg.corner},${fromCfg.clockwise}`;
  useEffect(() => {
    if (same(fromCfg, base)) return;
    if (same(dRef.current, base)) setD(fromCfg);
    setBase(fromCfg);
  }, [cfgKey]);

  // Leaving with unsaved changes (through the rail) keeps them for the next visit.
  const latest = useRef({ d, base });
  latest.current = { d, base };
  useEffect(() => () => {
    clearTimeout(timer.current);
    clearTimeout(typeTimer.current);
    const l = latest.current;
    kept = discarded.current || same(l.d, l.base) ? null : { host: c.host, base: l.base, draft: l.d };
  }, []);

  // ---------------------------------------------------------------- requests
  const esp = () => p.backend.esp32(c.host);
  const preview = (next: Draft, told: boolean) => esp().previewCalibration(body(next)).then(
    () => setNote(told ? { tone: 'ok', text: `The strip shows this layout for ${PREVIEW_S} s.` } : (n) => (n?.tone === 'bad' ? null : n)),
    (e) => setNote({ tone: 'bad', text: `Preview failed: ${errText(e)}` }),
  );
  const change = (patch: Partial<Draft>) => {
    if (off) return;
    const next = { ...dRef.current, ...patch };
    if (same(next, dRef.current)) return;
    dRef.current = next;
    setD(next);
    // Sent once the remote stops repeating.
    clearTimeout(timer.current);
    timer.current = window.setTimeout(() => { timer.current = 0; preview(next, false); }, PREVIEW_DELAY_MS);
  };
  const lightUp = () => {
    if (off) return;
    clearTimeout(timer.current);
    timer.current = 0;
    preview(d, true);
  };
  const save = async () => {
    if (off || !dirty) return;
    clearTimeout(timer.current);
    timer.current = 0;
    const draft = d;
    setBusy(true);
    setNote(null);
    try {
      await esp().saveCalibration(body(draft));
      setBase(draft);
      setNote({ tone: 'ok', text: `Saved. The strip shows the layout for ${PREVIEW_S} s.` });
    } catch (e) {
      setNote({ tone: 'bad', text: `Not saved: ${errText(e)}` });
    } finally {
      setBusy(false);
    }
  };
  const leave = (then: () => void) => {
    if (!dirty) return then();
    setConfirmFocus('keep');
    setConfirm({ then: () => { discarded.current = true; setConfirm(null); then(); } });
  };

  const typeDigit = (side: Side, n: number) => {
    if (off) return;
    const now = Date.now();
    const t = typed.current;
    let text = t && t.side === side && now - t.at < TYPE_MS ? t.text + n : String(n);
    if (+text > limits[`led_${side}`].max) text = String(n);
    typed.current = { side, at: now, text };
    setTyping(side);
    clearTimeout(typeTimer.current);
    typeTimer.current = window.setTimeout(() => { typed.current = null; setTyping(null); }, TYPE_MS);
    change({ [side]: +text } as Partial<Draft>);
  };
  const endTyping = () => {
    typed.current = null;
    if (typing) setTyping(null);
  };

  // ---------------------------------------------------------------- focus grid
  // Diagram (corners and sides where they are) on the left, the steppers on the right, the
  // buttons under them; the picker above the diagram when there are several controllers.
  const grid: FocusGrid = [];
  const stay = (id: string) => ({ down: id });
  if (list.length > 1) grid.push(list.map((_, i) => ({ id: `pick-${i}`, col: i / 10, nav: { down: `corner-${d.corner}` } })));
  const pickUp = list.length > 1 ? { up: `pick-${index}` } : {};
  grid.push([
    { id: 'corner-0', col: 0 },
    { id: 'top', col: 1, nav: { down: 'bottom' } },
    { id: 'corner-1', col: 2 },
    { id: 'n-top', col: 5, nav: pickUp },
  ]);
  grid.push([{ id: 'left', col: 0 }, { id: 'right', col: 2 }, { id: 'n-right', col: 5 }]);
  grid.push([
    { id: 'corner-3', col: 0, nav: stay('corner-3') },
    { id: 'bottom', col: 1, nav: { ...stay('bottom'), up: 'top' } },
    { id: 'corner-2', col: 2, nav: stay('corner-2') },
    { id: 'n-bottom', col: 5 },
  ]);
  grid.push([{ id: 'n-left', col: 5, nav: { down: 'preview' } }]);
  grid.push([{ id: 'preview', col: 4, nav: { left: 'corner-2' } }, { id: 'save', col: 5 }]);
  const ids = grid.flat().map((it) => it.id);
  const current = ids.includes(focus) ? focus : `corner-${d.corner}`;

  const stepperSide = current.startsWith('n-') ? (current.slice(2) as Side) : null;
  const area = current.startsWith('corner-') ? 0 : (SIDES as readonly string[]).includes(current) ? 1 : stepperSide ? 2
    : current === 'preview' || current === 'save' ? 3 : -1;

  const ok = (id: string) => {
    if (id.startsWith('corner-')) change({ corner: +id.slice(7) as Corner });
    else if ((SIDES as readonly string[]).includes(id)) change({ clockwise: !d.clockwise });
    else if (id.startsWith('pick-')) {
      const next = list[+id.slice(5)];
      if (next && next !== c) leave(() => onPick(next.host));
    } else if (id === 'preview') lightUp();
    else if (id === 'save') save();
  };

  p.bindKeys((key) => {
    if (confirm) {
      if (key === 'ok') { if (confirmFocus === 'discard') confirm.then(); else setConfirm(null); }
      else if (key === 'back') setConfirm(null);
      else if (key === 'left' || key === 'right') setConfirmFocus(key === 'left' ? 'keep' : 'discard');
      return true;
    }
    if (key === 'back') {
      if (!dirty) return false;
      leave(() => p.open('home'));
      return true;
    }
    if (stepperSide && (key === 'left' || key === 'right')) {
      endTyping();
      change({ [stepperSide]: clampSide(stepperSide, d[stepperSide] + (key === 'left' ? -1 : 1)) } as Partial<Draft>);
      return true;
    }
    if (key === 'ok') { ok(current); return true; }
    const next = step(grid, current, key);
    if (next === EDGE_LEFT) return 'rail';
    if (next && next !== current) { endTyping(); setFocus(next); }
    return true;
  });

  const onDigit = useRef<(n: number) => void>(() => {});
  onDigit.current = (n) => { if (p.active && !confirm && stepperSide) typeDigit(stepperSide, n); };
  useEffect(() => {
    const listener = (e: KeyboardEvent) => {
      const k = e.keyCode;
      const n = k >= 48 && k <= 57 ? k - 48 : k >= 96 && k <= 105 ? k - 96 : -1;
      if (n < 0) return;
      e.preventDefault();
      onDigit.current(n);
    };
    document.addEventListener('keydown', listener);
    return () => document.removeEventListener('keydown', listener);
  }, []);

  const on = (id: string) => p.active && !confirm && current === id;
  const diagramFocus = p.active && !confirm && area >= 0 && area <= 1 ? current : undefined;

  // ---------------------------------------------------------------- view
  const dir = d.clockwise ? 'clockwise' : 'counter-clockwise';
  // With several controllers the picker shows the addresses.
  const where = list.length > 1 ? controllerTitle(index, list.length) : c.host;

  return (
    <main class="main lay">
      <Header subtitle={`Tell the controller where its LEDs are · ${where}`} step={area} />

      <div class="lay-body">
        <Card class="lay-front" title="Seen from the front" aside={<span class="lay-aside">Start: {CORNER_NAMES[d.corner]} · {dir}</span>}>
          {list.length > 1 && (
            <div class="lay-picker" role="radiogroup" aria-label="Controller">
              {list.map((x, i) => (
                <span key={x.host} role="radio" aria-checked={x === c}
                  class={`lay-pick${x === c ? ' checked' : ''}${on(`pick-${i}`) ? ' focused' : ''}${unreachable(x) ? ' silent' : ''}`}>
                  {controllerTitle(i, list.length)} <span class="lay-pick-host">{x.host}</span>
                </span>
              ))}
              <span class="lay-picker-note">Each controller keeps its own layout</span>
            </div>
          )}
          <div class={`lay-diagram start-${d.corner}${list.length > 1 ? ' compact' : ''}`}>
            <span class="lay-led1">LED 1 · cable here</span>
            <StripLayoutDiagram top={d.top} right={d.right} bottom={d.bottom} left={d.left} startCorner={d.corner}
              clockwise={d.clockwise} focused={diagramFocus} disabled={off} />
          </div>
          <p class="lay-tip"><span>OK on a corner: start there</span><span>OK on a side: reverse the direction</span></p>
        </Card>

        <div class="lay-side">
          <Card class="lay-counts" title="LEDs per side">
            {SIDES.map((s) => {
              const v = d[s];
              const { min, max } = limits[`led_${s}`];
              return (
                <div key={s} class={`lay-row${on(`n-${s}`) ? ' focused' : ''}${typing === s ? ' typing' : ''}`}
                  aria-label={`${SIDE_NAME[s]}: ${v} LEDs`}>
                  <span class={`lay-chip ${s}`} aria-hidden="true" />
                  <span class="lay-name">{SIDE_NAME[s]}</span>
                  <span class="lay-stepper" aria-hidden="true">
                    <span class={`lay-step${v <= min || off ? ' dim' : ''}`}>−</span>
                    <span class="lay-value">{v}</span>
                    <span class={`lay-step${v >= max || off ? ' dim' : ''}`}>+</span>
                  </span>
                </div>
              );
            })}
            <div class="lay-total">
              <span class="lay-total-label">Total</span>
              <span class="lay-total-value">{total} <span class="lay-unit">LEDs</span></span>
            </div>
            {total > c.config!.max_leds ? (
              <p class="lay-warn">The controller drives at most {c.config!.max_leds} LEDs.</p>
            ) : !dirty && same(base, fromCfg) && c.config!.num_leds !== total ? (
              <p class="lay-small">It drives {c.config!.num_leds} LEDs now; saving the layout sets it to {total}.</p>
            ) : null}
          </Card>

          {confirm ? (
            <Card class="lay-check" title="Discard the changes?" aside={<StatusPill tone="warn">Not saved</StatusPill>}>
              <p class="lay-text">The controller keeps its saved layout; the strip shows it again when the preview ends.</p>
              <div class="lay-actions">
                <Button focused={p.active && confirmFocus === 'keep'}>Keep editing</Button>
                <Button variant="danger" focused={p.active && confirmFocus === 'discard'}>Discard</Button>
              </div>
            </Card>
          ) : (
            <Card class="lay-check" title="Check on the strip"
              aside={busy ? <StatusPill tone="off">Saving…</StatusPill> : dirty ? <StatusPill tone="warn">Not saved</StatusPill> : <StatusPill tone="ok">Saved</StatusPill>}>
              {unreachable(c) ? (
                <p class="lay-text bad">The controller does not answer. The changes stay here until it does.</p>
              ) : note ? (
                <p class={`lay-text ${note.tone}`}>{note.text}</p>
              ) : (
                <p class="lay-text">For {PREVIEW_S} s each side lights in its color from the diagram and LED 1 turns white. If a side shows the wrong color, change the start corner or the direction.</p>
              )}
              <div class="lay-actions">
                <Button focused={on('preview')} disabled={off}>Light up the strip</Button>
                <Button variant="primary" focused={on('save')} disabled={off || !dirty}>{busy ? 'Saving…' : 'Save layout'}</Button>
              </div>
            </Card>
          )}
        </div>
      </div>

      <Footer>
        {confirm ? (
          <>
            <KeyHint k="◀▶">Choose</KeyHint>
            <KeyHint k="OK">Confirm</KeyHint>
            <KeyHint k="↩">Keep editing</KeyHint>
          </>
        ) : area === 2 ? (
          <>
            <KeyHint k="▲▼">Side</KeyHint>
            <KeyHint k="◀▶">Fewer / more LEDs</KeyHint>
            {digits && <KeyHint k="0–9">Type a number</KeyHint>}
            <KeyHint k="↩">Back</KeyHint>
            <span class="footer-note">Up to {limits.led_top.max} LEDs per side</span>
          </>
        ) : (
          <>
            <KeyHint k="OK">{area === 0 ? 'Start the strip here' : area === 1 ? 'Reverse the direction' : current === 'save' ? 'Save layout' : current === 'preview' ? 'Light up the strip' : 'Edit this controller'}</KeyHint>
            <KeyHint k="◀▶▲▼">Move</KeyHint>
            <KeyHint k="↩">Back</KeyHint>
            {dirty && <span class="footer-note">Changes are not saved until Save layout</span>}
          </>
        )}
      </Footer>
    </main>
  );
}

/** step: the current one (-1: none), null: no step indicator. */
function Header({ subtitle, step: cur }: { subtitle: string; step: number | null }) {
  return (
    <header class="header">
      <div class="header-titles">
        <h1>Strip layout</h1>
        <p class="subtitle">{subtitle}</p>
      </div>
      {cur != null && <ol class="lay-steps" aria-label="Steps">
        {STEPS.map((s, i) => (
          <li key={s} class={i === cur ? 'current' : i < cur ? 'done' : ''} aria-current={i === cur ? 'step' : undefined}>
            {i < cur ? <CheckIcon size={20} /> : <span class="lay-step-n">{i + 1}</span>}{s}
          </li>
        ))}
      </ol>}
    </header>
  );
}

/**
 * The number keys reach the page only once registered on the TV (tv.inputdevice privilege);
 * a desktop browser has them anyway. True when they work.
 */
function useDigitKeys(): boolean {
  const input = window.tizen?.tvinputdevice;
  const [ok, setOk] = useState(!window.tizen);
  useEffect(() => {
    if (!input) return;
    try {
      input.registerKeyBatch(DIGITS, () => setOk(true), (e) => console.warn('Number keys not registered:', e.message));
    } catch (e) {
      console.warn('Number keys not registered:', e);
      return;
    }
    return () => { try { input.unregisterKeyBatch(DIGITS); } catch { /* gone with the page */ } };
  }, []);
  return ok;
}
