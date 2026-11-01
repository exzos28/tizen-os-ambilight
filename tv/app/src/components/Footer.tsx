// Remote-control key caps for hints and the footer.

import type { ComponentChildren } from 'preact';

export const Kbd = ({ children }: { children: ComponentChildren }) => <span class="kbd">{children}</span>;

export function Hint({ k, children }: { k: string; children: ComponentChildren }) {
  return <span class="hint"><Kbd>{k}</Kbd>{children}</span>;
}

export function Footer({ children }: { children: ComponentChildren }) {
  return <footer class="footer">{children}</footer>;
}
