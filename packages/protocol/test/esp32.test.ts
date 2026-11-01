import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { Esp32Error, createEsp32Client, formBody } from '../src/index.ts'
import type { Esp32Config, Esp32Metrics, Esp32Status } from '../src/index.ts'
import { fixture, fixtureText } from './fixtures.ts'

interface Call {
  url: string
  init: RequestInit
}

// fetch that records calls and answers with the next reply.
function fakeFetch(...replies: (() => Response | Promise<Response>)[]) {
  const calls: Call[] = []
  const fn: typeof fetch = async (input, init) => {
    calls.push({ url: String(input), init: init ?? {} })
    const reply = replies.shift()
    if (!reply) throw new Error('unexpected request')
    return reply()
  }
  return { fn, calls }
}

const json = (body: string, status = 200) => () =>
  new Response(body, { status, headers: { 'Content-Type': 'application/json' } })

describe('createEsp32Client', () => {
  it('GETs /status, /config and /metrics from the base URL', async () => {
    const f = fakeFetch(
      json(fixtureText('esp32/status-response')),
      json(fixtureText('esp32/config-response')),
      json(fixtureText('esp32/metrics-response')),
    )
    const esp = createEsp32Client('http://192.168.1.245/', { fetch: f.fn })

    assert.deepEqual(await esp.getStatus(), fixture<Esp32Status>('esp32/status-response'))
    assert.deepEqual(await esp.getConfig(), fixture<Esp32Config>('esp32/config-response'))
    assert.deepEqual(await esp.getMetrics(), fixture<Esp32Metrics>('esp32/metrics-response'))
    assert.deepEqual(f.calls.map((c) => [c.init.method, c.url]), [
      ['GET', 'http://192.168.1.245/status'],
      ['GET', 'http://192.168.1.245/config'],
      ['GET', 'http://192.168.1.245/metrics'],
    ])
  })

  it('uses relative URLs for an empty base URL (web UI served by the device)', async () => {
    const f = fakeFetch(json(fixtureText('esp32/status-response')))
    await createEsp32Client('', { fetch: f.fn }).getStatus()
    assert.equal(f.calls[0]?.url, '/status')
  })

  it('POSTs form-urlencoded bodies and returns the message', async () => {
    const f = fakeFetch(json('{"message":"Applied."}'))
    const esp = createEsp32Client('http://esp', { fetch: f.fn })

    const reply = await esp.saveLeds({ brightness: 128, color: '#ff8800', gamma: 2.2, num_leds: undefined })

    assert.deepEqual(reply, { message: 'Applied.' })
    const call = f.calls[0]!
    assert.equal(call.url, 'http://esp/save/leds')
    assert.equal(call.init.method, 'POST')
    assert.deepEqual(call.init.headers, { 'Content-Type': 'application/x-www-form-urlencoded' })
    assert.equal(call.init.body, 'brightness=128&color=%23ff8800&gamma=2.2')
  })

  it('maps every POST endpoint to its path', async () => {
    const ok = json('{"message":"ok"}')
    const f = fakeFetch(ok, ok, ok, ok, ok, ok, ok, ok)
    const esp = createEsp32Client('http://esp', { fetch: f.fn })
    const layout = { led_top: 10, led_right: 6, led_bottom: 10, led_left: 6, start_corner: 0, clockwise: 1 } as const

    await esp.saveCalibration(layout)
    await esp.previewCalibration(layout)
    await esp.saveMode({ led_mode: 0 })
    await esp.saveNetwork({ wifi_ssid: 'HomeWiFi', wifi_pass: '', ip_octet: 0 })
    await esp.savePower({ power: 0 })
    await esp.identify()
    await esp.restart()

    assert.deepEqual(f.calls.map((c) => [c.url, c.init.body]), [
      ['http://esp/save/calibration', 'led_top=10&led_right=6&led_bottom=10&led_left=6&start_corner=0&clockwise=1'],
      ['http://esp/preview/calibration', 'led_top=10&led_right=6&led_bottom=10&led_left=6&start_corner=0&clockwise=1'],
      ['http://esp/save/mode', 'led_mode=0'],
      ['http://esp/save/network', 'wifi_ssid=HomeWiFi&wifi_pass=&ip_octet=0'],
      ['http://esp/save/power', 'power=0'],
      ['http://esp/identify', undefined],
      ['http://esp/restart', undefined],
    ])
    assert.equal(f.calls[5]?.init.headers, undefined, 'no body, no Content-Type')
  })

  it("throws Esp32Error with the firmware's message on non-2xx", async () => {
    const f = fakeFetch(json(fixtureText('esp32/message-response.bad-ip-octet'), 400))
    const esp = createEsp32Client('http://esp', { fetch: f.fn })

    await assert.rejects(esp.saveNetwork({ ip_octet: 255 }), (e: unknown) => {
      assert.ok(e instanceof Esp32Error)
      assert.equal(e.reason, 'http')
      assert.equal(e.status, 400)
      assert.equal(e.path, '/save/network')
      assert.equal(e.message, 'ip_octet must be 0 (DHCP) or 1-254. Nothing was saved.')
      return true
    })
  })

  it('names the status when a non-2xx reply has no message', async () => {
    const f = fakeFetch(() => new Response('oops', { status: 500 }))
    await assert.rejects(createEsp32Client('http://esp', { fetch: f.fn }).getStatus(), {
      name: 'Esp32Error', reason: 'http', status: 500, message: 'HTTP 500 from /status',
    })
  })

  it('rejects a 2xx reply that is not the expected JSON (e.g. old firmware redirecting to the UI)', async () => {
    const html = () => new Response('<!doctype html>', { status: 200, headers: { 'Content-Type': 'text/html' } })
    const f = fakeFetch(html, json('{"ok":true}'))
    const esp = createEsp32Client('http://esp', { fetch: f.fn })

    await assert.rejects(esp.savePower({ power: 1 }), { reason: 'invalid-response', status: 200, path: '/save/power' })
    await assert.rejects(esp.identify(), { reason: 'invalid-response', path: '/identify' })
  })

  it('times out via AbortController', async () => {
    const hang: typeof fetch = (_input, init) =>
      new Promise((_resolve, reject) => {
        init?.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')))
      })
    const esp = createEsp32Client('http://esp', { fetch: hang, timeoutMs: 20 })
    await assert.rejects(esp.getMetrics(), { reason: 'timeout', status: null, message: 'No reply from /metrics within 20 ms' })
  })

  it('reports network failures', async () => {
    const down: typeof fetch = async () => {
      throw new TypeError('fetch failed')
    }
    await assert.rejects(createEsp32Client('http://esp', { fetch: down }).getConfig(), {
      reason: 'network', status: null, message: 'Request to /config failed: fetch failed',
    })
  })
})

describe('formBody', () => {
  it('leaves out undefined fields and keeps empty strings', () => {
    assert.equal(formBody({ a: 1, b: undefined, c: '', d: 'x y&z' }), 'a=1&c=&d=x+y%26z')
  })
})
