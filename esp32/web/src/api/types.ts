export interface StatusResponse {
  ip: string
  mode: string
  ssid: string
  rssi: number
  connected: boolean
}

export interface ConfigResponse {
  wifi_ssid: string
  ota_host: string
  num_leds: number
  max_leds: number
  brightness: number
  color: string
  led_mode: number
  led_top: number
  led_right: number
  led_bottom: number
  led_left: number
  start_corner: number
  clockwise: boolean
}

export interface CalibrationRequest {
  led_top: number
  led_right: number
  led_bottom: number
  led_left: number
  start_corner: number
  clockwise: number  // 0 or 1
}

export interface NetworkSaveRequest {
  wifi_ssid?: string
  wifi_pass?: string
  ota_host?: string
  ota_pass?: string
}

export interface LedSaveRequest {
  num_leds?: number
  brightness?: number
  color?: string
}

export interface MetricsResponse {
  heap_free:  number
  heap_total: number
  heap_min:   number
  uptime_s:   number
  cpu_mhz:    number
}

export interface SaveResponse {
  message: string
}
