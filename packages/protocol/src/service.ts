// TV UI ↔ TV service messages over Tizen MessagePort (protocol/schemas/service).
//
//   const local = tizen.messageport.requestLocalMessagePort(constants.messagePort.uiPort)
//   local.addMessagePortListener((data) => { const reply = parseServiceReply(data); … })
//   tizen.messageport
//     .requestRemoteMessagePort(constants.tvApp.serviceAppId, constants.messagePort.servicePort)
//     .sendMessage(buildStatusRequest(), local)
import { constants } from './generated/constants.ts'
import { CaptureFailureValues, CaptureStateValues, OutputsSourceValues, ServiceCommandValues } from './generated/types.ts'
import type { ServiceCommand, ServiceCommandResult, ServiceOutputs, ServiceStatus } from './generated/types.ts'

/** One entry of a MessagePort message, as tizen.messageport sends and delivers it. */
export interface MessagePortDataItem {
  key: string
  value: string | string[] | number[] | number[][]
}

export type ServiceReply =
  | { kind: 'status'; status: ServiceStatus }
  /** `id`: the command request's id, echoed by the service; null if the request had none. */
  | { kind: 'result'; id: string | null; result: ServiceCommandResult }

/** Options of a command request. */
export interface CommandRequestOptions {
  /**
   * Request id (at most 64 characters), echoed in the reply's `id`, so a reply that arrives
   * late can be matched to its request. Left out: the reply has no id either.
   */
  id?: string
}

/** Longest request id the service accepts (command-request.schema.json `commandId`). */
export const MAX_COMMAND_ID_LENGTH = 64

/** A reply that doesn't match protocol/schemas/service. */
export class ServiceReplyError extends Error {
  /**
   * Which reply was malformed, a `status` or a `result` one (e.g. to fail the pending command
   * on a bad `result`); null when that isn't known (a message with both keys).
   */
  readonly reply: ServiceReply['kind'] | null

  constructor(message: string, reply: ServiceReply['kind'] | null = null) {
    super(message)
    this.name = 'ServiceReplyError'
    this.reply = reply
  }
}

const mp = constants.messagePort

/** {request: "status"} — the service answers with a `status` reply. */
export function buildStatusRequest(): MessagePortDataItem[] {
  return [{ key: mp.requestKey, value: 'status' }]
}

/** {request: "command", command, id?} — the service answers with a `result` reply. */
export function buildCommandRequest(command: ServiceCommand, options: CommandRequestOptions = {}): MessagePortDataItem[] {
  if (!(ServiceCommandValues as readonly string[]).includes(command)) {
    throw new TypeError(`Unknown service command: ${String(command)}`)
  }
  return [
    { key: mp.requestKey, value: 'command' },
    { key: mp.commandKey, value: command },
    ...idItem(options.id),
  ]
}

/**
 * {request: "command", command: "set-outputs", outputs: "<JSON>", id?} — turns the outputs on or
 * off and sets the HyperHDR target, live, and saves it on the TV. Only the keys of
 * ServiceOutputs are sent (extra properties of the object are left out). The service checks
 * the values (e.g. a host while HyperHDR is enabled) and answers ok: false if one is invalid;
 * this throws TypeError only for what isn't ServiceOutputs at all.
 */
