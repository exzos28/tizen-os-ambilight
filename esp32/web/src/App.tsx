import { useState, useEffect } from 'react'
import { getConfig, saveMode, saveCalibration } from './api'
import type { ConfigResponse } from './api'
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

  const [ledMode,    setLedMode]    = useState(0)
  const [savingMode, setSavingMode] = useState(false)

  // Network settings
  const [wifiSSID,  setWifiSSID]  = useState('')
  const [wifiPass,  setWifiPass]  = useState('')
  const [otaHost,   setOtaHost]   = useState('ambilight')
  const [otaPass,   setOtaPass]   = useState('')

  // LED settings
  const [numLeds,    setNumLeds]    = useState(300)
  const [maxLeds,    setMaxLeds]    = useState<number | undefined>(undefined)
  const [brightness, setBrightness] = useState(100)
  const [color,      setColor]      = useState('#ffffff')

  // Calibration
  const [ledTop,      setLedTop]      = useState(10)
  const [ledRight,    setLedRight]    = useState(6)
  const [ledBottom,   setLedBottom]   = useState(10)
  const [ledLeft,     setLedLeft]     = useState(6)
  const [startCorner, setStartCorner] = useState(0)
  const [clockwise,   setClockwise]   = useState(true)

  useEffect(() => {
    getConfig()
      .then((cfg: ConfigResponse) => {
        setWifiSSID(cfg.wifi_ssid)
        setOtaHost(cfg.ota_host)
        setNumLeds(cfg.num_leds)
        setMaxLeds(cfg.max_leds)
        setBrightness(cfg.brightness)
        setColor(cfg.color)
        setLedMode(cfg.led_mode ?? 0)
        setLedTop(cfg.led_top ?? 10)
        setLedRight(cfg.led_right ?? 6)
        setLedBottom(cfg.led_bottom ?? 10)
        setLedLeft(cfg.led_left ?? 6)
        setStartCorner(cfg.start_corner ?? 0)
        setClockwise(cfg.clockwise ?? true)
      })
      .catch(console.error)
  }, [])

  async function handleModeToggle() {
    const next = ledMode === 0 ? 1 : 0
    setSavingMode(true)
    try {
      await saveMode(next)
      setLedMode(next)
      if (next === 1) {
        // Show calibration preview so the strip lights up while waiting for UDP
        await saveCalibration({
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

  return (
    <div className="wrap">
      <Header status={status} />
      <TabNav active={tab} onChange={setTab} />

      {tab === 'status' && (
        <StatusTab status={status} metrics={metrics} />
      )}

      {tab === 'wifi' && (
        <WiFiTab
          wifiSSID={wifiSSID}   setWifiSSID={setWifiSSID}
          wifiPass={wifiPass}   setWifiPass={setWifiPass}
          otaHost={otaHost}     setOtaHost={setOtaHost}
          otaPass={otaPass}     setOtaPass={setOtaPass}
          preferredLastOct={status?.preferred_last_oct ?? 0}
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
        />
      )}
    </div>
  )
}
