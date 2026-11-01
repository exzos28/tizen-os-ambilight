import { useState, useEffect } from 'react'
import type { Esp32Status, Esp32Metrics } from '@ambilight/protocol'
import { esp } from '../esp'

export function useDeviceData() {
  const [status,  setStatus]  = useState<Esp32Status  | null>(null)
  const [metrics, setMetrics] = useState<Esp32Metrics | null>(null)

  useEffect(() => {
    esp.getStatus().then(setStatus).catch(console.error)
    esp.getMetrics().then(setMetrics).catch(console.error)

    const id = setInterval(() => {
      esp.getMetrics().then(setMetrics).catch(console.error)
    }, 5000)
    return () => clearInterval(id)
  }, [])

  return { status, metrics }
}
