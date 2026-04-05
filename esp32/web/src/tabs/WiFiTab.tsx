import { useState } from 'react'
import { Field } from '../components/ui/Field'
import { saveNetwork } from '../api'

interface WiFiTabProps {
  wifiSSID: string; setWifiSSID: (v: string) => void
  wifiPass: string; setWifiPass: (v: string) => void
  otaHost:  string; setOtaHost:  (v: string) => void
  otaPass:  string; setOtaPass:  (v: string) => void
  preferredLastOct: number  // from firmware constant, 0 = disabled
}

export function WiFiTab({
  wifiSSID, setWifiSSID,
  wifiPass, setWifiPass,
  otaHost,  setOtaHost,
  otaPass,  setOtaPass,
  preferredLastOct,
}: WiFiTabProps) {
  const [saving,  setSaving]  = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setMessage(null)
    try {
      const res = await saveNetwork({
        wifi_ssid: wifiSSID,
        wifi_pass: wifiPass || undefined,
        ota_host:  otaHost,
        ota_pass:  otaPass  || undefined,
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
        {preferredLastOct > 0 && (
          <p style={{ fontSize: '0.85em', opacity: 0.65, marginTop: '0.5rem' }}>
            After connecting, device will request IP ending in <strong>.{preferredLastOct}</strong>
          </p>
        )}
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
