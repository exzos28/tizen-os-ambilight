// Picks the backend: the TV (window.tizen) or the desktop mock.

import type { Backend, PlatformApi } from './api';
import { esp32Http } from './esp32';
import { createMock, readMockState } from './mock';
import { createServiceClient } from './service';

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
  return { service: createServiceClient(tz), esp32: esp32Http, platform: tizenPlatform(tz) };
}

export type { Backend, ServiceLink } from './api';
export * from './types';
