// Wire formats, field names exactly as they travel. Mirrors the contract of the TV service
// (tv/service, local message port "ambilight.status") and the ESP32 HTTP API
// (docs/NETWORKING.md). To be replaced by the shared protocol package.

// ---------------------------------------------------------------- ESP32 HTTP API

/** GET /status */
export interface Esp32Status {
  version: string;
  ip: string;
  mode: 'STA' | 'AP';
  ssid: string;
  rssi: number;
  connected: boolean;
  gateway: string;
}

/** 0..3: the corner the strip starts at. */
export type StartCorner = 0 | 1 | 2 | 3;

/** 0 = plain (static color), 1 = dynamic (follows the TV frames). */
export type LedMode = 0 | 1;

/** GET /config */
export interface Esp32Config {
  wifi_ssid: string;
  ota_host: string;
  ip_octet: number;
  num_leds: number;
  max_leds: number;
  /** 0..255 */
  brightness: number;
  /** "#rrggbb" */
  color: string;
  led_mode: LedMode;
  led_top: number;
  led_right: number;
  led_bottom: number;
  led_left: number;
  start_corner: StartCorner;
  clockwise: boolean;
  gamma: number;
  saturation: number;
  wb_r: number;
  wb_g: number;
  wb_b: number;
  power_limit_w: number;
  pixel_watts: number;
  power: boolean;
}

/** GET /metrics */
export interface Esp32Metrics {
  heap_free: number;
  heap_total: number;
  heap_min: number;
  uptime_s: number;
  cpu_mhz: number;
  power_w: number;
  power_limited: boolean;
  frames_rx: number;
  /** ms since the last UDP frame, -1 = none yet */
  last_frame_ms: number;
}

/** Reply of every POST endpoint. */
export interface Esp32Message {
  message: string;
}

/** POST /save/leds (form-urlencoded). Omitted fields keep their value. */
export interface SaveLedsForm {
  num_leds?: number;
  brightness?: number;
  color?: string;
  gamma?: number;
  saturation?: number;
  wb_r?: number;
  wb_g?: number;
  wb_b?: number;
  power_limit_w?: number;
  pixel_watts?: number;
}

/** POST /save/mode */
export interface SaveModeForm {
  led_mode: LedMode;
}

/** POST /save/power */
export interface SavePowerForm {
  power: 0 | 1;
}

// ---------------------------------------------------------------- TV service status

export type CaptureState = 'waiting' | 'starting' | 'running' | 'paused' | 'failed';
export type CaptureFailure = 'library' | 'symbols' | 'hardware';

/** The hardware measurement condition (ppi_ve_get_rgb_measure_condition). */
export interface HwCondition {
  ret: number;
  blocks: number;
  blockW: number;
  blockH: number;
  fullW: number;
  fullH: number;
  hwDelayMs: number;
  delayMs: number;
}

export interface CaptureStatus {
  state: CaptureState;
  failure: CaptureFailure | null;
  error: string | null;
  failedAttempts: number;
  retryInMs?: number;
  hw?: HwCondition;
  frames: number;
  frameMs?: number;
  lastFrameAgoMs?: number;
  reads?: number;
  badReads?: number;
  lastBadRet?: number;
  /** samples on the top and bottom edge */
  h?: number;
  /** samples on the left and right edge */
  v?: number;
  /** hex, 2*(h+v)*3 bytes, frame order: top L→R, right T→B, bottom R→L, left B→T */
  rgb?: string;
}

export type Esp32Output =
  | { enabled: false }
  | { enabled: true; hosts: string[]; framesSent: number; sendErrors: number; lastError: string | null };

export type HyperHdrOutput =
  | { enabled: false }
  | { enabled: true; endpoint: string; connected: boolean; replies: number; lastError: string | null };

/** Reply key "status" (a JSON string). */
export interface ServiceStatus {
  version: string;
  capture: CaptureStatus;
  esp32: Esp32Output;
  hyperhdr: HyperHdrOutput;
  configWarnings: string[];
}

export type ServiceCommand = 'pause' | 'resume' | 'rediscover';

/** Reply key "result" (a JSON string) to {request: "command", command}. */
export interface CommandResult {
  ok: boolean;
  error: string | null;
}
