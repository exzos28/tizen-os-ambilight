// Inline SVG icons on a 24×24 grid, stroked with currentColor. `size` is in CSS px; the TV
// launcher uses 28 in the rail, 26 in options and 46 in the problem banner.

import type { ComponentChildren } from 'preact'

export interface IconProps {
  size?: number
}

function Icon({ size = 24, children }: IconProps & { children: ComponentChildren }) {
  return (
    <svg class="ui-icon" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
      stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
      {children}
    </svg>
  )
}

/** Knobs of the sliders icon are filled with the surface behind it: --ui-icon-bg (the rail by default). */
const knob = { fill: 'var(--ui-icon-bg, var(--ui-rail))' }

export const HomeIcon = (p: IconProps) => <Icon {...p}><path d="M3 11l9-7 9 7" /><path d="M5 10v10h14V10" /></Icon>

export const PictureIcon = (p: IconProps) => (
  <Icon {...p}>
    <line x1="4" y1="6" x2="20" y2="6" /><line x1="4" y1="12" x2="20" y2="12" /><line x1="4" y1="18" x2="20" y2="18" />
    <circle cx="9" cy="6" r="2" style={knob} /><circle cx="15" cy="12" r="2" style={knob} /><circle cx="7" cy="18" r="2" style={knob} />
  </Icon>
)

export const LayoutIcon = (p: IconProps) => (
  <Icon {...p}><rect x="5" y="6" width="14" height="10" rx="1.5" /><path d="M2 4v14M22 4v14M5 20h14M5 2h14" /></Icon>
)

export const ChipIcon = (p: IconProps) => (
  <Icon {...p}><rect x="6" y="6" width="12" height="12" rx="2" /><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4" /></Icon>
)

export const PulseIcon = (p: IconProps) => <Icon {...p}><polyline points="2 12 6 12 9 4 15 20 18 12 22 12" /></Icon>

export const ScreenIcon = (p: IconProps) => <Icon {...p}><rect x="2" y="4" width="20" height="13" rx="2" /><path d="M8 21h8" /></Icon>

export const CircleIcon = (p: IconProps) => <Icon {...p}><circle cx="12" cy="12" r="8" /></Icon>

export const PowerIcon = (p: IconProps) => <Icon {...p}><path d="M12 3v9" /><path d="M6.3 6.8a8 8 0 1 0 11.4 0" /></Icon>

export const WarnIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" />
    <line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" />
  </Icon>
)

export const ErrorIcon = (p: IconProps) => (
  <Icon {...p}><circle cx="12" cy="12" r="9" /><line x1="9" y1="9" x2="15" y2="15" /><line x1="15" y1="9" x2="9" y2="15" /></Icon>
)

export const CheckIcon = (p: IconProps) => <Icon {...p}><polyline points="4 12.5 9.5 18 20 6.5" /></Icon>

export const RefreshIcon = (p: IconProps) => (
  <Icon {...p}><path d="M20 11a8 8 0 0 0-14.6-4.5L3 9" /><path d="M3 4v5h5" /><path d="M4 13a8 8 0 0 0 14.6 4.5L21 15" /><path d="M21 20v-5h-5" /></Icon>
)

export const WifiIcon = (p: IconProps) => (
  <Icon {...p}><path d="M2 8.8a15 15 0 0 1 20 0" /><path d="M5 12.6a10 10 0 0 1 14 0" /><path d="M8.5 16.4a5 5 0 0 1 7 0" /><line x1="12" y1="20" x2="12.01" y2="20" /></Icon>
)

export const BlinkIcon = (p: IconProps) => (
  <Icon {...p}><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></Icon>
)

/** Rainbow-framed screen: the app icon. Gradient id is unique per size so two logos on a page don't clash. */
export function Logo({ size = 52 }: IconProps) {
  const id = `ui-logo-${size}`
  return (
    <svg class="ui-logo" width={size} height={size} viewBox="0 0 52 52" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#FF6B6B" /><stop offset="0.35" stop-color="#F2B544" />
          <stop offset="0.65" stop-color="#3DD68C" /><stop offset="1" stop-color="#4DA3FF" />
        </linearGradient>
      </defs>
      <rect x="6" y="12" width="40" height="28" rx="7" fill="none" stroke={`url(#${id})`} stroke-width="4" />
      <rect x="12" y="18" width="28" height="16" rx="3" fill="#1A2130" />
    </svg>
  )
}
