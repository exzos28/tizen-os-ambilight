import type { Esp32Config, Esp32Metrics, Esp32Status, LedMode, SaveLedsRequest } from '@ambilight/protocol'
import type { TabId } from './components/chrome'
import type { Problem } from './problems'

/** What every screen gets from the App: the controller's state and the ways to change it. */
export interface Ctl {
  status: Esp32Status | null
  /** null until GET /config answered. */
  cfg: Esp32Config | null
  metrics: Esp32Metrics | null
  offline: boolean
  problems: Problem[]
  /** Applies at once on screen, sent `delayMs` after the last change (fields merged). */
  saveLeds(body: SaveLedsRequest, delayMs?: number): void
  setPower(on: boolean): void
  setMode(mode: LedMode): void
  /** A static color: saves it and switches to static color mode. */
  setColor(color: string, delayMs?: number): void
  /** Local update after a request the screen made itself. */
  patch(p: Partial<Esp32Config>): void
  reload(): void
  notify(text: string, tone?: 'ok' | 'bad'): void
  go(tab: TabId): void
}
