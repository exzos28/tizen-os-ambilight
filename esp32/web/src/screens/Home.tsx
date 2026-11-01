// Home: what people change most — lighting on/off, brightness, mode and static color — then a
// compact live status. A problem banner comes first when something is wrong.

import { LedMode, limits } from '@ambilight/protocol'
import {
  Button, Card, CircleIcon, ColorSwatches, Option, ProblemBanner, SWATCHES, ScreenIcon, SliderBar, StatusLine, StatusList, Switch,
} from '@ambilight/ui'
import { RangeInput } from '../components/controls'
import type { Ctl } from '../ctl'
import { ago, byteOf, pctOf, signal, watts } from '../format'
import { type Problem, receiving } from '../problems'

export function Home({ ctl }: { ctl: Ctl }) {
  const { cfg, metrics: m, status, offline } = ctl
  const problem = ctl.problems[0] as Problem | undefined
  const ready = !!cfg && !offline
  const on = !!cfg?.power
  const follow = cfg?.led_mode === LedMode.dynamic
  const live = receiving(m)
  const pct = cfg ? pctOf(cfg.brightness) : 0

  const lightingText = !cfg ? 'Waiting for the controller'
    : !on ? 'The strip is dark'
      : !follow ? 'Showing a static color'
        : live ? 'Following the picture' : 'Waiting for the picture'

  const custom = !!cfg && !SWATCHES.some((s) => s.color === cfg.color.toLowerCase())

  return (
    <>
      {problem && (
        <ProblemBanner tone={problem.tone} title={problem.title} text={problem.text} hints={problem.hints}
          actions={
            problem.action === 'retry' ? <Button variant="primary" block onClick={() => location.reload()}>Try again</Button>
              : problem.action === 'static' ? <Button block onClick={() => cfg && ctl.setColor(cfg.color)}>Use a static color</Button>
                : problem.action === 'power' ? <Button block onClick={() => ctl.go('picture')}>Power settings</Button>
                  : undefined
          } />
      )}

      <Card class="lighting">
        <button type="button" role="switch" aria-checked={on} class="lighting-btn" disabled={!ready} onClick={() => ctl.setPower(!on)}>
          <span class="lighting-text">
            <span class="ui-label">Lighting</span>
            <span class="ui-value-xl">{cfg ? (on ? 'On' : 'Off') : '—'}</span>
            <span class="ui-desc">{lightingText}</span>
          </span>
          <Switch on={on} accent />
        </button>
      </Card>

      <Card class="brightness">
        <div class="row-head">
          <label class="ui-label" for="brightness">Brightness</label>
          <span class="ui-value-xl ui-num" aria-hidden="true">{cfg ? <>{pct}<span class="ui-unit">%</span></> : '—'}</span>
        </div>
        <RangeInput id="brightness" min={0} max={100} value={pct} valueText={`${pct}%`} disabled={!ready}
          onInput={(v) => ctl.saveLeds({ brightness: Math.max(limits.brightness.min, Math.min(limits.brightness.max, byteOf(v))) })} />
      </Card>

      <Card title="Mode">
        <div class="options" role="radiogroup" aria-label="Lighting mode">
          <label class="option">
            <input class="ui-sr" type="radio" name="mode" checked={follow} disabled={!ready} onChange={() => ctl.setMode(LedMode.dynamic)} />
            <Option checked={follow} icon={<ScreenIcon />}>Follow the picture</Option>
          </label>
          <label class="option">
            <input class="ui-sr" type="radio" name="mode" checked={!!cfg && !follow} disabled={!ready} onChange={() => ctl.setMode(LedMode.plain)} />
            <Option checked={!!cfg && !follow} icon={<CircleIcon />}>Static color</Option>
          </label>
        </div>
        <ColorSwatches selected={cfg && !follow ? cfg.color : null} onSelect={(c) => ctl.setColor(c)} disabled={!ready}>
          <label class={`ui-swatch custom${cfg && !follow && custom ? ' selected' : ''}`} title="Custom color">
            <input type="color" class="color-input" aria-label="Custom color" value={cfg?.color ?? '#ffffff'} disabled={!ready}
              onInput={(e) => ctl.setColor(e.currentTarget.value.toLowerCase(), 300)} />
            <span class="ui-swatch-dot" style={custom ? `background:${cfg!.color}` : undefined} />
          </label>
        </ColorSwatches>
        <p class="ui-muted">{!cfg ? '' : follow ? 'A color stays on the strip without the TV.' : `Static color ${cfg.color}${custom ? ' (custom)' : ''}.`}</p>
      </Card>

      <Card title="Status">
        <StatusList>
          {!m ? (
            <StatusLine tone="off" title="TV picture" detail={offline ? 'The controller does not answer' : 'Asking the controller…'} value="—" />
          ) : live ? (
            <StatusLine tone="ok" title="Receiving from the TV" detail={`${m.frames_rx.toLocaleString('en')} frames · last ${ago(m.last_frame_ms)}`} value="Live" />
          ) : (
            <StatusLine tone={on && follow ? 'warn' : 'off'} title="No frames from the TV"
              detail={m.last_frame_ms < 0 ? 'None since the controller started' : `${m.frames_rx.toLocaleString('en')} frames · last ${ago(m.last_frame_ms)}`}
              value={on && follow ? 'Missing' : 'Not needed'} />
          )}
          {m && cfg && <PowerLine ctl={ctl} />}
          {status && (
            <StatusLine tone={status.mode === 'AP' ? 'warn' : signal(status.rssi).tone} title="Wi-Fi"
              detail={status.mode === 'AP' ? 'Setup hotspot ambilight-setup' : `${status.ssid} · ${status.ip}`}
              value={status.mode === 'AP' ? 'Not set up' : signal(status.rssi).text} />
          )}
        </StatusList>
      </Card>
    </>
  )
}

function PowerLine({ ctl }: { ctl: Ctl }) {
  const m = ctl.metrics!, cfg = ctl.cfg!
  const limit = cfg.power_limit_w
  return (
    <div class="power-line">
      <StatusLine tone={!cfg.power ? 'off' : m.power_limited ? 'warn' : 'ok'} title="Strip power"
        detail={limit ? `Estimated · limit ${limit} W${m.power_limited ? ', dimming' : ''}` : 'Estimated · no limit set'}
        value={watts(m.power_w)} />
      {limit > 0 && <SliderBar value={m.power_w / limit} color={m.power_limited ? 'var(--ui-warn)' : 'var(--ui-ok)'} />}
    </div>
  )
}
