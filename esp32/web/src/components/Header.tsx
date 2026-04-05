import type { StatusResponse } from '../api'

interface HeaderProps {
  status: StatusResponse | null
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
