// Shell: polls the controller, owns its state and the writes, shows the Wi-Fi setup first when
// the controller runs its setup hotspot, otherwise the four tabs.

import { useEffect, useMemo, useRef, useState } from 'preact/hooks'
import { configChanged, type Esp32Config, type Esp32Metrics, type Esp32Status, LedMode, type SaveLedsRequest } from '@ambilight/protocol'
import { Logo } from '@ambilight/ui'
import { errorText, esp } from './api'
import { Header, TABS, TabBar, type TabId } from './components/chrome'
import type { Ctl } from './ctl'
import { findProblems, summary } from './problems'
import { Device } from './screens/Device'
import { Home } from './screens/Home'
import { Layout } from './screens/Layout'
import { Picture } from './screens/Picture'
import { Setup } from './screens/Setup'

const POLL_MS = 2000
/** Status (Wi-Fi signal) every this many polls. */
const STATUS_EVERY = 5
/** Failed polls in a row before the controller counts as not answering. */
const OFFLINE_AFTER = 2

const SUBTITLE: Record<TabId, string> = {
  home: '',
  picture: 'Color and power',
  layout: 'Where the LEDs are',
  device: 'Network and updates',
}

const tabFromHash = (): TabId => TABS.find((t) => `#${t.id}` === location.hash)?.id ?? 'home'

