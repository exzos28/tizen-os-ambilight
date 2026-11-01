// Page header and the bottom tab bar.

import type { ComponentChildren } from 'preact'
import { ChipIcon, HomeIcon, LayoutIcon, Logo, PictureIcon, StatusPill, type Tone } from '@ambilight/ui'

export type TabId = 'home' | 'picture' | 'layout' | 'device'

export const TABS: { id: TabId; label: string; icon: () => ComponentChildren }[] = [
  { id: 'home', label: 'Home', icon: () => <HomeIcon /> },
  { id: 'picture', label: 'Picture', icon: () => <PictureIcon /> },
  { id: 'layout', label: 'Layout', icon: () => <LayoutIcon /> },
  { id: 'device', label: 'Device', icon: () => <ChipIcon /> },
]

export function Header({ title, subtitle, pill }: { title: string; subtitle?: string; pill?: { tone: Tone; text: string } }) {
  return (
    <header class="top">
      <Logo size={36} />
      <div class="top-titles">
        <h1>{title}</h1>
        {subtitle && <p class="ui-muted top-sub">{subtitle}</p>}
      </div>
      {pill && <StatusPill tone={pill.tone}>{pill.text}</StatusPill>}
    </header>
  )
}

export function TabBar({ current, problems, onChange }: { current: TabId; problems: number; onChange: (t: TabId) => void }) {
  return (
    <nav class="tabbar" aria-label="Sections">
      {TABS.map((t) => (
        <a key={t.id} href={`#${t.id}`} class={`tab${t.id === current ? ' current' : ''}`} aria-current={t.id === current ? 'page' : undefined}
          onClick={(e) => { e.preventDefault(); onChange(t.id) }}>
          <span class="tab-icon">{t.icon()}{t.id === 'home' && problems > 0 && <span class="badge" aria-label={`${problems} problems`}>{problems}</span>}</span>
          {t.label}
        </a>
      ))}
    </nav>
  )
}
