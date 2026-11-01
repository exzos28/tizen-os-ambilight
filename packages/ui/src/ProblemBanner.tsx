// The one thing that needs attention, on top of Home: icon, title, what it means, optional
// technical detail and hints, and the fix as buttons (`actions`).

import type { ComponentChildren } from 'preact'
import { ErrorIcon, WarnIcon } from './icons.tsx'
import './problem-banner.css'

export interface ProblemBannerProps {
  tone: 'warn' | 'bad'
  title: ComponentChildren
  text?: ComponentChildren
  /** Technical detail (an error message), smaller and on one line. */
  detail?: ComponentChildren
  hints?: string[]
  /** Buttons with the fix. */
  actions?: ComponentChildren
}

export function ProblemBanner({ tone, title, text, detail, hints, actions }: ProblemBannerProps) {
  return (
    <section class={`ui-banner ui-banner-${tone}`} role="alert">
      <span class="ui-banner-icon">{tone === 'bad' ? <ErrorIcon size={46} /> : <WarnIcon size={46} />}</span>
      <div class="ui-banner-body">
        <h2 class="ui-banner-title">{title}</h2>
        {text && <p class="ui-banner-text">{text}</p>}
        {detail && <p class="ui-banner-detail">{detail}</p>}
        {hints && hints.length > 0 && <ul class="ui-banner-hints">{hints.map((h) => <li key={h}>{h}</li>)}</ul>}
      </div>
      {actions && <div class="ui-banner-actions">{actions}</div>}
    </section>
  )
}
