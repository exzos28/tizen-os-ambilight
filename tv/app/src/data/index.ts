// Picks the backend: the TV (window.tizen) or the desktop mock.

import { createEsp32Client, type Esp32Client } from '@ambilight/protocol';
import type { Backend, Esp32Api, PlatformApi } from './api';
import { createMock, readMockState } from './mock';
import { createServiceClient } from './service';

/** Per ESP32 request, including reading the reply. */
const ESP32_TIMEOUT_MS = 3000;

// /metrics is polled every second: never answer from the HTTP cache.
const noStore: typeof fetch = (input, init) => fetch(input, { ...init, cache: 'no-store' });

/** The launcher talks to each controller directly (docs/NETWORKING.md, ESP32 HTTP API). */
function esp32Http(): Esp32Api {
  const clients = new Map<string, Esp32Client>();
  return (host) => {
    let client = clients.get(host);
    if (!client) {
      client = createEsp32Client(`http://${host}`, { timeoutMs: ESP32_TIMEOUT_MS, fetch: noStore });
      clients.set(host, client);
    }
    return client;
  };
}

function tizenPlatform(tz: TizenApi.Tizen): PlatformApi {
  return {
    appVersion() {
      try {
        return tz.application.getCurrentApplication().appInfo.version || null;
      } catch {
        return null;
      }
    },
    modelName() {
      try {
        const name = tz.systeminfo.getCapability('http://tizen.org/system/model_name');
        return typeof name === 'string' && name ? name : null;
      } catch {
        return null;
      }
    },
    exit() {
      tz.application.getCurrentApplication().exit();
    },
  };
}

export function createBackend(): Backend {
  const tz = window.tizen;
  if (!tz) return createMock(readMockState(location.search));
  return { service: createServiceClient(tz), esp32: esp32Http(), platform: tizenPlatform(tz) };
}

export type { Backend, ServiceLink } from './api';
