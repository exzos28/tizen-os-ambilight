// Primary (filled blue) and secondary (outlined) buttons. A real <button>; on the TV, where the
// focus manager owns the focus, pass `focused` instead of relying on :focus-visible.

import type { ButtonHTMLAttributes, ComponentChildren } from 'preact'
import './button.css'

export interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'class' | 'className' | 'icon'> {
  variant?: 'primary' | 'secondary' | 'danger'
  focused?: boolean
  /** Full width of its container. */
  block?: boolean
  icon?: ComponentChildren
  class?: string
  children?: ComponentChildren
}

export function Button({ variant = 'secondary', focused, block, icon, class: cls, type = 'button', children, ...rest }: ButtonProps) {
  const c = `ui-btn ui-btn-${variant}${focused ? ' focused' : ''}${block ? ' block' : ''}${cls ? ` ${cls}` : ''}`
  return <button {...rest} type={type} class={c}>{icon}{children}</button>
}
