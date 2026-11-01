// Full-screen rotating color wheel (the old launcher's Debug mode): the strip should follow
// the colors at the screen edges. A 96×54 canvas scaled up, redrawn every animation frame —
// the only animation in the app, and only while this screen is open.

import { useEffect, useRef } from 'preact/hooks';
import { Kbd } from '@ambilight/ui';

const W = 96, H = 54;

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  s /= 100; l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = l - c / 2;
  let r = 0, g = 0, b = 0;
  if (h < 60) { r = c; g = x; }
  else if (h < 120) { r = x; g = c; }
  else if (h < 180) { g = c; b = x; }
  else if (h < 240) { g = x; b = c; }
  else if (h < 300) { r = x; b = c; }
  else { r = c; b = x; }
  return [((r + m) * 255) | 0, ((g + m) * 255) | 0, ((b + m) * 255) | 0];
}

export function TestPattern() {
  const canvas = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const el = canvas.current;
    const ctx = el?.getContext('2d');
    if (!el || !ctx) return;
    const img = ctx.createImageData(W, H);
    const cx = W / 2, cy = H / 2, maxR = Math.sqrt(cx * cx + cy * cy);
    let offset = 0;
    let raf = 0;
    const frame = () => {
      const d = img.data;
      let i = 0;
      for (let y = 0; y < H; y++) {
        const dy = y - cy;
        for (let x = 0; x < W; x++) {
          const dx = x - cx;
          const angle = ((Math.atan2(dy, dx) * 180) / Math.PI + 360 + offset) % 360;
          const sat = Math.min(Math.sqrt(dx * dx + dy * dy) / maxR, 1) * 100;
          const rgb = hslToRgb(angle, sat, 50);
          d[i++] = rgb[0]; d[i++] = rgb[1]; d[i++] = rgb[2]; d[i++] = 255;
        }
      }
      ctx.putImageData(img, 0, 0);
      offset = (offset + 3) % 360;
      raf = requestAnimationFrame(frame);
    };
    frame();
    return () => cancelAnimationFrame(raf);
  }, []);

  return (
    <div class="test-pattern">
      <canvas ref={canvas} width={W} height={H} />
      <div class="test-hint">The strip should follow the colors at the screen edges · <Kbd>↩</Kbd> Close</div>
    </div>
  );
}
