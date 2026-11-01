// Client of the TV service's local message port (tv/service/StatusPort.cs): IPC between the
// two apps of the package on the TV, nothing leaves the device. The messages are built and
// read by @ambilight/protocol (protocol/schemas/service); this file is the Tizen glue:
// the ports, the reply timeouts and the AppControl launch.
//
// Requests carry the launcher's own port (constants.messagePort.uiPort) as the reply port.

import {
  type MessagePortDataItem, type ServiceCommandResult, type ServiceReply,
  buildCommandRequest, buildSetOutputsRequest, buildStatusRequest, constants, parseServiceReply,
} from '@ambilight/protocol';
import type { ServiceApi, ServiceLink } from './api';

const { serviceAppId } = constants.tvApp;
const { servicePort, uiPort } = constants.messagePort;
/** The port is there but the service does not answer. */
const NO_REPLY_MS = 3000;
const COMMAND_TIMEOUT_MS = 3000;

interface PendingCommand {
  id: string;
  resolve: (r: ServiceCommandResult) => void;
  timer: number;
}

export function createServiceClient(tz: TizenApi.Tizen): ServiceApi {
  const listeners = new Set<(link: ServiceLink) => void>();
  const pending: PendingCommand[] = [];
  let nextId = 1;
  let localPort: TizenApi.LocalMessagePort | null = null;
  let portError = '';
  let waitingSince = 0; // first unanswered status request, 0 when the last one was answered

  const emit = (link: ServiceLink) => listeners.forEach((cb) => cb(link));

  // A reply carries the id of its request. A reply without one comes from a service that
  // predates ids and answers in request order: it settles the oldest command. A reply whose
  // id is no longer pending (it timed out) is dropped instead of answering another command.
  const settle = (id: string | null, result: ServiceCommandResult) => {
    const i = id === null ? 0 : pending.findIndex((p) => p.id === id);
    if (i < 0 || i >= pending.length) return;
    const [p] = pending.splice(i, 1);
    clearTimeout(p.timer);
    p.resolve(result);
  };

  try {
    localPort = tz.messageport.requestLocalMessagePort(uiPort);
    localPort.addMessagePortListener((data) => {
      let reply: ServiceReply | null;
      try {
        reply = parseServiceReply(data);
      } catch (e) {
        console.warn('Bad reply from the service', e);
        // A malformed result can't be matched to its command; that command times out.
        return;
      }
      if (reply?.kind === 'status') {
        waitingSince = 0;
        emit({ kind: 'up', status: reply.status });
      } else if (reply?.kind === 'result') {
        settle(reply.id, reply.result);
      }
    });
  } catch (e) {
    portError = e instanceof Error ? e.message : String(e);
  }

  // Throws NotFoundError while the service (or its port) is not up.
  const remote = () => tz.messageport.requestRemoteMessagePort(serviceAppId, servicePort);

  // One command request with a fresh id; resolves with the reply that carries that id.
  const send = (build: (id: string) => MessagePortDataItem[]) =>
    new Promise<ServiceCommandResult>((resolve) => {
      if (!localPort) return resolve({ ok: false, error: 'Message ports are unavailable' });
      let port: TizenApi.RemoteMessagePort;
      try {
        port = remote();
      } catch {
        return resolve({ ok: false, error: 'The service is not running' });
      }
      const entry: PendingCommand = {
        id: String(nextId++),
        resolve,
        timer: window.setTimeout(() => {
          const i = pending.indexOf(entry);
          if (i >= 0) pending.splice(i, 1);
          resolve({ ok: false, error: 'The service did not answer' });
        }, COMMAND_TIMEOUT_MS),
      };
      pending.push(entry);
      try {
        port.sendMessage(build(entry.id), localPort);
      } catch (e) {
        clearTimeout(entry.timer);
        pending.splice(pending.indexOf(entry), 1);
        resolve({ ok: false, error: e instanceof Error ? e.message : String(e) });
      }
    });

  return {
    subscribe(cb) {
      listeners.add(cb);
      return () => listeners.delete(cb);
    },

    requestStatus() {
      if (!localPort) {
        emit({ kind: 'down', reason: 'unavailable', detail: portError });
        return;
      }
      let port: TizenApi.RemoteMessagePort;
      try {
        port = remote();
      } catch {
        waitingSince = 0;
        emit({ kind: 'down', reason: 'not-running' });
        return;
      }
      try {
        port.sendMessage(buildStatusRequest(), localPort);
      } catch (e) {
        emit({ kind: 'down', reason: 'not-responding', detail: e instanceof Error ? e.message : String(e) });
        return;
      }
      const now = Date.now();
      if (!waitingSince) waitingSince = now;
      else if (now - waitingSince > NO_REPLY_MS) emit({ kind: 'down', reason: 'not-responding' });
    },

    command: (command) => send((id) => buildCommandRequest(command, { id })),
    setOutputs: (outputs) => send((id) => buildSetOutputsRequest(outputs, { id })),

    start() {
      return new Promise<void>((resolve, reject) => {
        try {
          const control = new tz.ApplicationControl(
            'http://tizen.org/appcontrol/operation/default',
            null, null, null,
            [new tz.ApplicationControlData('command', ['start'])],
          );
          tz.application.launchAppControl(control, serviceAppId, () => resolve(), (err) => reject(new Error(err.message)));
        } catch (e) {
          reject(e instanceof Error ? e : new Error(String(e)));
        }
      });
    },
  };
}
