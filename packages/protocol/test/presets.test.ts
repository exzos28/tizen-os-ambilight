import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { defaultResponsePreset, limits, presetOf, presetTolerance, responsePresets } from '../src/index.ts'
import type { Esp32Config } from '../src/index.ts'
import { fixture } from './fixtures.ts'

const byId = (id: string) => responsePresets.find((p) => p.id === id)!

describe('responsePresets', () => {
  it('are the four presets, slowest first, with labels and descriptions', () => {
    assert.deepEqual(responsePresets.map((p) => p.id), ['subtle', 'moderate', 'high', 'intense'])
    for (let i = 1; i < responsePresets.length; i++) {
      assert.ok(responsePresets[i]!.values.smoothing_ms < responsePresets[i - 1]!.values.smoothing_ms)
    }
    for (const p of responsePresets) {
      assert.ok(p.label.length > 0 && p.description.length > 0, p.id)
    }
  })

  it('stay inside the setting ranges', () => {
    for (const { id, values } of responsePresets) {
      assert.ok(values.smoothing_ms >= limits.smoothing_ms.min && values.smoothing_ms <= limits.smoothing_ms.max, id)
      assert.ok(values.saturation >= limits.saturation.min && values.saturation <= limits.saturation.max, id)
    }
  })

  it('default to the device defaults', () => {
    assert.deepEqual(byId(defaultResponsePreset).values, {
      smoothing_ms: limits.smoothing_ms.default,
      saturation: limits.saturation.default,
    })
  })

  it('are far enough apart for the tolerance', () => {
    for (const a of responsePresets) {
      for (const b of responsePresets) {
        if (a === b) continue
        const apart = Math.abs(a.values.smoothing_ms - b.values.smoothing_ms) > 2 * presetTolerance.smoothing_ms
          || Math.abs(a.values.saturation - b.values.saturation) > 2 * presetTolerance.saturation
        assert.ok(apart, `${a.id} / ${b.id}`)
      }
    }
  })
})

describe('presetOf', () => {
  it('finds each preset from its own values', () => {
    for (const p of responsePresets) assert.equal(presetOf(p.values), p.id)
  })

  it('reads a fresh device as the default preset', () => {
    assert.equal(presetOf(fixture<Esp32Config>('esp32/config-response')), defaultResponsePreset)
  })

  it('accepts the float the device stores', () => {
    assert.equal(presetOf({ smoothing_ms: 100, saturation: Math.fround(1.15) }), 'high')
    assert.equal(presetOf({ smoothing_ms: 52, saturation: 1.31 }), 'intense')
  })

  it('is custom when a value differs', () => {
    assert.equal(presetOf({ smoothing_ms: 300, saturation: 1 }), 'custom')
    assert.equal(presetOf({ smoothing_ms: 200, saturation: 1.1 }), 'custom')
    assert.equal(presetOf({ smoothing_ms: 400, saturation: 1.3 }), 'custom')
  })

  it('is custom for firmware without smoothing_ms', () => {
    const old = { saturation: 1 } as Pick<Esp32Config, 'smoothing_ms' | 'saturation'>
    assert.equal(presetOf(old), 'custom')
  })
})
