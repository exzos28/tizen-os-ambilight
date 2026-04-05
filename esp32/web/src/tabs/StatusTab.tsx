import { Stat } from '../components/ui/Stat'
import type { StatusResponse, MetricsResponse } from '../api'

function fmtUptime(s: number): string {
  const h   = Math.floor(s / 3600)
  const m   = Math.floor((s % 3600) / 60)
  const sec = s % 60
  return `${h}h ${m}m ${sec}s`
}

function fmtKB(bytes: number): string {
  return `${(bytes / 1024).toFixed(1)} KB`
}

interface StatusTabProps {
  status:  StatusResponse  | null
  metrics: MetricsResponse | null
}

export function StatusTab({ status, metrics }: StatusTabProps) {
  return (
    <>
      {status && (
        <section className="card">
          <h2>Network</h2>
          <div className="grid2">
            <Stat label="IP Address" value={status.ip}                                 />
            <Stat label="Mode"       value={status.mode}                               />
            <Stat label="SSID"       value={status.ssid || '—'}                        />
            <Stat label="Signal"     value={status.rssi ? `${status.rssi} dBm` : '—'} />
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
          </div>
        </section>
      )}
    </>
  )
}
