// Pure helpers behind LivePreview and StripLayoutDiagram (no DOM, tested with node --test).

export type Rgb = [number, number, number]

export const SIDES = ['top', 'right', 'bottom', 'left'] as const
export type Side = (typeof SIDES)[number]

/** Corner index as the controller numbers them (start_corner): 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left. */
export type Corner = 0 | 1 | 2 | 3

export const CORNER_NAMES = ['top-left', 'top-right', 'bottom-right', 'bottom-left'] as const

/**
 * The edge samples of a frame: `rgb` is hex without separators, 6 characters per sample, in
 * EdgeFrame order — top left→right (h), right top→bottom (v), bottom right→left (h), left
 * bottom→top (v). Null when the string is missing or too short for h and v.
 */
export function parseEdgeColors(rgb: string | undefined | null, h: number, v: number): Rgb[] | null {
  const n = 2 * (h + v)
  if (!rgb || h <= 0 || v <= 0 || rgb.length < n * 6) return null
  const out: Rgb[] = []
  for (let i = 0; i < n; i++) {
    const o = i * 6
    const c: Rgb = [parseInt(rgb.slice(o, o + 2), 16), parseInt(rgb.slice(o + 2, o + 4), 16), parseInt(rgb.slice(o + 4, o + 6), 16)]
    if (c.some((x) => Number.isNaN(x))) return null
    out.push(c)
  }
  return out
}

/** `count` colors spread linearly over `src` (the controller blends in HSV; this is a preview). */
export function interpolate(src: readonly Rgb[], count: number): Rgb[] {
  const out: Rgb[] = []
  if (!src.length) return out
  for (let i = 0; i < count; i++) {
    if (src.length === 1 || count === 1) { out.push(src[0]!); continue }
    const p = (i / (count - 1)) * (src.length - 1)
    const lo = Math.min(Math.floor(p), src.length - 2)
    const f = p - lo
    const a = src[lo]!, b = src[lo + 1]!
    out.push([a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f])
  }
  return out
}

export const cssRgb = (c: Rgb) => `rgb(${c[0] | 0},${c[1] | 0},${c[2] | 0})`

/**
 * Position (1…4) of each side along the strip, indexed like SIDES. Side i runs from corner i to
 * corner i+1 clockwise, so a clockwise strip starting at corner c covers side c first; a
 * counter-clockwise one covers side c−1 first.
 */
export function sideOrder(startCorner: Corner, clockwise: boolean): [number, number, number, number] {
  const order: [number, number, number, number] = [0, 0, 0, 0]
  for (let s = 0; s < 4; s++) {
    const side = clockwise ? (startCorner + s) % 4 : (startCorner + 3 - s) % 4
    order[side] = s + 1
  }
  return order
}

/**
 * Direction the LEDs run along each side as seen from the front, indexed like SIDES:
 * clockwise = top →, right ↓, bottom ←, left ↑.
 */
export function sideArrows(clockwise: boolean): [string, string, string, string] {
  return clockwise ? ['→', '↓', '←', '↑'] : ['←', '↑', '→', '↓']
}
