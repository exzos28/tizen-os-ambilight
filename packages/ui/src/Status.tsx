// Status in three sizes: a dot, a pill (header summary) and a line (title, detail, value). The
// color is never the only cue: the pill and the line always carry text.

import type { ComponentChildren } from 'preact'
import type { Tone } from './tone.ts'
import './status.css'

export function StatusDot({ tone }: { tone: Tone }) {
  return <span class={`ui-dot ui-tone-${tone}`} aria-hidden="true" />
}

export function StatusPill({ tone, children }: { tone: Tone; children: ComponentChildren }) {
  return <span class={`ui-pill ui-tone-${tone}`}><span class="ui-dot" aria-hidden="true" />{children}</span>
}

export interface StatusLineProps {
  tone: Tone
  title: ComponentChildren
  detail?: ComponentChildren
  /** Short verdict on the right: Working, Streaming, Not found… */
  value?: ComponentChildren
  /** Standalone card (the TV's compact status row) instead of a row in a list. */
  card?: boolean
}

export function StatusLine({ tone, title, detail, value, card }: StatusLineProps) {
  return (
    <div class={`ui-line${card ? ' card' : ''} ui-tone-${tone}`}>
      <span class="ui-dot" aria-hidden="true" />
      <span class="ui-line-text">
        <span class="ui-line-title">{title}</span>
        {detail != null && <span class="ui-line-detail">{detail}</span>}
      </span>
      {value != null && <span class="ui-line-value">{value}</span>}
    </div>
  )
}

/** Rows of StatusLine separated by hairlines. */
export function StatusList({ children, label }: { children: ComponentChildren; label?: string }) {
  return <div class="ui-lines" role={label ? 'group' : undefined} aria-label={label}>{children}</div>
}
