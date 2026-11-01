import { Placeholder } from './Placeholder';
import type { ScreenProps } from './types';

export function Picture(p: ScreenProps) {
  return <Placeholder id="picture" title="Picture" bindKeys={p.bindKeys} />;
}