export function App() {
  const [status, setStatus] = useState<Esp32Status | null>(null)
  const [cfg, setCfg] = useState<Esp32Config | null>(null)
  const [metrics, setMetrics] = useState<Esp32Metrics | null>(null)
  const [failures, setFailures] = useState(0)
  const [visible, setVisible] = useState(!document.hidden)
  const [tab, setTab] = useState<TabId>(tabFromHash)
  const [skipSetup, setSkipSetup] = useState(false)
  const [toast, setToast] = useState<{ text: string; tone: 'ok' | 'bad'; id: number } | null>(null)

  const cfgRef = useRef(cfg)
  cfgRef.current = cfg
  const statusRef = useRef(status)
  statusRef.current = status
  const failuresRef = useRef(failures)
  failuresRef.current = failures
  const reloadRef = useRef(false)
  // config_rev of the /config we show: another client (the TV app) changing a setting bumps it.
  const revRef = useRef<number | null>(null)

  // ---------------------------------------------------------------- poll
  useEffect(() => {
    const onVis = () => setVisible(!document.hidden)
    const onHash = () => setTab(tabFromHash())
    document.addEventListener('visibilitychange', onVis)
    window.addEventListener('hashchange', onHash)
    return () => {
      document.removeEventListener('visibilitychange', onVis)
      window.removeEventListener('hashchange', onHash)
    }
  }, [])

  useEffect(() => {
    if (!visible) return
    let n = 0
    let timer = 0
    let alive = true
    // One request round at a time (the controller serves one request at a time), then wait.
    const tick = async () => {
      const i = n++
      const wasOffline = failuresRef.current >= OFFLINE_AFTER
      try {
        const m = await esp.getMetrics()
        if (!alive) return
        setMetrics(m)
        // Re-read the settings when the revision changed (another client — or our own save,
        // which then confirms what we show). Not while an edit of ours is waiting or on its
        // way, and a read answered after we changed something is dropped: an older answer
        // must never put the old value back on the screen. The next poll catches up.
        const idle = !Object.keys(pending.current).length && writes.current.inFlight === 0
        const changed = configChanged(revRef.current, m) && idle
        const jobs: Promise<unknown>[] = []
        if (!cfgRef.current || wasOffline || reloadRef.current || changed) {
          reloadRef.current = false
          const rev = typeof m.config_rev === 'number' ? m.config_rev : null
          const seq = writes.current.seq
          jobs.push(esp.getConfig().then((c) => {
            if (alive && writes.current.seq === seq) { setCfg(c); revRef.current = rev }
          }))
        }
        if (!statusRef.current || wasOffline || i % STATUS_EVERY === 0) jobs.push(esp.getStatus().then((s) => alive && setStatus(s)))
        await Promise.all(jobs)
        if (alive) setFailures(0)
      } catch {
        if (alive) setFailures((f) => f + 1)
      }
      if (alive) timer = window.setTimeout(tick, POLL_MS)
    }
    tick()
    return () => { alive = false; clearTimeout(timer) }
  }, [visible])

  const offline = failures >= OFFLINE_AFTER
  const problems = useMemo(() => findProblems(cfg, metrics, offline), [cfg, metrics, offline])

  // ---------------------------------------------------------------- writes
  const pending = useRef<SaveLedsRequest>({})
  // Our own writes: `seq` grows with every change shown optimistically, `inFlight` counts the
  // requests not answered yet (see the poll above).
  const writes = useRef({ seq: 0, inFlight: 0 })
  const track = <T,>(p: Promise<T>) => {
    writes.current.inFlight++
    return p.finally(() => { writes.current.inFlight-- })
  }
  const saveTimer = useRef(0)

  const notify = (text: string, tone: 'ok' | 'bad' = 'ok') => setToast({ text, tone, id: Date.now() })
  useEffect(() => {
    if (!toast) return
    const id = setTimeout(() => setToast(null), toast.tone === 'bad' ? 6000 : 3000)
    return () => clearTimeout(id)
  }, [toast])

  const reload = () => {
    reloadRef.current = true
    esp.getConfig().then(setCfg, () => undefined)
  }
  const failed = (e: unknown) => {
    notify(`Not saved: ${errorText(e)}`, 'bad')
    reload()
  }
  const patch = (p: Partial<Esp32Config>) => {
    writes.current.seq++
    setCfg((c) => (c ? { ...c, ...p } : c))
  }

  const flush = () => {
    const body = pending.current
    pending.current = {}
    if (Object.keys(body).length) track(esp.saveLeds(body)).catch(failed)
  }

  const ctl: Ctl = {
    status, cfg, metrics, offline, problems, patch, reload, notify,
    saveLeds(body, delayMs = 300) {
      patch(body)
      Object.assign(pending.current, body)
      clearTimeout(saveTimer.current)
      saveTimer.current = window.setTimeout(flush, delayMs)
    },
    setPower(on) {
      patch({ power: on })
      track(esp.savePower({ power: on ? 1 : 0 })).catch(failed)
    },
    setMode(mode) {
      patch({ led_mode: mode })
      track(esp.saveMode({ led_mode: mode })).catch(failed)
    },
    setColor(color, delayMs = 0) {
      ctl.saveLeds({ color }, delayMs)
      if (cfg?.led_mode !== LedMode.plain) ctl.setMode(LedMode.plain)
    },
    go(t) {
      if (location.hash !== `#${t}`) location.hash = t
      setTab(t)
      window.scrollTo(0, 0)
    },
  }

  // ---------------------------------------------------------------- view
  if (!status && !offline) {
    return (
      <main class="splash" aria-busy="true">
        <Logo size={64} />
        <p class="ui-desc">Connecting to the controller…</p>
      </main>
    )
  }

  if (status?.mode === 'AP' && !skipSetup) return <Setup ctl={ctl} onSkip={() => setSkipSetup(true)} />

  const pill = summary(cfg, metrics, problems, offline)
  const title = tab === 'home' ? 'Ambilight' : TABS.find((t) => t.id === tab)!.label
  const sub = tab === 'home'
    ? [status?.mode === 'AP' ? 'Setup hotspot' : status?.ip, status?.version ? `v${status.version}` : ''].filter(Boolean).join(' · ')
    : SUBTITLE[tab]

  return (
    <div class="shell">
      <Header title={title} subtitle={sub} pill={pill} />
      <main class="screen">
        {tab === 'home' && <Home ctl={ctl} />}
        {tab === 'picture' && <Picture ctl={ctl} />}
        {tab === 'layout' && <Layout ctl={ctl} />}
        {tab === 'device' && <Device ctl={ctl} />}
      </main>
      <div class={`toast${toast ? ' show' : ''}${toast?.tone === 'bad' ? ' bad' : ''}`} role="status" aria-live="polite">{toast?.text}</div>
      <TabBar current={tab} problems={problems.length} onChange={ctl.go} />
    </div>
  )
}
