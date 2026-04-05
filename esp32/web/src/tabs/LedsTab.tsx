import { useRef, useCallback } from 'react'
import { saveLeds, saveCalibration } from '../api'
import type { CalibrationRequest, LedSaveRequest } from '../api'
import { CalibrationDiagram } from '../components/CalibrationDiagram'
import { Stat } from '../components/ui/Stat'

const CORNER_NAMES = ['Top-Left', 'Top-Right', 'Bot-Right', 'Bot-Left']

interface LedsTabProps {
  // Mode
  ledMode:    number
  savingMode: boolean
  onModeToggle: () => void
  // LED settings
  numLeds:    number;   setNumLeds:    (v: number) => void
  maxLeds:    number | undefined
  brightness: number;   setBrightness: (v: number) => void
  color:      string;   setColor:      (v: string) => void
  // Calibration
  ledTop:      number;   setLedTop:      (v: number) => void
  ledRight:    number;   setLedRight:    (v: number) => void
  ledBottom:   number;   setLedBottom:   (v: number) => void
  ledLeft:     number;   setLedLeft:     (v: number) => void
  startCorner: number;   setStartCorner: (v: number) => void
  clockwise:   boolean;  setClockwise:   (fn: (prev: boolean) => boolean) => void
}

export function LedsTab({
  ledMode, savingMode, onModeToggle,
  numLeds,    setNumLeds,    maxLeds,
  brightness, setBrightness,
  color,      setColor,
  ledTop,      setLedTop,
  ledRight,    setLedRight,
  ledBottom,   setLedBottom,
  ledLeft,     setLedLeft,
  startCorner, setStartCorner,
  clockwise,   setClockwise,
}: LedsTabProps) {
  const ledsTimer  = useRef<ReturnType<typeof setTimeout> | null>(null)
  const calibTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const debouncedSaveLeds = useCallback((params: LedSaveRequest, delay = 350) => {
    if (ledsTimer.current) clearTimeout(ledsTimer.current)
    ledsTimer.current = setTimeout(() => {
      saveLeds(params).catch(console.error)
    }, delay)
  }, [])

  const debouncedSaveCalib = useCallback((params: CalibrationRequest, delay = 350) => {
    if (calibTimer.current) clearTimeout(calibTimer.current)
    calibTimer.current = setTimeout(() => {
      saveCalibration(params).catch(console.error)
    }, delay)
  }, [])

  function makeCalibParams(
    top    = ledTop,    right  = ledRight,
    bottom = ledBottom, left   = ledLeft,
    corner = startCorner, cw   = clockwise,
  ): CalibrationRequest {
    return { led_top: top, led_right: right, led_bottom: bottom, led_left: left, start_corner: corner, clockwise: cw ? 1 : 0 }
  }

  function ledsParams(b = brightness, c = color, n = numLeds): LedSaveRequest {
    return { num_leds: n, brightness: b, color: c }
  }

  const isDynamic = ledMode === 1

  return (
    <div>
      {/* Mode selector */}
      <div className="seg">
        <button
          type="button"
          className={`seg-btn${isDynamic ? ' active dynamic' : ''}`}
          onClick={() => { if (!isDynamic) onModeToggle() }}
          disabled={savingMode}
        >
          Dynamic
        </button>
        <button
          type="button"
          className={`seg-btn${!isDynamic ? ' active' : ''}`}
          onClick={() => { if (isDynamic) onModeToggle() }}
          disabled={savingMode}
        >
          Plain
        </button>
      </div>

      {isDynamic ? (
        <>
          <section className="card">
            <h2>Brightness</h2>
            <div className="field">
              <label>Level — <strong>{brightness}</strong></label>
              <input
                type="range"
                min={0} max={255}
                value={brightness}
                onChange={(e) => {
                  const v = Number(e.target.value)
                  setBrightness(v)
                  debouncedSaveLeds(ledsParams(v))
                }}
              />
            </div>
          </section>

          <section className="card">
            <h2>Strip Layout</h2>
            <CalibrationDiagram
              ledTop={ledTop}         ledRight={ledRight}
              ledBottom={ledBottom}   ledLeft={ledLeft}
              startCorner={startCorner} clockwise={clockwise}
              onTopChange={v => {
                setLedTop(v)
                debouncedSaveCalib(makeCalibParams(v))
              }}
              onRightChange={v => {
                setLedRight(v)
                debouncedSaveCalib(makeCalibParams(ledTop, v))
              }}
              onBottomChange={v => {
                setLedBottom(v)
                debouncedSaveCalib(makeCalibParams(ledTop, ledRight, v))
              }}
              onLeftChange={v => {
                setLedLeft(v)
                debouncedSaveCalib(makeCalibParams(ledTop, ledRight, ledBottom, v))
              }}
              onCornerClick={c => {
                setStartCorner(c)
                debouncedSaveCalib(makeCalibParams(ledTop, ledRight, ledBottom, ledLeft, c))
              }}
              onDirectionToggle={() => {
                setClockwise(cw => {
                  const next = !cw
                  debouncedSaveCalib(makeCalibParams(ledTop, ledRight, ledBottom, ledLeft, startCorner, next))
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
              <Stat label="Mode"       value="Dynamic"                                          />
            </div>
          </section>
        </>
      ) : (
        <section className="card">
          <h2>LED Settings</h2>

          <div className="field">
            <label>LED Count{maxLeds !== undefined && <> — <strong>max {maxLeds}</strong></>}</label>
            <input
              type="number"
              min={1}
              max={maxLeds}
              value={numLeds}
              onChange={(e) => setNumLeds(Math.max(1, Number(e.target.value)))}
              onBlur={(e) => {
                const v = Math.max(1, Number(e.target.value))
                saveLeds(ledsParams(brightness, color, v)).catch(console.error)
              }}
            />
          </div>

          <div className="field">
            <label>Brightness — <strong>{brightness}</strong></label>
            <input
              type="range"
              min={0} max={255}
              value={brightness}
              onChange={(e) => {
                const v = Number(e.target.value)
                setBrightness(v)
                debouncedSaveLeds(ledsParams(v))
              }}
            />
          </div>

          <div className="field">
            <label>Static Color</label>
            <div className="color-row">
              <input
                type="color"
                value={color}
                onChange={(e) => {
                  const v = e.target.value
                  setColor(v)
                  debouncedSaveLeds(ledsParams(brightness, v))
                }}
              />
              <span className="color-preview" style={{ background: color }} />
              <code>{color}</code>
            </div>
          </div>
        </section>
      )}
    </div>
  )
}
