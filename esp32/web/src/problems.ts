// What needs the user's attention (most important first) and the header pill, from what the
// controller reports. Same verdicts as the TV launcher where they overlap.

import { type Esp32Config, type Esp32Metrics, LedMode } from '@ambilight/protocol'
import type { Tone } from '@ambilight/ui'
import { ago, duration } from './format'

/** A frame within this window: the controller is receiving from the TV (as on the TV). */
export const RECEIVING_MS = 2000
/** Following the picture with no frame for this long is a problem, not a pause. */
const NO_FRAMES_MS = 30_000

export interface Problem {
  tone: 'warn' | 'bad'
  title: string
  text: string
  hints?: string[]
  action?: 'retry' | 'static' | 'power'
}

export const receiving = (m: Esp32Metrics | null) => !!m && m.last_frame_ms >= 0 && m.last_frame_ms < RECEIVING_MS

export function findProblems(cfg: Esp32Config | null, m: Esp32Metrics | null, offline: boolean): Problem[] {
  if (offline) {
    return [{
      tone: 'bad', action: 'retry',
      title: 'The controller does not answer',
      text: 'It may be restarting, switched off, or on another network than this phone.',
    }]
  }
  if (!cfg || !m) return []
  const out: Problem[] = []
  const following = cfg.power && cfg.led_mode === LedMode.dynamic
  const never = m.last_frame_ms < 0
  if (following && (never ? m.uptime_s * 1000 > NO_FRAMES_MS : m.last_frame_ms > NO_FRAMES_MS)) {
    out.push({
      tone: 'warn', action: 'static',
      title: 'No picture from the TV',
      text: never
        ? `Nothing has arrived since the controller started ${duration(m.uptime_s)} ago, so the strip stays dark.`
        : `The last frame came ${ago(m.last_frame_ms)}. The strip goes dark 5 s after the picture stops.`,
      hints: ['Is the TV on, with the Ambilight app installed?', 'Is the TV on the same Wi-Fi?', 'A static color works without the TV'],
    })
  }
  if (cfg.power && m.power_limited) {
    out.push({
      tone: 'warn', action: 'power',
      title: 'The power limit dims the strip',
      text: `The strip would draw more than ${cfg.power_limit_w} W, so the controller lowers the brightness. Raise the limit if your power supply allows it.`,
    })
  }
  return out
}

export function summary(cfg: Esp32Config | null, m: Esp32Metrics | null, list: Problem[], offline: boolean): { tone: Tone; text: string } {
  if (offline) return { tone: 'bad', text: 'Not answering' }
  if (!cfg) return { tone: 'off', text: 'Connecting…' }
  if (list.length) return { tone: 'warn', text: 'Needs attention' }
  if (!cfg.power) return { tone: 'off', text: 'Lighting off' }
  if (cfg.led_mode === LedMode.dynamic && !receiving(m)) return { tone: 'off', text: 'Waiting for the TV' }
  return { tone: 'ok', text: 'Everything works' }
}
