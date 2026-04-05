import { useState } from 'react'
import { Stat } from '../components/ui/Stat'
import { CalibrationDiagram } from '../components/CalibrationDiagram'
import { useCalibrationPreview } from '../hooks/useCalibrationPreview'
import { saveCalibration } from '../api'
import type { CalibrationRequest } from '../api'

const CORNER_NAMES = ['Top-Left', 'Top-Right', 'Bot-Right', 'Bot-Left']

interface CalibrationTabProps {
  ledMode:     number
  ledTop:      number;   setLedTop:      (v: number) => void
  ledRight:    number;   setLedRight:    (v: number) => void
  ledBottom:   number;   setLedBottom:   (v: number) => void
  ledLeft:     number;   setLedLeft:     (v: number) => void
  startCorner: number;   setStartCorner: (v: number) => void
  clockwise:   boolean;  setClockwise:   (fn: (prev: boolean) => boolean) => void
}

export function CalibrationTab({
  ledMode,
  ledTop,      setLedTop,
  ledRight,    setLedRight,
  ledBottom,   setLedBottom,
  ledLeft,     setLedLeft,
  startCorner, setStartCorner,
  clockwise,   setClockwise,
}: CalibrationTabProps) {
  const [saving,  setSaving]  = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const preview = useCalibrationPreview()

  function makeParams(
    top    = ledTop,    right  = ledRight,
    bottom = ledBottom, left   = ledLeft,
    corner = startCorner, cw   = clockwise,
  ): CalibrationRequest {
    return { led_top: top, led_right: right, led_bottom: bottom, led_left: left, start_corner: corner, clockwise: cw ? 1 : 0 }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setMessage(null)
    try {
      const res = await saveCalibration(makeParams())
      setMessage(res.message)
    } catch {
      setMessage('Error: could not save calibration.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <section className="card">
        <h2>Strip Layout</h2>
        <CalibrationDiagram
          ledTop={ledTop}         ledRight={ledRight}
          ledBottom={ledBottom}   ledLeft={ledLeft}
          startCorner={startCorner} clockwise={clockwise}
          onTopChange={v    => { setLedTop(v);    preview(makeParams(v)) }}
          onRightChange={v  => { setLedRight(v);  preview(makeParams(ledTop, v)) }}
          onBottomChange={v => { setLedBottom(v); preview(makeParams(ledTop, ledRight, v)) }}
          onLeftChange={v   => { setLedLeft(v);   preview(makeParams(ledTop, ledRight, ledBottom, v)) }}
          onCornerClick={c  => { setStartCorner(c); preview(makeParams(ledTop, ledRight, ledBottom, ledLeft, c)) }}
          onDirectionToggle={() => {
            setClockwise(cw => {
              const next = !cw
              preview(makeParams(ledTop, ledRight, ledBottom, ledLeft, startCorner, next))
              return next
            })
          }}
        />
      </section>

      <section className="card">
        <h2>Summary</h2>
        <div className="grid2">
          <Stat label="Total LEDs" value={String(ledTop + ledRight + ledBottom + ledLeft)} />
          <Stat label="Start"      value={CORNER_NAMES[startCorner]}                       />
          <Stat label="Direction"  value={clockwise ? 'Clockwise' : 'Counter-CW'}          />
          <Stat label="Mode"       value={ledMode === 1 ? 'Dynamic' : 'Plain'}             />
        </div>
      </section>

      <button type="submit" disabled={saving} className="btn-apply">
        {saving ? 'Saving…' : 'Save Calibration'}
      </button>

      {message && <div className="toast">{message}</div>}
    </form>
  )
}
