// Turns the service status and the controllers into what Home shows: status rows, the
// problem banner and the header pill. Same verdicts as the old launcher's status panel.

import { type Controller, unreachable } from './controllers';
import { type CaptureStatus, type Esp32OutputStatus, type HyperHdrOutputStatus, LedMode, type ServiceStatus } from '@ambilight/protocol';
import type { ServiceLink } from './data';

export type Tone = 'ok' | 'warn' | 'bad' | 'off';

export interface Row {
  title: string;
  detail: string;
  value: string;
  tone: Tone;
}

export interface Problem {
  tone: 'warn' | 'bad';
  title: string;
  text: string;
  /** Technical detail (the service's error message), shown smaller. */
  detail?: string;
  hints?: string[];
  action?: 'start' | 'search';
  /** The problem concerns a controller: offer "Controller details". */
  controller?: boolean;
}

/** Capture "running" but no frame for this long. */
const STALL_MS = 2000;
/** A controller that got a UDP frame within this window is receiving. */
const RECEIVING_MS = 2000;

export const fps = (c: CaptureStatus) => (c.frameMs && c.frameMs > 0 ? (1000 / c.frameMs).toFixed(1) : '0');

const ago = (ms: number) => {
  const s = Math.max(0, Math.round(ms / 1000));
  if (s < 60) return `${s} s ago`;
  const m = Math.round(s / 60);
  return m < 60 ? `${m} min ago` : `${Math.round(m / 60)} h ago`;
};

const retry = (c: CaptureStatus) => (c.retryInMs != null ? `retry in ${Math.ceil(c.retryInMs / 1000)} s` : '');

export const stalled = (c: CaptureStatus) => c.state === 'running' && (c.lastFrameAgoMs ?? 0) > STALL_MS;
export const allReadsFail = (c: CaptureStatus) => c.state === 'running' && c.reads != null && c.reads > 0 && (c.badReads ?? 0) >= c.reads;

/** Capture is producing colors right now. */
export const live = (c: CaptureStatus) => c.state === 'running' && !stalled(c) && !allReadsFail(c);

export function captureRow(link: ServiceLink, compact: boolean): Row {
  const title = 'Screen capture';
  if (link.kind === 'checking') return { title, detail: 'Asking the service…', value: '—', tone: 'off' };
  if (link.kind === 'down') return { title, detail: 'The service is not running', value: '—', tone: 'off' };
  const c = link.status.capture;
  const hw = c.hw ? `${c.hw.fullW}×${c.hw.fullH}` : '';
  switch (c.state) {
    case 'waiting':
      return { title, detail: 'Waiting for the service', value: 'Starting', tone: 'off' };
    case 'starting':
      return {
        title, value: 'Starting', tone: 'warn',
        detail: c.hw ? `Hardware found (${hw}, ${c.hw.blocks} slots), waiting for the first frame` : 'Initialising…',
      };
    case 'paused':
      return { title, detail: 'Lighting is off', value: 'Paused', tone: 'off' };
    case 'failed':
      return {
        title, value: c.failure === 'hardware' ? 'Not ready' : 'Not available', tone: 'bad',
        detail: [c.failure === 'hardware' ? 'Hardware not ready' : 'Not available on this TV', retry(c)].filter(Boolean).join(' · '),
      };
    case 'running': {
      if (stalled(c)) return { title, detail: `No new frames for ${Math.round((c.lastFrameAgoMs ?? 0) / 1000)} s`, value: 'Stalled', tone: 'bad' };
      if (allReadsFail(c)) return { title, detail: `The hardware returns errors (ret=${c.lastBadRet})`, value: 'Failing', tone: 'bad' };
      const reads = c.badReads ? `${c.badReads} of ${c.reads} reads failed (ret=${c.lastBadRet})` : 'no failed reads';
      return {
        title, value: 'Working', tone: c.badReads ? 'warn' : 'ok',
        detail: [compact ? `${fps(c)} fps` : hw, reads].filter(Boolean).join(' · '),
      };
    }
  }
  return { title, detail: `Unknown state: ${String(c.state)}`, value: '—', tone: 'off' };
}

export function controllerTitle(i: number, count: number) {
  return count > 1 ? `LED controller ${i + 1}` : 'LED controller';
}

/**
 * prev: the status before `status` — UDP has no replies, so without the controller's own
 * /metrics "sending" means the service handed frames to the network.
 */
export function controllerRow(c: Controller, title: string, status: ServiceStatus | null, prev: ServiceStatus | null): Row {
  const detail = `ESP32 · ${c.host}`;
  if (!c.present) return { title, detail: `Searching · last seen ${ago(Date.now() - c.lastSeenAt)}`, value: 'Not found', tone: 'warn' };
  if (unreachable(c)) return { title, detail, value: 'Not answering', tone: 'warn' };
  if (!c.config) return { title, detail, value: 'Connecting', tone: 'off' };
  if (!c.config.power) return { title, detail, value: 'Off', tone: 'off' };
  if (c.config.led_mode === LedMode.plain) return { title, detail, value: 'Static color', tone: 'ok' };

  const capturing = status != null && live(status.capture);
  const m = c.metrics;
  if (m && typeof m.last_frame_ms === 'number') {
    if (m.last_frame_ms >= 0 && m.last_frame_ms < RECEIVING_MS) return { title, detail, value: 'Streaming', tone: 'ok' };
    return capturing ? { title, detail, value: 'No frames', tone: 'warn' } : { title, detail, value: 'Waiting', tone: 'off' };
  }
  const e: Esp32OutputStatus | undefined = status?.esp32;
  const p: Esp32OutputStatus | undefined = prev?.esp32;
  if (e?.enabled) {
    const before = p?.enabled ? p : { framesSent: 0, sendErrors: 0 };
    if (e.sendErrors > before.sendErrors) return { title, detail: `Sending fails: ${e.lastError ?? ''}`, value: 'Failing', tone: 'bad' };
    if (e.framesSent > before.framesSent) return { title, detail, value: 'Sending', tone: 'ok' };
  }
  return capturing ? { title, detail, value: 'No frames sent', tone: 'warn' } : { title, detail, value: 'Waiting', tone: 'off' };
}

