// First-run setup: "What lights do you have?" — an ESP32 strip, a HyperHDR server or both.
// The strip alone is sent at once (set-outputs); HyperHDR asks for the server's address first
// (step 2), connects and shows whether it answers. Shown once: when the outputs were never
// set (status.outputs.source "package") and the wizard was not finished or skipped on this TV.
// Outputs opens it again ("Run setup again"), over its own screen (`inline`).

import '@ambilight/ui/tokens.css';
import type { ComponentChildren } from 'preact';
import { useState } from 'preact/hooks';
import { Button, KeyHint, Logo, StatusDot } from '@ambilight/ui';
import type { ServiceStatus } from '@ambilight/protocol';
import { Footer } from '../components/Footer';
import { EDGE_LEFT, type FocusGrid, step } from '../focus';
import {
  type EditEnd, Field, type FieldId, type HyperHdrDraft, connectionLine, draftOf, outputsWith, settingsOf, useLastReply,
} from './hyperhdr';
import type { ScreenProps } from './types';
import './setup.css';

const DONE_KEY = 'ambilight.setupDone';
/** After Connect, how long "not connected yet" is shown as connecting rather than an error. */
const TEST_MS = 8000;

function setupDone(): boolean {
  try {
    return localStorage.getItem(DONE_KEY) === '1';
  } catch {
    return false;
  }
}

function markSetupDone() {
  try {
    localStorage.setItem(DONE_KEY, '1');
  } catch {
    // No storage (private mode, blocked): the wizard shows again until the outputs are saved.
  }
}

/** Show the wizard: the outputs were never set on this TV and it was not finished or skipped. */
export function shouldShowSetup(status: ServiceStatus | null): boolean {
  return status?.outputs.source === 'package' && !setupDone();
}

type Choice = 'esp' | 'hyperhdr' | 'both';
const CHOICES: Choice[] = ['esp', 'hyperhdr', 'both'];

export interface SetupProps extends ScreenProps {
  /** Finished or skipped (the outputs are set, or left as they are). */
  onClose: () => void;
  /** Opened from Outputs: Back closes it. Otherwise (first run) Back leaves to the TV. */
  inline?: boolean;
}

