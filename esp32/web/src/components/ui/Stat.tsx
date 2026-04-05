interface StatProps {
  label: string
  value: string
}

export function Stat({ label, value }: StatProps) {
  return (
    <div className="stat">
      <div className="stat-val">{value}</div>
      <div className="stat-key">{label}</div>
    </div>
  )
}
