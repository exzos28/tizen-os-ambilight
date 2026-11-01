import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { interpolate, parseEdgeColors, sideArrows, sideOrder } from '../src/geometry.ts'

describe('parseEdgeColors', () => {
  it('reads 2·(h+v) samples in EdgeFrame order', () => {
    const rgb = 'ff0000' + '00ff00' + '0000ff' + '010203'
    assert.deepEqual(parseEdgeColors(rgb, 1, 1), [[255, 0, 0], [0, 255, 0], [0, 0, 255], [1, 2, 3]])
  })
  it('rejects a short or broken string and empty sides', () => {
    assert.equal(parseEdgeColors('ff0000', 1, 1), null)
    assert.equal(parseEdgeColors('zz0000'.repeat(4), 1, 1), null)
    assert.equal(parseEdgeColors('ff0000'.repeat(4), 0, 2), null)
    assert.equal(parseEdgeColors(undefined, 1, 1), null)
  })
})

describe('interpolate', () => {
  it('keeps the ends and blends between', () => {
    assert.deepEqual(interpolate([[0, 0, 0], [100, 200, 50]], 3), [[0, 0, 0], [50, 100, 25], [100, 200, 50]])
  })
  it('repeats a single sample and handles empty input', () => {
    assert.deepEqual(interpolate([[1, 2, 3]], 2), [[1, 2, 3], [1, 2, 3]])
    assert.deepEqual(interpolate([], 4), [])
  })
})

describe('sideOrder', () => {
  // Sides indexed top, right, bottom, left.
  it('clockwise from the top-left covers top first', () => {
    assert.deepEqual(sideOrder(0, true), [1, 2, 3, 4])
  })
  it('counter-clockwise from the top-left covers left first', () => {
    assert.deepEqual(sideOrder(0, false), [4, 3, 2, 1])
  })
  it('clockwise from the bottom-right covers bottom first', () => {
    assert.deepEqual(sideOrder(2, true), [3, 4, 1, 2])
  })
  it('counter-clockwise from the bottom-left covers bottom first', () => {
    assert.deepEqual(sideOrder(3, false), [3, 2, 1, 4])
  })
})

describe('sideArrows', () => {
  it('reverses every side', () => {
    assert.deepEqual(sideArrows(true), ['→', '↓', '←', '↑'])
    assert.deepEqual(sideArrows(false), ['←', '↑', '→', '↓'])
  })
})
