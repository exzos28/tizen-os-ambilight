// In-memory controller for development and screenshots (see api.ts: never in the flash build).
//
// ?mock=ok        streaming from the TV, everything works (default in the mock build)
// ?mock=ap        first start: setup hotspot, no Wi-Fi saved
// ?mock=noframes  follows the picture, but nothing has come from the TV for 6 minutes
// ?mock=limited   the power limit dims the strip
// ?mock=offline   the controller does not answer

import {
  type Esp32Client, type Esp32Config, Esp32Error, type Esp32Message, type Esp32Metrics, type Esp32Status, LedMode, limits,
} from '@ambilight/protocol'

export type MockScenario = 'ok' | 'ap' | 'noframes' | 'limited' | 'offline'

const SCENARIOS: readonly MockScenario[] = ['ok', 'ap', 'noframes', 'limited', 'offline']

export function mockScenario(search: string, fallbackOk: boolean): MockScenario | null {
  const s = new URLSearchParams(search).get('mock')
  if (s && (SCENARIOS as readonly string[]).includes(s)) return s as MockScenario
  return fallbackOk ? 'ok' : null
}

const delay = <T>(value: T, ms = 80) => new Promise<T>((r) => setTimeout(() => r(value), ms))

export function createMockClient(scenario: MockScenario): Esp32Client {
  const t0 = Date.now()
  const ap = scenario === 'ap'
  const config: Esp32Config = {
    wifi_ssid: ap ? '' : 'HomeNet', ota_host: 'ambilight', ip_octet: 245,
    num_leds: 32, max_leds: limits.num_leds.max, brightness: 200, color: '#ffd9a8', led_mode: LedMode.dynamic,
    led_top: 10, led_right: 6, led_bottom: 10, led_left: 6, start_corner: 0, clockwise: true,
    gamma: 2.2, saturation: 1.2, wb_r: 255, wb_g: 236, wb_b: 214,
    power_limit_w: scenario === 'limited' ? 10 : 60, pixel_watts: 0.72, power: true,
  }
  const status: Esp32Status = ap
    ? { version: '1.2.0', ip: '192.168.4.1', mode: 'AP', ssid: 'ambilight-setup', rssi: 0, connected: false, gateway: '0.0.0.0' }
    : { version: '1.2.0', ip: '192.168.50.245', mode: 'STA', ssid: 'HomeNet', rssi: -58, connected: true, gateway: '192.168.50.1' }

  const metrics = (): Esp32Metrics => {
    const s = (Date.now() - t0) / 1000
    const streaming = scenario === 'ok' || scenario === 'limited'
    const lit = config.power
    const full = (config.led_top + config.led_right + config.led_bottom + config.led_left) * config.pixel_watts * config.brightness / 255
    // Following the picture without frames: dark 5 s after the last one.
    const want = !lit ? 0 : config.led_mode === LedMode.plain ? full : streaming ? full * (0.75 + 0.05 * Math.sin(s / 3)) : 0
    const limited = config.power_limit_w > 0 && want > config.power_limit_w
    return {
      heap_free: 186_400, heap_total: 327_680, heap_min: 175_100, cpu_mhz: 240,
      uptime_s: Math.round(275_000 + s),
      power_w: Math.round((limited ? config.power_limit_w : want) * 10) / 10,
      power_limited: limited,
      frames_rx: ap ? 0 : streaming ? Math.round(1_928_400 + s * 7) : 1_928_400,
      last_frame_ms: ap ? -1 : streaming ? 40 + Math.round(Math.random() * 100) : Math.round(372_000 + s * 1000),
    }
  }

  const ok = (message: string) => delay<Esp32Message>({ message })
  const offline = <T>(path: string) => new Promise<T>((_, reject) =>
    setTimeout(() => reject(new Esp32Error('timeout', `No reply from ${path} within 5000 ms`, path)), 300))
  const answer = <T>(path: string, value: () => T) => (scenario === 'offline' ? offline<T>(path) : delay(value()))

  return {
    getStatus: () => answer('/status', () => ({ ...status })),
    getConfig: () => answer('/config', () => ({ ...config })),
    getMetrics: () => answer('/metrics', metrics),
    saveLeds: (b) => { Object.assign(config, b); return ok('LED settings saved.') },
    saveCalibration: (b) => {
      Object.assign(config, { ...b, clockwise: b.clockwise === undefined ? config.clockwise : b.clockwise === 1 })
      config.num_leds = config.led_top + config.led_right + config.led_bottom + config.led_left
      return ok('Calibration saved.')
    },
    previewCalibration: () => ok('Preview shown for 10 s.'),
    saveMode: (b) => { config.led_mode = b.led_mode; return ok('Mode saved.') },
    saveNetwork: (b) => {
      if (b.wifi_ssid) config.wifi_ssid = b.wifi_ssid
      if (b.ota_host) config.ota_host = b.ota_host
      if (b.ip_octet !== undefined) config.ip_octet = b.ip_octet
      return ok('Network settings saved. Restarting…')
    },
    savePower: (b) => { config.power = b.power === 1; return ok('Power saved.') },
    identify: () => ok('Blinking.'),
    restart: () => ok('Restarting…'),
  }
}
