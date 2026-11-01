import { useRef } from 'react'
import { limits } from '@ambilight/protocol'
import type { SaveLedsRequest } from '@ambilight/protocol'
import { esp } from '../esp'

const { power_limit_w: LIMIT, pixel_watts: PIXEL } = limits

interface PowerCardProps {
  powerLimit: string; setPowerLimit: (v: string) => void   // W, "0" = no limit
  pixelWatts: string; setPixelWatts: (v: string) => void   // W per pixel at full white
  numLeds:    number
  brightness: number
}

// Power limit: the firmware estimates the strip's power each frame and scales the
// brightness down when it would exceed the limit. Saved on blur — only the fields the user
// actually edited, so a value rounded for display never overwrites the stored one.
export function PowerCard({ powerLimit, setPowerLimit, pixelWatts, setPixelWatts, numLeds, brightness }: PowerCardProps) {
  const dirty = useRef({ limit: false, pixel: false })
  const limit = Number(powerLimit)
  const perPixel = Number(pixelWatts)
  const limitValid = /^\d+$/.test(powerLimit.trim()) && limit >= LIMIT.min && limit <= LIMIT.max
  const pixelValid = pixelWatts.trim() !== '' && Number.isFinite(perPixel) && perPixel >= PIXEL.min && perPixel <= PIXEL.max
  const fullWhite = pixelValid ? numLeds * perPixel * brightness / 255 : null

  function save() {
    const req: SaveLedsRequest = {}
    if (dirty.current.limit && limitValid) req.power_limit_w = limit
    if (dirty.current.pixel && pixelValid) req.pixel_watts = perPixel
    if (req.power_limit_w === undefined && req.pixel_watts === undefined) return
    if (req.power_limit_w !== undefined) dirty.current.limit = false
    if (req.pixel_watts !== undefined) dirty.current.pixel = false
    esp.saveLeds(req).catch((err) => {
      // Keep the edit pending so the next blur retries it.
      if (req.power_limit_w !== undefined) dirty.current.limit = true
      if (req.pixel_watts !== undefined) dirty.current.pixel = true
      console.error(err)
    })
  }

  return (
    <section className="card">
      <h2>Power Limit</h2>
      <div className="field">
        <label>Max power, W (0 = no limit)</label>
        <input type="number" min={LIMIT.min} max={LIMIT.max} value={powerLimit}
               onChange={(e) => { dirty.current.limit = true; setPowerLimit(e.target.value) }} onBlur={save} />
      </div>
      <div className="field">
        <label>Power per LED pixel at full white, W</label>
        <input type="number" min={PIXEL.min} max={PIXEL.max} step={0.01} value={pixelWatts}
               onChange={(e) => { dirty.current.pixel = true; setPixelWatts(e.target.value) }} onBlur={save} />
      </div>
      <p style={{ fontSize: '0.85em', opacity: 0.65, marginTop: '0.5rem' }}>
        {!limitValid || !pixelValid
          ? `Max power must be ${LIMIT.min}–${LIMIT.max} W; power per pixel ${PIXEL.min}–${PIXEL.max} W.`
          : <>Full white at the current brightness ≈ <strong>{fullWhite!.toFixed(0)} W</strong>
             {limit > 0 && fullWhite! > limit ? <> — will be limited to {limit} W</> : null}.
             {' '}For a 12 V WS2811 strip: watts per meter ÷ pixels per meter (e.g. 14.4 W/m ÷ 20 = 0.72).</>}
      </p>
    </section>
  )
}
