import type { Tone } from '@ambilight/ui'

/** 3 d 4 h · 2 h 5 min · 4 min · 12 s */
export function duration(seconds: number): string {
  const s = Math.max(0, Math.round(seconds))
  if (s < 60) return `${s} s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m} min`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h} h ${m % 60} min`
  return `${Math.floor(h / 24)} d ${h % 24} h`
}

export const ago = (ms: number) => (ms < 1000 ? 'just now' : `${duration(ms / 1000)} ago`)

export const kb = (bytes: number) => `${Math.round(bytes / 1024)} KB`

export const watts = (w: number) => `${w < 10 ? w.toFixed(1) : Math.round(w)} W`

export const pctOf = (byte: number) => Math.round((byte * 100) / 255)
export const byteOf = (pct: number) => Math.round((pct * 255) / 100)

/** Wi-Fi signal in words. 0 dBm = not connected (setup hotspot). */
export function signal(rssi: number): { text: string; tone: Tone } {
  if (!rssi) return { text: '—', tone: 'off' }
  if (rssi >= -67) return { text: `${rssi} dBm · good`, tone: 'ok' }
  if (rssi >= -78) return { text: `${rssi} dBm · fair`, tone: 'ok' }
  return { text: `${rssi} dBm · weak`, tone: 'warn' }
}

/** float32 noise from the device (0.7200000286 → 0.72) without rounding real digits away. */
export const clean = (n: number) => Number(n.toPrecision(6))

/** "192.168.1.1" → "192.168.1."; unknown → null */
export function subnetPrefix(gateway?: string): string | null {
  const parts = gateway?.split('.')
  return parts && parts.length === 4 && gateway !== '0.0.0.0' ? parts.slice(0, 3).join('.') + '.' : null
}
