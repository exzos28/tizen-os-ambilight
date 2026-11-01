import { useState, useEffect } from 'react'
import { LedMode, limits } from '@ambilight/protocol'
import type { StartCorner } from '@ambilight/protocol'
import { esp } from './esp'
import { useDeviceData } from './hooks/useDeviceData'
import { Header } from './components/Header'
import { TabNav } from './components/TabNav'
import type { Tab } from './components/TabNav'
import { StatusTab } from './tabs/StatusTab'
import { WiFiTab } from './tabs/WiFiTab'
import { LedsTab } from './tabs/LedsTab'

export default function App() {
  const [tab, setTab] = useState<Tab>('status')
  const { status, metrics } = useDeviceData()

  const [ledMode,    setLedMode]    = useState<LedMode>(limits.led_mode.default)
  const [savingMode, setSavingMode] = useState(false)

  // Lighting on/off; null until /config is loaded
  const [power,       setPower]       = useState<boolean | null>(null)
  const [savingPower, setSavingPower] = useState(false)

  // Network settings
  const [wifiSSID,  setWifiSSID]  = useState<string>(limits.wifi_ssid.default)
  const [wifiPass,  setWifiPass]  = useState('')
  const [otaHost,   setOtaHost]   = useState<string>(limits.ota_host.default)
  const [otaPass,   setOtaPass]   = useState('')
  const [ipOctet,   setIpOctet]   = useState(String(limits.ip_octet.default))

  // LED settings
  const [numLeds,    setNumLeds]    = useState<number>(limits.num_leds.default)
  const [maxLeds,    setMaxLeds]    = useState<number | undefined>(undefined)
  const [brightness, setBrightness] = useState<number>(limits.brightness.default)
  const [color,      setColor]      = useState<string>(limits.color.default)

  // Calibration
  const [ledTop,      setLedTop]      = useState<number>(limits.led_top.default)
  const [ledRight,    setLedRight]    = useState<number>(limits.led_right.default)
  const [ledBottom,   setLedBottom]   = useState<number>(limits.led_bottom.default)
  const [ledLeft,     setLedLeft]     = useState<number>(limits.led_left.default)
  const [startCorner, setStartCorner] = useState<StartCorner>(limits.start_corner.default)
  const [clockwise,   setClockwise]   = useState<boolean>(limits.clockwise.default)

  // Color correction
  const [gamma,      setGamma]      = useState<number>(limits.gamma.default)
  const [saturation, setSaturation] = useState<number>(limits.saturation.default)
  const [wbR,        setWbR]        = useState<number>(limits.wb_r.default)
  const [wbG,        setWbG]        = useState<number>(limits.wb_g.default)
  const [wbB,        setWbB]        = useState<number>(limits.wb_b.default)

  // Power limit (kept as strings while editing)
  const [powerLimit, setPowerLimit] = useState(String(limits.power_limit_w.default))
  const [pixelWatts, setPixelWatts] = useState(String(limits.pixel_watts.default))

  useEffect(() => {
    esp.getConfig()
      .then((cfg) => {
        setPower(cfg.power)
        setWifiSSID(cfg.wifi_ssid)
        setOtaHost(cfg.ota_host)
        setIpOctet(String(cfg.ip_octet))
        setPowerLimit(String(cfg.power_limit_w))
        // toPrecision strips float32 noise (0.7200000286 → 0.72) without rounding real digits away
        setPixelWatts(String(Number(cfg.pixel_watts.toPrecision(6))))
        setNumLeds(cfg.num_leds)
        setMaxLeds(cfg.max_leds)
        setBrightness(cfg.brightness)
        setColor(cfg.color)
        setLedMode(cfg.led_mode)
        setLedTop(cfg.led_top)
        setLedRight(cfg.led_right)
        setLedBottom(cfg.led_bottom)
        setLedLeft(cfg.led_left)
        setStartCorner(cfg.start_corner)
        setClockwise(cfg.clockwise)
        setGamma(cfg.gamma)
        setSaturation(cfg.saturation)
        setWbR(cfg.wb_r)
        setWbG(cfg.wb_g)
        setWbB(cfg.wb_b)
      })
      .catch(console.error)
  }, [])

  async function handleModeToggle() {
    const next = ledMode === LedMode.plain ? LedMode.dynamic : LedMode.plain
    setSavingMode(true)
    try {
      await esp.saveMode({ led_mode: next })
      setLedMode(next)
      if (next === LedMode.dynamic) {
        // Show calibration preview so the strip lights up while waiting for UDP
        await esp.saveCalibration({
          led_top: ledTop, led_right: ledRight,
          led_bottom: ledBottom, led_left: ledLeft,
          start_corner: startCorner, clockwise: clockwise ? 1 : 0,
        })
      }
    } catch {
      console.error('Failed to switch mode')
    } finally {
      setSavingMode(false)
    }
  }

  async function handlePowerToggle() {
    const next = !power
    setSavingPower(true)
    try {
      await esp.savePower({ power: next ? 1 : 0 })
      setPower(next)
    } catch (err) {
      console.error(err)
    } finally {
      setSavingPower(false)
    }
  }

  return (
    <div className="wrap">
      <Header status={status} />
      <TabNav active={tab} onChange={setTab} />

      {tab === 'status' && (
        <StatusTab
          status={status} metrics={metrics}
          power={power}   savingPower={savingPower} onPowerToggle={handlePowerToggle}
        />
      )}

      {tab === 'wifi' && (
        <WiFiTab
          wifiSSID={wifiSSID}   setWifiSSID={setWifiSSID}
          wifiPass={wifiPass}   setWifiPass={setWifiPass}
          otaHost={otaHost}     setOtaHost={setOtaHost}
          otaPass={otaPass}     setOtaPass={setOtaPass}
          ipOctet={ipOctet}     setIpOctet={setIpOctet}
          gateway={status?.gateway}
        />
      )}

      {tab === 'leds' && (
        <LedsTab
          ledMode={ledMode}       savingMode={savingMode}  onModeToggle={handleModeToggle}
          numLeds={numLeds}       setNumLeds={setNumLeds}  maxLeds={maxLeds}
          brightness={brightness} setBrightness={setBrightness}
          color={color}           setColor={setColor}
          ledTop={ledTop}         setLedTop={setLedTop}
          ledRight={ledRight}     setLedRight={setLedRight}
          ledBottom={ledBottom}   setLedBottom={setLedBottom}
          ledLeft={ledLeft}       setLedLeft={setLedLeft}
          startCorner={startCorner} setStartCorner={setStartCorner}
          clockwise={clockwise}   setClockwise={setClockwise}
          gamma={gamma}           setGamma={setGamma}
          saturation={saturation} setSaturation={setSaturation}
          wbR={wbR}               setWbR={setWbR}
          wbG={wbG}               setWbG={setWbG}
          wbB={wbB}               setWbB={setWbB}
          powerLimit={powerLimit} setPowerLimit={setPowerLimit}
          pixelWatts={pixelWatts} setPixelWatts={setPixelWatts}
        />
      )}
    </div>
  )
}
