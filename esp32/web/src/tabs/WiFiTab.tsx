import { useState } from 'react'
import { Field } from '../components/ui/Field'
import { saveNetwork } from '../api'

interface WiFiTabProps {
  wifiSSID: string; setWifiSSID: (v: string) => void
  wifiPass: string; setWifiPass: (v: string) => void
  otaHost:  string; setOtaHost:  (v: string) => void
  otaPass:  string; setOtaPass:  (v: string) => void
  ipOctet:  string; setIpOctet:  (v: string) => void
  gateway?: string
}

// 0 = DHCP, otherwise 2–254 (1 is usually the router, 255 is broadcast).
function parseOctet(v: string): number | null {
  if (!/^\d{1,3}$/.test(v.trim())) return null
  const n = Number(v)
  return n === 0 || (n >= 2 && n <= 254) ? n : null
}

export function WiFiTab({
  wifiSSID, setWifiSSID,
  wifiPass, setWifiPass,
  otaHost,  setOtaHost,
  otaPass,  setOtaPass,
  ipOctet,  setIpOctet,
  gateway,
}: WiFiTabProps) {
  const [saving,  setSaving]  = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const octet = parseOctet(ipOctet)
    if (octet === null) {
      setMessage('IP last octet must be 0 (DHCP) or 2–254.')
      return
    }
    setSaving(true)
    setMessage(null)
    try {
      const res = await saveNetwork({
        wifi_ssid: wifiSSID,
        wifi_pass: wifiPass || undefined,
        ota_host:  otaHost,
        ota_pass:  otaPass  || undefined,
        ip_octet:  octet,
      })
      setMessage(res.message)
    } catch {
      setMessage('Error: could not save network settings.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <section className="card">
        <h2>WiFi</h2>
        <Field label="SSID"     type="text"     value={wifiSSID} onChange={setWifiSSID} placeholder="Network name"                />
        <Field label="Password" type="password" value={wifiPass} onChange={setWifiPass} placeholder="Leave empty to keep current" />
        <Field label="Static IP — last number (0 = DHCP)" type="number" min={0} max={254}
               value={ipOctet} onChange={setIpOctet} placeholder="245" />
        <p style={{ fontSize: '0.85em', opacity: 0.65, marginTop: '0.5rem' }}>
          {octetHint(parseOctet(ipOctet), gateway)}
        </p>
      </section>

      <section className="card">
        <h2>OTA Updates</h2>
        <Field label="Hostname" type="text"     value={otaHost} onChange={setOtaHost} placeholder="ambilight"                   />
        <Field label="Password" type="password" value={otaPass} onChange={setOtaPass} placeholder="Leave empty to keep current" />
      </section>

      <button type="submit" disabled={saving}>
        {saving ? 'Saving…' : 'Save & Restart'}
      </button>

      {message && <div className="toast">{message}</div>}
    </form>
  )
}

// "192.168.1.1" → "192.168.1."; unknown → "x.x.x."
function subnetPrefix(gateway?: string): string {
  const parts = gateway?.split('.')
  return parts && parts.length === 4 && gateway !== '0.0.0.0' ? parts.slice(0, 3).join('.') + '.' : 'x.x.x.'
}

function octetHint(octet: number | null, gateway?: string) {
  if (octet === null) return 'Enter 0 (DHCP) or a number from 2 to 254.'
  if (octet === 0) return 'The device keeps the address it gets from the router (DHCP).'
  return <>After connecting, the device switches to <strong>{subnetPrefix(gateway)}{octet}</strong> if
    that address fits the router's network (otherwise it keeps the DHCP address). It doesn't check
    whether another device already uses it — give each controller its own number.</>
}
