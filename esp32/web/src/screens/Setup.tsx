// First-time setup, shown before anything else while the controller runs its setup hotspot
// (GET /status mode "AP", captive portal on ambilight-setup): the Wi-Fi network to join, then
// what happens next and how to find the controller again.

import { useEffect, useState } from 'preact/hooks'
import { constants, limits } from '@ambilight/protocol'
import { Button, Card, CheckIcon, Logo, ProblemBanner } from '@ambilight/ui'
import { errorText, esp } from '../api'
import { Field } from '../components/controls'
import type { Ctl } from '../ctl'
import { networkRequest } from '../network'

export function Setup({ ctl, onSkip }: { ctl: Ctl; onSkip: () => void }) {
  const saved = ctl.cfg?.wifi_ssid ?? ''
  const [ssid, setSsid] = useState(saved)
  const [pass, setPass] = useState('')
  const [show, setShow] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)
  // /config may answer after /status: fill in the saved network once it is known.
  useEffect(() => { if (saved) setSsid((v) => v || saved) }, [saved])

  const submit = async (e: Event) => {
    e.preventDefault()
    if (!ssid.trim()) return
    setBusy(true)
    setError(null)
    try {
      await esp.saveNetwork(networkRequest({ ssid, pass }))
      setDone(ssid.trim())
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  if (done) {
    const host = ctl.cfg?.ota_host || limits.ota_host.default
    const octet = ctl.cfg?.ip_octet ?? limits.ip_octet.default
    return (
      <main class="setup">
        <div class="setup-head">
          <span class="done-icon"><CheckIcon size={32} /></span>
          <h1>Saved. The controller is restarting</h1>
          <p class="ui-desc">It joins <strong class="em">{done}</strong> in a few seconds.</p>
        </div>
        <Card title="What next">
          <ol class="steps">
            <li><span>The <strong class="em">{constants.esp32.apSsid}</strong> hotspot goes away. Connect this phone back to <strong class="em">{done}</strong>.</span></li>
            <li>
              <span>
                Open <strong class="em">http://{host}.local</strong>
                {octet > 0
                  ? <> or the controller's address: it ends in <strong class="em">.{octet}</strong> on your network, e.g. http://192.168.1.{octet}.</>
                  : <> or the address your router gave it (see the router's list of devices).</>}
              </span>
            </li>
            <li><span>On the TV, open Ambilight. It finds the controller on its own — no address to type.</span></li>
          </ol>
        </Card>
        <p class="ui-muted">
          If the controller can't join {done} (wrong password, 5 GHz-only network), the {constants.esp32.apSsid} hotspot
          comes back within a minute: connect to it and try again.
        </p>
      </main>
    )
  }

  return (
    <main class="setup">
      <div class="setup-head">
        <Logo size={56} />
        <h1>Connect the controller to your Wi-Fi</h1>
        <p class="ui-desc">It needs the same network as the TV. You only do this once.</p>
      </div>

      {saved && (
        <ProblemBanner tone="warn" title={`Can't reach ${saved}`}
          text="The controller keeps trying every minute while no one is connected to this hotspot. Check the router, or enter the network again." />
      )}

      <form class="stack-gap" onSubmit={submit}>
        <Card>
          <Field label="Network name" hint="2.4 GHz networks only, as the name appears on your phone">
            <input class="input" type="text" required value={ssid} autoComplete="off" autoCapitalize="off" spellcheck={false}
              placeholder="Your Wi-Fi" onInput={(e) => setSsid(e.currentTarget.value)} />
          </Field>
          <Field label="Password" hint={saved ? 'Leave empty to keep the saved one' : 'Leave empty for an open network'}>
            {show
              ? <input class="input" type="text" value={pass} autoComplete="off" autoCapitalize="off" spellcheck={false} onInput={(e) => setPass(e.currentTarget.value)} />
              : <input class="input" type="password" value={pass} autoComplete="off" onInput={(e) => setPass(e.currentTarget.value)} />}
          </Field>
          <label class="check">
            <input type="checkbox" checked={show} onChange={(e) => setShow(e.currentTarget.checked)} />
            Show password
          </label>
          {error && <p class="error-text" role="alert">Not saved: {error}</p>}
        </Card>
        <Button type="submit" variant="primary" block disabled={busy || !ssid.trim()}>{busy ? 'Saving…' : 'Connect'}</Button>
      </form>

      <Button block onClick={onSkip}>Skip, open the settings</Button>
    </main>
  )
}
