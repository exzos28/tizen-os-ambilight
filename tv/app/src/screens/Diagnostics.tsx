// Diagnostics: whether this TV supports hardware capture and what its video processor reports,
// what the outputs (controllers, HyperHDR) report, the settings warnings, and a QR code that
// opens a pre-filled TV compatibility report on GitHub. ▲▼ move between the sections and
// scroll a section that is taller than the screen; ◀ goes to the rail.
//
// No event log here: the service only POSTs its events to tools/server.py (ARCHITECTURE.md,
// Event log), the TV keeps none.

import { Card, CheckIcon, ErrorIcon, KeyHint, PulseIcon, StatusLine, StatusList, WarnIcon, type Tone } from '@ambilight/ui';
import type { CaptureStatus, HwCondition, ServiceStatus } from '@ambilight/protocol';
import type { ComponentChildren } from 'preact';
import { useEffect, useMemo, useRef, useState } from 'preact/hooks';
import type { Controller } from '../controllers';
import type { ServiceLink } from '../data';
import { qrEncode } from '../qr';
import { allReadsFail, controllerRow, controllerTitle, fps, hyperHdrRow, stalled } from '../status';
import { Footer } from '../components/Footer';
import type { ScreenProps } from './types';
import './diagnostics.css';

const REPO = 'https://github.com/exzos28/tizen-os-ambilight';
/** The library Capture.cs loads (tv/service/Capture.cs LibraryName). */
const LIBRARY = 'libvideoenhance.so';
/** ▲▼ inside a section taller than the visible area. */
const SCROLL_STEP = 240;

interface Verdict { tone: Tone; title: string; text: string }

/** Same reading of the status as Home's banner and pill (status.ts). */
function verdict(link: ServiceLink): Verdict {
  if (link.kind === 'checking') return { tone: 'off', title: 'Checking this TV…', text: 'Asking the Ambilight service' };
  if (link.kind === 'down') {
    return { tone: 'off', title: 'Can’t check this TV', text: link.reason === 'unavailable' ? 'This TV offers no message ports to reach the service' : 'The Ambilight service is not running. Start it on Home.' };
  }
  const c = link.status.capture;
  if (c.state === 'failed') {
    if (c.failure === 'library' || c.failure === 'symbols') {
      return { tone: 'bad', title: 'This TV does not expose the capture API', text: 'The strip cannot follow the picture here. A static color still works.' };
    }
    if (c.failure === 'hardware') {
      return { tone: 'bad', title: 'The capture hardware is not ready', text: `The API is there, but the video processor reported an unusable state.${retry(c)}` };
    }
    return { tone: 'bad', title: 'Capture failed to start', text: `An unexpected error.${retry(c)}` };
  }
  if (stalled(c)) return { tone: 'bad', title: 'Capture stalled', text: `No new frames for ${Math.round((c.lastFrameAgoMs ?? 0) / 1000)} s` };
  if (allReadsFail(c)) return { tone: 'bad', title: 'Capture does not work on this TV', text: `Every read of the screen colors fails (ret=${c.lastBadRet})` };
  if (c.state === 'running') {
    return c.badReads
      ? { tone: 'warn', title: 'This TV is supported', text: `Hardware capture works, but ${c.badReads} of ${c.reads} reads per frame fail` }
      : { tone: 'ok', title: 'This TV is supported', text: 'Hardware screen capture works' };
  }
  if (c.state === 'paused') {
    return c.frames > 0 && c.hw?.ret === 0
      ? { tone: 'ok', title: 'This TV is supported', text: 'Capture worked; it is paused while the lighting is off' }
      : { tone: 'off', title: 'Capture is paused', text: 'The lighting is off' };
  }
  return { tone: 'off', title: 'Checking the hardware…', text: 'Capture is starting' };
}

const retry = (c: CaptureStatus) => (c.retryInMs != null ? ` Trying again in ${Math.ceil(c.retryInMs / 1000)} s.` : '');

/** The issue form's `result` option, when the status tells; otherwise the reporter picks it. */
function result(link: ServiceLink): 'Works' | 'Partially works' | "Doesn't work" | null {
  if (link.kind !== 'up') return null;
  const c = link.status.capture;
  if (c.state === 'failed' || stalled(c) || allReadsFail(c)) return "Doesn't work";
  if (c.state === 'running' || (c.state === 'paused' && c.frames > 0 && c.reads != null)) return c.badReads ? 'Partially works' : 'Works';
  return null;
}

