// On/off switch, visual only (aria-hidden): put it inside the element that carries the state —
// a <button role="switch" aria-checked> on the phone, the focused tile on the TV.

import './switch.css'

export interface SwitchProps {
  on: boolean
  /** Big blue variant (the Lighting card); the plain one is grey when on. */
  accent?: boolean
}

export function Switch({ on, accent }: SwitchProps) {
  return <span class={`ui-switch${on ? ' on' : ''}${accent ? ' accent' : ''}`} aria-hidden="true"><span class="ui-knob" /></span>
}
