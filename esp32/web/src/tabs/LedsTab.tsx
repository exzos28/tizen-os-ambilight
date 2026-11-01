import { useRef, useCallback } from 'react'
import { LedMode, limits } from '@ambilight/protocol'
import type { CalibrationRequest, SaveLedsRequest, StartCorner } from '@ambilight/protocol'
import { esp } from '../esp'
import { CalibrationDiagram } from '../components/CalibrationDiagram'
import { Stat } from '../components/ui/Stat'
import { PowerCard } from '../components/PowerCard'

const CORNER_NAMES = ['Top-Left', 'Top-Right', 'Bot-Right', 'Bot-Left']

interface LedsTabProps {
  // Mode
  ledMode:    LedMode
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
  startCorner: StartCorner; setStartCorner: (v: StartCorner) => void
  clockwise:   boolean;  setClockwise:   (fn: (prev: boolean) => boolean) => void
  // Color correction
  gamma:      number;  setGamma:      (v: number) => void
  saturation: number;  setSaturation: (v: number) => void
  wbR:        number;  setWbR:        (v: number) => void
  wbG:        number;  setWbG:        (v: number) => void
  wbB:        number;  setWbB:        (v: number) => void
  // Power limit
  powerLimit: string;  setPowerLimit: (v: string) => void
  pixelWatts: string;  setPixelWatts: (v: string) => void
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
  gamma,       setGamma,
  saturation,  setSaturation,
  wbR,         setWbR,
  wbG,         setWbG,
  wbB,         setWbB,
  powerLimit,  setPowerLimit,
  pixelWatts,  setPixelWatts,
}: LedsTabProps) {
  const ledsTimer  = useRef<ReturnType<typeof setTimeout> | null>(null)
  const calibTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const debouncedSaveLeds = useCallback((params: SaveLedsRequest, delay = 350) => {
    if (ledsTimer.current) clearTimeout(ledsTimer.current)
    ledsTimer.current = setTimeout(() => {
      esp.saveLeds(params).catch(console.error)
    }, delay)
  }, [])

  const debouncedSaveCalib = useCallback((params: CalibrationRequest, delay = 350) => {
    if (calibTimer.current) clearTimeout(calibTimer.current)
    calibTimer.current = setTimeout(() => {
      esp.saveCalibration(params).catch(console.error)
    }, delay)
  }, [])

  function makeCalibParams(
    top    = ledTop,    right  = ledRight,
    bottom = ledBottom, left   = ledLeft,
    corner = startCorner, cw   = clockwise,
  ): CalibrationRequest {
    return { led_top: top, led_right: right, led_bottom: bottom, led_left: left, start_corner: corner, clockwise: cw ? 1 : 0 }
  }

  function ledsParams(
    b = brightness, c = color, n = numLeds,
    g = gamma, s = saturation, r = wbR, gr = wbG, bl = wbB
  ): SaveLedsRequest {
    return {
      num_leds: n, brightness: b, color: c,
      gamma: g, saturation: s, wb_r: r, wb_g: gr, wb_b: bl
    }
  }

  const isDynamic = ledMode === LedMode.dynamic

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
                min={limits.brightness.min} max={limits.brightness.max}
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
            <h2>Color Correction</h2>
            <div className="field">
              <label>Gamma — <strong>{gamma.toFixed(1)}</strong></label>
              <input
                type="range"
                min={limits.gamma.min} max={limits.gamma.max} step={0.1}
                value={gamma}
                onChange={(e) => {
                  const v = Number(e.target.value)
                  setGamma(v)
                  debouncedSaveLeds(ledsParams(brightness, color, numLeds, v))
                }}
              />
            </div>
            <div className="field">
              <label>Saturation Boost — <strong>{saturation.toFixed(1)}x</strong></label>
              <input
                type="range"
                min={limits.saturation.min} max={limits.saturation.max} step={0.1}
                value={saturation}
                onChange={(e) => {
                  const v = Number(e.target.value)
                  setSaturation(v)
                  debouncedSaveLeds(ledsParams(brightness, color, numLeds, gamma, v))
                }}
              />
            </div>
            <div className="field">
              <label>White Balance (R/G/B)</label>
              <div className="wb-controls">
                <input
                  type="range" className="accent-red"
                  min={limits.wb_r.min} max={limits.wb_r.max} value={wbR}
                  onChange={(e) => {
                    const v = Number(e.target.value)
                    setWbR(v)
                    debouncedSaveLeds(ledsParams(brightness, color, numLeds, gamma, saturation, v))
                  }}
                />
                <input
                  type="range" className="accent-green"
                  min={limits.wb_g.min} max={limits.wb_g.max} value={wbG}
                  onChange={(e) => {
                    const v = Number(e.target.value)
                    setWbG(v)
                    debouncedSaveLeds(ledsParams(brightness, color, numLeds, gamma, saturation, wbR, v))
                  }}
                />
                <input
                  type="range" className="accent-blue"
                  min={limits.wb_b.min} max={limits.wb_b.max} value={wbB}
                  onChange={(e) => {
                    const v = Number(e.target.value)
                    setWbB(v)
                    debouncedSaveLeds(ledsParams(brightness, color, numLeds, gamma, saturation, wbR, wbG, v))
                  }}
                />
              </div>
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
              min={limits.num_leds.min}
              max={maxLeds}
              value={numLeds}
              onChange={(e) => setNumLeds(Math.max(limits.num_leds.min, Number(e.target.value)))}
              onBlur={(e) => {
                const v = Math.max(limits.num_leds.min, Number(e.target.value))
                esp.saveLeds(ledsParams(brightness, color, v)).catch(console.error)
              }}
            />
          </div>

          <div className="field">
            <label>Brightness — <strong>{brightness}</strong></label>
            <input
              type="range"
              min={limits.brightness.min} max={limits.brightness.max}
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

      <PowerCard
        powerLimit={powerLimit} setPowerLimit={setPowerLimit}
        pixelWatts={pixelWatts} setPixelWatts={setPixelWatts}
        numLeds={numLeds}       brightness={brightness}
      />
    </div>
  )
}