/** The service's HwCondition event line (same format, tv/service/Capture.cs). */
const hwLine = (h: HwCondition) =>
  `HwCondition ret=${h.ret} blocks=${h.blocks} blockSize=${h.blockW}x${h.blockH} full=${h.fullW}x${h.fullH} delay=${h.hwDelayMs}ms${h.delayMs !== h.hwDelayMs ? ` used=${h.delayMs}ms` : ''}`;

/**
 * The `log` field: the HwCondition line and the capture counters of the moment. No frame or
 * attempt count: they change every second, and the QR code would change with them.
 */
function reportLog(app: string | null, status: ServiceStatus | null): string {
  const lines = [`TV app ${app ?? '?'}, service ${status?.version ?? '?'} (Diagnostics screen)`];
  const c = status?.capture;
  if (c?.hw) lines.push(hwLine(c.hw));
  if (c?.state === 'failed') {
    lines.push(`CaptureError failure=${c.failure ?? 'exception'} ${(c.error ?? '').slice(0, 160)}`);
  } else if (c) {
    lines.push(`Capture ${c.state}${c.frameMs != null ? ` frameMs=${c.frameMs} reads=${c.reads} badReads=${c.badReads} lastBadRet=${c.lastBadRet}` : ''}`);
  }
  return lines.join('\n');
}

function reportUrl(model: string | null, res: string | null, log: string): string {
  // GitHub fills issue form fields from query parameters named after their ids.
  const q = new URLSearchParams({ template: 'tv-compatibility.yml' });
  if (model) {
    q.set('title', `TV report: ${model}`);
    q.set('model', model);
  }
  if (res) q.set('result', res);
  q.set('log', log);
  return `${REPO}/issues/new?${q}`;
}

// ---------------------------------------------------------------- formatting

const kb = (bytes: number) => `${Math.round(bytes / 1024)} KB`;

function duration(s: number): string {
  const d = Math.floor(s / 86400), h = Math.floor((s % 86400) / 3600), m = Math.floor((s % 3600) / 60);
  return d ? `${d} d ${h} h` : h ? `${h} h ${m} min` : `${m} min ${s % 60} s`;
}

function since(ms: number): string {
  if (ms < 0) return 'none yet';
  return ms < 1000 ? `${ms} ms ago` : `${Math.round(ms / 1000)} s ago`;
}

// ---------------------------------------------------------------- view

type Section = 'tv' | 'settings' | 'outputs' | 'report';
/** ▲▼ order: the left column top to bottom, then the right one. */
const SECTIONS: Section[] = ['tv', 'settings', 'outputs', 'report'];