export function buildSetOutputsRequest(outputs: ServiceOutputs, options: CommandRequestOptions = {}): MessagePortDataItem[] {
  const { esp32, hyperhdr } = outputs ?? ({} as Partial<ServiceOutputs>)
  if (typeof esp32?.enabled !== 'boolean') throw new TypeError('outputs.esp32.enabled must be a boolean')
  if (typeof hyperhdr?.enabled !== 'boolean') throw new TypeError('outputs.hyperhdr.enabled must be a boolean')
  if (typeof hyperhdr.host !== 'string') throw new TypeError('outputs.hyperhdr.host must be a string')
  if (typeof hyperhdr.port !== 'number') throw new TypeError('outputs.hyperhdr.port must be a number')
  if (hyperhdr.priority !== undefined && typeof hyperhdr.priority !== 'number') {
    throw new TypeError('outputs.hyperhdr.priority must be a number')
  }
  const payload: ServiceOutputs = {
    esp32: { enabled: esp32.enabled },
    hyperhdr: {
      enabled: hyperhdr.enabled,
      host: hyperhdr.host,
      port: hyperhdr.port,
      ...(hyperhdr.priority !== undefined ? { priority: hyperhdr.priority } : {}),
    },
  }
  return [
    { key: mp.requestKey, value: 'command' },
    { key: mp.commandKey, value: 'set-outputs' },
    { key: mp.outputsKey, value: JSON.stringify(payload) },
    ...idItem(options.id),
  ]
}

function idItem(id: string | undefined): MessagePortDataItem[] {
  if (id === undefined) return []
  if (typeof id !== 'string' || id.length > MAX_COMMAND_ID_LENGTH) {
    throw new TypeError(`Command id must be a string of at most ${MAX_COMMAND_ID_LENGTH} characters`)
  }
  return [{ key: mp.idKey, value: id }]
}

/**
 * Reads a message received on the UI port. Returns null for a message that is neither a
 * `status` nor a `result` reply; throws ServiceReplyError for a malformed one.
 */
export function parseServiceReply(data: readonly MessagePortDataItem[]): ServiceReply | null {
  const status = data.find((item) => item.key === mp.statusKey)
  const result = data.find((item) => item.key === mp.resultKey)
  if (status && result) throw new ServiceReplyError(`Reply has both "${mp.statusKey}" and "${mp.resultKey}"`)
  if (status) return { kind: 'status', status: tagged('status', () => parseServiceStatus(stringValue(status))) }
  if (result) {
    const idEntry = data.find((item) => item.key === mp.idKey)
    const id = idEntry ? tagged('result', () => stringValue(idEntry)) : null
    return { kind: 'result', id, result: tagged('result', () => parseCommandResult(stringValue(result))) }
  }
  return null
}

/** Parses and checks the ServiceStatus JSON (structure and enums, not every optional field). */
export function parseServiceStatus(json: string): ServiceStatus {
  return tagged('status', () => checkServiceStatus(parseObject(json, 'status')))
}

/** Parses and checks the ServiceCommandResult JSON. */
export function parseCommandResult(json: string): ServiceCommandResult {
  return tagged('result', () => checkCommandResult(parseObject(json, 'result')))
}

type Json = Record<string, unknown>

function checkServiceStatus(s: Json): ServiceStatus {
  expect(s.version === null || typeof s.version === 'string', 'status.version')
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

  const o = object(s.outputs, 'status.outputs')
  expect(oneOf(o.source, OutputsSourceValues), 'status.outputs.source')
  expect(typeof object(o.esp32, 'status.outputs.esp32').enabled === 'boolean', 'status.outputs.esp32.enabled')
  const oh = object(o.hyperhdr, 'status.outputs.hyperhdr')
  expect(typeof oh.enabled === 'boolean' && typeof oh.host === 'string', 'status.outputs.hyperhdr')
  expect(typeof oh.port === 'number' && typeof oh.priority === 'number', 'status.outputs.hyperhdr port / priority')
  return s as unknown as ServiceStatus
}

function checkCommandResult(r: Json): ServiceCommandResult {
  expect(typeof r.ok === 'boolean', 'result.ok')
  expect(r.error === null || typeof r.error === 'string', 'result.error')
  return r as unknown as ServiceCommandResult
}

/** Runs `read`, marking a ServiceReplyError it throws as one about a `reply` reply. */
function tagged<T>(reply: ServiceReply['kind'], read: () => T): T {
  try {
    return read()
  } catch (e) {
    throw e instanceof ServiceReplyError && e.reply === null ? new ServiceReplyError(e.message, reply) : e
  }
}

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
