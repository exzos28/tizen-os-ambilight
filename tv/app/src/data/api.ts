// What the screens talk to. Two implementations: the real one on the TV (service.ts and the
// @ambilight/protocol ESP32 client, see index.ts) and an in-memory mock for a desktop browser
// (mock.ts). Wire types come from @ambilight/protocol.

import type { Esp32Client, ServiceCommand, ServiceCommandResult, ServiceOutputs, ServiceStatus } from '@ambilight/protocol';

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
  command(command: ServiceCommand): Promise<ServiceCommandResult>;
  /**
   * `set-outputs`: turns the ESP32 / HyperHDR outputs on or off and sets the HyperHDR target,
   * live; the service saves it on the TV. ok: false with the service's reason if it is invalid.
   */
  setOutputs(outputs: ServiceOutputs): Promise<ServiceCommandResult>;
  /** Launches the service (AppControl command=start), the old launcher's *Start*. */
  start(): Promise<void>;
}

/** The ESP32 HTTP API endpoints the launcher uses. */
export type Esp32Controller = Pick<
  Esp32Client,
  'getStatus' | 'getConfig' | 'getMetrics' | 'savePower' | 'saveLeds' | 'saveMode' | 'identify' | 'saveCalibration' | 'previewCalibration'
>;

/** The controller at `host` (an IPv4 address from status.esp32.hosts). */
export type Esp32Api = (host: string) => Esp32Controller;

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
