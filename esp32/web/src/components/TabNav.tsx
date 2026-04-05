export type Tab = 'status' | 'wifi' | 'leds'

const TABS: { id: Tab; label: string }[] = [
  { id: 'status', label: 'Status' },
  { id: 'wifi',   label: 'WiFi'   },
  { id: 'leds',   label: 'LEDs'   },
]

interface TabNavProps {
  active: Tab
  onChange: (tab: Tab) => void
}

export function TabNav({ active, onChange }: TabNavProps) {
  return (
    <div className="tabs">
      {TABS.map(({ id, label }) => (
        <button
          key={id}
          className={`tab-btn${active === id ? ' active' : ''}`}
          onClick={() => onChange(id)}
        >
          {label}
        </button>
      ))}
    </div>
  )
}
