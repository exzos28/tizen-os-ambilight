import { saveLeds } from '../api'

interface PowerCardProps {
  powerLimit: string; setPowerLimit: (v: string) => void   // W, "0" = no limit
  pixelWatts: string; setPixelWatts: (v: string) => void   // W per pixel at full white
  numLeds:    number
  brightness: number
}

// Power limit: the firmware estimates the strip's power each frame and scales the
// brightness down when it would exceed the limit. Saved on blur.
export function PowerCard({ powerLimit, setPowerLimit, pixelWatts, setPixelWatts, numLeds, brightness }: PowerCardProps) {
  const limit = Number(powerLimit)
  const perPixel = Number(pixelWatts)
  const limitValid = /^\d{1,4}$/.test(powerLimit.trim()) && limit <= 2000
  const pixelValid = pixelWatts.trim() !== '' && Number.isFinite(perPixel) && perPixel >= 0.05 && perPixel <= 5
  const fullWhite = pixelValid ? numLeds * perPixel * brightness / 255 : null

  function save() {
    if (!limitValid || !pixelValid) return
    saveLeds({ power_limit_w: limit, pixel_watts: perPixel }).catch(console.error)
  }

  return (
    <section className="card">
      <h2>Power Limit</h2>
      <div className="field">
        <label>Max power, W (0 = no limit)</label>
        <input type="number" min={0} max={2000} value={powerLimit}
               onChange={(e) => setPowerLimit(e.target.value)} onBlur={save} />
      </div>
      <div className="field">
        <label>Power per LED pixel at full white, W</label>
        <input type="number" min={0.05} max={5} step={0.01} value={pixelWatts}
               onChange={(e) => setPixelWatts(e.target.value)} onBlur={save} />
      </div>
      <p style={{ fontSize: '0.85em', opacity: 0.65, marginTop: '0.5rem' }}>
        {!limitValid || !pixelValid
          ? 'Max power must be 0–2000 W; power per pixel 0.05–5 W.'
          : <>Full white at the current brightness ≈ <strong>{fullWhite!.toFixed(0)} W</strong>
             {limit > 0 && fullWhite! > limit ? <> — will be limited to {limit} W</> : null}.
             {' '}For a 12 V WS2811 strip: watts per meter ÷ pixels per meter (e.g. 14.4 W/m ÷ 20 = 0.72).</>}
      </p>
    </section>
  )
}
