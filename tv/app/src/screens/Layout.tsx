import { Placeholder } from './Placeholder';
import type { ScreenProps } from './types';

export function Layout(p: ScreenProps) {
  return <Placeholder id="layout" title="Strip layout" bindKeys={p.bindKeys} />;
}
