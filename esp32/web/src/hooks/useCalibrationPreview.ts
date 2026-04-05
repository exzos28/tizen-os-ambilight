import { useRef, useCallback } from 'react'
import { previewCalibration } from '../api'
import type { CalibrationRequest } from '../api'

export function useCalibrationPreview(delayMs = 300) {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const preview = useCallback((params: CalibrationRequest) => {
    if (timerRef.current) clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => {
      previewCalibration(params).catch(console.error)
    }, delayMs)
  }, [delayMs])

  return preview
}
