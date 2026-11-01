import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { readFileSync } from 'node:fs'
import {
  MAX_COMMAND_ID_LENGTH,
  ServiceReplyError,
  buildCommandRequest,
  buildSetOutputsRequest,
  buildStatusRequest,
  constants,
  parseCommandResult,
  parseServiceReply,
  parseServiceStatus,
} from '../src/index.ts'
import type { MessagePortDataItem, ServiceCommand, ServiceOutputs } from '../src/index.ts'
import { fixture, fixtureText } from './fixtures.ts'

const schemaFile = (name: string) =>
  JSON.parse(readFileSync(new URL(`../../../protocol/schemas/service/${name}.schema.json`, import.meta.url), 'utf8'))

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
    assert.throws(() => buildCommandRequest('set-outputs' as ServiceCommand), TypeError)
  })

  it('adds the request id when given', () => {
    assert.deepEqual(buildCommandRequest('resume', { id: 'ui-1730459000123-4' }), asItems(fixture('service/command-request.with-id')))
    assert.deepEqual(buildCommandRequest('pause', {}), asItems(fixture('service/command-request')))
  })

  it('rejects ids the service would not echo', () => {
    assert.equal(MAX_COMMAND_ID_LENGTH, schemaFile('command-request').$defs.commandId.maxLength)
    assert.doesNotThrow(() => buildCommandRequest('pause', { id: 'x'.repeat(MAX_COMMAND_ID_LENGTH) }))
    assert.throws(() => buildCommandRequest('pause', { id: 'x'.repeat(MAX_COMMAND_ID_LENGTH + 1) }), TypeError)
    assert.throws(() => buildCommandRequest('pause', { id: 7 as unknown as string }), TypeError)
  })

  it('builds the set-outputs request', () => {
    const outputs = fixture<ServiceOutputs>('service/outputs')
    assert.deepEqual(buildSetOutputsRequest(outputs, { id: '7' }), asItems(fixture('service/command-request.set-outputs')))
  })

  it('sends only the ServiceOutputs keys, priority only when given', () => {
    const items = buildSetOutputsRequest({
      esp32: { enabled: false, hosts: ['10.0.0.2'] } as ServiceOutputs['esp32'],
      hyperhdr: { enabled: true, host: 'hyperhdr.local', port: 19401, endpoint: 'x' } as ServiceOutputs['hyperhdr'],
    })
    assert.deepEqual(items.map((i) => i.key), ['request', 'command', 'outputs'])
    assert.deepEqual(JSON.parse(items[2]!.value as string), fixture('service/outputs.hyperhdr-only'))
  })

  it('rejects what is not ServiceOutputs at all', () => {
    const good = fixture<ServiceOutputs>('service/outputs')
    const bad: unknown[] = [
      null,
      {},
      { ...good, esp32: { enabled: 'yes' } },
      { ...good, hyperhdr: { ...good.hyperhdr, host: undefined } },
      { ...good, hyperhdr: { ...good.hyperhdr, port: '19400' } },
      { ...good, hyperhdr: { ...good.hyperhdr, priority: null } },
    ]
    for (const outputs of bad) assert.throws(() => buildSetOutputsRequest(outputs as ServiceOutputs), TypeError)
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

  it('parses a command reply without id (older services, requests without id)', () => {
    const reply = parseServiceReply(asItems(fixture('service/command-reply')))
    assert.deepEqual(reply, { kind: 'result', id: null, result: { ok: true, error: null } })
  })

  it('parses a command reply with id', () => {
    const reply = parseServiceReply(asItems(fixture('service/command-reply.with-id')))
    assert.deepEqual(reply, {
      kind: 'result',
      id: '7',
      result: { ok: false, error: 'invalid outputs: hyperhdr.host is required when hyperhdr.enabled is true' },
    })
  })

  it('rejects a reply whose id is not a string, as a bad result', () => {
    assert.throws(
      () => parseServiceReply([{ key: 'result', value: '{"ok":true,"error":null}' }, { key: 'id', value: ['7'] }]),
      (e) => e instanceof ServiceReplyError && e.reply === 'result',
    )
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
  for (const name of ['service/status', 'service/status.failed', 'service/status.paused', 'service/status.unknown-version']) {
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
    ['a missing outputs', (s) => { delete s.outputs }],
    ['an unknown outputs.source', (s) => { s.outputs.source = 'cloud' }],
    ['outputs.hyperhdr without priority', (s) => { delete s.outputs.hyperhdr.priority }],
    ['outputs.esp32 that is not an object', (s) => { s.outputs.esp32 = true }],
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
