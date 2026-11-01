// What the screens talk to. Two implementations: the real one on the TV (service.ts +
// esp32.ts + platform.ts) and an in-memory mock for a desktop browser (mock.ts).

import type {
  CommandResult, Esp32Config, Esp32Message, Esp32Metrics, Esp32Status, LedMode,
  SaveLedsForm, ServiceCommand, ServiceStatus,
} from './types';

/** Whether the launcher can reach the service, and its last status. */
export type ServiceLink =
  | { kind: 'checking' }
  /** not-running: the status port does not exist (requestRemoteMessagePort throws);
   *  not-responding: the port is there but no reply for 3 s, or sending failed;
   *  unavailable: this runtime has no message ports at all. */
  | { kind: 'down'; reason: 'not-running' | 'not-responding' | 'unavailable'; detail?: string }
  | { kind: 'up'; status: ServiceStatus };

export interface ServiceApi {
  /** Every status reply or link change. Returns an unsubscribe function. */
  subscribe(cb: (link: ServiceLink) => void): () => void;
  /** Sends one status request; the answer arrives through subscribe(). */
  requestStatus(): void;
  command(command: ServiceCommand): Promise<CommandResult>;
  /** Launches the service (AppControl command=start), the old launcher's *Start*. */
  start(): Promise<void>;
}

export interface Esp32Api {
  getStatus(host: string): Promise<Esp32Status>;
  getConfig(host: string): Promise<Esp32Config>;
  getMetrics(host: string): Promise<Esp32Metrics>;
  savePower(host: string, on: boolean): Promise<Esp32Message>;
  saveLeds(host: string, form: SaveLedsForm): Promise<Esp32Message>;
  saveMode(host: string, mode: LedMode): Promise<Esp32Message>;
}

export interface PlatformApi {
  /** The launcher's version from config.xml, null when unknown. */
  appVersion(): string | null;
  /** The TV model (system info), null when unknown. */
  modelName(): string | null;
  exit(): void;
}

export interface Backend {
  service: ServiceApi;
  esp32: Esp32Api;
  platform: PlatformApi;
}
