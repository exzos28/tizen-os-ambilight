interface FieldProps {
  label: string
  type: 'text' | 'password'
  value: string
  onChange: (v: string) => void
  placeholder?: string
}

export function Field({ label, type, value, onChange, placeholder }: FieldProps) {
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