export function Diagnostics(p: ScreenProps) {
  const { link, controllers, backend } = p;
  const status = link.kind === 'up' ? link.status : null;
  const c = status?.capture;
  const hw = c?.hw;
  const model = backend.platform.modelName();
  const app = backend.platform.appVersion();

  // The status before this one: controllerRow tells "sending" from a growing framesSent.
  const hist = useRef<{ cur: ServiceStatus | null; prev: ServiceStatus | null }>({ cur: null, prev: null });
  if (hist.current.cur !== status) hist.current = { cur: status, prev: hist.current.cur };

  // ---------------------------------------------------------------- remote
  const [focus, setFocus] = useState<Section>('tv');
  const body = useRef<HTMLDivElement>(null);
  const el = (s: Section) => body.current?.querySelector<HTMLElement>(`[data-section="${s}"]`) ?? null;

  useEffect(() => {
    const b = body.current, e = el(focus);
    if (!b || !e) return;
    const top = e.offsetTop, bottom = top + e.offsetHeight;
    if (top < b.scrollTop) b.scrollTop = top;
    else if (bottom > b.scrollTop + b.clientHeight) b.scrollTop = Math.min(top, bottom - b.clientHeight);
  }, [focus]);

  p.bindKeys((key) => {
    if (key === 'left') return 'rail';
    if (key !== 'up' && key !== 'down') return key === 'back' ? false : true;
    const b = body.current, e = el(focus);
    // A section taller than the visible area scrolls first.
    if (b && e) {
      if (key === 'down' && e.offsetTop + e.offsetHeight > b.scrollTop + b.clientHeight + 1) { b.scrollTop += SCROLL_STEP; return true; }
      if (key === 'up' && e.offsetTop < b.scrollTop - 1) { b.scrollTop -= SCROLL_STEP; return true; }
    }
    const i = SECTIONS.indexOf(focus) + (key === 'down' ? 1 : -1);
    if (i >= 0 && i < SECTIONS.length) setFocus(SECTIONS[i]);
    return true;
  });
  const f = (s: Section) => p.active && focus === s;

  // ---------------------------------------------------------------- content
  const v = verdict(link);
  const res = result(link);
  // A new report (and QR code) only when the verdict or the hardware changes, not with every
  // frame time: the code has to hold still while a phone reads it.
  const stable = [model, app, status?.version, c?.state, c?.failure, c?.error, hw && hwLine(hw), res].join('|');
  const qr = useMemo(() => qrEncode(reportUrl(model, res, reportLog(app, status))), [stable]);

  const api = !c ? '—'
    : c.failure === 'library' ? `${LIBRARY} · not loaded`
      : c.failure === 'symbols' ? `${LIBRARY} · functions missing`
        : hw ? `${LIBRARY} · loaded` : LIBRARY;
  const reads = c?.reads != null
    ? `${c.badReads ?? 0} of ${c.reads} per frame${c.badReads ? ` · last ret ${c.lastBadRet}` : ''}`
    : '—';

  return (
    <main class="main diag">
      <header class="header">
        <div class="header-titles">
          <h1>Diagnostics</h1>
          <p class="subtitle">What this TV’s hardware reports, and what the outputs report back</p>
        </div>
      </header>

      <div class="diag-body" ref={body}>
        <div class="diag-col">
          <div data-section="tv">
            <Card label="This TV" focused={f('tv')}>
              <div class={`diag-verdict ui-tone-${v.tone}`}>
                <span class="diag-verdict-icon">{v.tone === 'ok' ? <CheckIcon size={26} /> : v.tone === 'off' ? <PulseIcon size={26} /> : v.tone === 'warn' ? <WarnIcon size={26} /> : <ErrorIcon size={26} />}</span>
                <span class="diag-verdict-text"><h2>{v.title}</h2><span>{v.text}</span></span>
              </div>
              <Facts rows={[
                ['Model', model ?? '—'],
                ['TV app', app ?? '—'],
                ['Service', status ? status.version ?? 'unknown' : '—'],
                ['Capture API', api],
                ['Hardware reply', hw ? <span class={hw.ret ? 'diag-bad' : ''}>ret {hw.ret}</span> : '—'],
                ['Screen', hw ? `${hw.fullW}×${hw.fullH}` : '—'],
                ['Measuring', hw ? `${hw.blocks} slots · ${hw.blockW}×${hw.blockH} px · ${hw.delayMs} ms${hw.delayMs !== hw.hwDelayMs ? ` (asks ${hw.hwDelayMs} ms)` : ''}` : '—'],
                ['Frame time', c?.frameMs != null ? `${c.frameMs} ms · ${fps(c)} fps · ${c.frames} frames` : '—'],
                ['Failed reads', <span class={c?.badReads ? 'diag-warn' : ''}>{reads}</span>],
                ...(c?.state === 'failed' ? [['Attempts', `${c.failedAttempts}${c.retryInMs != null ? ` · next in ${Math.ceil(c.retryInMs / 1000)} s` : ''}`] as Fact] : []),
                ...(c?.error ? [['Error', <span class="diag-bad diag-wrap">{c.error}</span>] as Fact] : []),
              ]} />
            </Card>
          </div>

          <div data-section="settings">
            <Card title="Output settings" focused={f('settings')}
              aside={status && <span class="diag-muted">{status.outputs.source === 'saved' ? 'Saved on the TV' : 'res/ambilight.json'}</span>}>
              {!status ? <p class="diag-muted">—</p>
                : status.configWarnings.length
                  ? <ul class="diag-warnings">{status.configWarnings.map((w) => <li key={w}>{w}</li>)}</ul>
                  : <p class="diag-note">No problems in the settings in effect.</p>}
            </Card>
          </div>
        </div>

        <div class="diag-col">
          <div data-section="outputs">
            <Card title="Outputs" focused={f('outputs')}>
              <Outputs link={link} controllers={controllers} prev={hist.current.prev} />
            </Card>
          </div>

          <div data-section="report">
            <Card label="Report this TV" focused={f('report')} class="diag-report">
              {qr ? <QrCode modules={qr} /> : null}
              <div class="diag-report-text">
                <h2>Report this TV</h2>
                {qr
                  ? <p>Scan with a phone: a GitHub report opens with the model{res ? ', the result' : ''} and the values on this screen filled in. It helps other owners of this model.</p>
                  : <p>Open github.com/exzos28/tizen-os-ambilight/issues, choose “TV compatibility report” and paste the values on this screen.</p>}
                <span class="diag-url">github.com/exzos28/tizen-os-ambilight</span>
              </div>
            </Card>
          </div>
        </div>
      </div>

      <Footer>
        <KeyHint k="▲▼">Sections, scroll</KeyHint>
        <KeyHint k="◀">Menu</KeyHint>
        <KeyHint k="↩">Menu</KeyHint>
        <span class="footer-note">Updates every second · the event log is in tools/server.py</span>
      </Footer>
    </main>
  );
}

