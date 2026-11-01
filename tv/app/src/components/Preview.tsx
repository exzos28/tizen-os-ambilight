// Mini screen: inner bands = what the TV samples (status.capture.rgb, frame order top L→R,
// right T→B, bottom R→L, left B→T), dots = the LEDs along each side, interpolated like the
// controller does (linear RGB here, the firmware blends in HSV). Plain divs, no effects.

import { type Esp32Config, LedMode } from '@ambilight/protocol';

const W = 440, H = 248, OX = 40, OY = 36, B = 26, R = 16, DOT = 10;
const BOX_W = W + 2 * OX, BOX_H = H + 2 * OY;
const DARK = '#1A2130';

type Rgb = [number, number, number];

const parse = (hex: string, i: number): Rgb => [
  parseInt(hex.substr(i * 6, 2), 16), parseInt(hex.substr(i * 6 + 2, 2), 16), parseInt(hex.substr(i * 6 + 4, 2), 16),
];
const css = (c: Rgb) => `rgb(${c[0] | 0},${c[1] | 0},${c[2] | 0})`;

function interpolate(src: Rgb[], count: number): string[] {
  const out: string[] = [];
  for (let i = 0; i < count; i++) {
    if (src.length === 1 || count === 1) { out.push(css(src[0])); continue; }
    const p = (i / (count - 1)) * (src.length - 1);
    const lo = Math.min(Math.floor(p), src.length - 2), f = p - lo;
    const a = src[lo], b = src[lo + 1];
    out.push(css([a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f]));
  }
  return out;
}

interface Props {
  rgb?: string;
  h?: number;
  v?: number;
  /** Samples are current (capture running). */
  live: boolean;
  config?: Esp32Config;
}

const box = (x: number, y: number, w: number, h: number, bg: string) =>
  `left:${x}px;top:${y}px;width:${w}px;height:${h}px;background:${bg}`;

export function Preview({ rgb, h = 0, v = 0, live, config }: Props) {
  const samples: Rgb[] = [];
  const valid = !!rgb && h > 0 && v > 0 && rgb.length >= 2 * (h + v) * 6;
  if (valid) for (let i = 0; i < 2 * (h + v); i++) samples.push(parse(rgb!, i));

  const bands: string[] = [];
  if (valid) {
    const tw = W / h, sh = (H - 2 * B) / v;
    let k = 0;
    for (let i = 0; i < h; i++) bands.push(box(i * tw, 0, tw, B, css(samples[k++])));
    for (let i = 0; i < v; i++) bands.push(box(W - B, B + i * sh, B, sh, css(samples[k++])));
    for (let i = 0; i < h; i++) bands.push(box(W - (i + 1) * tw, H - B, tw, B, css(samples[k++])));
    for (let i = 0; i < v; i++) bands.push(box(0, H - B - (i + 1) * sh, B, sh, css(samples[k++])));
  }

  const dots: string[] = [];
  if (config) {
    const n = [config.led_top, config.led_right, config.led_bottom, config.led_left];
    const side = (s: number, from: number, len: number): string[] => {
      if (!config.power) return new Array<string>(n[s]).fill(DARK);
      if (config.led_mode === LedMode.plain) return new Array<string>(n[s]).fill(config.color);
      if (!valid) return new Array<string>(n[s]).fill(DARK);
      return interpolate(samples.slice(from, from + len), n[s]);
    };
    const top = side(0, 0, h), right = side(1, h, v), bottom = side(2, h + v, h), left = side(3, 2 * h + v, v);
    const d = DOT / 2;
    top.forEach((c, i) => dots.push(box(OX + ((i + 0.5) * W) / top.length - d, OY - R - d, DOT, DOT, c)));
    right.forEach((c, i) => dots.push(box(OX + W + R - d, OY + ((i + 0.5) * H) / right.length - d, DOT, DOT, c)));
    bottom.forEach((c, i) => dots.push(box(OX + W - ((i + 0.5) * W) / bottom.length - d, OY + H + R - d, DOT, DOT, c)));
    left.forEach((c, i) => dots.push(box(OX - R - d, OY + H - ((i + 0.5) * H) / left.length - d, DOT, DOT, c)));
  }

  return (
    <div class={`preview${live ? '' : ' stale'}`} style={`width:${BOX_W}px;height:${BOX_H}px`}>
      <div class="preview-screen" style={box(OX, OY, W, H, '#05070A')}>
        {bands.map((s, i) => <div key={i} class="band" style={s} />)}
        {!valid && <span class="preview-empty">No picture yet</span>}
      </div>
      {dots.map((s, i) => <div key={i} class="dot" style={s} />)}
    </div>
  );
}
