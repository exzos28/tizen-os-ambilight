// Device: the controller itself (firmware, uptime, memory, Wi-Fi signal), Blink to identify,
// Restart, and the network / OTA settings (saving them restarts the controller).

import { useState } from 'preact/hooks'
import { limits } from '@ambilight/protocol'
import { BlinkIcon, Button, Card, RefreshIcon, StatusLine, StatusList } from '@ambilight/ui'
import { errorText, esp } from '../api'
import { Field } from '../components/controls'
import type { Ctl } from '../ctl'
import { duration, kb, signal, subnetPrefix } from '../format'
import { OCTET, networkRequest, parseOctet } from '../network'

export function Device({ ctl }: { ctl: Ctl }) {
  const { status, metrics: m } = ctl
  const [confirm, setConfirm] = useState(false)
  const [busy, setBusy] = useState(false)

  const run = async (action: () => Promise<{ message: string }>) => {
    setBusy(true)
    try {
      ctl.notify((await action()).message)
    } catch (e) {
      ctl.notify(errorText(e), 'bad')
    } finally {
      setBusy(false)
      setConfirm(false)
    }
  }

  const wifi = status ? signal(status.rssi) : null

  return (
    <>
      <Card title="Controller" aside={<span class="ui-muted">ESP32</span>}>
        <StatusList>
          <StatusLine tone="off" title="Firmware" detail="Updates come with each release" value={status?.version ? `v${status.version}` : '—'} />
          <StatusLine tone="off" title="Running for" detail="Since the last start" value={m ? duration(m.uptime_s) : '—'} />
          <StatusLine tone="off" title="Free memory" detail={m ? `Lowest ${kb(m.heap_min)} of ${kb(m.heap_total)}` : '—'} value={m ? kb(m.heap_free) : '—'} />
          <StatusLine tone={status?.mode === 'AP' ? 'warn' : wifi?.tone ?? 'off'} title="Wi-Fi"
            detail={!status ? '—' : status.mode === 'AP' ? 'Setup hotspot · 192.168.4.1' : `${status.ssid} · ${status.ip}`}
            value={status?.mode === 'AP' ? 'Not set up' : wifi?.text ?? '—'} />
        </StatusList>
        <Button block icon={<BlinkIcon />} disabled={busy || ctl.offline} onClick={() => run(esp.identify)}>Blink to identify</Button>
        <Button block icon={<RefreshIcon />} disabled={busy || ctl.offline} aria-expanded={confirm} onClick={() => setConfirm(true)}>Restart…</Button>
        {confirm && (
          <div class="confirm" role="group" aria-label="Restart the controller?">
            <p class="ui-desc">Restart the controller? The strip goes dark for a few seconds and this page reconnects.</p>
            <div class="grid2">
              <Button onClick={() => setConfirm(false)}>Cancel</Button>
              <Button variant="danger" disabled={busy} onClick={() => run(esp.restart)}>Restart now</Button>
            </div>
          </div>
        )}
      </Card>

      {ctl.cfg ? <NetworkForm ctl={ctl} /> : null}
    </>
  )
}

function NetworkForm({ ctl }: { ctl: Ctl }) {
  const cfg = ctl.cfg!
  const [ssid, setSsid] = useState(cfg.wifi_ssid)
  const [pass, setPass] = useState('')
  const [octet, setOctet] = useState(String(cfg.ip_octet))
  const [host, setHost] = useState(cfg.ota_host)
  const [otaPass, setOtaPass] = useState('')
  const [saving, setSaving] = useState(false)
  const n = parseOctet(octet)
  const prefix = subnetPrefix(ctl.status?.gateway) ?? 'x.x.x.'

  const submit = async (e: Event) => {
    e.preventDefault()
    if (n === null) return
    setSaving(true)
    try {
      const res = await esp.saveNetwork(networkRequest({ ssid, pass, host, otaPass, octet: n }))
      ctl.notify(res.message)
    } catch (err) {
      ctl.notify(`Not saved: ${errorText(err)}`, 'bad')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form class="stack-gap" onSubmit={submit}>
      <Card title="Wi-Fi">
        <Field label="Network name">
          <input class="input" type="text" value={ssid} autoComplete="off" autoCapitalize="off" spellcheck={false}
            placeholder="Your Wi-Fi" onInput={(e) => setSsid(e.currentTarget.value)} />
        </Field>
        <Field label="Password" hint="Leave empty to keep the saved one">
          <input class="input" type="password" value={pass} autoComplete="new-password" onInput={(e) => setPass(e.currentTarget.value)} />
        </Field>
        <Field label="Fixed address, last number" error={n === null}
          hint={n === null ? `0 (address from the router) or 1–${OCTET.max}`
            : n === 0 ? 'The controller keeps the address the router gives it.'
              : <>The controller moves to <strong class="em">{prefix}{n}</strong> if that fits the router's network. Give each controller its own number.</>}>
          <input class="input ui-num" type="number" inputMode="numeric" min={OCTET.min} max={OCTET.max} value={octet}
            onInput={(e) => setOctet(e.currentTarget.value)} />
        </Field>
      </Card>

      <Card title="Updates over Wi-Fi" aside={<span class="ui-muted">Arduino OTA</span>}>
        <Field label="Name on the network" hint={`Also the address http://${host.trim() || limits.ota_host.default}.local`}>
          <input class="input" type="text" value={host} autoComplete="off" autoCapitalize="off" spellcheck={false}
            placeholder={limits.ota_host.default} onInput={(e) => setHost(e.currentTarget.value)} />
        </Field>
        <Field label="Update password" hint="Leave empty to keep the saved one">
          <input class="input" type="password" value={otaPass} autoComplete="new-password" onInput={(e) => setOtaPass(e.currentTarget.value)} />
        </Field>
      </Card>

      <Button type="submit" variant="primary" block disabled={saving || ctl.offline || n === null}>{saving ? 'Saving…' : 'Save and restart'}</Button>
      <p class="ui-muted center">The controller restarts to apply network settings.</p>
    </form>
  )
}