export function hyperHdrRow(link: ServiceLink): Row {
  const title = 'HyperHDR';
  if (link.kind !== 'up') return { title, detail: '—', value: '—', tone: 'off' };
  const h: HyperHdrOutputStatus = link.status.hyperhdr;
  if (!h.enabled) return { title, detail: 'Not set up', value: 'Off', tone: 'off' };
  if (h.connected && h.replies > 0) return { title, detail: h.endpoint, value: 'Connected', tone: 'ok' };
  if (h.connected) return { title, detail: h.endpoint, value: 'Connecting', tone: 'warn' };
  return { title, detail: h.lastError ? `${h.endpoint} · ${h.lastError}` : h.endpoint, value: 'Not connected', tone: 'bad' };
}

const CONTROLLER_HINTS = ['Is the controller powered?', 'Same Wi-Fi as the TV?', 'Hotspot “ambilight-setup” visible on a phone?'];

/** Everything that needs the user's attention, most important first. */
export function problems(link: ServiceLink, controllers: Controller[]): Problem[] {
  if (link.kind === 'checking') return [];
  if (link.kind === 'down') {
    if (link.reason === 'unavailable') {
      return [{ tone: 'bad', title: 'The app cannot reach the Ambilight service', text: 'This TV does not offer the message ports the app uses to talk to the service.', detail: link.detail }];
    }
    return [{
      tone: 'bad', action: 'start',
      title: link.reason === 'not-running' ? 'Ambilight service is not running' : 'Ambilight service is not responding',
      text: 'The service captures the screen and sends the colors to the strip. It normally starts with the TV.',
      detail: link.detail,
    }];
  }

  const out: Problem[] = [];
  const s = link.status;
  const c = s.capture;
  if (c.state === 'failed' && (c.failure === 'library' || c.failure === 'symbols')) {
    out.push({
      tone: 'bad', title: 'This TV does not expose the capture API',
      text: 'The strip cannot follow the picture on this TV. A static color still works.',
      detail: c.error ?? undefined,
    });
  } else if (c.state === 'failed') {
    out.push({
      tone: 'bad', title: 'Capture does not work on this TV right now',
      text: `The TV's video processor reported an unusable state. The service tries again${c.retryInMs != null ? ` in ${Math.ceil(c.retryInMs / 1000)} s` : ''}.`,
      detail: c.error ?? undefined,
    });
  } else if (stalled(c)) {
    out.push({ tone: 'bad', title: 'Capture stalled', text: `No new frames for ${Math.round((c.lastFrameAgoMs ?? 0) / 1000)} s.` });
  } else if (allReadsFail(c)) {
    out.push({ tone: 'bad', title: 'Capture does not work on this TV', text: `Every read of the screen colors fails (ret=${c.lastBadRet}).` });
  }

  // The ESP32 output switched off (Outputs, or res/ambilight.json): a HyperHDR-only setup must
  // never hear about a missing controller.
  if (s.esp32.enabled && s.outputs.esp32.enabled) {
    const present = controllers.filter((x) => x.present);
    if (!s.esp32.hosts.length) {
      const lost = controllers.filter((x) => !x.present).sort((a, b) => b.lastSeenAt - a.lastSeenAt)[0];
      out.push(lost
        ? {
          tone: 'warn', action: 'search', controller: true, hints: CONTROLLER_HINTS,
          title: 'The LED controller stopped answering',
          text: `Last seen ${ago(Date.now() - lost.lastSeenAt)} at ${lost.host}. ${live(c) ? 'The screen is still captured; the colors have nowhere to go. ' : ''}The TV keeps searching every 10 s.`,
        }
        : {
          tone: 'warn', action: 'search', hints: CONTROLLER_HINTS,
          title: 'No LED controller found',
          text: 'The TV looks for controllers on the local network and keeps searching every 10 s.',
        });
    } else {
      for (const x of present) {
        if (!unreachable(x)) continue;
        out.push({
          tone: 'warn', action: 'search', controller: true,
          title: `The LED controller at ${x.host} does not answer`,
          text: x.lastOkAt
            ? `It stopped answering ${ago(Date.now() - x.lastOkAt)}. The TV still sends it the colors, but brightness and mode can't be changed.`
            : 'The TV found it on the network, but it does not answer the app. Update the controller firmware if it is older than this app.',
          detail: x.error,
        });
      }
    }
  }

  if (s.configWarnings.length) {
    out.push({
      tone: 'warn', title: 'The service settings have problems',
      text: 'res/ambilight.json in the TV package: the service uses its defaults where it can’t read a setting.',
      detail: s.configWarnings.join(' · '),
    });
  }
  return out;
}

export function summary(link: ServiceLink, list: Problem[]): { tone: Tone; text: string } {
  if (link.kind === 'checking') return { tone: 'off', text: 'Checking…' };
  if (link.kind === 'down') return { tone: 'bad', text: 'Not running' };
  const c = link.status.capture;
  if (c.state === 'failed' && (c.failure === 'library' || c.failure === 'symbols')) return { tone: 'bad', text: 'Not supported' };
  if (!list.length) return c.state === 'paused' ? { tone: 'off', text: 'Lighting off' } : { tone: 'ok', text: 'Everything works' };
  return list.some((p) => p.tone === 'bad') ? { tone: 'bad', text: 'Not working' } : { tone: 'warn', text: 'Needs attention' };
}
