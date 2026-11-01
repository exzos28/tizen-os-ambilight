import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { CaptureStateValues, LedMode, StartCorner, constants, limits } from '../src/index.ts'

// constexpr NAME = value; from the C++ header generated alongside the TS files.
const header = readFileSync(new URL('../../../esp32/src/generated/ProtocolLimits.h', import.meta.url), 'utf8')
const cpp = new Map(
  [...header.matchAll(/^constexpr\s+.+?\s+(\w+)\s*=\s*([^;]+);/gm)].map((m) => [m[1]!, m[2]!.trim()]),
)
const cppNumber = (name: string): number => {
  const raw = cpp.get(name)
  assert.ok(raw !== undefined, `${name} is missing from ProtocolLimits.h`)
  return Number(raw.replace(/f$/, ''))
}

describe('limits', () => {
  it('agree with constants.json', () => {
    assert.equal(limits.num_leds.max, constants.esp32.maxLeds)
    assert.equal(limits.led_top.max, constants.esp32.maxLedsPerSide)
  })

  it('leave out write-only settings', () => {
    assert.equal('wifi_pass' in limits, false)
    assert.equal('ota_pass' in limits, false)
  })

  it('have defaults inside their ranges', () => {
    for (const [name, l] of Object.entries(limits)) {
      if ('min' in l) {
        assert.ok(l.default >= l.min && l.default <= l.max, `${name}: ${l.default} outside ${l.min}..${l.max}`)
      }
    }
  })

  it('match the C++ header', () => {
    for (const [name, l] of Object.entries(limits)) {
      const base = name.toUpperCase()
      if ('min' in l) {
        assert.equal(cppNumber(`${base}_MIN`), l.min, `${base}_MIN`)
        assert.equal(cppNumber(`${base}_MAX`), l.max, `${base}_MAX`)
        assert.equal(cppNumber(`${base}_DEFAULT`), l.default, `${base}_DEFAULT`)
      } else {
        assert.equal(cpp.get(`${base}_DEFAULT`), JSON.stringify(l.default), `${base}_DEFAULT`)
      }
    }
    assert.equal(cppNumber('PORT_FRAME'), constants.ports.frame)
    assert.equal(cpp.get('DISCOVERY_MAGIC'), JSON.stringify(constants.discovery.magic))
    assert.equal(cppNumber('LED_MODE_DYNAMIC'), LedMode.dynamic)
  })
})

describe('enums', () => {
  it('expose named values', () => {
    assert.deepEqual(LedMode, { plain: 0, dynamic: 1 })
    assert.equal(StartCorner.bottomLeft, 3)
    assert.ok(CaptureStateValues.includes('paused'))
  })
})
