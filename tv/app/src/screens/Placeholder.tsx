import { Card, KeyHint } from '@ambilight/ui';
import { Footer } from '../components/Footer';

const ABOUT: Record<string, string> = {
  picture: 'Gamma, saturation, white balance and the power limit of the strip.',
  layout: 'LEDs per side, the corner the strip starts at and its direction.',
  controllers: 'Every LED controller on the network: address, firmware, Wi-Fi and power.',
  diagnostics: 'Capture timing, failed reads, frames sent and the controller metrics.',
};

export function Placeholder({ title, id }: { title: string; id: string }) {
  return (
    <main class="main">
      <header class="header">
        <div class="header-titles">
          <h1>{title}</h1>
          <p class="subtitle">{ABOUT[id] ?? ''}</p>
        </div>
      </header>
      <Card class="placeholder">
        <h2>Coming soon</h2>
        <p class="ui-muted">This screen is not in this version of the app yet. Home has the lighting controls and the status.</p>
      </Card>
      <Footer>
        <KeyHint k="▲▼">Sections</KeyHint>
        <KeyHint k="↩">Home</KeyHint>
      </Footer>
    </main>
  );
}
