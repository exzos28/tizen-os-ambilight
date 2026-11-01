import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  ServiceReplyError,
  buildCommandRequest,
  buildStatusRequest,
  constants,
  parseCommandResult,
  parseServiceReply,
  parseServiceStatus,
} from '../src/index.ts'
import type { MessagePortDataItem, ServiceCommand } from '../src/index.ts'
import { fixture, fixtureText } from './fixtures.ts'

// A bundle fixture ({key: value}) as the [{key, value}] array tizen.messageport delivers.
const asItems = (bundle: Record<string, string>): MessagePortDataItem[] =>
  Object.entries(bundle).map(([key, value]) => ({ key, value }))

describe('requests', () => {
  it('builds the status request', () => {
    assert.deepEqual(buildStatusRequest(), asItems(fixture('service/status-request')))
  })

  it('builds command requests', () => {
    assert.deepEqual(buildCommandRequest('pause'), asItems(fixture('service/command-request')))
    assert.deepEqual(buildCommandRequest('rediscover'), [
      { key: 'request', value: 'command' },
      { key: 'command', value: 'rediscover' },
    ])
  })

  it('rejects unknown commands from untyped callers', () => {
    assert.throws(() => buildCommandRequest('reboot' as ServiceCommand), TypeError)
  })

  it('uses the message port names from constants.json', () => {
    assert.equal(constants.messagePort.servicePort, 'ambilight.status')
    assert.equal(constants.messagePort.uiPort, 'ambilight.ui')
    assert.equal(constants.tvApp.serviceAppId, 'fCAYP0k0uD.Service')
  })
})

describe('parseServiceReply', () => {
  it('parses a status reply', () => {
    const reply = parseServiceReply(asItems(fixture('service/status-reply')))
    assert.deepEqual(reply, { kind: 'status', status: fixture('service/status') })
  })

  it('parses a command reply', () => {
    const reply = parseServiceReply(asItems(fixture('service/command-reply')))
    assert.deepEqual(reply, { kind: 'result', result: { ok: true, error: null } })
  })

  it('returns null for other messages', () => {
    assert.equal(parseServiceReply([{ key: 'hello', value: 'world' }]), null)
    assert.equal(parseServiceReply([]), null)
  })

  it('rejects malformed replies', () => {
    assert.throws(() => parseServiceReply([{ key: 'status', value: '{' }]), ServiceReplyError)
    assert.throws(() => parseServiceReply([{ key: 'status', value: ['{}'] }]), ServiceReplyError)
    assert.throws(
      () => parseServiceReply([{ key: 'status', value: '{}' }, { key: 'result', value: '{}' }]),
      ServiceReplyError,
    )
  })

  it('tells which reply was malformed', () => {
    const replyOf = (data: MessagePortDataItem[]) => {
      try {
        parseServiceReply(data)
      } catch (e) {
        assert.ok(e instanceof ServiceReplyError)
        return e.reply
      }
      return assert.fail('no ServiceReplyError')
    }
    assert.equal(replyOf([{ key: 'status', value: '{' }]), 'status')
    assert.equal(replyOf([{ key: 'status', value: '{"version":1}' }]), 'status')
    assert.equal(replyOf([{ key: 'result', value: 'nope' }]), 'result')
    assert.equal(replyOf([{ key: 'result', value: ['{}'] }]), 'result')
    assert.equal(replyOf([{ key: 'result', value: '{"error":null}' }]), 'result')
    assert.equal(replyOf([{ key: 'status', value: '{}' }, { key: 'result', value: '{}' }]), null)
  })
})

describe('parseServiceStatus', () => {
  for (const name of ['service/status', 'service/status.failed', 'service/status.paused']) {
    it(`accepts ${name}`, () => {
      assert.deepEqual(parseServiceStatus(fixtureText(name)), fixture(name))
    })
  }

  const running = () => fixture<Record<string, any>>('service/status')
  const bad: [string, (s: Record<string, any>) => void][] = [
    ['an unknown capture state', (s) => { s.capture.state = 'sleeping' }],
    ['an unknown failure', (s) => { s.capture.failure = 'cosmic-rays' }],
    ['a missing version', (s) => { delete s.version }],
    ['esp32.enabled that is not boolean', (s) => { s.esp32.enabled = 'yes' }],
    ['enabled esp32 without hosts', (s) => { delete s.esp32.hosts }],
    ['enabled hyperhdr without endpoint', (s) => { s.hyperhdr = { enabled: true, connected: false } }],
    ['configWarnings that are not strings', (s) => { s.configWarnings = [1] }],
    ['rgb without h/v', (s) => { delete s.capture.h }],
    ['an array', (s) => { s.capture = [] }],
  ]
  for (const [what, mutate] of bad) {
    it(`rejects ${what}`, () => {
      const s = running()
      mutate(s)
      assert.throws(() => parseServiceStatus(JSON.stringify(s)), ServiceReplyError)
    })
  }
})

describe('parseCommandResult', () => {
  it('accepts both outcomes', () => {
    assert.deepEqual(parseCommandResult(fixtureText('service/command-result')), { ok: true, error: null })
    assert.deepEqual(parseCommandResult(fixtureText('service/command-result.error')), fixture('service/command-result.error'))
  })

  it('rejects a result without ok', () => {
    assert.throws(() => parseCommandResult('{"error":null}'), (e) => e instanceof ServiceReplyError && e.reply === 'result')
    assert.throws(() => parseServiceStatus('[]'), (e) => e instanceof ServiceReplyError && e.reply === 'status')
  })
})