type Fact = [string, ComponentChildren];

function Facts({ rows }: { rows: Fact[] }) {
  return (
    <dl class="diag-facts">
      {rows.map(([k, val]) => [<dt key={`${k}-t`}>{k}</dt>, <dd key={`${k}-d`}>{val}</dd>])}
    </dl>
  );
}

function Outputs({ link, controllers, prev }: { link: ServiceLink; controllers: Controller[]; prev: ServiceStatus | null }) {
  const status = link.kind === 'up' ? link.status : null;
  const esp = status?.esp32;
  // As on Home: the listed hosts; while none is listed, the ones lost in this session.
  const listed = !esp?.enabled ? [] : controllers.filter((x) => x.present === esp.hosts.length > 0);
  const hh = status?.hyperhdr;
  const hrow = hyperHdrRow(link);

  return (
    <StatusList label="Outputs">
      {!esp ? <StatusLine tone="off" title="LED controller" detail="—" value="—" />
        : !esp.enabled ? <StatusLine tone="off" title="LED controllers" detail="Turned off in the output settings" value="Off" />
          : !listed.length ? <StatusLine tone="warn" title="LED controller" detail="Searching the network" value="Not found" /> : null}
      {listed.map((x, i) => {
        const row = controllerRow(x, controllerTitle(i, listed.length), status, prev);
        const m = x.metrics;
        return (
          <div key={x.host} class="diag-output">
            <StatusLine tone={row.tone} title={row.title} detail={row.detail} value={row.value} />
            <Facts rows={[
              ['Firmware', x.status ? `${x.status.version} · ${x.status.mode === 'AP' ? 'setup hotspot' : `Wi-Fi ${x.status.ssid} ${x.status.rssi} dBm`}` : '—'],
              ['Uptime', m ? duration(m.uptime_s) : '—'],
              ['Heap', m ? `${kb(m.heap_free)} free · lowest ${kb(m.heap_min)} · of ${kb(m.heap_total)}` : '—'],
              ['Frames received', m ? `${m.frames_rx} · last ${since(m.last_frame_ms)}` : '—'],
              ...(x.error ? [['Last error', <span class="diag-bad diag-wrap">{x.error}</span>] as Fact] : []),
            ]} />
          </div>
        );
      })}
      {esp?.enabled && (
        <div class="diag-output">
          <Facts rows={[
            ['TV sent', `${esp.framesSent} frames · ${esp.sendErrors} failed`],
            ...(esp.lastError ? [['Send error', <span class="diag-bad diag-wrap">{esp.lastError}</span>] as Fact] : []),
          ]} />
        </div>
      )}
      <div class="diag-output">
        <StatusLine tone={hrow.tone} title={hrow.title} detail={hrow.detail} value={hrow.value} />
        {hh?.enabled && (
          <Facts rows={[
            ['Connection', hh.connected ? 'open' : 'closed'],
            ['Replies', String(hh.replies)],
            ...(hh.lastError ? [['Last error', <span class="diag-bad diag-wrap">{hh.lastError}</span>] as Fact] : []),
          ]} />
        )}
      </div>
    </StatusList>
  );
}

/** Dark modules as one SVG path (horizontal runs), 4 modules of quiet zone. */
function QrCode({ modules }: { modules: boolean[][] }) {
  const n = modules.length + 8;
  let d = '';
  modules.forEach((row, y) => {
    for (let x = 0; x < row.length; x++) {
      if (!row[x]) continue;
      let w = 1;
      while (row[x + w]) w++;
      d += `M${x + 4} ${y + 4}h${w}v1h-${w}z`;
      x += w - 1;
    }
  });
  return (
    <svg class="diag-qr" viewBox={`0 0 ${n} ${n}`} shape-rendering="crispEdges" role="img" aria-label="QR code of the report link">
      <rect width={n} height={n} fill="#fff" />
      <path d={d} fill="#000" />
    </svg>
  );
}
