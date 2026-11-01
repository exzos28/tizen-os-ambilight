import { Placeholder } from './Placeholder';
import type { ScreenProps } from './types';

export function Outputs(p: ScreenProps) {
  return <Placeholder id="outputs" title="Outputs" bindKeys={p.bindKeys} />;
}
