import { Placeholder } from './Placeholder';
import type { ScreenProps } from './types';

export function Diagnostics(p: ScreenProps) {
  return <Placeholder id="diagnostics" title="Diagnostics" bindKeys={p.bindKeys} />;
}
