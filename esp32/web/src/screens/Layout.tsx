// Strip layout: start corner, direction and LEDs per side. Every change lights the 10 s
// calibration preview on the strip (POST /preview/calibration, not saved); Save layout stores it
// (POST /save/calibration, which also sets num_leds to the sum of the sides).

import { useEffect, useRef, useState } from 'preact/hooks'
import { type CalibrationRequest, type StartCorner, constants, limits } from '@ambilight/protocol'
import { Button, CORNER_NAMES, Card, type Corner, SIDES, type Side, StatusPill, StripLayoutDiagram } from '@ambilight/ui'
import { errorText, esp } from '../api'
import { Stepper } from '../components/controls'
import type { Ctl } from '../ctl'

interface Draft {
  top: number
  right: number
  bottom: number
  left: number
  corner: StartCorner
  clockwise: boolean
}

const PREVIEW_DELAY_MS = 400
const SIDE_NAME: Record<Side, string> = { top: 'Top', right: 'Right', bottom: 'Bottom', left: 'Left' }

const body = (d: Draft): CalibrationRequest => ({
  led_top: d.top, led_right: d.right, led_bottom: d.bottom, led_left: d.left, start_corner: d.corner, clockwise: d.clockwise ? 1 : 0,
})

export function Layout({ ctl }: { ctl: Ctl }) {
  const { cfg } = ctl
  if (!cfg) return <Card><p class="ui-desc">{ctl.offline ? 'The controller does not answer.' : 'Loading the settings…'}</p></Card>
  return <Editor ctl={ctl} />
}

function Editor({ ctl }: { ctl: Ctl }) {
  const cfg = ctl.cfg!
  const saved: Draft = {
    top: cfg.led_top, right: cfg.led_right, bottom: cfg.led_bottom, left: cfg.led_left, corner: cfg.start_corner, clockwise: cfg.clockwise,
  }
  const [d, setD] = useState<Draft>(saved)
  const [busy, setBusy] = useState(false)
  const timer = useRef(0)
  useEffect(() => () => clearTimeout(timer.current), [])

  const total = d.top + d.right + d.bottom + d.left
  const dirty = (Object.keys(saved) as (keyof Draft)[]).some((k) => saved[k] !== d[k])

  const preview = (next: Draft) => esp.previewCalibration(body(next)).catch((e) => ctl.notify(`Preview failed: ${errorText(e)}`, 'bad'))
  const change = (p: Partial<Draft>) => {
    const next = { ...d, ...p }
    setD(next)
    clearTimeout(timer.current)
    timer.current = window.setTimeout(() => preview(next), PREVIEW_DELAY_MS)
  }
  const save = async () => {
    clearTimeout(timer.current)
    setBusy(true)
    try {
      await esp.saveCalibration(body(d))
      ctl.patch({
        led_top: d.top, led_right: d.right, led_bottom: d.bottom, led_left: d.left, start_corner: d.corner, clockwise: d.clockwise, num_leds: total,
      })
      ctl.notify('Layout saved')
    } catch (e) {
      ctl.notify(`Not saved: ${errorText(e)}`, 'bad')
    } finally {
      setBusy(false)
    }
  }

  const off = ctl.offline || busy
  const previewSeconds = constants.esp32.calibrationPreviewMs / 1000

  return (
    <>
      <Card title="Seen from the front" aside={<span class="ui-muted">{d.clockwise ? 'Clockwise' : 'Counter-clockwise'}</span>}>
        <StripLayoutDiagram top={d.top} right={d.right} bottom={d.bottom} left={d.left} startCorner={d.corner} clockwise={d.clockwise}
          disabled={off}
          onCorner={(c: Corner) => change({ corner: c })}
          onSide={() => change({ clockwise: !d.clockwise })} />
        <p class="ui-muted">
          LED 1 (where the cable comes in) is at the <strong class="em">{CORNER_NAMES[d.corner]}</strong> corner.
          Tap a corner to start there, tap a side to reverse the direction.
        </p>
      </Card>

      <Card title="LEDs per side" aside={<span class="ui-muted">Up to {limits.led_top.max} each</span>}>
        <div class="sides">
          {SIDES.map((s) => (
            <div key={s} class="side-row">
              <span class={`side-chip ${s}`} aria-hidden="true" />
              <span class="side-name">{SIDE_NAME[s]}</span>
              <Stepper label={SIDE_NAME[s]} value={d[s]} min={limits[`led_${s}`].min} max={limits[`led_${s}`].max} disabled={off}
                onChange={(v) => change({ [s]: v } as Partial<Draft>)} />
            </div>
          ))}
          <div class="side-row total">
            <span class="side-name">Total</span>
            <span class="ui-value ui-num">{total} <span class="ui-muted">LEDs</span></span>
          </div>
        </div>
        {!dirty && cfg.num_leds !== total && (
          <p class="ui-muted">The controller drives {cfg.num_leds} LEDs now; saving the layout sets it to {total}.</p>
        )}
      </Card>

      <Card title="Check on the strip" aside={dirty ? <StatusPill tone="warn">Not saved</StatusPill> : <StatusPill tone="ok">Saved</StatusPill>}>
        <p class="ui-desc">
          For {previewSeconds} s each side lights in its color from the diagram and LED 1 turns white. If a side shows the
          wrong color, change the start corner or the direction.
        </p>
        <Button block disabled={off} onClick={() => preview(d)}>Light up the strip</Button>
        <Button variant="primary" block disabled={off || !dirty} onClick={save}>{busy ? 'Saving…' : 'Save layout'}</Button>
      </Card>
    </>
  )
}
