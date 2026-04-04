import { useState, useEffect, useRef, useCallback } from 'react'
import {
  getStatus,
  getConfig,
  getMetrics,
  saveNetwork,
  saveLeds,
  saveCalibration,
  previewCalibration,
  saveMode,
  type StatusResponse,
  type ConfigResponse,
  type MetricsResponse,
} from './api'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

interface FieldProps {
  label: string
  type: 'text' | 'password'
  value: string
  onChange: (v: string) => void
  placeholder?: string
}

function Field({ label, type, value, onChange, placeholder }: FieldProps) {
  return (
    <div className="field">
      <label>{label}</label>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        autoComplete="off"
      />
    </div>
  )
}

interface StatProps { label: string; value: string }

function Stat({ label, value }: StatProps) {
  return (
    <div className="stat">
      <div className="stat-val">{value}</div>
      <div className="stat-key">{label}</div>
    </div>
  )
}

function fmtUptime(s: number): string {
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  return `${h}h ${m}m ${sec}s`
}

function fmtKB(bytes: number): string {
  return `${(bytes / 1024).toFixed(1)} KB`
}

// ---------------------------------------------------------------------------
// App
// ---------------------------------------------------------------------------

type Tab = 'status' | 'wifi' | 'leds' | 'calibration'

// ---------------------------------------------------------------------------
// CalibrationDiagram
// ---------------------------------------------------------------------------

// Corner indices: 0=TL, 1=TR, 2=BR, 3=BL
const CORNER_LABELS = ['TL', 'TR', 'BR', 'BL']
const CORNER_CX     = [70, 230, 230, 70]
const CORNER_CY     = [50, 50, 170, 170]

// Side labels / arrows
// In CW mode sides go in their natural direction:
//   TOP: →, RIGHT: ↓, BOTTOM: ←, LEFT: ↑
// In CCW mode everything reverses.
const CW_ARROWS  = ['→', '↓', '←', '↑']   // TOP, RIGHT, BOTTOM, LEFT
const CCW_ARROWS = ['←', '↑', '→', '↓']

// Strip-order index for each side given startCorner + clockwise
function sideStripOrder(startCorner: number, clockwise: boolean): number[] {
  // Returns [orderOfTop, orderOfRight, orderOfBottom, orderOfLeft] (0-based)
  const order: number[] = [0, 0, 0, 0]
  for (let s = 0; s < 4; s++) {
    const si = clockwise
      ? (startCorner + s) % 4
      : (startCorner + 3 - s + 4) % 4
    order[si] = s + 1
  }
  return order
}

interface CalibDiagramProps {
  ledTop: number; ledRight: number; ledBottom: number; ledLeft: number
  startCorner: number; clockwise: boolean
  onTopChange: (n: number) => void
  onRightChange: (n: number) => void
  onBottomChange: (n: number) => void
  onLeftChange: (n: number) => void
  onCornerClick: (c: number) => void
  onDirectionToggle: () => void
}

