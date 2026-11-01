// A tiny focus manager for the remote: the screen describes its focusable elements as rows,
// each element with a column position. ◀▶ move within a row, ▲▼ to the nearest column of
// the next non-empty row; `nav` overrides a direction where the geometry would guess wrong.

export type Dir = 'up' | 'down' | 'left' | 'right';
export type Key = Dir | 'ok' | 'back';

export interface FocusItem {
  id: string;
  /** Horizontal position used to pick the closest element when moving up or down. */
  col: number;
  nav?: Partial<Record<Dir, string>>;
}

export type FocusGrid = FocusItem[][];

/** Moving left from the first element of a row leaves the screen (to the rail). */
export const EDGE_LEFT = '<edge-left>';

export function keyOf(e: KeyboardEvent): Key | null {
  switch (e.keyCode) {
    case 37: return 'left';
    case 38: return 'up';
    case 39: return 'right';
    case 40: return 'down';
    case 13: return 'ok';
    case 10009: // Tizen remote Back
    case 27:    // Escape, Backspace: Back in a desktop browser
    case 8:
      return 'back';
  }
  return null;
}

export function contains(grid: FocusGrid, id: string): boolean {
  return grid.some((row) => row.some((it) => it.id === id));
}

export function first(grid: FocusGrid): string | null {
  for (const row of grid) if (row.length) return row[0].id;
  return null;
}

/** The element to focus after moving `dir` from `id`: an id, EDGE_LEFT, or null (stay). */
export function step(grid: FocusGrid, id: string, dir: Dir): string | null {
  let r = -1, c = -1;
  for (let i = 0; i < grid.length && r < 0; i++) {
    const j = grid[i].findIndex((it) => it.id === id);
    if (j >= 0) { r = i; c = j; }
  }
  if (r < 0) return first(grid);
  const item = grid[r][c];

  const explicit = item.nav?.[dir];
  if (explicit && (explicit === EDGE_LEFT || contains(grid, explicit))) return explicit;

  if (dir === 'left') return c > 0 ? grid[r][c - 1].id : EDGE_LEFT;
  if (dir === 'right') return c < grid[r].length - 1 ? grid[r][c + 1].id : null;

  const d = dir === 'down' ? 1 : -1;
  for (let i = r + d; i >= 0 && i < grid.length; i += d) {
    if (!grid[i].length) continue;
    let best = grid[i][0];
    for (const it of grid[i]) if (Math.abs(it.col - item.col) < Math.abs(best.col - item.col)) best = it;
    return best.id;
  }
  return null;
}
