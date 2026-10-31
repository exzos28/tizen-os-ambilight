interface FieldProps {
  label: string
  type: 'text' | 'password' | 'number'
  value: string
  onChange: (v: string) => void
  placeholder?: string
  min?: number
  max?: number
}

export function Field({ label, type, value, onChange, placeholder, min, max }: FieldProps) {
  return (
    <div className="field">
      <label>{label}</label>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        min={min}
        max={max}
        autoComplete="off"
      />
    </div>
  )
}
