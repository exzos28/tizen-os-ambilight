// Outputs: where the screen colors go. Two independent outputs with their own switch: the ESP32
// controllers the service discovers (UDP) and a HyperHDR server (TCP, host / port / priority).
// Switches, "Apply and test" and the setup wizard send set-outputs; the service applies it
// live and saves it on the TV. "Run setup again" opens the wizard over this screen.

import '@ambilight/ui/tokens.css';
import { useEffect, useState } from 'preact/hooks';
import { BlinkIcon, Button, CheckIcon, ChipIcon, KeyHint, RefreshIcon, StatusDot, StatusPill, Switch } from '@ambilight/ui';
import type { OutputsStatus, ServiceOutputs } from '@ambilight/protocol';
import { Footer } from '../components/Footer';
import { type Controller, unreachable } from '../controllers';
import type { ServiceLink } from '../data';
import { EDGE_LEFT, type FocusGrid, step } from '../focus';
import { controllerRow, controllerTitle, fps } from '../status';
import {
  type EditEnd, Field, type FieldId, type HyperHdrDraft, connectionLine, draftDirty, draftOf, outputsWith, settingsOf,
  useLastReply,
} from './hyperhdr';
import { Setup } from './Setup';
import type { ScreenProps } from './types';
import './outputs.css';

/** How long "Apply and test" waits for the connection before showing an error. */
const TEST_MS = 8000;
/** "Blinking…" / "Searching…" on the buttons. */
const FEEDBACK_MS = 3000;

/** Desktop preview only: ?screen=outputs&setup=1 opens the wizard over the screen. */
const devSetup = !window.tizen && new URLSearchParams(location.search).get('setup') === '1';

const FIELDS: { id: FieldId; label: string; hint: string; numeric?: boolean }[] = [
  { id: 'host', label: 'Host', hint: 'IP or name of the server' },
  { id: 'port', label: 'Port', hint: 'Flatbuffers server · default 19400', numeric: true },
  { id: 'priority', label: 'Priority', hint: '50–250 · default 150', numeric: true },
];

type Where = 'esp' | 'hdr' | 'blink';

const OK_HINT: Record<string, string> = {
  'run-setup': 'Run setup', 'esp-switch': 'On / off', 'hdr-switch': 'On / off',
  'esp-blink': 'Blink', 'esp-search': 'Search', 'hdr-test': 'Test',
};

