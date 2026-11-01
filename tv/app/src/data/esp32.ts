// ESP32 HTTP API (docs/NETWORKING.md). The launcher talks to each controller directly; the
// hosts come from the service's discovery (status.esp32.hosts).

import type { Esp32Api } from './api';
import type { Esp32Config, Esp32Message, Esp32Metrics, Esp32Status, LedMode, SaveLedsForm } from './types';

const TIMEOUT_MS = 3000;

async function request<T>(host: string, path: string, form?: Record<string, string | number>): Promise<T> {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), TIMEOUT_MS);
  try {
    const init: RequestInit = { signal: ctrl.signal, cache: 'no-store' };
    if (form) {
      const body = new URLSearchParams();
      for (const k in form) body.append(k, String(form[k]));
      init.method = 'POST';
      // A URLSearchParams body is sent as application/x-www-form-urlencoded.
      init.body = body;
    }
    const res = await fetch(`http://${host}${path}`, init);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return (await res.json()) as T;
  } catch (e) {
    if (ctrl.signal.aborted) throw new Error('No answer in 3 s');
    throw e;
  } finally {
    clearTimeout(timer);
  }
}

function defined(form: SaveLedsForm): Record<string, string | number> {
  const out: Record<string, string | number> = {};
  for (const [k, v] of Object.entries(form)) if (v !== undefined) out[k] = v;
  return out;
}

export const esp32Http: Esp32Api = {
  getStatus: (host) => request<Esp32Status>(host, '/status'),
  getConfig: (host) => request<Esp32Config>(host, '/config'),
  getMetrics: (host) => request<Esp32Metrics>(host, '/metrics'),
  savePower: (host, on) => request<Esp32Message>(host, '/save/power', { power: on ? 1 : 0 }),
  saveLeds: (host, form) => request<Esp32Message>(host, '/save/leds', defined(form)),
  saveMode: (host, mode: LedMode) => request<Esp32Message>(host, '/save/mode', { led_mode: mode }),
};
