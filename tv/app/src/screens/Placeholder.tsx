import { Footer, Hint } from '../components/Keys';
import type { KeyHandler } from './Home';

const ABOUT: Record<string, string> = {
  picture: 'Gamma, saturation, white balance and the power limit of the strip.',
  layout: 'LEDs per side, the corner the strip starts at and its direction.',
  outputs: 'Where the colors go: the LED controllers on the network and a HyperHDR server.',
  diagnostics: 'Capture timing, failed reads, frames sent and the controller metrics.',
};

export function Placeholder({ title, id, bindKeys }: { title: string; id: string; bindKeys: (h: KeyHandler) => void }) {
  // Nothing to focus here: every key but Back goes back to the rail.
  bindKeys((key) => (key === 'back' ? false : 'rail'));
  return (
    <main class="main">
      <header class="header">
        <div class="header-titles">
          <h1>{title}</h1>
          <p class="subtitle">{ABOUT[id] ?? ''}</p>
        </div>
      </header>
      <section class="panel placeholder">
        <h2>Coming soon</h2>
        <p class="muted-s">This screen is not in this version of the app yet. Home has the lighting controls and the status.</p>
      </section>
      <Footer>
        <Hint k="▲▼">Sections</Hint>
        <Hint k="↩">Home</Hint>
      </Footer>
    </main>
  );
}
