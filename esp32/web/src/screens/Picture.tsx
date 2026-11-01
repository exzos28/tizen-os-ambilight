// Picture: color correction of the TV frames (gamma, saturation, white balance) and the power
// limit. Ranges and defaults from @ambilight/protocol `limits`.

import { useState } from 'preact/hooks'
import { LedMode, type SaveLedsRequest, limits } from '@ambilight/protocol'
import { Button, Card, SliderBar } from '@ambilight/ui'
import { Field, RangeRow } from '../components/controls'
import type { Ctl } from '../ctl'
import { clean, watts } from '../format'

const { gamma: G, saturation: S, wb_r: R, wb_g: GR, wb_b: B, power_limit_w: LIMIT, pixel_watts: PIXEL } = limits

const DEFAULTS: SaveLedsRequest = { gamma: G.default, saturation: S.default, wb_r: R.default, wb_g: GR.default, wb_b: B.default }

export function Picture({ ctl }: { ctl: Ctl }) {
  const { cfg } = ctl
  if (!cfg) return <Card><p class="ui-desc">{ctl.offline ? 'The controller does not answer.' : 'Loading the settings…'}</p></Card>
  const off = ctl.offline
  const set = (body: SaveLedsRequest) => ctl.saveLeds(body)
  const isDefault = Object.entries(DEFAULTS).every(([k, v]) => clean(cfg[k as keyof typeof DEFAULTS] as number) === v)

  return (
    <>
      <Card title="Color" aside={<span class="ui-muted">Applies to the TV picture</span>}>
        {cfg.led_mode === LedMode.plain && <p class="ui-muted">The strip shows a static color now; these settings apply when it follows the picture.</p>}
        <RangeRow id="gamma" label="Gamma" hint="Higher: darker shadows, richer color" min={G.min} max={G.max} step={0.1}
          value={cfg.gamma} display={cfg.gamma.toFixed(1)} disabled={off} onInput={(v) => set({ gamma: v })} />
        <RangeRow id="saturation" label="Saturation" hint="1.0× = as on the screen" min={S.min} max={S.max} step={0.1}
          value={cfg.saturation} display={`${cfg.saturation.toFixed(1)}×`} disabled={off} onInput={(v) => set({ saturation: v })} />
        <div class="wb">
          <p class="row-label">White balance</p>
          <p class="ui-muted">Lower a channel until the wall's white matches the screen</p>
          <RangeRow id="wb-r" label="Red" min={R.min} max={R.max} value={cfg.wb_r} display={String(cfg.wb_r)} color="var(--ui-side-top)" disabled={off} onInput={(v) => set({ wb_r: v })} />
          <RangeRow id="wb-g" label="Green" min={GR.min} max={GR.max} value={cfg.wb_g} display={String(cfg.wb_g)} color="var(--ui-side-right)" disabled={off} onInput={(v) => set({ wb_g: v })} />
          <RangeRow id="wb-b" label="Blue" min={B.min} max={B.max} value={cfg.wb_b} display={String(cfg.wb_b)} color="var(--ui-side-left)" disabled={off} onInput={(v) => set({ wb_b: v })} />
        </div>
        <Button block disabled={off || isDefault} onClick={() => { ctl.saveLeds(DEFAULTS, 0); ctl.notify('Color reset to defaults') }}>
          Reset color to defaults
        </Button>
        <p class="ui-muted">Gamma {G.default}, saturation {S.default.toFixed(1)}×, white balance {R.default} / {GR.default} / {B.default}. Brightness and power stay.</p>
      </Card>

      <PowerCard ctl={ctl} />
    </>
  )
}

// Saved when a field is committed (blur / Enter), only the edited field, so a value rounded
// for display never overwrites the stored one.
function PowerCard({ ctl }: { ctl: Ctl }) {
  const cfg = ctl.cfg!, m = ctl.metrics
  const [limit, setLimit] = useState(String(cfg.power_limit_w))
  const [pixel, setPixel] = useState(String(clean(cfg.pixel_watts)))
  const limitN = Number(limit), pixelN = Number(pixel)
  const limitOk = /^\d+$/.test(limit.trim()) && limitN >= LIMIT.min && limitN <= LIMIT.max
  const pixelOk = pixel.trim() !== '' && Number.isFinite(pixelN) && pixelN >= PIXEL.min && pixelN <= PIXEL.max
  const fullWhite = cfg.num_leds * clean(cfg.pixel_watts) * cfg.brightness / 255
  const used = m && cfg.power_limit_w > 0 ? m.power_w / cfg.power_limit_w : null

  return (
    <Card title="Power" aside={<span class="ui-muted">Keeps the strip within your power supply</span>}>
      <div class="grid2">
        <Field label="Limit, W" hint={limitOk ? '0 = no limit' : `${LIMIT.min}–${LIMIT.max} W`} error={!limitOk}>
          <input class="input ui-num" type="number" inputMode="numeric" min={LIMIT.min} max={LIMIT.max} value={limit} disabled={ctl.offline}
            onInput={(e) => setLimit(e.currentTarget.value)}
            onChange={() => { if (limitOk && limitN !== cfg.power_limit_w) ctl.saveLeds({ power_limit_w: limitN }, 0) }} />
        </Field>
        <Field label="One LED, W" hint={pixelOk ? 'At full white: W per m ÷ LEDs per m' : `${PIXEL.min}–${PIXEL.max} W`} error={!pixelOk}>
          <input class="input ui-num" type="number" inputMode="decimal" min={PIXEL.min} max={PIXEL.max} step={0.01} value={pixel} disabled={ctl.offline}
            onInput={(e) => setPixel(e.currentTarget.value)}
            onChange={() => { if (pixelOk && pixelN !== clean(cfg.pixel_watts)) ctl.saveLeds({ pixel_watts: pixelN }, 0) }} />
        </Field>
      </div>
      <div class="using">
        <div class="row-head">
          <span class="row-label">Using now</span>
          <span class="ui-value ui-num">{m ? watts(m.power_w) : '—'}</span>
        </div>
        {used !== null && <SliderBar value={used} color={m!.power_limited ? 'var(--ui-warn)' : 'var(--ui-ok)'} />}
        <p class="ui-muted">
          {used !== null ? `${Math.round(used * 100)}% of the limit · ${m!.power_limited ? 'dimmed to stay under it' : 'not reduced'}. ` : 'No limit set. '}
          Full white at this brightness ≈ {watts(fullWhite)}.
        </p>
      </div>
    </Card>
  )
}
