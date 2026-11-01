import { useState } from 'react'
import type { Esp32Status, Esp32Metrics, Esp32Message } from '@ambilight/protocol'
import { Stat } from '../components/ui/Stat'
import { esp } from '../esp'

function fmtUptime(s: number): string {
  const h   = Math.floor(s / 3600)
  const m   = Math.floor((s % 3600) / 60)
  const sec = s % 60
  return `${h}h ${m}m ${sec}s`
}

function fmtKB(bytes: number): string {
  return `${(bytes / 1024).toFixed(1)} KB`
}

// last_frame_ms: -1 = no frame since boot
function fmtLastFrame(ms: number): string {
  if (ms < 0) return 'none'
  const s = Math.floor(ms / 1000)
  return `${s < 60 ? `${s} s` : fmtUptime(s)} ago`
}

interface StatusTabProps {
  status:  Esp32Status  | null
  metrics: Esp32Metrics | null
  power:   boolean | null   // null until /config is loaded
  savingPower:   boolean
  onPowerToggle: () => void
}

export function StatusTab({ status, metrics, power, savingPower, onPowerToggle }: StatusTabProps) {
  const [busy,    setBusy]    = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function run(action: () => Promise<Esp32Message>) {
    setBusy(true)
    setMessage(null)
    try {
      setMessage((await action()).message)
    } catch (err) {
      setMessage(`Error: ${err instanceof Error ? err.message : String(err)}`)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <section className="card">
        <div className="switch-row">
          <h2>Lighting {power === null ? '' : power ? 'on' : 'off'}</h2>
          <button
            type="button"
            role="switch"
            aria-checked={power === true}
            aria-label="Lighting"
            className={`switch${power ? ' on' : ''}`}
            onClick={onPowerToggle}
            disabled={power === null || savingPower}
          />
        </div>
      </section>

      {status && (
        <section className="card">
          <h2>Network</h2>
          <div className="grid2">
            <Stat label="IP Address" value={status.ip}                                 />
            <Stat label="Mode"       value={status.mode}                               />
            <Stat label="SSID"       value={status.ssid || '—'}                        />
            <Stat label="Signal"     value={status.rssi ? `${status.rssi} dBm` : '—'} />
            <Stat label="Firmware"   value={status.version ? `v${status.version}` : '—'} />
          </div>
        </section>
      )}

      {metrics && (
        <section className="card">
          <h2>System</h2>
          <div className="grid2">
            <Stat label="Free Heap"  value={fmtKB(metrics.heap_free)}    />
            <Stat label="Total Heap" value={fmtKB(metrics.heap_total)}   />
            <Stat label="Min Heap"   value={fmtKB(metrics.heap_min)}     />
            <Stat label="CPU"        value={`${metrics.cpu_mhz} MHz`}    />
            <Stat label="Uptime"     value={fmtUptime(metrics.uptime_s)} />
            <Stat label="LED Power (est.)"
                  value={`${metrics.power_w.toFixed(0)} W${metrics.power_limited ? ' · limited' : ''}`} />
            <Stat label="Frames Received" value={String(metrics.frames_rx)}             />
            <Stat label="Last Frame"      value={fmtLastFrame(metrics.last_frame_ms)} />
          </div>
        </section>
      )}

      <section className="card">
        <h2>Device</h2>
        <div className="grid2">
          <button type="button" className="btn-apply" disabled={busy}
                  onClick={() => run(esp.identify)}>
            Blink to identify
          </button>
          <button type="button" disabled={busy}
                  onClick={() => { if (window.confirm('Restart the controller?')) run(esp.restart) }}>
            Restart
          </button>
        </div>
        {message && <div className="toast">{message}</div>}
      </section>
    </>
  )
}
