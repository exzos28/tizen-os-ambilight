// The strip around the screen, seen from the front: each side in the color the controller's
// calibration preview lights it (top red, right green, bottom yellow, left blue), an arrow for
// the direction, LED 1 at the start corner. With onCorner / onSide the corners and sides are
// buttons; without them a focus manager passes `focused` (TV).

import type { ComponentChildren } from 'preact'
import { CORNER_NAMES, type Corner, SIDES, type Side, sideArrows, sideOrder } from './geometry.ts'
import './strip-layout.css'

export interface StripLayoutDiagramProps {
  top: number
  right: number
  bottom: number
  left: number
  startCorner: Corner
  clockwise: boolean
  /** Corner pressed: make it the start. */
  onCorner?: (corner: Corner) => void
  /** Side pressed: the app reverses the direction. */
  onSide?: (side: Side) => void
  /** `corner-0`…`corner-3` or a side name. */
  focused?: string
  disabled?: boolean
  /** Inside the screen. */
  children?: ComponentChildren
}

const SIDE_LABEL: Record<Side, string> = { top: 'Top', right: 'Right', bottom: 'Bottom', left: 'Left' }

export function StripLayoutDiagram(p: StripLayoutDiagramProps) {
  const counts = [p.top, p.right, p.bottom, p.left]
  const order = sideOrder(p.startCorner, p.clockwise)
  const arrows = sideArrows(p.clockwise)
  // Vertical labels are rotated text, so they use a horizontal arrow that the rotation turns.
  const glyph = (i: number) => (i === 1 || i === 3 ? (p.clockwise ? '→' : '←') : arrows[i])
  const dir = p.clockwise ? 'clockwise' : 'counter-clockwise'

  const corners = ([0, 1, 2, 3] as const).map((c) => {
    const start = c === p.startCorner
    const cls = `ui-strip-corner c${c}${start ? ' start' : ''}${p.focused === `corner-${c}` ? ' focused' : ''}`
    const mark = <span class="ui-strip-led">{start ? '1' : ''}</span>
    return p.onCorner
      ? <button key={`c${c}`} type="button" class={cls} disabled={p.disabled} aria-pressed={start}
          aria-label={`Start at the ${CORNER_NAMES[c]} corner`} onClick={() => p.onCorner!(c)}>{mark}</button>
      : <span key={`c${c}`} class={cls} aria-hidden="true">{mark}</span>
  })

  const sides = SIDES.map((s, i) => {
    const cls = `ui-strip-side ${s}${p.focused === s ? ' focused' : ''}`
    const text = <span class="ui-strip-text"><span aria-hidden="true">{glyph(i)} </span>{SIDE_LABEL[s]} · {counts[i]}</span>
    const label = `${SIDE_LABEL[s]}: ${counts[i]} LEDs, part ${order[i]} of the strip, ${dir}. Reverse the direction`
    return p.onSide
      ? <button key={s} type="button" class={cls} disabled={p.disabled} aria-label={label} onClick={() => p.onSide!(s)}>{text}</button>
      : <span key={s} class={cls}>{text}</span>
  })

  return (
    <div class="ui-strip" role={p.onCorner || p.onSide ? 'group' : 'img'}
      aria-label={`Strip layout: starts at the ${CORNER_NAMES[p.startCorner]} corner, ${dir}`}>
      {corners[0]}{sides[0]}{corners[1]}
      {sides[3]}
      <div class="ui-strip-screen">{p.children ?? <span>TV</span>}</div>
      {sides[1]}
      {corners[3]}{sides[2]}{corners[2]}
    </div>
  )
}
