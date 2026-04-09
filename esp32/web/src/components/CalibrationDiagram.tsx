import { useState } from 'react'

const CORNER_CX = [70, 230, 230, 70]
const CORNER_CY = [50, 50, 170, 170]

// In CW mode: TOP →, RIGHT ↓, BOTTOM ←, LEFT ↑. CCW reverses all.
const CW_ARROWS  = ['→', '↓', '←', '↑']
const CCW_ARROWS = ['←', '↑', '→', '↓']

const SIDE_COLORS = ['#ff0000', '#00ff00', '#ffff00', '#0000ff'] // TOP, RIGHT, BOTTOM, LEFT

const SIDES = ['top', 'right', 'bottom', 'left'] as const
type Side = typeof SIDES[number]

function sideStripOrder(startCorner: number, clockwise: boolean): number[] {
  const order: number[] = [0, 0, 0, 0]
  for (let s = 0; s < 4; s++) {
    const si = clockwise
      ? (startCorner + s) % 4
      : (startCorner + 3 - s + 4) % 4
    order[si] = s + 1
  }
  return order
}

interface CalibrationDiagramProps {
  ledTop: number
  ledRight: number
  ledBottom: number
  ledLeft: number
  startCorner: number
  clockwise: boolean
  onTopChange: (n: number) => void
  onRightChange: (n: number) => void
  onBottomChange: (n: number) => void
  onLeftChange: (n: number) => void
  onCornerClick: (c: number) => void
  onDirectionToggle: () => void
}

export function CalibrationDiagram({
  ledTop, ledRight, ledBottom, ledLeft,
  startCorner, clockwise,
  onTopChange, onRightChange, onBottomChange, onLeftChange,
  onCornerClick, onDirectionToggle,
}: CalibrationDiagramProps) {
  const [editing, setEditing] = useState<Side | null>(null)
  const [draft,   setDraft]   = useState('')

  const arrows = clockwise ? CW_ARROWS : CCW_ARROWS
  const orders = sideStripOrder(startCorner, clockwise)

  const sideValues   = { top: ledTop, right: ledRight, bottom: ledBottom, left: ledLeft }
  const sideHandlers = { top: onTopChange, right: onRightChange, bottom: onBottomChange, left: onLeftChange }

  function startEdit(side: Side, current: number) {
    setEditing(side)
    setDraft(String(current))
  }

  function commitEdit(side: Side) {
    const v = Math.max(0, Math.min(255, parseInt(draft) || 0))
    sideHandlers[side](v)
    setEditing(null)
  }

  const sideStyle = { cursor: 'pointer' } as const

  return (
    <div className="calib-diagram">
      <svg viewBox="0 0 300 220" width="100%">
        {/* TV screen */}
        <rect x="70" y="50" width="160" height="120" fill="#0a111e" stroke="#334155" strokeWidth="1.5" rx="3"/>
        <text x="150" y="117" textAnchor="middle" fill="#334155" fontSize="11" fontFamily="monospace">TV</text>

        {/* TOP */}
        <rect x="74" y="28" width="152" height="20" rx="4"
              fill={SIDE_COLORS[0] + '22'} stroke={SIDE_COLORS[0]} strokeWidth="1.2"
              style={sideStyle} onClick={onDirectionToggle}/>
        <text x="150" y="41" textAnchor="middle" fill={SIDE_COLORS[0]} fontSize="10" fontWeight="700"
              style={sideStyle} onClick={onDirectionToggle}>
          {arrows[0]} #{orders[0]}
        </text>

        {/* BOTTOM */}
        <rect x="74" y="172" width="152" height="20" rx="4"
              fill={SIDE_COLORS[2] + '22'} stroke={SIDE_COLORS[2]} strokeWidth="1.2"
              style={sideStyle} onClick={onDirectionToggle}/>
        <text x="150" y="185" textAnchor="middle" fill={SIDE_COLORS[2]} fontSize="10" fontWeight="700"
              style={sideStyle} onClick={onDirectionToggle}>
          {arrows[2]} #{orders[2]}
        </text>

        {/* RIGHT */}
        <rect x="232" y="54" width="20" height="112" rx="4"
              fill={SIDE_COLORS[1] + '22'} stroke={SIDE_COLORS[1]} strokeWidth="1.2"
              style={sideStyle} onClick={onDirectionToggle}/>
        <text x="242" y="114" textAnchor="middle" fill={SIDE_COLORS[1]} fontSize="10" fontWeight="700"
              transform="rotate(90 242 114)" style={sideStyle} onClick={onDirectionToggle}>
          {arrows[1]} #{orders[1]}
        </text>

        {/* LEFT */}
        <rect x="48" y="54" width="20" height="112" rx="4"
              fill={SIDE_COLORS[3] + '22'} stroke={SIDE_COLORS[3]} strokeWidth="1.2"
              style={sideStyle} onClick={onDirectionToggle}/>
        <text x="58" y="114" textAnchor="middle" fill={SIDE_COLORS[3]} fontSize="10" fontWeight="700"
              transform="rotate(-90 58 114)" style={sideStyle} onClick={onDirectionToggle}>
          {arrows[3]} #{orders[3]}
        </text>

        {/* Corner dots */}
        {[0, 1, 2, 3].map(c => (
          <circle key={c}
            cx={CORNER_CX[c]} cy={CORNER_CY[c]} r="9"
            fill={startCorner === c ? '#f97316' : '#1e293b'}
            stroke={startCorner === c ? '#f97316' : '#475569'}
            strokeWidth="2"
            style={sideStyle}
            onClick={() => onCornerClick(c)}
          />
        ))}
        <circle cx={CORNER_CX[startCorner]} cy={CORNER_CY[startCorner]} r="4" fill="#fff"/>
      </svg>

      <div className="calib-counts">
        {SIDES.map((side, i) => (
          <div key={side} className="calib-count-row">
            <span className="calib-side-label" style={{ color: SIDE_COLORS[i] }}>
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
                  onKeyDown={e => {
                    if (e.key === 'Enter')  commitEdit(side)
                    if (e.key === 'Escape') setEditing(null)
                  }}
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
