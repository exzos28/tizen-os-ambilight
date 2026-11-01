import type { JSX } from 'preact';
import { ChipIcon, HomeIcon, LayoutIcon, Logo, PictureIcon, PulseIcon } from './icons';

export type ScreenId = 'home' | 'picture' | 'layout' | 'outputs' | 'diagnostics';

export const RAIL: { id: ScreenId; label: string; icon: () => JSX.Element }[] = [
  { id: 'home', label: 'Home', icon: HomeIcon },
  { id: 'picture', label: 'Picture', icon: PictureIcon },
  { id: 'layout', label: 'Strip layout', icon: LayoutIcon },
  { id: 'outputs', label: 'Outputs', icon: ChipIcon },
  { id: 'diagnostics', label: 'Diagnostics', icon: PulseIcon },
];

interface Props {
  current: ScreenId;
  /** Index of the focused entry, -1 when the focus is on the screen. */
  focused: number;
  problems: number;
  version: string | null;
}

export function Rail({ current, focused, problems, version }: Props) {
  return (
    <nav class="rail" aria-label="Sections">
      <div class="brand">
        <Logo />
        <div class="brand-text">
          <span class="brand-name">Ambilight</span>
          {version && <span class="brand-version">v{version}</span>}
        </div>
      </div>
      <div class="rail-items">
        {RAIL.map((it, i) => (
          <div key={it.id} class={`rail-item${it.id === current ? ' current' : ''}${i === focused ? ' focused' : ''}`} aria-current={it.id === current ? 'page' : undefined}>
            <it.icon />
            {it.label}
            {it.id === 'home' && problems > 0 && <span class="badge" aria-label={`${problems} problems`}>{problems}</span>}
          </div>
        ))}
      </div>
      <p class="rail-note">Starts with the TV. Settings live on the controller, so the phone app shows the same values.</p>
    </nav>
  );
}
