// In-memory backend for a desktop browser (window.tizen is undefined there). Values like the
// design mockup: a QE50QN92D, 3840×2160, 2 slots, 20 ms, ~7.1 fps, one ESP32 with 32 LEDs.
//
// ?state=ok           everything works (default)
// ?state=lost         the controller answers for 2 s, then disappears ("Search now" brings it back)
// ?state=none         no controller found at all
// ?state=off          the service is not running ("Start" starts it)
// ?state=unsupported  the TV has no capture API (libvideoenhance.so missing)

import {
  type CaptureStatus, type Esp32Config, type Esp32Metrics, type Esp32Status, LedMode, type ServiceCommandResult,
  type ServiceStatus, limits,
} from '@ambilight/protocol';
import type { Backend, ServiceLink } from './api';

export type MockState = 'ok' | 'lost' | 'none' | 'off' | 'unsupported';

const HOST = '192.168.50.245';
const FRAME_MS = 140;

// A sunset scene, EdgeFrame order: top L→R (4), right T→B (3), bottom R→L (4), left B→T (3).
const SCENE = [
  'F6B04A', 'F28C3B', 'E8663A', 'C94D4A',
  '9B4A63', '4A4E7A', '21406B',
  '1C3A63', '245A7A', '2E7186', '2A5E7F',
  '3B4F7A', '8E5A6E', 'E39A52',
];

function drift(hex: string, t: number, i: number): string {
  const k = Math.sin(t / 1500 + i) * 18;
  let out = '';
  for (let c = 0; c < 3; c++) {
    const v = Math.max(0, Math.min(255, parseInt(hex.slice(c * 2, c * 2 + 2), 16) + k));
    out += Math.round(v).toString(16).padStart(2, '0');
  }
  return out;
}

const delay = <T>(value: T, ms = 60) => new Promise<T>((r) => setTimeout(() => r(value), ms));
const fail = (ms = 400) => new Promise<never>((_, j) => setTimeout(() => j(new Error('No answer in 3 s')), ms));

export function readMockState(search: string): MockState {
  const s = new URLSearchParams(search).get('state');
  return s === 'lost' || s === 'none' || s === 'off' || s === 'unsupported' ? s : 'ok';
}

