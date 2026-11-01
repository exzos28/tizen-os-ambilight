// HTTP client for the ESP32 API (protocol/schemas/esp32). GET responses are typed but not
// validated at runtime; POST replies must be {"message": string}.
import { constants } from './generated/constants.ts'
import type {
  CalibrationRequest,
  Esp32Config,
  Esp32Message,
  Esp32Metrics,
  Esp32Status,
  SaveLedsRequest,
  SaveModeRequest,
  SaveNetworkRequest,
  SavePowerRequest,
} from './generated/types.ts'

export interface Esp32ClientOptions {
  /** fetch implementation; the global fetch by default. */
  fetch?: typeof fetch
  /** Abort a request (including reading the body) after this many ms. Default 5000. */
  timeoutMs?: number
}

/**
 * - `http`: non-2xx reply; `message` is the firmware's own message when it sent one.
 * - `timeout`: no complete reply within `timeoutMs`.
 * - `network`: the request could not be made (device offline, CORS, …).
 * - `invalid-response`: 2xx, but not the JSON the endpoint returns (e.g. firmware too old for
 *   the endpoint: unknown paths redirect to the web UI).
 */
export type Esp32ErrorReason = 'http' | 'timeout' | 'network' | 'invalid-response'

export class Esp32Error extends Error {
  readonly reason: Esp32ErrorReason
  /** HTTP status, or null when there was no reply. */
  readonly status: number | null
  /** API path of the request, e.g. `/save/leds`. */
  readonly path: string

  constructor(reason: Esp32ErrorReason, message: string, path: string, status: number | null = null) {
    super(message)
    this.name = 'Esp32Error'
    this.reason = reason
    this.path = path
    this.status = status
  }
}

export interface Esp32Client {
  getStatus(): Promise<Esp32Status>
  getConfig(): Promise<Esp32Config>
  getMetrics(): Promise<Esp32Metrics>
  /** Saves LED settings; applied without a restart. */
  saveLeds(body: SaveLedsRequest): Promise<Esp32Message>
  /** Saves the strip layout (sets num_leds to the sum of the sides) and shows the preview. */
  saveCalibration(body: CalibrationRequest): Promise<Esp32Message>
  /** Shows the calibration preview without saving. */
  previewCalibration(body: CalibrationRequest): Promise<Esp32Message>
  saveMode(body: SaveModeRequest): Promise<Esp32Message>
  /** Saves Wi-Fi / OTA settings; the device restarts after replying. */
  saveNetwork(body: SaveNetworkRequest): Promise<Esp32Message>
  /** Turns the lighting on or off (saved). */
  savePower(body: SavePowerRequest): Promise<Esp32Message>
  /** Blinks the strip white a few times, then restores it. */
  identify(): Promise<Esp32Message>
  /** Restarts the device after replying. */
  restart(): Promise<Esp32Message>
}

const paths = constants.httpPaths

/**
 * @param baseUrl Device origin, e.g. `http://192.168.1.245`; `''` for the page's own origin
 *   (the web UI served by the device).
 */
export function createEsp32Client(baseUrl: string, options: Esp32ClientOptions = {}): Esp32Client {
  const origin = baseUrl.replace(/\/+$/, '')
  const timeoutMs = options.timeoutMs ?? 5000
  const doFetch: typeof fetch = options.fetch ?? ((input, init) => fetch(input, init))

  // → the parsed JSON body and the HTTP status of a 2xx reply.
  async function request(path: string, init: RequestInit): Promise<[unknown, number]> {
    const controller = new AbortController()
    const timer = setTimeout(() => controller.abort(), timeoutMs)
    let res: Response
    let text: string
    try {
      res = await doFetch(origin + path, { ...init, signal: controller.signal })
      text = await res.text()
    } catch (e) {
      if (controller.signal.aborted) throw new Esp32Error('timeout', `No reply from ${path} within ${timeoutMs} ms`, path)
      throw new Esp32Error('network', `Request to ${path} failed: ${e instanceof Error ? e.message : String(e)}`, path)
    } finally {
      clearTimeout(timer)
    }
    let body: unknown
    try {
      body = JSON.parse(text)
    } catch {
      body = undefined
    }
    if (!res.ok) {
      const message = messageOf(body)
      throw new Esp32Error('http', message ?? `HTTP ${res.status} from ${path}`, path, res.status)
    }
    if (body === undefined) throw new Esp32Error('invalid-response', `${path} did not reply with JSON`, path, res.status)
    return [body, res.status]
  }

  const get = <T>(path: string) => async (): Promise<T> => (await request(path, { method: 'GET' }))[0] as T

  async function post(path: string, fields?: object): Promise<Esp32Message> {
    const init: RequestInit = { method: 'POST' }
    if (fields) {
      init.headers = { 'Content-Type': 'application/x-www-form-urlencoded' }
      init.body = formBody(fields)
    }
    const [body, status] = await request(path, init)
    const message = messageOf(body)
    if (message === undefined) throw new Esp32Error('invalid-response', `${path} did not reply with {"message"}`, path, status)
    return { message }
  }

  return {
    getStatus: get<Esp32Status>(paths.status),
    getConfig: get<Esp32Config>(paths.config),
    getMetrics: get<Esp32Metrics>(paths.metrics),
    saveLeds: (body) => post(paths.saveLeds, body),
    saveCalibration: (body) => post(paths.saveCalibration, body),
    previewCalibration: (body) => post(paths.previewCalibration, body),
    saveMode: (body) => post(paths.saveMode, body),
    saveNetwork: (body) => post(paths.saveNetwork, body),
    savePower: (body) => post(paths.savePower, body),
    identify: () => post(paths.identify),
    restart: () => post(paths.restart),
  }
}

/**
 * application/x-www-form-urlencoded body. `undefined` fields are left out (the device keeps
 * their value); everything else is sent as is, including empty strings.
 */
export function formBody(fields: object): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(fields)) {
    if (value !== undefined) params.append(key, String(value))
  }
  return params.toString()
}

/**
 * Whether the device's settings may differ from the GET /config read at revision `readAt`, so
 * /config should be read again: true when `metrics.config_rev` differs from it (revisions are
 * compared for equality only). `readAt` null — no revision known for the config in hand (e.g.
 * /config was read before the first /metrics) — counts as changed. Firmware that doesn't send
 * `config_rev` (older than the field) can't tell: false.
 *
 *   if (configChanged(rev, metrics)) { config = await esp.getConfig(); rev = metrics.config_rev }
 */
export function configChanged(readAt: number | null, metrics: Esp32Metrics): boolean {
  const rev: unknown = metrics.config_rev
  if (typeof rev !== 'number') return false
  return readAt === null || rev !== readAt
}

function messageOf(body: unknown): string | undefined {
  if (typeof body === 'object' && body !== null && typeof (body as { message?: unknown }).message === 'string') {
    return (body as { message: string }).message
  }
  return undefined
}
