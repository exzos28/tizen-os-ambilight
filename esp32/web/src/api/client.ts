import type {
  StatusResponse,
  ConfigResponse,
  CalibrationRequest,
  NetworkSaveRequest,
  LedSaveRequest,
  MetricsResponse,
  SaveResponse,
} from './types'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, init)
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${path}`)
  return res.json() as Promise<T>
}

function toFormBody(data: Record<string, string | number | undefined>): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(data)) {
    if (value !== undefined && value !== '') {
      params.append(key, String(value))
    }
  }
  return params.toString()
}

const POST_FORM: RequestInit = {
  method: 'POST',
  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
}

export function getStatus(): Promise<StatusResponse> {
  return request<StatusResponse>('/status')
}

export function getMetrics(): Promise<MetricsResponse> {
  return request<MetricsResponse>('/metrics')
}

export function getConfig(): Promise<ConfigResponse> {
  return request<ConfigResponse>('/config')
}

/** Saves WiFi + OTA settings. The device will restart after this call. */
export function saveNetwork(data: NetworkSaveRequest): Promise<SaveResponse> {
  return request<SaveResponse>('/save/network', { ...POST_FORM, body: toFormBody(data) })
}

/** Applies LED settings immediately — no restart. */
export function saveLeds(data: LedSaveRequest): Promise<SaveResponse> {
  return request<SaveResponse>('/save/leds', { ...POST_FORM, body: toFormBody(data) })
}

/** Saves calibration (LED counts per side + layout) and shows it on the strip. */
export function saveCalibration(data: CalibrationRequest): Promise<SaveResponse> {
  return request<SaveResponse>('/save/calibration', { ...POST_FORM, body: toFormBody(data) })
}

/** Temporarily lights the strip to visualise the current calibration. Does not save. */
export function previewCalibration(data: CalibrationRequest): Promise<SaveResponse> {
  return request<SaveResponse>('/preview/calibration', { ...POST_FORM, body: toFormBody(data) })
}

/** Switches between plain (0) and dynamic (1) mode. */
export function saveMode(ledMode: number): Promise<SaveResponse> {
  return request<SaveResponse>('/save/mode', { ...POST_FORM, body: toFormBody({ led_mode: ledMode }) })
}
