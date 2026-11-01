// TV UI ↔ TV service messages over Tizen MessagePort (protocol/schemas/service).
//
//   const local = tizen.messageport.requestLocalMessagePort(constants.messagePort.uiPort)
//   local.addMessagePortListener((data) => { const reply = parseServiceReply(data); … })
//   tizen.messageport
//     .requestRemoteMessagePort(constants.tvApp.serviceAppId, constants.messagePort.servicePort)
//     .sendMessage(buildStatusRequest(), local)
import { constants } from './generated/constants.ts'
import { CaptureFailureValues, CaptureStateValues, ServiceCommandValues } from './generated/types.ts'
import type { ServiceCommand, ServiceCommandResult, ServiceStatus } from './generated/types.ts'

/** One entry of a MessagePort message, as tizen.messageport sends and delivers it. */
export interface MessagePortDataItem {
  key: string
  value: string | string[] | number[] | number[][]
}

export type ServiceReply =
  | { kind: 'status'; status: ServiceStatus }
  | { kind: 'result'; result: ServiceCommandResult }

/** A reply that doesn't match protocol/schemas/service. */
export class ServiceReplyError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'ServiceReplyError'
  }
}

const mp = constants.messagePort

/** {request: "status"} — the service answers with a `status` reply. */
export function buildStatusRequest(): MessagePortDataItem[] {
  return [{ key: mp.requestKey, value: 'status' }]
}

/** {request: "command", command} — the service answers with a `result` reply. */
export function buildCommandRequest(command: ServiceCommand): MessagePortDataItem[] {
  if (!(ServiceCommandValues as readonly string[]).includes(command)) {
    throw new TypeError(`Unknown service command: ${String(command)}`)
  }
  return [
    { key: mp.requestKey, value: 'command' },
    { key: mp.commandKey, value: command },
  ]
}

/**
 * Reads a message received on the UI port. Returns null for a message that is neither a
 * `status` nor a `result` reply; throws ServiceReplyError for a malformed one.
 */
export function parseServiceReply(data: readonly MessagePortDataItem[]): ServiceReply | null {
  const status = data.find((item) => item.key === mp.statusKey)
  const result = data.find((item) => item.key === mp.resultKey)
  if (status && result) throw new ServiceReplyError(`Reply has both "${mp.statusKey}" and "${mp.resultKey}"`)
  if (status) return { kind: 'status', status: parseServiceStatus(stringValue(status)) }
  if (result) return { kind: 'result', result: parseCommandResult(stringValue(result)) }
  return null
}

/** Parses and checks the ServiceStatus JSON (structure and enums, not every optional field). */
export function parseServiceStatus(json: string): ServiceStatus {
  const s = parseObject(json, 'status')
  expect(typeof s.version === 'string', 'status.version')
  expect(Array.isArray(s.configWarnings) && s.configWarnings.every((w) => typeof w === 'string'), 'status.configWarnings')

  const c = object(s.capture, 'status.capture')
  expect(oneOf(c.state, CaptureStateValues), 'status.capture.state')
  expect(c.failure === null || oneOf(c.failure, CaptureFailureValues), 'status.capture.failure')
  expect(c.error === null || typeof c.error === 'string', 'status.capture.error')
  expect(typeof c.failedAttempts === 'number', 'status.capture.failedAttempts')
  expect(typeof c.frames === 'number', 'status.capture.frames')
  expect(c.rgb === undefined || (typeof c.rgb === 'string' && typeof c.h === 'number' && typeof c.v === 'number'), 'status.capture.rgb')

  const e = object(s.esp32, 'status.esp32')
  expect(typeof e.enabled === 'boolean', 'status.esp32.enabled')
  if (e.enabled) {
    expect(Array.isArray(e.hosts) && e.hosts.every((h) => typeof h === 'string'), 'status.esp32.hosts')
    expect(typeof e.framesSent === 'number' && typeof e.sendErrors === 'number', 'status.esp32 counters')
  }

  const h = object(s.hyperhdr, 'status.hyperhdr')
  expect(typeof h.enabled === 'boolean', 'status.hyperhdr.enabled')
  if (h.enabled) {
    expect(typeof h.endpoint === 'string' && typeof h.connected === 'boolean', 'status.hyperhdr')
  }
  return s as unknown as ServiceStatus
}

/** Parses and checks the ServiceCommandResult JSON. */
export function parseCommandResult(json: string): ServiceCommandResult {
  const r = parseObject(json, 'result')
  expect(typeof r.ok === 'boolean', 'result.ok')
  expect(r.error === null || typeof r.error === 'string', 'result.error')
  return r as unknown as ServiceCommandResult
}

type Json = Record<string, unknown>

function stringValue(item: MessagePortDataItem): string {
  if (typeof item.value !== 'string') throw new ServiceReplyError(`"${item.key}" is not a string`)
  return item.value
}

function parseObject(json: string, what: string): Json {
  let value: unknown
  try {
    value = JSON.parse(json)
  } catch (e) {
    throw new ServiceReplyError(`${what} is not JSON: ${e instanceof Error ? e.message : String(e)}`)
  }
  return object(value, what)
}

function object(value: unknown, what: string): Json {
  expect(typeof value === 'object' && value !== null && !Array.isArray(value), what)
  return value as Json
}

function oneOf(value: unknown, values: readonly string[]): boolean {
  return typeof value === 'string' && values.includes(value)
}

function expect(ok: boolean, what: string): void {
  if (!ok) throw new ServiceReplyError(`Unexpected ${what} in the service reply`)
}
