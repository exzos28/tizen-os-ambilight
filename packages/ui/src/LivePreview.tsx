// Mini screen: inner bands = what the TV samples (a status `rgb` string in EdgeFrame order),
// dots = the LEDs along each side, interpolated like the controller does. One SVG, no effects;
// it scales to `width` (520×320 units, the TV draws it at 1:1).

import { type Rgb, cssRgb, interpolate, parseEdgeColors } from './geometry.ts'
import './live-preview.css'

const W = 440, H = 248, OX = 40, OY = 36, B = 26, R = 16, DOT = 10
/** Screen frame: the bands sit inside it, like content inside a CSS border of BW px. */
const BW = 2, RX = 8
const BOX_W = W + 2 * OX, BOX_H = H + 2 * OY
/** Middle of the "No picture yet" line, from the top of the area inside the frame. */
const EMPTY_Y = 119

/** The controller settings the dots depend on; an Esp32Config fits. */
export interface PreviewConfig {
  led_top: number
  led_right: number
  led_bottom: number
  led_left: number
  power: boolean
  /** 0 = static color, 1 = follow the picture. */
  led_mode: number
  color: string
}

export interface LivePreviewProps {
  rgb?: string | null
  /** Samples per horizontal / vertical edge. */
  h?: number
  v?: number
  /** The samples are current; otherwise they are dimmed. */
  live: boolean
  config?: PreviewConfig
  /** Rendered width in CSS px; the container's width when left out. */
  width?: number
  emptyText?: string
}

export function LivePreview({ rgb, h = 0, v = 0, live, config, width, emptyText = 'No picture yet' }: LivePreviewProps) {
  const samples = parseEdgeColors(rgb, h, v)

  const bands: { x: number; y: number; w: number; h: number; c: string }[] = []
  if (samples) {
    const tw = W / h, sh = (H - 2 * B) / v
    let k = 0
    const c = () => cssRgb(samples[k++]!)
    for (let i = 0; i < h; i++) bands.push({ x: i * tw, y: 0, w: tw, h: B, c: c() })
    for (let i = 0; i < v; i++) bands.push({ x: W - B, y: B + i * sh, w: B, h: sh, c: c() })
    for (let i = 0; i < h; i++) bands.push({ x: W - (i + 1) * tw, y: H - B, w: tw, h: B, c: c() })
    for (let i = 0; i < v; i++) bands.push({ x: 0, y: H - B - (i + 1) * sh, w: B, h: sh, c: c() })
  }

  const dots: { x: number; y: number; c: string }[] = []
  if (config) {
    const n = [config.led_top, config.led_right, config.led_bottom, config.led_left]
    const side = (s: number, from: number, len: number): string[] => {
      const count = n[s] ?? 0
      if (!config.power || (config.led_mode !== 0 && !samples)) return new Array<string>(count).fill('var(--ui-led-dark)')
      if (config.led_mode === 0) return new Array<string>(count).fill(config.color)
      return interpolate(samples!.slice(from, from + len) as Rgb[], count).map(cssRgb)
    }
    const top = side(0, 0, h), right = side(1, h, v), bottom = side(2, h + v, h), left = side(3, 2 * h + v, v)
    top.forEach((c, i) => dots.push({ x: OX + ((i + 0.5) * W) / top.length, y: OY - R, c }))
    right.forEach((c, i) => dots.push({ x: OX + W + R, y: OY + ((i + 0.5) * H) / right.length, c }))
    bottom.forEach((c, i) => dots.push({ x: OX + W - ((i + 0.5) * W) / bottom.length, y: OY + H + R, c }))
    left.forEach((c, i) => dots.push({ x: OX - R, y: OY + H - ((i + 0.5) * H) / left.length, c }))
  }

  return (
    <svg class={`ui-preview${live ? '' : ' stale'}`} viewBox={`0 0 ${BOX_W} ${BOX_H}`}
      width={width} height={width ? (width * BOX_H) / BOX_W : undefined} role="img"
      aria-label={samples ? 'Colors at the edges of the picture and on the LEDs' : emptyText}>
      {/* Same id in every instance on purpose: the definitions are identical. */}
      <clipPath id="ui-preview-clip"><rect width={W - 2 * BW} height={H - 2 * BW} rx={RX - BW} /></clipPath>
      <g transform={`translate(${OX} ${OY})`}>
        <rect class="ui-preview-screen" width={W} height={H} rx={RX} />
        <rect class="ui-preview-frame" x={BW / 2} y={BW / 2} width={W - BW} height={H - BW} rx={RX - BW / 2} stroke-width={BW} />
        <g transform={`translate(${BW} ${BW})`}>
          <g clip-path="url(#ui-preview-clip)">
            {bands.map((b, i) => <rect key={i} class="ui-preview-band" x={b.x} y={b.y} width={b.w} height={b.h} style={`fill:${b.c}`} />)}
          </g>
          {!samples && <text class="ui-preview-empty" x={W / 2 - BW} y={EMPTY_Y} text-anchor="middle" dominant-baseline="central">{emptyText}</text>}
        </g>
      </g>
      {dots.map((d, i) => <circle key={i} class="ui-preview-dot" cx={d.x} cy={d.y} r={DOT / 2} style={`fill:${d.c}`} />)}
    </svg>
  )
}
