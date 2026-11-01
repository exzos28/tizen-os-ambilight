// Inline SVG icons (24×24 grid, stroked with currentColor).

import type { ComponentChildren } from 'preact';

function Icon({ size = 28, children }: { size?: number; children: ComponentChildren }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      {children}
    </svg>
  );
}

export const HomeIcon = () => <Icon><path d="M3 11l9-7 9 7" /><path d="M5 10v10h14V10" /></Icon>;

export const PictureIcon = () => (
  <Icon>
    <line x1="4" y1="6" x2="20" y2="6" /><line x1="4" y1="12" x2="20" y2="12" /><line x1="4" y1="18" x2="20" y2="18" />
    <circle cx="9" cy="6" r="2" fill="#0F131A" /><circle cx="15" cy="12" r="2" fill="#0F131A" /><circle cx="7" cy="18" r="2" fill="#0F131A" />
  </Icon>
);

export const LayoutIcon = () => <Icon><rect x="5" y="6" width="14" height="10" rx="1.5" /><path d="M2 4v14M22 4v14M5 20h14M5 2h14" /></Icon>;

export const ChipIcon = () => (
  <Icon><rect x="6" y="6" width="12" height="12" rx="2" /><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4" /></Icon>
);

export const PulseIcon = () => <Icon><polyline points="2 12 6 12 9 4 15 20 18 12 22 12" /></Icon>;

export const ScreenIcon = () => <Icon size={26}><rect x="2" y="4" width="20" height="13" rx="2" /><path d="M8 21h8" /></Icon>;

export const CircleIcon = () => <Icon size={26}><circle cx="12" cy="12" r="8" /></Icon>;

export const WarnIcon = () => (
  <Icon size={46}>
    <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" />
    <line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" />
  </Icon>
);

export const ErrorIcon = () => (
  <Icon size={46}><circle cx="12" cy="12" r="9" /><line x1="9" y1="9" x2="15" y2="15" /><line x1="15" y1="9" x2="9" y2="15" /></Icon>
);

export const Logo = () => (
  <svg width="52" height="52" viewBox="0 0 52 52" aria-hidden="true">
    <defs>
      <linearGradient id="logo" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stop-color="#FF6B6B" /><stop offset="0.35" stop-color="#F2B544" />
        <stop offset="0.65" stop-color="#3DD68C" /><stop offset="1" stop-color="#4DA3FF" />
      </linearGradient>
    </defs>
    <rect x="6" y="12" width="40" height="28" rx="7" fill="none" stroke="url(#logo)" stroke-width="4" />
    <rect x="12" y="18" width="28" height="16" rx="3" fill="#1A2130" />
  </svg>
);
