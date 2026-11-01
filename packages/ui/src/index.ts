// @ambilight/ui — the shared look of the TV launcher and the controller web app. Import
// '@ambilight/ui/tokens.css' and '@ambilight/ui/base.css' once in the app; every component
// brings its own small stylesheet, so a bundle only contains what it renders.
export type { Tone } from './tone.ts'
export * from './icons.tsx'
export { Card, type CardProps } from './Card.tsx'
export { Switch, type SwitchProps } from './Switch.tsx'
export { SliderBar, type SliderBarProps } from './SliderBar.tsx'
export { Button, type ButtonProps } from './Button.tsx'
export { StatusDot, StatusPill, StatusLine, StatusList, type StatusLineProps } from './Status.tsx'
export { ProblemBanner, type ProblemBannerProps } from './ProblemBanner.tsx'
export { ColorSwatches, SWATCHES, type ColorSwatchesProps, type Swatch } from './ColorSwatches.tsx'
export { Option, type OptionProps } from './Option.tsx'
export { Kbd, KeyHint } from './KeyHint.tsx'
export { LivePreview, type LivePreviewProps, type PreviewConfig } from './LivePreview.tsx'
export { StripLayoutDiagram, type StripLayoutDiagramProps } from './StripLayoutDiagram.tsx'
export * from './geometry.ts'
