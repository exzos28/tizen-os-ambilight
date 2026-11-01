// Full-screen test patterns for the strip and its calibration, switched with ◀ ▶ (OK / Back
// close the screen, in App). Only the current pattern is mounted. Static patterns are plain
// DOM; the animated ones are CSS animations, except the color wheel: a small canvas redrawn
// every animation frame, only while it is shown. Regions are laid out around the points the
// service samples, so each point sits well inside one color. A centred label names the
// pattern and hides after LABEL_MS; any key shows it again.

import type { JSX } from 'preact';
import { useEffect, useRef, useState } from 'preact/hooks';
import type { KeyHandler } from './Home';
import './test-pattern.css';

const LABEL_MS = 3000;

// Where the service samples (tv/service/Capture.cs, Config.cs), in stage px: half the
// 3840×2160 panel, so EdgeInsetPx 250 → 125 and the 16 px block → 8.
const SW = 1920, SH = 1080, INSET = 125, BLOCK = 8, H_POINTS = 4, V_POINTS = 3;
const near = INSET + BLOCK / 2;
/** Centres of the sampled blocks, clockwise from the top-left, in the service's frame order. */
const SAMPLES: [number, number][] = [];
for (let i = 0; i < H_POINTS; i++) SAMPLES.push([(i + 0.5) * SW / H_POINTS, near]);
for (let i = 0; i < V_POINTS; i++) SAMPLES.push([SW - near, (i + 0.5) * SH / V_POINTS]);
for (let i = H_POINTS - 1; i >= 0; i--) SAMPLES.push([(i + 0.5) * SW / H_POINTS, SH - near]);
for (let i = V_POINTS - 1; i >= 0; i--) SAMPLES.push([near, (i + 0.5) * SH / V_POINTS]);

// ---------------------------------------------------------------- color wheel

const W = 96, H = 54;

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  s /= 100; l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = l - c / 2;
  let r = 0, g = 0, b = 0;
  if (h < 60) { r = c; g = x; }
  else if (h < 120) { r = x; g = c; }
  else if (h < 180) { g = c; b = x; }
  else if (h < 240) { g = x; b = c; }
  else if (h < 300) { r = x; b = c; }
  else { r = c; b = x; }
  return [((r + m) * 255) | 0, ((g + m) * 255) | 0, ((b + m) * 255) | 0];
}

/** The old launcher's Debug mode: a rotating wheel on a 96×54 canvas scaled up. */
function Wheel() {
  const canvas = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const el = canvas.current;
    const ctx = el?.getContext('2d');
    if (!el || !ctx) return;
    const img = ctx.createImageData(W, H);
    const cx = W / 2, cy = H / 2, maxR = Math.sqrt(cx * cx + cy * cy);
    let offset = 0;
    let raf = 0;
    const frame = () => {
      const d = img.data;
      let i = 0;
      for (let y = 0; y < H; y++) {
        const dy = y - cy;
        for (let x = 0; x < W; x++) {
          const dx = x - cx;
          const angle = ((Math.atan2(dy, dx) * 180) / Math.PI + 360 + offset) % 360;
          const sat = Math.min(Math.sqrt(dx * dx + dy * dy) / maxR, 1) * 100;
          const rgb = hslToRgb(angle, sat, 50);
          d[i++] = rgb[0]; d[i++] = rgb[1]; d[i++] = rgb[2]; d[i++] = 255;
        }
      }
      ctx.putImageData(img, 0, 0);
      offset = (offset + 3) % 360;
      raf = requestAnimationFrame(frame);
    };
    frame();
    return () => cancelAnimationFrame(raf);
  }, []);

  return <canvas ref={canvas} width={W} height={H} />;
}

// ---------------------------------------------------------------- patterns

type Side = 'top' | 'right' | 'bottom' | 'left';

/** Four edge bands, each split into equal segments listed clockwise; the middle stays black. */
function Edges(sides: Record<Side, string[]>) {
  return (
    <>
      {(['top', 'right', 'bottom', 'left'] as const).map((s) => (
        <div key={s} class={`tp-band tp-${s}`}>{sides[s].map((c) => <i style={{ background: c }} />)}</div>
      ))}
    </>
  );
}

/** n grey levels from black to white. */
const ramp = (n: number) => Array.from({ length: n }, (_, i) => {
  const v = Math.round((i * 255) / (n - 1));
  return `rgb(${v},${v},${v})`;
});