export function Outputs(p: ScreenProps) {
  const { link, controllers } = p;
  const status = link.kind === 'up' ? link.status : null;
  const outputs: OutputsStatus | null = status?.outputs ?? null;

  const [setupOpen, setSetupOpen] = useState(devSetup);
  const [focus, setFocus] = useState('esp-switch');
  const [editing, setEditing] = useState<{ id: FieldId; before: string } | null>(null);
  const [draft, setDraft] = useState<HyperHdrDraft | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<{ where: Where; text: string } | null>(null);
  const [testUntil, setTestUntil] = useState(0);
  const [feedback, setFeedback] = useState<{ id: string; text: string } | null>(null);
  const lastReplyAt = useLastReply(link);

  // The draft follows the settings in effect until the user changes a field.
  const saved = outputs?.hyperhdr ?? null;
  const shown: HyperHdrDraft | null = draft ?? (saved ? draftOf(saved) : null);
  const dirty = !!(shown && saved && draftDirty(shown, saved));
  useEffect(() => { if (draft && saved && !editing && !draftDirty(draft, saved)) setDraft(null); }, [draft, saved?.host, saved?.port, saved?.priority, editing]);
  useEffect(() => {
    if (!feedback) return;
    const t = setTimeout(() => setFeedback(null), FEEDBACK_MS);
    return () => clearTimeout(t);
  }, [feedback]);

  const espOn = !!outputs?.esp32.enabled;
  const hdrOn = !!outputs?.hyperhdr.enabled;
  const watching = Date.now() < testUntil;

  // ---------------------------------------------------------------- focus grid
  // Left column: the ESP32 card; right column: HyperHDR. ◀ from the left column → rail.
  const grid: FocusGrid = [
    [{ id: 'run-setup', col: 2 }],
    [{ id: 'esp-switch', col: 0 }, { id: 'hdr-switch', col: 2 }],
  ];
  if (espOn) {
    grid.push([
      { id: 'esp-blink', col: 0, nav: { down: 'esp-blink' } },
      { id: 'esp-search', col: 1, nav: { down: 'esp-search' } },
      { id: 'hdr-host', col: 2 },
    ]);
  } else {
    grid.push([{ id: 'hdr-host', col: 2, nav: { left: 'esp-switch' } }]);
  }
  const back = espOn ? 'esp-search' : 'esp-switch';
  grid.push([{ id: 'hdr-port', col: 2, nav: { left: back } }]);
  grid.push([{ id: 'hdr-priority', col: 2, nav: { left: back } }]);
  grid.push([{ id: 'hdr-test', col: 2, nav: { left: back } }]);
  const ids = grid.flat().map((it) => it.id);
  const current = ids.includes(focus) ? focus : 'esp-switch';
  useEffect(() => { if (current !== focus) setFocus(current); }, [current]);

  // ---------------------------------------------------------------- actions
  const apply = async (where: Where, label: string, next: ServiceOutputs) => {
    if (busy) return false;
    setBusy(label);
    setError(null);
    const r = await p.backend.service.setOutputs(next);
    setBusy(null);
    if (!r.ok) setError({ where, text: r.error ?? 'The service did not take the settings' });
    return r.ok;
  };

  const toggleEsp = () => {
    if (!outputs) return;
    apply('esp', 'esp-switch', outputsWith(outputs, { esp32: { enabled: !espOn } }));
  };

  // On: the form as shown (a host typed before switching on is used). Off: the settings in
  // effect, the address is kept.
  const toggleHdr = () => {
    if (!outputs || !shown) return;
    if (hdrOn) {
      apply('hdr', 'hdr-switch', outputsWith(outputs, { hyperhdr: { ...outputs.hyperhdr, enabled: false } }));
      return;
    }
    const s = settingsOf(shown, true);
    if (typeof s === 'string') { setError({ where: 'hdr', text: s }); return; }
    apply('hdr', 'hdr-switch', outputsWith(outputs, { hyperhdr: s })).then((ok) => { if (ok) setTestUntil(Date.now() + TEST_MS); });
  };

  const test = async () => {
    if (!outputs || !shown) return;
    if (hdrOn && !dirty) { setError(null); setTestUntil(Date.now() + TEST_MS); return; }
    const s = settingsOf(shown, true);
    if (typeof s === 'string') { setError({ where: 'hdr', text: s }); return; }
    if (await apply('hdr', 'hdr-test', outputsWith(outputs, { hyperhdr: s }))) setTestUntil(Date.now() + TEST_MS);
  };

  const blink = async () => {
    const targets = controllers.filter((c) => c.present && !unreachable(c));
    if (!targets.length) { setError({ where: 'blink', text: 'No controller answers right now.' }); return; }
    setError(null);
    setFeedback({ id: 'esp-blink', text: 'Blinking…' });
    const results = await Promise.allSettled(targets.map((c) => p.backend.esp32(c.host).identify()));
    const failed = results.map((r, i) => (r.status === 'rejected' ? `${targets[i].host}: ${r.reason instanceof Error ? r.reason.message : String(r.reason)}` : null)).filter(Boolean);
    if (failed.length) setError({ where: 'blink', text: failed.join(' · ') });
  };

  const search = async () => {
    setError(null);
    setFeedback({ id: 'esp-search', text: 'Searching…' });
    const r = await p.backend.service.command('rediscover');
    if (!r.ok) setError({ where: 'esp', text: r.error ?? 'The search did not start' });
  };

  const startEdit = (id: FieldId) => {
    if (!shown) return;
    if (!draft) setDraft(shown);
    setEditing({ id, before: shown[id] });
  };
  const endEdit = (how: EditEnd) => {
    if (!editing) return;
    if (how === 'cancel' && draft) setDraft({ ...draft, [editing.id]: editing.before });
    setEditing(null);
    if (how === 'up' || how === 'down') {
      const next = step(grid, `hdr-${editing.id}`, how);
      if (next && next !== EDGE_LEFT) setFocus(next);
    }
  };

  const ok = (id: string) => {
    if (id === 'run-setup') { setSetupOpen(true); return; }
    if (id === 'esp-switch') toggleEsp();
    else if (id === 'hdr-switch') toggleHdr();
    else if (id === 'esp-blink') blink();
    else if (id === 'esp-search') search();
    else if (id === 'hdr-test') test();
    else if (id.startsWith('hdr-')) startEdit(id.slice(4) as FieldId);
  };

  if (setupOpen) {
    return <Setup {...p} inline onClose={() => { setSetupOpen(false); setFocus('run-setup'); }} />;
  }

  p.bindKeys((key) => {
    // While a field is edited its input gets the keys; this only sees what it lets through.
    if (editing) return true;
    if (key === 'ok') { ok(current); return true; }
    if (key === 'back') return false;
    const next = step(grid, current, key);
    if (next === EDGE_LEFT) return 'rail';
    if (next) setFocus(next);
    return true;
  });

  const f = (id: string) => p.active && current === id;

  // ---------------------------------------------------------------- view
  const esp = status?.esp32;
  const listed = !esp?.enabled ? [] : controllers.filter((c) => c.present === esp.hosts.length > 0);
  const compact = listed.length > 1;
  const espSub = !status ? 'The Ambilight service is not running'
    : !espOn ? 'Off · nothing is sent to the controllers'
      : !listed.length ? 'Found automatically · searching the network'
        : `Found automatically · ${listed.filter((c) => c.present).length} on this network`;

  const conn = connectionLine(link, lastReplyAt, watching);
  const testLabel = busy === 'hdr-test' ? 'Applying…' : !hdrOn ? 'Turn on and test' : dirty ? 'Apply and test' : 'Test connection';

  return (
    <main class="main outputs">
      <header class="header">
        <div class="header-titles">
          <h1>Outputs</h1>
          <p class="subtitle">Where the screen colors go · both can run at the same time</p>
        </div>
        <div class="out-header-aside">
          <span class="out-chip"><span class="out-chip-check"><CheckIcon size={20} /></span>Changes apply at once, no reinstall</span>
          <Button focused={f('run-setup')} icon={<RefreshIcon size={22} />} tabIndex={-1}>Run setup again</Button>
        </div>
      </header>

      <div class="out-cards">
        <section class="out-card" aria-labelledby="out-esp">
          <div class="out-card-head">
            <span class="out-icon"><ChipIcon size={34} /></span>
            <div class="out-card-titles">
              <h2 id="out-esp">LED controllers (ESP32)</h2>
              <span class="out-card-sub">{espSub}</span>
            </div>
            <div class={`out-switch${f('esp-switch') ? ' focused' : ''}`} role="switch" aria-checked={espOn} aria-labelledby="out-esp">
              <Switch on={espOn} accent />
            </div>
          </div>
          {error?.where === 'esp' && <p class="out-error" role="alert">{error.text}</p>}

          {espOn ? (
            <>
              <ul class="out-controllers" aria-label="Found controllers">
                {listed.length ? listed.slice(0, 4).map((c, i) => (
                  <ControllerItem key={c.host} c={c} title={controllerTitle(i, listed.length)} compact={compact} link={link} />
                )) : (
                  <li class="out-controller out-empty">
                    <span class="out-empty-title">No controller found yet</span>
                    <span class="out-card-sub">The TV keeps searching every 10 s.</span>
                  </li>
                )}
              </ul>
              <div class="out-buttons">
                <Button block focused={f('esp-blink')} icon={<BlinkIcon size={24} />} tabIndex={-1}>
                  {feedback?.id === 'esp-blink' ? feedback.text : 'Blink to identify'}
                </Button>
                <Button block focused={f('esp-search')} icon={<RefreshIcon size={24} />} tabIndex={-1}>
                  {feedback?.id === 'esp-search' ? feedback.text : 'Search again'}
                </Button>
              </div>
              {error?.where === 'blink' && <p class="out-error" role="alert">{error.text}</p>}
            </>
          ) : (
            <div class="out-off">
              <span class="out-empty-title">Switched off</span>
              <span class="out-card-sub">The TV sends no colors to the controllers. Switch it on to use the strip behind the TV.</span>
            </div>
          )}

          <div class="out-help">
            <h3>A controller is missing?</h3>
            <p>It must be on the same Wi-Fi as the TV. A new one opens the hotspot “ambilight-setup” for a phone. New controllers appear within 30 s.</p>
          </div>
          <p class="out-note">Switched off, nothing goes to the controllers and Home never reports a missing one.</p>
        </section>

        <section class="out-card" aria-labelledby="out-hdr">
          <div class="out-card-head">
            <span class="out-icon"><ServerIcon /></span>
            <div class="out-card-titles">
              <h2 id="out-hdr">HyperHDR</h2>
              <span class="out-card-sub">WLED, Philips Hue and other lights through a server</span>
            </div>
            <div class={`out-switch${f('hdr-switch') ? ' focused' : ''}`} role="switch" aria-checked={hdrOn} aria-labelledby="out-hdr">
              <Switch on={hdrOn} accent />
            </div>
          </div>

          <div class="out-fields">
            {FIELDS.map((it) => (
              <Field
                key={it.id}
                id={`hdr-${it.id}`}
                label={it.label}
                hint={it.hint}
                numeric={it.numeric}
                value={shown?.[it.id] ?? ''}
                pending={!!(shown && saved && shown[it.id].trim() !== draftOf(saved)[it.id])}
                focused={f(`hdr-${it.id}`)}
                editing={editing?.id === it.id}
                onInput={(v) => setDraft({ ...(draft ?? shown!), [it.id]: v })}
                onEnd={endEdit}
              />
            ))}
          </div>

          <div class="out-conn" role="status">
            <StatusDot tone={conn.tone} />
            <span class="out-conn-text">
              <span class={`out-conn-title tone-${conn.tone}`}>{conn.title}</span>
              <span class="out-card-sub">{conn.detail}</span>
            </span>
            <Button focused={f('hdr-test')} icon={<ArrowIcon />} tabIndex={-1}>{testLabel}</Button>
          </div>
          {error?.where === 'hdr' && <p class="out-error" role="alert">{error.text}</p>}

          <p class="out-note bottom">Switched off, the address is kept and nothing is sent to the server. A lower priority number wins over other HyperHDR sources.</p>
        </section>
      </div>

      <Footer>
        {editing ? (
          <>
            <KeyHint k="OK">Done</KeyHint>
            <KeyHint k="↩">Cancel</KeyHint>
            <KeyHint k="▲▼">Done, next field</KeyHint>
          </>
        ) : (
          <>
            <KeyHint k="OK">{OK_HINT[current] ?? 'Edit'}</KeyHint>
            <KeyHint k="▲▼">Move</KeyHint>
            <KeyHint k="◀▶">Other output</KeyHint>
            <KeyHint k="↩">Back</KeyHint>
          </>
        )}
        <span class="footer-note">Saved on the TV · used right away</span>
      </Footer>
    </main>
  );
}

