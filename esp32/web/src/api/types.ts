export interface StatusResponse {
  ip: string
  mode: string
  ssid: string
  rssi: number
  connected: boolean
  gateway: string
  preferred_last_oct: number  // 0 = DHCP, otherwise the static last IP octet
}

export interface ConfigResponse {
  wifi_ssid: string
  ota_host: string
  ip_octet: number  // 0 = DHCP, otherwise the static last IP octet
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
  gamma: number
  saturation: number
  wb_r: number
  wb_g: number
  wb_b: number
  power_limit_w: number  // 0 = no limit
  pixel_watts: number    // W per LED pixel at full white
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
  ip_octet?: number
}

export interface LedSaveRequest {
  num_leds?: number
  brightness?: number
  color?: string
  gamma?: number
  saturation?: number
  wb_r?: number
  wb_g?: number
  wb_b?: number
  power_limit_w?: number
  pixel_watts?: number
}

export interface MetricsResponse {
  heap_free:  number
  heap_total: number
  heap_min:   number
  uptime_s:   number
  cpu_mhz:    number
  power_w:       number   // estimated strip power of the last frame
  power_limited: boolean  // the power limit reduced brightness
}

export interface SaveResponse {
  message: string
}