function Flicker() {
  return (
    <>
      {SAMPLES.map(([x, y], i) => (
        <div class="tp-patch" style={{ left: `${x - 48}px`, top: `${y - 48}px` }}>
          <div class={i % 2 ? 'tp-ticker' : 'tp-stripes'} />
        </div>
      ))}
    </>
  );
}

interface Pattern {
  id: string;
  name: string;
  /** What the strip should do. */
  note: string;
  view: () => JSX.Element;
}

const solid = (id: string, name: string, color: string, note: string): Pattern => ({
  id, name, note, view: () => <div class="tp-fill" style={{ background: color }} />,
});

const PATTERNS: Pattern[] = [
  { id: 'wheel', name: 'Color wheel', note: 'The strip should follow the colors at the screen edges', view: Wheel },
  solid('red', 'Red', '#ff0000', 'The whole strip red'),
  solid('green', 'Green', '#00ff00', 'The whole strip green'),
  solid('blue', 'Blue', '#0000ff', 'The whole strip blue'),
  solid('white', 'White', '#ffffff', 'An even, neutral white: checks white balance'),
  solid('grey', 'Grey 50%', '#808080', 'An even, neutral grey at about half brightness'),
  solid('black', 'Black', '#000000', 'The strip should go dark'),
  {
    id: 'sides', name: 'Sides',
    note: 'Top red, right green, bottom yellow, left blue: the controller’s calibration preview colors',
    view: () => <Edges top={['#ff0000']} right={['#00ff00']} bottom={['#ffff00']} left={['#0000ff']} />,
  },
  {
    id: 'quadrants', name: 'Quadrants',
    note: 'Red top-left, green top-right, blue bottom-right, white bottom-left',
    view: () => (
      <div class="tp-quads">
        <i style={{ background: '#ff0000' }} />
        <i style={{ background: '#00ff00' }} />
        <i style={{ background: '#ffffff' }} />
        <i style={{ background: '#0000ff' }} />
      </div>
    ),
  },
  {
    id: 'chase', name: 'Chase',
    note: 'One white block running clockwise from the top-left corner, a lap in 6 s',
    view: () => <div class="tp-chase" />,
  },
  {
    id: 'ramp', name: 'Grey ramp',
    note: 'Black to white clockwise along each edge: even steps, no color tint',
    view: () => <Edges top={ramp(H_POINTS)} right={ramp(V_POINTS)} bottom={ramp(H_POINTS)} left={ramp(V_POINTS)} />,
  },
  {
    id: 'hue', name: 'Hue sweep',
    note: 'The whole screen cycles through the hue circle in 20 s',
    view: () => <div class="tp-fill tp-hue" />,
  },
  {
    id: 'flicker', name: 'Flicker torture',
    note: 'Fine moving detail at the sample points: compare smoothing settings',
    view: Flicker,
  },
];

// ---------------------------------------------------------------- screen

/** The pattern shown last, kept while the app runs. Desktop preview: ?pattern=<id>. */
let current = Math.max(0, window.tizen ? 0 : PATTERNS.findIndex((p) => p.id === new URLSearchParams(location.search).get('pattern')));

export function TestPattern({ bindKeys }: { bindKeys: (h: KeyHandler) => void }) {
  const [index, setIndex] = useState(current);
  const [label, setLabel] = useState(true);
  /** Bumped on every key: restarts the label timer. */
  const [keyCount, setKeyCount] = useState(0);

  useEffect(() => {
    const id = setTimeout(() => setLabel(false), LABEL_MS);
    return () => clearTimeout(id);
  }, [keyCount]);

  bindKeys((key) => {
    if (key === 'left' || key === 'right') {
      current = (current + (key === 'right' ? 1 : PATTERNS.length - 1)) % PATTERNS.length;
      setIndex(current);
    }
    setLabel(true);
    setKeyCount((n) => n + 1);
    return true;
  });

  const p = PATTERNS[index];
  const View = p.view;
  return (
    <div class="test-pattern">
      <View key={p.id} />
      {label && (
        <div class="test-hint">
          <b>{p.name} <span class="ui-muted">{index + 1}/{PATTERNS.length}</span></b>
          <span>{p.note}</span>
          <span class="ui-muted">◀ ▶ switch · OK/Back close</span>
        </div>
      )}
    </div>
  );
}
