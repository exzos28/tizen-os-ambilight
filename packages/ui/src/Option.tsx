// One choice of a radio group (Follow the picture / Static color), visual only. The phone puts a
// native radio input right before it inside a <label> (focus ring via :focus-visible + .ui-option);
// the TV passes `focused`.

import type { ComponentChildren } from 'preact'
import './option.css'

export interface OptionProps {
  checked: boolean
  focused?: boolean
  icon?: ComponentChildren
  children: ComponentChildren
}

export function Option({ checked, focused, icon, children }: OptionProps) {
  return <span class={`ui-option${checked ? ' checked' : ''}${focused ? ' focused' : ''}`}>{icon}{children}</span>
}
