import { useState, useEffect } from 'react'
import { getStatus, getMetrics } from '../api'
import type { StatusResponse, MetricsResponse } from '../api'

export function useDeviceData() {
  const [status,  setStatus]  = useState<StatusResponse  | null>(null)
  const [metrics, setMetrics] = useState<MetricsResponse | null>(null)

  useEffect(() => {
    getStatus().then(setStatus).catch(console.error)
    getMetrics().then(setMetrics).catch(console.error)

    const id = setInterval(() => {
      getMetrics().then(setMetrics).catch(console.error)
    }, 5000)
    return () => clearInterval(id)
  }, [])

  return { status, metrics }
}
