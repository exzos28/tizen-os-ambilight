// What every screen gets from the app shell. A screen owns the remote keys while the focus is
// in its content: it registers a handler with bindKeys on every render; returning 'rail' moves
// the focus to the rail, false leaves the key to the shell (Back → Home).

import type { ScreenId } from '../components/Rail';
import type { Controller, ControllerActions } from '../controllers';
import type { Backend, ServiceLink } from '../data';
import type { Problem } from '../status';
import type { KeyHandler } from './Home';

export interface ScreenProps {
  backend: Backend;
  /** The service: its status while it answers. */
  link: ServiceLink;
  /** The ESP32 controllers the service discovered, polled over HTTP while a screen is shown. */
  controllers: Controller[];
  actions: ControllerActions;
  problems: Problem[];
  /** The focus is in this screen's content (not on the rail). */
  active: boolean;
  bindKeys: (h: KeyHandler) => void;
  open: (id: ScreenId) => void;
}
