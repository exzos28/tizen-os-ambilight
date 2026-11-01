// The ESP32 controllers the service discovered, as seen over their HTTP API. The launcher
// reads /status and /config once per controller (and again after a failed write), then
// /metrics on every poll while Home is shown. /config is read again when the metrics'
// config_rev changes, i.e. when another client (the controller's web app) saved a setting.

import { useCallback, useEffect, useRef, useState } from 'preact/hooks';
import { configChanged, type Esp32Config, type Esp32Metrics, type Esp32Status, type LedMode } from '@ambilight/protocol';
import type { Backend } from './data';

export interface Controller {
  host: string;
  /** In the service's current host list. False: it was there earlier in this session. */
  present: boolean;
  status?: Esp32Status;
  config?: Esp32Config;
  metrics?: Esp32Metrics;
  /** config_rev the shown config was read at; null = unknown (read it again on the next metrics). */
  configRev?: number | null;
  /** Consecutive failed requests. */
  failures: number;
  /** Last successful HTTP answer (Date.now()), 0 = never. */
  lastOkAt: number;
  /** Last time the service listed it, or it answered. */
  lastSeenAt: number;
  error?: string;
}

/** Answering for the UI: it replied before and the last two requests did not fail. */
export const unreachable = (c: Controller) => c.failures >= 2 || (c.lastOkAt === 0 && c.failures >= 1);

export interface ControllerActions {
  setPower(on: boolean): void;
  setBrightness(value: number): void;
  setMode(mode: LedMode): void;
  setColor(color: string): void;
  /**
   * Any other setting: `apply` patches the shown config of every present controller at once,
   * `send` writes it to one controller; a controller that rejects it is read again.
   */
  update(apply: (c: Esp32Config) => Partial<Esp32Config>, send: (host: string) => Promise<unknown>): void;
}

const BRIGHTNESS_DEBOUNCE_MS = 300;

/**
 * hosts: status.esp32.hosts (null while the service status is unknown).
 * active: poll /metrics (Home is shown and the page is visible).
 * tick: increments on every status poll; drives the HTTP polling.
 */
export function useControllers(backend: Backend, hosts: string[] | null, active: boolean, tick: number) {
  const [list, setList] = useState<Controller[]>([]);
  const ref = useRef(list);
  ref.current = list;
  const inFlight = useRef(new Set<string>());
  const brightnessTimer = useRef<number>(0);
  const { esp32 } = backend;

  const patch = useCallback((host: string, p: Partial<Controller> | ((c: Controller) => Partial<Controller>)) => {
    setList((cur) => cur.map((c) => (c.host === host ? { ...c, ...(typeof p === 'function' ? p(c) : p) } : c)));
  }, []);

  // Merge the service's host list: new hosts appear, missing ones are kept as "not present".
  useEffect(() => {
    if (!hosts) return;
    const now = Date.now();
    setList((cur) => {
      const next = cur.map((c) => {
        const present = hosts.includes(c.host);
        return present === c.present && !present ? c : { ...c, present, lastSeenAt: present ? now : c.lastSeenAt };
      });
      for (const h of hosts) {
        if (!next.some((c) => c.host === h)) next.push({ host: h, present: true, failures: 0, lastOkAt: 0, lastSeenAt: now });
      }
      return next;
    });
  }, [hosts ? hosts.join(',') : null]);

  const ok = (host: string, p: Partial<Controller>) =>
    patch(host, (c) => ({ ...p, failures: 0, lastOkAt: Date.now(), lastSeenAt: Date.now(), error: undefined, present: c.present }));
  const failed = (host: string, e: unknown) =>
    patch(host, (c) => ({ failures: c.failures + 1, error: e instanceof Error ? e.message : String(e) }));

  const refresh = useCallback(async (c: Controller) => {
    if (inFlight.current.has(c.host)) return;
    inFlight.current.add(c.host);
    try {
      if (!c.config || !c.status) {
        const esp = esp32(c.host);
        const [status, config] = await Promise.all([esp.getStatus(), esp.getConfig()]);
        ok(c.host, { status, config });
      } else {
        const esp = esp32(c.host);
        const metrics = await esp.getMetrics();
        // Changed elsewhere: re-read, unless a brightness change of ours is still on its way
        // (it is sent shortly and bumps the revision again; last write wins).
        if (configChanged(c.configRev ?? null, metrics) && !brightnessTimer.current) {
          ok(c.host, { metrics, config: await esp.getConfig(), configRev: metrics.config_rev });
        } else {
          ok(c.host, { metrics });
        }
      }
    } catch (e) {
      failed(c.host, e);
    } finally {
      inFlight.current.delete(c.host);
    }
  }, [esp32]);

  useEffect(() => {
    if (!active) return;
    for (const c of ref.current) if (c.present) refresh(c);
  }, [tick, active]);

  // Writes go to every controller the service currently knows; the screen shows the result
  // right away and re-reads the config of a controller that rejected it.
  const write = (apply: (c: Esp32Config) => Partial<Esp32Config>, send: (host: string) => Promise<unknown>) => {
    for (const c of ref.current) {
      if (!c.present || !c.config) continue;
      const host = c.host;
      patch(host, (cur) => (cur.config ? { config: { ...cur.config, ...apply(cur.config) } } : {}));
      send(host).catch((e) => {
        failed(host, e);
        patch(host, { config: undefined }); // re-read on the next poll
      });
    }
  };

  const actions: ControllerActions = {
    setPower: (on) => write(() => ({ power: on }), (h) => esp32(h).savePower({ power: on ? 1 : 0 })),
    setMode: (mode) => write(() => ({ led_mode: mode }), (h) => esp32(h).saveMode({ led_mode: mode })),
    setColor: (color) => write(() => ({ color }), (h) => esp32(h).saveLeds({ color })),
    update: write,
    setBrightness(value) {
      // Shown at once; sent once the remote stops repeating.
      for (const c of ref.current) {
        if (c.present && c.config) patch(c.host, (cur) => (cur.config ? { config: { ...cur.config, brightness: value } } : {}));
      }
      clearTimeout(brightnessTimer.current);
      brightnessTimer.current = window.setTimeout(() => {
        brightnessTimer.current = 0;
        write(() => ({ brightness: value }), (h) => esp32(h).saveLeds({ brightness: value }));
      }, BRIGHTNESS_DEBOUNCE_MS);
    },
  };

  return { controllers: list, actions };
}
