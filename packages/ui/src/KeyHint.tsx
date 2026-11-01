// TV only: remote-control key caps for hints and the footer.

import type { ComponentChildren } from 'preact'
import './key-hint.css'

export const Kbd = ({ children }: { children: ComponentChildren }) => <span class="ui-kbd">{children}</span>

export function KeyHint({ k, children }: { k: string; children: ComponentChildren }) {
  return <span class="ui-hint"><Kbd>{k}</Kbd>{children}</span>
}