export function Setup(p: SetupProps) {
  const { link } = p;
  const outputs = link.kind === 'up' ? link.status.outputs : null;

  const [stepNo, setStepNo] = useState<1 | 2>(1);
  const [choice, setChoice] = useState<Choice>(() =>
    outputs?.hyperhdr.enabled ? (outputs.esp32.enabled ? 'both' : 'hyperhdr') : 'esp');
  const [focus, setFocus] = useState<string>(`opt-${choice}`);
  const [draft, setDraft] = useState<HyperHdrDraft | null>(null);
  const [editing, setEditing] = useState<{ id: FieldId; before: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** Step 2: when the settings were sent and taken (Date.now()), 0 = not yet. */
  const [appliedAt, setAppliedAt] = useState(0);
  const applied = appliedAt > 0;
  const lastReplyAt = useLastReply(link);

  const shown: HyperHdrDraft = draft ?? (outputs ? draftOf(outputs.hyperhdr) : { host: '', port: '19400', priority: '150' });

  const finish = () => {
    markSetupDone();
    p.onClose();
  };

  const send = async (enableEsp: boolean, enableHdr: boolean) => {
    if (!outputs || busy) {
      if (!outputs) setError('The Ambilight service is not running, so nothing can be set yet.');
      return false;
    }
    const s = settingsOf(shown, enableHdr);
    if (typeof s === 'string') { setError(s); return false; }
    setBusy(true);
    setError(null);
    const r = await p.backend.service.setOutputs(outputsWith(outputs, {
      esp32: { enabled: enableEsp },
      // The strip alone: HyperHDR off, its address kept as it was.
      hyperhdr: enableHdr ? s : { ...outputs.hyperhdr, enabled: false },
    }));
    setBusy(false);
    if (!r.ok) setError(r.error ?? 'The service did not take the settings');
    return r.ok;
  };

  // ---------------------------------------------------------------- focus grid
  const grid: FocusGrid = stepNo === 1
    ? [
      CHOICES.map((c, i) => ({ id: `opt-${c}`, col: i, nav: { down: 'skip' } })),
      [{ id: 'skip', col: 2, nav: { up: `opt-${choice}` } }],
    ]
    : [
      [{ id: 'f-host', col: 0 }],
      [{ id: 'f-port', col: 0 }],
      [{ id: 'connect', col: 0 }, { id: 'back', col: 1 }],
    ];
  const ids = grid.flat().map((it) => it.id);
  const current = ids.includes(focus) ? focus : ids[0];

  const move = (to: string) => {
    setFocus(to);
    if (to.startsWith('opt-')) setChoice(to.slice(4) as Choice);
  };

  const goBack = () => {
    if (stepNo === 2) {
      setStepNo(1);
      setError(null);
      setAppliedAt(0);
      setFocus(`opt-${choice}`);
      return true;
    }
    if (p.inline) { p.onClose(); return true; }
    return false; // first run: the shell's Back (to the TV)
  };

  const ok = async (id: string) => {
    if (id.startsWith('opt-')) {
      setChoice(id.slice(4) as Choice);
      if (id === 'opt-esp') {
        if (await send(true, false)) finish();
      } else {
        setError(null);
        setStepNo(2);
        setFocus(shown.host ? 'connect' : 'f-host');
      }
    } else if (id === 'skip') finish();
    else if (id === 'f-host' || id === 'f-port') {
      const field = id.slice(2) as FieldId;
      if (!draft) setDraft(shown);
      setEditing({ id: field, before: shown[field] });
    } else if (id === 'connect') {
      if (applied && !error) finish();
      else if (await send(choice === 'both', true)) setAppliedAt(Date.now());
    } else if (id === 'back') goBack();
  };

  const endEdit = (how: EditEnd) => {
    if (!editing) return;
    if (how === 'cancel' && draft) setDraft({ ...draft, [editing.id]: editing.before });
    else if (draft && draft[editing.id] !== editing.before) setAppliedAt(0); // changed: connect again
    setEditing(null);
    if (how === 'up' || how === 'down') {
      const next = step(grid, `f-${editing.id}`, how);
      if (next && next !== EDGE_LEFT) setFocus(next);
    }
  };

  p.bindKeys((key) => {
    if (editing) return true;
    if (key === 'back') return goBack();
    if (key === 'ok') { ok(current); return true; }
    const next = step(grid, current, key);
    if (next && next !== EDGE_LEFT) move(next);
    return true; // the rail is hidden behind the wizard
  });

  const f = (id: string) => p.active && current === id;
  const found = p.controllers.filter((c) => c.present).length;
  const conn = connectionLine(link, lastReplyAt, Date.now() - appliedAt < TEST_MS);

  return (
    <div class={`setup${p.inline ? ' inline' : ''}`}>
      <header class="setup-header">
        <div class="setup-brand">
          <Logo size={52} />
          <span class="setup-brand-text">
            <span class="setup-brand-name">Ambilight</span>
            {p.backend.platform.appVersion() && <span class="setup-brand-version ui-num">v{p.backend.platform.appVersion()}</span>}
          </span>
        </div>
        <ol class="setup-steps" aria-label="Setup steps">
          <li class={stepNo === 1 ? 'current' : ''} aria-current={stepNo === 1 ? 'step' : undefined}><span class="ui-num">1</span>Your lights</li>
          <li class={stepNo === 2 ? 'current' : ''} aria-current={stepNo === 2 ? 'step' : undefined}><span class="ui-num">2</span>Connect and check</li>
        </ol>
      </header>

      {stepNo === 1 ? (
        <section class="setup-body">
          <div class="setup-titles">
            <h1>Set up Ambilight</h1>
            <div class="setup-step-title"><span class="ui-num">Step 1 of 2</span><h2>What lights do you have?</h2></div>
            <p>The TV sends the colors from the edges of the picture to them. You can change this later in Outputs.</p>
          </div>

          <div class="setup-options" role="radiogroup" aria-label="Your lights">
            <OptionCard checked={choice === 'esp'} focused={f('opt-esp')} badge="Recommended"
              art={<StripArt />} title="LED strip with an ESP32"
              text="A WS2811 strip behind the TV. The controller is found automatically."
              foot={found
                ? <span class="setup-found"><StatusDot tone="ok" />{found} found on this network</span>
                : <span class="setup-foot">Searching the network…</span>} />
            <OptionCard checked={choice === 'hyperhdr'} focused={f('opt-hyperhdr')}
              art={<ServerArt />} title="HyperHDR server"
              text="WLED, Philips Hue and other lights, through your HyperHDR."
              foot={<span class="setup-foot">You type its address · port 19400</span>} />
            <OptionCard checked={choice === 'both'} focused={f('opt-both')}
              art={<BothArt />} title="Both"
              text="The strip behind the TV and the HyperHDR lights, at the same time."
              foot={<span class="setup-foot">Each one has its own switch later</span>} />
          </div>

          {error && <p class="setup-error" role="alert">{error}</p>}
          <div class="setup-actions">
            <p>{busy ? 'Saving…' : 'Not sure? Pick the strip: the TV is already looking for controllers, and nothing is sent until you finish.'}</p>
            <Button focused={f('skip')} tabIndex={-1}>Skip for now</Button>
          </div>
        </section>
      ) : (
        <section class="setup-body">
          <div class="setup-titles">
            <h1>Set up Ambilight</h1>
            <div class="setup-step-title"><span class="ui-num">Step 2 of 2</span><h2>Where is your HyperHDR?</h2></div>
            <p>
              The address of the computer HyperHDR runs on, and the port of its Flatbuffers server.
              {choice === 'both' ? ' The strip keeps working next to it.' : ''}
            </p>
          </div>

          <div class="setup-form">
            <Field id="f-host" label="Host" hint="IP or name of the server, e.g. 192.168.1.20"
              value={shown.host} focused={f('f-host')} editing={editing?.id === 'host'}
              onInput={(v) => setDraft({ ...shown, host: v })} onEnd={endEdit} />
            <Field id="f-port" label="Port" hint="Flatbuffers server · default 19400" numeric
              value={shown.port} focused={f('f-port')} editing={editing?.id === 'port'}
              onInput={(v) => setDraft({ ...shown, port: v })} onEnd={endEdit} />

            <div class="out-conn" role="status">
              <StatusDot tone={applied ? conn.tone : 'off'} />
              <span class="out-conn-text">
                <span class={`out-conn-title tone-${applied ? conn.tone : 'off'}`}>{applied ? conn.title : 'Not connected yet'}</span>
                <span class="out-card-sub">{applied ? conn.detail : 'Connect sends the address to the TV service and checks that HyperHDR answers.'}</span>
              </span>
            </div>
            {error && <p class="setup-error" role="alert">{error}</p>}

            <div class="setup-form-actions">
              <Button variant="primary" focused={f('connect')} tabIndex={-1}>
                {busy ? 'Connecting…' : applied && !error ? 'Done' : 'Connect'}
              </Button>
              <Button focused={f('back')} tabIndex={-1}>Back</Button>
            </div>
          </div>
        </section>
      )}

      <Footer>
        {editing ? (
          <>
            <KeyHint k="OK">Done</KeyHint>
            <KeyHint k="↩">Cancel</KeyHint>
          </>
        ) : stepNo === 1 ? (
          <>
            <KeyHint k="◀▶">Choose</KeyHint>
            <KeyHint k="OK">Continue</KeyHint>
            <KeyHint k="▼">Skip for now</KeyHint>
            <KeyHint k="↩">{p.inline ? 'Back to Outputs' : 'Back to TV'}</KeyHint>
          </>
        ) : (
          <>
            <KeyHint k="OK">{current.startsWith('f-') ? 'Edit' : 'Select'}</KeyHint>
            <KeyHint k="▲▼">Move</KeyHint>
            <KeyHint k="↩">Your lights</KeyHint>
          </>
        )}
        <span class="footer-note">Shown once · run it again from Outputs</span>
      </Footer>
    </div>
  );
}

function OptionCard(p: { checked: boolean; focused: boolean; badge?: string; art: ComponentChildren; title: string; text: string; foot: ComponentChildren }) {
  return (
    <div class={`setup-option${p.checked ? ' checked' : ''}${p.focused ? ' focused' : ''}`} role="radio" aria-checked={p.checked}>
      <span class="setup-option-top">
        <span class="setup-radio" aria-hidden="true" />
        {p.badge && <span class="setup-badge">{p.badge}</span>}
      </span>
      <span class="setup-art">{p.art}</span>
      <span class="setup-option-text">
        <span class="setup-option-title">{p.title}</span>
        <span class="setup-option-desc">{p.text}</span>
      </span>
      {p.foot}
    </div>
  );
}

// ---------------------------------------------------------------- illustrations (from the design)

const Rainbow = ({ id }: { id: string }) => (
  <defs>
    <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#FF6B6B" /><stop offset="0.35" stop-color="#F2B544" />
      <stop offset="0.65" stop-color="#3DD68C" /><stop offset="1" stop-color="#4DA3FF" />
    </linearGradient>
  </defs>
);

const StripArt = () => (
  <svg width="260" height="160" viewBox="0 0 260 160" aria-hidden="true">
    <Rainbow id="setup-g1" />
    <rect x="38" y="16" width="168" height="108" rx="14" fill="none" stroke="url(#setup-g1)" stroke-width="5" stroke-linecap="round" stroke-dasharray="0.5 11" />
    <rect x="52" y="28" width="140" height="84" rx="6" fill="#05070A" stroke="#2A3340" stroke-width="2" />
    <path d="M206 110 C 222 120 222 132 210 138" fill="none" stroke="#4A5566" stroke-width="2" />
    <rect x="186" y="134" width="44" height="22" rx="4" fill="#141A23" stroke="#3DD68C" stroke-width="2" />
    <text x="208" y="149" text-anchor="middle" font-family="monospace" font-size="11" font-weight="600" fill="#6FE3A8">ESP32</text>
  </svg>
);

const ServerArt = () => (
  <svg width="260" height="160" viewBox="0 0 260 160" aria-hidden="true">
    <rect x="10" y="52" width="76" height="48" rx="5" fill="#05070A" stroke="#2A3340" stroke-width="2" />
    <path d="M92 76h26" stroke="#4A5566" stroke-width="2" stroke-linecap="round" />
    <path d="M112 70l6 6-6 6" fill="none" stroke="#4A5566" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
    <rect x="124" y="52" width="48" height="14" rx="3" fill="#141A23" stroke="#8C97A8" stroke-width="2" />
    <rect x="124" y="69" width="48" height="14" rx="3" fill="#141A23" stroke="#8C97A8" stroke-width="2" />
    <rect x="124" y="86" width="48" height="14" rx="3" fill="#141A23" stroke="#8C97A8" stroke-width="2" />
    <circle cx="132" cy="59" r="2" fill="#3DD68C" />
    <path d="M176 68 L206 40" stroke="#4A5566" stroke-width="2" stroke-linecap="round" />
    <path d="M176 84 L206 116" stroke="#4A5566" stroke-width="2" stroke-linecap="round" />
    <circle cx="224" cy="34" r="16" fill="rgba(242,181,68,0.18)" stroke="#F2B544" stroke-width="2" />
    <path d="M218 54h12M220 59h8" stroke="#F2B544" stroke-width="2" stroke-linecap="round" />
    <rect x="206" y="112" width="44" height="10" rx="5" fill="none" stroke="#B48CFF" stroke-width="2" />
    <circle cx="214" cy="117" r="2" fill="#FF6B6B" /><circle cx="222" cy="117" r="2" fill="#F2B544" />
    <circle cx="230" cy="117" r="2" fill="#3DD68C" /><circle cx="238" cy="117" r="2" fill="#4DA3FF" />
  </svg>
);

const BothArt = () => (
  <svg width="260" height="160" viewBox="0 0 260 160" aria-hidden="true">
    <Rainbow id="setup-g3" />
    <rect x="8" y="34" width="124" height="84" rx="12" fill="none" stroke="url(#setup-g3)" stroke-width="5" stroke-linecap="round" stroke-dasharray="0.5 11" />
    <rect x="20" y="45" width="100" height="62" rx="5" fill="#05070A" stroke="#2A3340" stroke-width="2" />
    <path d="M138 76h22" stroke="#4A5566" stroke-width="2" stroke-linecap="round" />
    <path d="M154 70l6 6-6 6" fill="none" stroke="#4A5566" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
    <rect x="166" y="60" width="38" height="12" rx="3" fill="#141A23" stroke="#8C97A8" stroke-width="2" />
    <rect x="166" y="76" width="38" height="12" rx="3" fill="#141A23" stroke="#8C97A8" stroke-width="2" />
    <path d="M206 64 L222 44" stroke="#4A5566" stroke-width="2" stroke-linecap="round" />
    <path d="M206 84 L222 106" stroke="#4A5566" stroke-width="2" stroke-linecap="round" />
    <circle cx="236" cy="34" r="13" fill="rgba(242,181,68,0.18)" stroke="#F2B544" stroke-width="2" />
    <path d="M231 51h10" stroke="#F2B544" stroke-width="2" stroke-linecap="round" />
    <rect x="218" y="104" width="36" height="10" rx="5" fill="none" stroke="#B48CFF" stroke-width="2" />
  </svg>
);
