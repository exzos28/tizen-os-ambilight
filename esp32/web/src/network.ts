import { type SaveNetworkRequest, limits } from '@ambilight/protocol'

export const OCTET = limits.ip_octet

/** 0 = DHCP, otherwise 1–254 (checked against the router's subnet when the controller connects). */
export function parseOctet(v: string): number | null {
  if (!/^\d{1,3}$/.test(v.trim())) return null
  const n = Number(v)
  return n >= OCTET.min && n <= OCTET.max ? n : null
}

/**
 * POST /save/network body. Empty fields are left out so the controller keeps their value: an
 * empty wifi_ssid would clear the network and open the setup hotspot, an empty ota_host would
 * leave it without a name; empty passwords keep the current ones.
 */
export function networkRequest(f: { ssid?: string; pass?: string; host?: string; otaPass?: string; octet?: number }): SaveNetworkRequest {
  return {
    wifi_ssid: f.ssid?.trim() || undefined,
    wifi_pass: f.pass || undefined,
    ota_host: f.host?.trim() || undefined,
    ota_pass: f.otaPass || undefined,
    ip_octet: f.octet,
  }
}
