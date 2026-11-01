// Phone controls built on the kit's visuals: a native input does the work (touch, keyboard,
// screen readers), the kit draws it.

import type { ComponentChildren } from 'preact'
import { SliderBar } from '@ambilight/ui'

interface RangeInputProps {
  id: string
  value: number
  min: number
  max: number
  step?: number
  /** Spoken value, e.g. "78%". */
  valueText: string
  onInput: (v: number) => void
  color?: string
  disabled?: boolean
  label?: string
}

/** Native range input laid transparently over a SliderBar; the knob lines up with its thumb. */
export function RangeInput({ id, value, min, max, step = 1, valueText, onInput, color, disabled, label }: RangeInputProps) {
  return (
    <div class="range">
      <input id={id} class="range-input" type="range" min={min} max={max} step={step} value={value} disabled={disabled}
        aria-label={label} aria-valuetext={valueText}
        onInput={(e) => onInput(Number(e.currentTarget.value))} />
      <SliderBar value={(value - min) / (max - min)} knob inset color={color} muted={disabled} />
    </div>
  )
}

interface RangeRowProps extends Omit<RangeInputProps, 'valueText'> {
  label: string
  hint?: string
  display: string
}

/** Label, value on the right, optional hint, slider. */
export function RangeRow({ label, hint, display, ...input }: RangeRowProps) {
  return (
    <div class="range-row">
      <div class="row-head">
        <label class="row-label" for={input.id}>{label}</label>
        <output class="row-value ui-num" for={input.id}>{display}</output>
      </div>
      {hint && <p class="ui-muted">{hint}</p>}
      <RangeInput {...input} valueText={display} />
    </div>
  )
}

interface FieldProps {
  label: string
  hint?: ComponentChildren
  error?: boolean
  children: ComponentChildren
}

/** Label above a control, hint (or error) below. Wrap the input itself as `children`. */
export function Field({ label, hint, error, children }: FieldProps) {
  return (
    <label class={`field${error ? ' error' : ''}`}>
      <span class="field-label">{label}</span>
      {children}
      {hint && <span class="field-hint">{hint}</span>}
    </label>
  )
}

interface StepperProps {
  label: string
  value: number
  min: number
  max: number
  onChange: (v: number) => void
  disabled?: boolean
}

/** − number + for LED counts; the number can be typed too. */
export function Stepper({ label, value, min, max, onChange, disabled }: StepperProps) {
  const clamp = (v: number) => Math.max(min, Math.min(max, Math.round(v)))
  return (
    <span class="stepper">
      <button type="button" class="step-btn" aria-label={`Fewer ${label} LEDs`} disabled={disabled || value <= min} onClick={() => onChange(clamp(value - 1))}>−</button>
      <input class="step-input ui-num" type="number" inputMode="numeric" min={min} max={max} value={value} disabled={disabled}
        aria-label={`${label} LEDs`}
        onChange={(e) => {
          // Committed (blur / Enter): clamp, show the clamped number even if the state is unchanged.
          const el = e.currentTarget
          const v = parseInt(el.value, 10)
          const c = Number.isNaN(v) ? value : clamp(v)
          el.value = String(c)
          if (c !== value) onChange(c)
        }} />
      <button type="button" class="step-btn" aria-label={`More ${label} LEDs`} disabled={disabled || value >= max} onClick={() => onChange(clamp(value + 1))}>+</button>
    </span>
  )
}