function CalibrationDiagram({
  ledTop, ledRight, ledBottom, ledLeft,
  startCorner, clockwise,
  onTopChange, onRightChange, onBottomChange, onLeftChange,
  onCornerClick, onDirectionToggle,
}: CalibDiagramProps) {
  const [editing, setEditing] = useState<null | 'top' | 'right' | 'bottom' | 'left'>(null)
  const [draft, setDraft] = useState('')

  const arrows  = clockwise ? CW_ARROWS : CCW_ARROWS
  const orders  = sideStripOrder(startCorner, clockwise)
  const COLORS  = ['#f97316', '#22c55e', '#0ea5e9', '#a855f7']   // TOP, RIGHT, BOTTOM, LEFT

  function commitEdit(side: 'top' | 'right' | 'bottom' | 'left') {
    const v = Math.max(0, Math.min(255, parseInt(draft) || 0))
    if (side === 'top')    onTopChange(v)
    if (side === 'right')  onRightChange(v)
    if (side === 'bottom') onBottomChange(v)
    if (side === 'left')   onLeftChange(v)
    setEditing(null)
  }

  function startEdit(side: 'top' | 'right' | 'bottom' | 'left', current: number) {
    setEditing(side)
    setDraft(String(current))
  }

  const sideValues = { top: ledTop, right: ledRight, bottom: ledBottom, left: ledLeft }

  return (
    <div className="calib-diagram">
      <svg viewBox="0 0 300 220" width="100%">
        {/* TV screen */}
        <rect x="70" y="50" width="160" height="120" fill="#0a111e" stroke="#334155" strokeWidth="1.5" rx="3"/>
        <text x="150" y="117" textAnchor="middle" fill="#334155" fontSize="11" fontFamily="monospace">TV</text>

        {/* TOP side */}
        <rect x="74" y="28" width="152" height="20" rx="4"
              fill={COLORS[0] + '22'} stroke={COLORS[0]} strokeWidth="1.2"
              style={{cursor:'pointer'}} onClick={onDirectionToggle}/>
        <text x="150" y="41" textAnchor="middle" fill={COLORS[0]} fontSize="10" fontWeight="700"
              style={{cursor:'pointer'}} onClick={onDirectionToggle}>
          {arrows[0]} #{orders[0]}
        </text>

        {/* BOTTOM side */}
        <rect x="74" y="172" width="152" height="20" rx="4"
              fill={COLORS[2] + '22'} stroke={COLORS[2]} strokeWidth="1.2"
              style={{cursor:'pointer'}} onClick={onDirectionToggle}/>
        <text x="150" y="185" textAnchor="middle" fill={COLORS[2]} fontSize="10" fontWeight="700"
              style={{cursor:'pointer'}} onClick={onDirectionToggle}>
          {arrows[2]} #{orders[2]}
        </text>

        {/* RIGHT side */}
        <rect x="232" y="54" width="20" height="112" rx="4"
              fill={COLORS[1] + '22'} stroke={COLORS[1]} strokeWidth="1.2"
              style={{cursor:'pointer'}} onClick={onDirectionToggle}/>
        <text x="242" y="114" textAnchor="middle" fill={COLORS[1]} fontSize="10" fontWeight="700"
              transform="rotate(90 242 114)" style={{cursor:'pointer'}} onClick={onDirectionToggle}>
          {arrows[1]} #{orders[1]}
        </text>

        {/* LEFT side */}
        <rect x="48" y="54" width="20" height="112" rx="4"
              fill={COLORS[3] + '22'} stroke={COLORS[3]} strokeWidth="1.2"
              style={{cursor:'pointer'}} onClick={onDirectionToggle}/>
        <text x="58" y="114" textAnchor="middle" fill={COLORS[3]} fontSize="10" fontWeight="700"
              transform="rotate(-90 58 114)" style={{cursor:'pointer'}} onClick={onDirectionToggle}>
          {arrows[3]} #{orders[3]}
        </text>

        {/* Corner dots */}
        {[0,1,2,3].map(c => (
          <circle key={c} cx={CORNER_CX[c]} cy={CORNER_CY[c]} r="9"
                  fill={startCorner === c ? '#f97316' : '#1e293b'}
                  stroke={startCorner === c ? '#f97316' : '#475569'}
                  strokeWidth="2"
                  style={{cursor:'pointer'}}
                  onClick={() => onCornerClick(c)}/>
        ))}
        {/* Start marker */}
        <circle cx={CORNER_CX[startCorner]} cy={CORNER_CY[startCorner]} r="4" fill="#fff"/>
      </svg>

      {/* LED count editors */}
      <div className="calib-counts">
        {(['top','right','bottom','left'] as const).map((side) => (
          <div key={side} className="calib-count-row">
            <span className="calib-side-label" style={{color: COLORS[['top','right','bottom','left'].indexOf(side)]}}>
              {side.toUpperCase()}
            </span>
            {editing === side ? (
              <span className="calib-edit-group">
                <input
                  className="calib-input"
                  type="number" min={0} max={255}
                  value={draft}
                  autoFocus
                  onChange={e => setDraft(e.target.value)}
                  onBlur={() => commitEdit(side)}
                  onKeyDown={e => { if (e.key === 'Enter') commitEdit(side); if (e.key === 'Escape') setEditing(null) }}
                />
                <button type="button" className="calib-ok" onClick={() => commitEdit(side)}>OK</button>
              </span>
            ) : (
              <span className="calib-count-val" onClick={() => startEdit(side, sideValues[side])}>
                {sideValues[side]} LEDs
              </span>
            )}
          </div>
        ))}
      </div>

      <p className="calib-hint">
        Click <strong>corner</strong> to set strip start &nbsp;·&nbsp;
        Click <strong>side</strong> to toggle direction &nbsp;·&nbsp;
        Click <strong>LED count</strong> to edit
      </p>
    </div>
  )
}