export function createMock(state: MockState): Backend {
  const t0 = Date.now();
  let serviceUp = state !== 'off';
  let paused = false;
  /** lost: the controller is gone from 2 s after start until "Search now". */
  let lostAt = state === 'lost' ? t0 + 2000 : Infinity;
  let controllerBackAt = Infinity;
  let framesSent = 0;
  const listeners = new Set<(l: ServiceLink) => void>();

  const config: Esp32Config = {
    wifi_ssid: 'Home', ota_host: 'ambilight', ip_octet: 245,
    num_leds: 32, max_leds: limits.num_leds.max, brightness: 200, color: '#ffd9a8', led_mode: LedMode.dynamic,
    led_top: 10, led_right: 6, led_bottom: 10, led_left: 6, start_corner: 0, clockwise: true,
    gamma: 2.2, saturation: 1.2, wb_r: 255, wb_g: 240, wb_b: 220,
    power_limit_w: 0, pixel_watts: 0.3, power: true,
  };

  const controllerPresent = () => {
    const now = Date.now();
    if (state === 'none') return false;
    if (now >= controllerBackAt) return true;
    return now < lostAt;
  };
  const captureRuns = () => state !== 'unsupported' && !paused;

  function capture(now: number): CaptureStatus {
    if (state === 'unsupported') {
      const attempts = 3 + Math.floor((now - t0) / 30000);
      return {
        state: 'failed', failure: 'library', failedAttempts: attempts, frames: 0,
        error: 'libvideoenhance.so: cannot open shared object file: No such file or directory',
        retryInMs: 30000 - ((now - t0) % 30000),
      };
    }
    const hw = { ret: 0, blocks: 2, blockW: 16, blockH: 16, fullW: 3840, fullH: 2160, hwDelayMs: 20, delayMs: 20 };
    const frames = Math.floor((now - t0) / FRAME_MS);
    if (paused) return { state: 'paused', failure: null, error: null, failedAttempts: 0, hw, frames };
    return {
      state: 'running', failure: null, error: null, failedAttempts: 0, hw, frames,
      frameMs: FRAME_MS, lastFrameAgoMs: (now - t0) % FRAME_MS, reads: 14, badReads: 0, lastBadRet: 0,
      h: 4, v: 3, rgb: SCENE.map((c, i) => drift(c, now - t0, i)).join(''),
    };
  }

  function status(): ServiceStatus {
    const now = Date.now();
    const hosts = controllerPresent() ? [HOST] : [];
    if (hosts.length && captureRuns()) framesSent += 7;
    return {
      version: '1.2.0',
      capture: capture(now),
      esp32: { enabled: true, hosts, framesSent, sendErrors: 0, lastError: null },
      hyperhdr: { enabled: false },
      configWarnings: [],
    };
  }

  const esp = <T>(make: () => T): Promise<T> => (controllerPresent() ? delay(make()) : fail());
  const espStatus = (): Esp32Status => ({
    version: '1.2.0', ip: HOST, mode: 'STA', ssid: 'Home', rssi: -58, connected: true, gateway: '192.168.50.1',
  });
  const metrics = (): Esp32Metrics => {
    const streaming = config.power && config.led_mode === LedMode.dynamic && captureRuns();
    return {
      heap_free: 182000, heap_total: 320000, heap_min: 171000,
      uptime_s: Math.floor((Date.now() - t0) / 1000) + 86400, cpu_mhz: 240,
      power_w: config.power ? Math.round(config.num_leds * config.pixel_watts * config.brightness / limits.brightness.max * 10) / 10 : 0,
      power_limited: false,
      frames_rx: Math.floor(framesSent / 7),
      last_frame_ms: streaming ? 40 : -1,
    };
  };

  function command(name: string): Promise<ServiceCommandResult> {
    if (!serviceUp) return delay({ ok: false, error: 'The service is not running' });
    if (name === 'pause') paused = true;
    else if (name === 'resume') paused = false;
    else if (name === 'rediscover') {
      // The controller "comes back" a moment after a search in the lost / none scenarios.
      if (state === 'lost' || state === 'none') {
        controllerBackAt = Date.now() + 1500;
        lostAt = Infinity;
        if (state === 'none') state = 'ok';
      }
    } else return delay({ ok: false, error: `Unknown command ${name}` });
    return delay({ ok: true, error: null });
  }

  return {
    service: {
      subscribe(cb) {
        listeners.add(cb);
        return () => listeners.delete(cb);
      },
      requestStatus() {
        const link: ServiceLink = serviceUp ? { kind: 'up', status: status() } : { kind: 'down', reason: 'not-running' };
        setTimeout(() => listeners.forEach((cb) => cb(link)), 20);
      },
      command,
      start() {
        setTimeout(() => { serviceUp = true; }, 800);
        return delay(undefined, 100);
      },
    },
    // One controller: every host gets the same one.
    esp32: () => ({
      getStatus: () => esp(espStatus),
      getConfig: () => esp(() => ({ ...config })),
      getMetrics: () => esp(metrics),
      savePower: ({ power }) => esp(() => { config.power = power === 1; return { message: 'saved' }; }),
      saveLeds: (form) => esp(() => { Object.assign(config, form); return { message: 'saved' }; }),
      saveMode: ({ led_mode }) => esp(() => { config.led_mode = led_mode; return { message: 'saved' }; }),
    }),
    platform: {
      appVersion: () => '1.2.0',
      modelName: () => 'QE50QN92D',
      exit: () => console.info('[mock] exit()'),
    },
  };
}
