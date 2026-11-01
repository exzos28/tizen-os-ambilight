// A level bar with an optional knob. Display only: the TV moves it with ◀ ▶, the phone lays a
// transparent native <input type="range"> over it (see `inset`).

import './slider-bar.css'

export interface SliderBarProps {
  /** 0…1 */
  value: number
  knob?: boolean
  /** Grey fill (the TV's compact tile while not adjusting, or a disabled control). */
  muted?: boolean
  /**
   * Keep the knob inside the bar: its centre runs from knob/2 to width − knob/2, like the thumb
   * of a native range input of the same width, so an input laid over it lines up.
   */
  inset?: boolean
  /** Fill color (white-balance channels, power use); the text color by default. */
  color?: string
}

export function SliderBar({ value, knob, muted, inset, color }: SliderBarProps) {
  const p = Math.max(0, Math.min(1, Number.isFinite(value) ? value : 0))
  const style = `--ui-p:${p.toFixed(4)}${color ? `;--ui-fill:${color}` : ''}`
  return (
    <span class={`ui-bar${muted ? ' muted' : ''}${inset ? ' inset' : ''}`} style={style} aria-hidden="true">
      <span class="ui-bar-fill" />
      {knob && <span class="ui-bar-knob" />}
    </span>
  )
}