function ControllerItem({ c, title, compact, link }: { c: Controller; title: string; compact: boolean; link: ServiceLink }) {
  const status = link.kind === 'up' ? link.status : null;
  const row = controllerRow(c, title, status, null);
  const value = row.value === 'Streaming' && status ? `Streaming · ${Math.round(+fps(status.capture))} fps` : row.value;
  const kind = c.config ? `ESP32 · ${c.config.num_leds} LEDs` : 'ESP32';
  const rssi = c.status?.connected ? c.status.rssi : null;
  return (
    <li class={`out-controller${compact ? ' compact' : ''}`}>
      <div class="out-controller-head">
        <div class="out-card-titles">
          <span class="out-controller-name">{title}</span>
          <span class="out-card-sub">{compact ? `${kind} · ${c.host}` : kind}</span>
        </div>
        <StatusPill tone={row.tone}>{value}</StatusPill>
      </div>
      {!compact && (
        <dl class="out-facts">
          <div><dt>Address</dt><dd class="ui-num">{c.host}</dd></div>
          <div><dt>Wi-Fi</dt><dd class="ui-num">{rssi == null ? '—' : <>{`${rssi} dBm`.replace('-', '−')}<Bars rssi={rssi} /></>}</dd></div>
          <div><dt>Firmware</dt><dd class="ui-num">{c.status?.version ? `v${c.status.version}` : '—'}</dd></div>
        </dl>
      )}
    </li>
  );
}

const SIGNAL = ['Weak signal', 'Fair signal', 'Good signal', 'Excellent signal'];

function Bars({ rssi }: { rssi: number }) {
  const n = rssi >= -50 ? 4 : rssi >= -60 ? 3 : rssi >= -70 ? 2 : 1;
  return (
    <span class="out-bars" role="img" aria-label={SIGNAL[n - 1]}>
      {[5, 9, 13, 18].map((h, i) => <span key={h} class={i < n ? 'on' : ''} style={`height:${h}px`} />)}
    </span>
  );
}

const ServerIcon = () => (
  <svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <rect x="3" y="3" width="18" height="6" rx="1.5" /><rect x="3" y="12" width="18" height="6" rx="1.5" /><path d="M7 6h.01M7 15h.01M12 18v3M8 21h8" />
  </svg>
);

const ArrowIcon = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <path d="M5 12h14" /><polyline points="13 6 19 12 13 18" />
  </svg>
);
