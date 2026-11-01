// Static color swatches. With `onSelect` they are real buttons (aria-pressed); without it plain
// elements for a focus manager that passes `focusedIndex` (TV). `children` are extra cells after
// the swatches, e.g. a custom color input.

import type { ComponentChildren } from 'preact'
import './color-swatches.css'

export interface Swatch {
  name: string
  /** #rrggbb, lowercase (the controller returns lowercase). */
  color: string
}

/** The six colors the TV and the phone offer. */
export const SWATCHES: readonly Swatch[] = [
  { name: 'Warm white', color: '#ffd9a8' },
  { name: 'Cool white', color: '#eaf2ff' },
  { name: 'Amber', color: '#f2a13b' },
  { name: 'Red', color: '#e5484d' },
  { name: 'Blue', color: '#3e7fd9' },
  { name: 'Purple', color: '#8e6ad8' },
]

export interface ColorSwatchesProps {
  /** The selected color (#rrggbb), or null when none is (e.g. the strip follows the picture). */
  selected: string | null
  swatches?: readonly Swatch[]
  focusedIndex?: number
  onSelect?: (color: string) => void
  small?: boolean
  disabled?: boolean
  label?: string
  children?: ComponentChildren
}

export function ColorSwatches({ selected, swatches = SWATCHES, focusedIndex, onSelect, small, disabled, label = 'Static colors', children }: ColorSwatchesProps) {
  const sel = selected?.toLowerCase()
  return (
    <div class={`ui-swatches${small ? ' small' : ''}`} role="group" aria-label={label}>
      {swatches.map((s, i) => {
        const cls = `ui-swatch${sel === s.color ? ' selected' : ''}${focusedIndex === i ? ' focused' : ''}`
        const dot = <span class="ui-swatch-dot" style={`background:${s.color}`} />
        return onSelect
          ? <button key={s.color} type="button" class={cls} aria-label={s.name} aria-pressed={sel === s.color} disabled={disabled} onClick={() => onSelect(s.color)}>{dot}</button>
          : <span key={s.color} class={cls} aria-label={s.name}>{dot}</span>
      })}
      {children}
    </div>
  )
}