export default function App() {
  const [tab, setTab] = useState<Tab>('status')

  const [status,  setStatus]  = useState<StatusResponse  | null>(null)
  const [metrics, setMetrics] = useState<MetricsResponse | null>(null)

  // Network settings
  const [wifiSSID, setWifiSSID] = useState('')
  const [wifiPass, setWifiPass] = useState('')
  const [otaHost,  setOtaHost]  = useState('ambilight')
  const [otaPass,  setOtaPass]  = useState('')

  // LED settings
  const [numLeds,    setNumLeds]    = useState(300)
  const [maxLeds,    setMaxLeds]    = useState<number | undefined>(undefined)
  const [brightness, setBrightness] = useState(100)
  const [color,      setColor]      = useState('#ffffff')

  // Calibration
  const [ledMode,      setLedMode]      = useState(0)
  const [ledTop,       setLedTop]       = useState(10)
  const [ledRight,     setLedRight]     = useState(6)
  const [ledBottom,    setLedBottom]    = useState(10)
  const [ledLeft,      setLedLeft]      = useState(6)
  const [startCorner,  setStartCorner]  = useState(0)
  const [clockwise,    setClockwise]    = useState(true)
  const [calibMsg,     setCalibMsg]     = useState<string | null>(null)
  const [savingCalib,  setSavingCalib]  = useState(false)
  const [savingMode,   setSavingMode]   = useState(false)

  const previewTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const [savingNetwork, setSavingNetwork] = useState(false)
  const [savingLeds,    setSavingLeds]    = useState(false)
  const [networkMsg,    setNetworkMsg]    = useState<string | null>(null)
  const [ledsMsg,       setLedsMsg]       = useState<string | null>(null)

  useEffect(() => {
    getStatus().then(setStatus).catch(console.error)
    getMetrics().then(setMetrics).catch(console.error)

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

    const interval = setInterval(() => {
      getMetrics().then(setMetrics).catch(console.error)
    }, 5000)
    return () => clearInterval(interval)
  }, [])

  async function handleNetworkSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSavingNetwork(true)
    setNetworkMsg(null)
    try {
      const res = await saveNetwork({
        wifi_ssid: wifiSSID,
        wifi_pass: wifiPass  || undefined,
        ota_host:  otaHost,
        ota_pass:  otaPass   || undefined,
      })
      setNetworkMsg(res.message)
    } catch {
      setNetworkMsg('Error: could not save network settings.')
    } finally {
      setSavingNetwork(false)
    }
  }

  // Debounced calibration preview — fires 300 ms after the last change.
  const sendPreview = useCallback((
    top: number, right: number, bottom: number, left: number,
    corner: number, cw: boolean,
  ) => {
    if (previewTimer.current) clearTimeout(previewTimer.current)
    previewTimer.current = setTimeout(() => {
      previewCalibration({
        led_top: top, led_right: right, led_bottom: bottom, led_left: left,
        start_corner: corner, clockwise: cw ? 1 : 0,
      }).catch(console.error)
    }, 300)
  }, [])

  function calibChange(
    top: number, right: number, bottom: number, left: number,
    corner: number, cw: boolean,
  ) {
    sendPreview(top, right, bottom, left, corner, cw)
  }

  async function handleCalibSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSavingCalib(true)
    setCalibMsg(null)
    try {
      const res = await saveCalibration({
        led_top: ledTop, led_right: ledRight,
        led_bottom: ledBottom, led_left: ledLeft,
        start_corner: startCorner, clockwise: clockwise ? 1 : 0,
      })
      setCalibMsg(res.message)
    } catch {
      setCalibMsg('Error: could not save calibration.')
    } finally {
      setSavingCalib(false)
    }
  }

  async function handleModeToggle() {
    const next = ledMode === 0 ? 1 : 0
    setSavingMode(true)
    try {
      await saveMode(next)
      setLedMode(next)
    } catch {
      console.error('Failed to switch mode')
    } finally {
      setSavingMode(false)
    }
  }

  async function handleLedsSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSavingLeds(true)
    setLedsMsg(null)
    try {
      const res = await saveLeds({ num_leds: numLeds, brightness, color })
      setLedsMsg(res.message)
    } catch {
      setLedsMsg('Error: could not apply LED settings.')
    } finally {
      setSavingLeds(false)
    }
  }

  return (
    <div className="wrap">
      {/* Header */}
      <header>
        <span className={`dot ${status == null ? '' : status.connected ? 'ok' : 'err'}`} />
        <h1>Ambilight</h1>
        <button
          className={`mode-pill${ledMode === 1 ? ' mode-dynamic' : ''}`}
          onClick={handleModeToggle}
          disabled={savingMode}
          title="Toggle plain / dynamic mode"
        >
          {ledMode === 1 ? 'Dynamic' : 'Plain'}
        </button>
      </header>

      {/* Tabs */}
      <div className="tabs">
        <button className={`tab-btn${tab === 'status'      ? ' active' : ''}`} onClick={() => setTab('status')}>Status</button>
        <button className={`tab-btn${tab === 'wifi'        ? ' active' : ''}`} onClick={() => setTab('wifi')}>WiFi</button>
        <button className={`tab-btn${tab === 'leds'        ? ' active' : ''}`} onClick={() => setTab('leds')}>LEDs</button>
        <button className={`tab-btn${tab === 'calibration' ? ' active' : ''}`} onClick={() => setTab('calibration')}>Calib</button>
      </div>

      {/* Tab: Status */}
      {tab === 'status' && (
        <>
          {status && (
            <section className="card">
              <h2>Network</h2>
              <div className="grid2">
                <Stat label="IP Address" value={status.ip}                                 />
                <Stat label="Mode"       value={status.mode}                               />
                <Stat label="SSID"       value={status.ssid || '—'}                        />
                <Stat label="Signal"     value={status.rssi ? `${status.rssi} dBm` : '—'} />
              </div>
            </section>
          )}

          {metrics && (
            <section className="card">
              <h2>System</h2>
              <div className="grid2">
                <Stat label="Free Heap"  value={fmtKB(metrics.heap_free)}    />
                <Stat label="Total Heap" value={fmtKB(metrics.heap_total)}   />
                <Stat label="Min Heap"   value={fmtKB(metrics.heap_min)}     />
                <Stat label="CPU"        value={`${metrics.cpu_mhz} MHz`}    />
                <Stat label="Uptime"     value={fmtUptime(metrics.uptime_s)} />
              </div>
            </section>
          )}
        </>
      )}

      {/* Tab: WiFi */}
      {tab === 'wifi' && (
        <form onSubmit={handleNetworkSubmit}>
          <section className="card">
            <h2>WiFi</h2>
            <Field label="SSID"     type="text"     value={wifiSSID} onChange={setWifiSSID} placeholder="Network name"                />
            <Field label="Password" type="password" value={wifiPass} onChange={setWifiPass} placeholder="Leave empty to keep current" />
          </section>

          <section className="card">
            <h2>OTA Updates</h2>
            <Field label="Hostname" type="text"     value={otaHost} onChange={setOtaHost} placeholder="ambilight"                   />
            <Field label="Password" type="password" value={otaPass} onChange={setOtaPass} placeholder="Leave empty to keep current" />
          </section>

          <button type="submit" disabled={savingNetwork}>
            {savingNetwork ? 'Saving…' : 'Save & Restart'}
          </button>

          {networkMsg && <div className="toast">{networkMsg}</div>}
        </form>
      )}

      {/* Tab: LEDs */}
      {tab === 'leds' && (
        <form onSubmit={handleLedsSubmit}>
          <section className="card">
            <h2>LED Settings</h2>

            <div className="field">
              <label>LED Count{maxLeds !== undefined && <> — <strong>max {maxLeds}</strong></>}</label>
              <input
                type="number"
                min={1}
                max={maxLeds}
                value={numLeds}
                onChange={(e) => setNumLeds(Math.max(1, Number(e.target.value)))}
              />
            </div>

            <div className="field">
              <label>Brightness — <strong>{brightness}</strong></label>
              <input
                type="range"
                min={0}
                max={255}
                value={brightness}
                onChange={(e) => setBrightness(Number(e.target.value))}
              />
            </div>

            <div className="field">
              <label>Static Color</label>
              <div className="color-row">
                <input
                  type="color"
                  value={color}
                  onChange={(e) => setColor(e.target.value)}
                />
                <span className="color-preview" style={{ background: color }} />
                <code>{color}</code>
              </div>
            </div>
          </section>

          <button type="submit" disabled={savingLeds} className="btn-apply">
            {savingLeds ? 'Applying…' : 'Apply'}
          </button>

          {ledsMsg && <div className="toast">{ledsMsg}</div>}
        </form>
      )}

      {/* Tab: Calibration */}
      {tab === 'calibration' && (
        <form onSubmit={handleCalibSubmit}>
          <section className="card">
            <h2>Strip Layout</h2>
            <CalibrationDiagram
              ledTop={ledTop}     ledRight={ledRight}
              ledBottom={ledBottom} ledLeft={ledLeft}
              startCorner={startCorner} clockwise={clockwise}
              onTopChange={v    => { setLedTop(v);    calibChange(v, ledRight, ledBottom, ledLeft, startCorner, clockwise) }}
              onRightChange={v  => { setLedRight(v);  calibChange(ledTop, v, ledBottom, ledLeft, startCorner, clockwise) }}
              onBottomChange={v => { setLedBottom(v); calibChange(ledTop, ledRight, v, ledLeft, startCorner, clockwise) }}
              onLeftChange={v   => { setLedLeft(v);   calibChange(ledTop, ledRight, ledBottom, v, startCorner, clockwise) }}
              onCornerClick={c  => { setStartCorner(c); calibChange(ledTop, ledRight, ledBottom, ledLeft, c, clockwise) }}
              onDirectionToggle={() => { setClockwise(cw => { const next = !cw; calibChange(ledTop, ledRight, ledBottom, ledLeft, startCorner, next); return next }) }}
            />
          </section>

          <section className="card">
            <h2>Summary</h2>
            <div className="grid2">
              <Stat label="Total LEDs" value={String(ledTop + ledRight + ledBottom + ledLeft)} />
              <Stat label="Start"      value={['Top-Left','Top-Right','Bot-Right','Bot-Left'][startCorner]} />
              <Stat label="Direction"  value={clockwise ? 'Clockwise' : 'Counter-CW'} />
              <Stat label="Mode"       value={ledMode === 1 ? 'Dynamic' : 'Plain'} />
            </div>
          </section>

          <button type="submit" disabled={savingCalib} className="btn-apply">
            {savingCalib ? 'Saving…' : 'Save Calibration'}
          </button>

          {calibMsg && <div className="toast">{calibMsg}</div>}
        </form>
      )}
    </div>
  )
}
