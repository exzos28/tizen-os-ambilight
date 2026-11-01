import type { Esp32Status } from '@ambilight/protocol'

interface HeaderProps {
  status: Esp32Status | null
}

export function Header({ status }: HeaderProps) {
  const dotClass = status == null ? '' : status.connected ? 'ok' : 'err'

  return (
    <header>
      <span className={`dot ${dotClass}`} />
      <h1>Ambilight</h1>
    </header>
  )
}
