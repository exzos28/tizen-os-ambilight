// Response presets: how fast the strip follows the picture and how strongly it colors it.
// Shared by the TV launcher and the phone web app; each one is one POST /save/leds with
// `smoothing_ms` and `saturation`. A separate axis from color presets (gamma, white balance).
import type { Esp32Config } from './generated/types.ts'

export type ResponsePresetId = 'subtle' | 'moderate' | 'high' | 'intense'

export interface ResponsePreset {
  id: ResponsePresetId
  label: string
  /** One line: what it suits. */
  description: string
  values: Pick<Esp32Config, 'smoothing_ms' | 'saturation'>
}

export const responsePresets: readonly ResponsePreset[] = [
  { id: 'subtle', label: 'Subtle', description: 'Films, calm', values: { smoothing_ms: 400, saturation: 1.0 } },
  { id: 'moderate', label: 'Moderate', description: 'Everything', values: { smoothing_ms: 200, saturation: 1.0 } },
  { id: 'high', label: 'High', description: 'Sport, music videos', values: { smoothing_ms: 100, saturation: 1.15 } },
  { id: 'intense', label: 'Intense', description: 'Games, least delay', values: { smoothing_ms: 50, saturation: 1.3 } },
]

/** The preset of a fresh device: the `smoothing_ms` and `saturation` defaults. */
export const defaultResponsePreset: ResponsePresetId = 'moderate'

/** Within these a value counts as the preset's (the device stores saturation as a float). */
export const presetTolerance = { smoothing_ms: 5, saturation: 0.025 } as const

/**
 * The response preset the config's `smoothing_ms` and `saturation` match, or 'custom'.
 * Firmware without `smoothing_ms` (the field missing) → 'custom'.
 */
export function presetOf(config: Pick<Esp32Config, 'smoothing_ms' | 'saturation'>): ResponsePresetId | 'custom' {
  const { smoothing_ms: ms, saturation: sat } = config
  if (typeof ms !== 'number' || typeof sat !== 'number') return 'custom'
  const hit = responsePresets.find((p) =>
    Math.abs(p.values.smoothing_ms - ms) <= presetTolerance.smoothing_ms
    && Math.abs(p.values.saturation - sat) <= presetTolerance.saturation)
  return hit ? hit.id : 'custom'
}
