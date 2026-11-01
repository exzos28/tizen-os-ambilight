// A panel: the TV's tiles and panels, the phone's sections. Focus ring from `focused` (TV) or
// :focus-visible when the card itself is a focusable element.

import type { ComponentChildren } from 'preact'
import type { Tone } from './tone.ts'
import './card.css'

export interface CardProps {
  title?: ComponentChildren
  /** Right side of the title row: a value, a pill, a hint. */
  aside?: ComponentChildren
  /** Tinted card for a status that needs attention. */
  tone?: Extract<Tone, 'warn' | 'bad'>
  focused?: boolean
  /** Being adjusted with the remote (TV): white focus ring. */
  adjusting?: boolean
  class?: string
  /** aria-label of the section when there is no title. */
  label?: string
  children?: ComponentChildren
}

export function Card({ title, aside, tone, focused, adjusting, class: cls, label, children }: CardProps) {
  const c = `ui-card${tone ? ` ui-card-${tone}` : ''}${focused ? ' focused' : ''}${adjusting ? ' adjusting' : ''}${cls ? ` ${cls}` : ''}`
  return (
    <section class={c} aria-label={title ? undefined : label}>
      {(title || aside) && (
        <header class="ui-card-head">
          {title && <h2 class="ui-card-title">{title}</h2>}
          {aside}
        </header>
      )}
      {children}
    </section>
  )
}
