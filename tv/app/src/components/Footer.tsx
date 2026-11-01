// The remote-key hints at the bottom of a screen (KeyHint from @ambilight/ui).

import type { ComponentChildren } from 'preact';

export function Footer({ children }: { children: ComponentChildren }) {
  return <footer class="footer">{children}</footer>;
}
