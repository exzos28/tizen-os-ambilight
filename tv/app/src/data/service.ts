// Client of the TV service's local message port (tv/service/StatusPort.cs): IPC between the
// two apps of the package on the TV, nothing leaves the device.
//
//   status:  {request: "status"}                    → reply key "status" = JSON ServiceStatus
//   command: {request: "command", command: <name>}  → reply key "result" = JSON CommandResult
//
// Requests carry the launcher's own port "ambilight.ui" as the reply port.

import type { ServiceApi, ServiceLink } from './api';
import type { CommandResult, ServiceCommand, ServiceStatus } from './types';

export const SERVICE_APP_ID = 'fCAYP0k0uD.Service';
const STATUS_PORT = 'ambilight.status';
const UI_PORT = 'ambilight.ui';
/** The port is there but the service does not answer. */
const NO_REPLY_MS = 3000;
const COMMAND_TIMEOUT_MS = 3000;

interface PendingCommand {
  resolve: (r: CommandResult) => void;
  timer: number;
}

export function createServiceClient(tz: TizenApi.Tizen): ServiceApi {
  const listeners = new Set<(link: ServiceLink) => void>();
  const pending: PendingCommand[] = [];
  let localPort: TizenApi.LocalMessagePort | null = null;
  let portError = '';
  let waitingSince = 0; // first unanswered status request, 0 when the last one was answered

  const emit = (link: ServiceLink) => listeners.forEach((cb) => cb(link));

  try {
    localPort = tz.messageport.requestLocalMessagePort(UI_PORT);
    localPort.addMessagePortListener((data) => {
      for (const item of data) {
        if (item.key === 'status') {
          let status: ServiceStatus;
          try {
            status = JSON.parse(String(item.value)) as ServiceStatus;
          } catch (e) {
            console.warn('Bad status reply', e);
            continue;
          }
          waitingSince = 0;
          emit({ kind: 'up', status });
        } else if (item.key === 'result') {
          // Replies come in request order; the port has no request ids.
          const p = pending.shift();
          if (!p) continue;
          clearTimeout(p.timer);
          try {
            p.resolve(JSON.parse(String(item.value)) as CommandResult);
          } catch {
            p.resolve({ ok: false, error: 'Bad reply from the service' });
          }
        }
      }
    });
  } catch (e) {
    portError = e instanceof Error ? e.message : String(e);
  }

  // Throws NotFoundError while the service (or its port) is not up.
  const remote = () => tz.messageport.requestRemoteMessagePort(SERVICE_APP_ID, STATUS_PORT);

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
        port.sendMessage([{ key: 'request', value: 'status' }], localPort);
      } catch (e) {
        emit({ kind: 'down', reason: 'not-responding', detail: e instanceof Error ? e.message : String(e) });
        return;
      }
      const now = Date.now();
      if (!waitingSince) waitingSince = now;
      else if (now - waitingSince > NO_REPLY_MS) emit({ kind: 'down', reason: 'not-responding' });
    },

    command(command: ServiceCommand) {
      return new Promise<CommandResult>((resolve) => {
        if (!localPort) return resolve({ ok: false, error: 'Message ports are unavailable' });
        let port: TizenApi.RemoteMessagePort;
        try {
          port = remote();
        } catch {
          return resolve({ ok: false, error: 'The service is not running' });
        }
        const entry: PendingCommand = {
          resolve,
          timer: window.setTimeout(() => {
            const i = pending.indexOf(entry);
            if (i >= 0) pending.splice(i, 1);
            resolve({ ok: false, error: 'The service did not answer' });
          }, COMMAND_TIMEOUT_MS),
        };
        pending.push(entry);
        try {
          port.sendMessage([
            { key: 'request', value: 'command' },
            { key: 'command', value: command },
          ], localPort);
        } catch (e) {
          clearTimeout(entry.timer);
          pending.splice(pending.indexOf(entry), 1);
          resolve({ ok: false, error: e instanceof Error ? e.message : String(e) });
        }
      });
    },

    start() {
      return new Promise<void>((resolve, reject) => {
        try {
          const control = new tz.ApplicationControl(
            'http://tizen.org/appcontrol/operation/default',
            null, null, null,
            [new tz.ApplicationControlData('command', ['start'])],
          );
          tz.application.launchAppControl(control, SERVICE_APP_ID, () => resolve(), (err) => reject(new Error(err.message)));
        } catch (e) {
          reject(e instanceof Error ? e : new Error(String(e)));
        }
      });
    },
  };
}
