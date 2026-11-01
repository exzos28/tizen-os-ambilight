// The launcher is laid out on a fixed 1920×1080 stage (TV web apps get a 1920-wide viewport,
// and the service samples the screen at fixed positions near the edges, so the geometry has
// to be predictable). This scales the whole stage to whatever window it actually gets —
// a 1280×720 set, an "extensive" viewport of another size, a desktop browser — keeping the
// aspect ratio and centring it. At exactly 1920×1080 it changes nothing.

export const STAGE_W = 1920;
export const STAGE_H = 1080;

export function fitStage(stage: HTMLElement): () => void {
  const apply = () => {
    const scale = Math.min(window.innerWidth / STAGE_W, window.innerHeight / STAGE_H);
    const s = stage.style;
    s.width = `${STAGE_W}px`;
    s.height = `${STAGE_H}px`;
    s.position = 'absolute';
    s.left = `${(window.innerWidth - STAGE_W * scale) / 2}px`;
    s.top = `${(window.innerHeight - STAGE_H * scale) / 2}px`;
    s.transformOrigin = '0 0';
    s.transform = Math.abs(scale - 1) < 0.001 ? '' : `scale(${scale})`;
  };
  apply();
  window.addEventListener('resize', apply);
  return () => window.removeEventListener('resize', apply);
}
