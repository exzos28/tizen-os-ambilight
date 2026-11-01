// App shell: the rail and the one mounted screen. Owns the remote keys, the status poll
// (once per second while the page is visible) and the controllers.

import { useEffect, useMemo, useRef, useState } from 'preact/hooks';
import { RAIL, Rail, type ScreenId } from './components/Rail';
import { useControllers } from './controllers';
import type { ServiceStatus } from '@ambilight/protocol';
import type { Backend, ServiceLink } from './data';
import { keyOf } from './focus';
import { Home, type KeyHandler } from './screens/Home';
import { Diagnostics } from './screens/Diagnostics';
import { Layout } from './screens/Layout';
import { Outputs } from './screens/Outputs';
import { Picture } from './screens/Picture';
import { TestPattern } from './screens/TestPattern';
import type { ScreenProps } from './screens/types';
import { problems as findProblems } from './status';

const POLL_MS = 1000;

/** Desktop preview only (no window.tizen): ?screen=picture|layout|outputs|diagnostics|test opens that screen
 *  (with test, &pattern=<id> picks the pattern: see PATTERNS in TestPattern.tsx). */
const devScreen = window.tizen ? null : new URLSearchParams(location.search).get('screen');

export function App({ backend }: { backend: Backend }) {
  const [link, setLink] = useState<ServiceLink>({ kind: 'checking' });
  const linkRef = useRef(link);
  const [prevStatus, setPrevStatus] = useState<ServiceStatus | null>(null);
  const [tick, setTick] = useState(0);
  const [visible, setVisible] = useState(!document.hidden);
  const initial = RAIL.find((r) => r.id === devScreen)?.id ?? 'home';
  const [screen, setScreen] = useState<ScreenId>(initial);
  const [testPattern, setTestPattern] = useState(devScreen === 'test');
  const [zone, setZone] = useState<'rail' | 'content'>('content');
  const [railIndex, setRailIndex] = useState(RAIL.findIndex((r) => r.id === initial));
  const keys = useRef<KeyHandler | null>(null);
  /** Where the focus goes when Home mounts again (after the test pattern). */
  const [homeFocus, setHomeFocus] = useState<string | null>(null);

  // ---------------------------------------------------------------- status poll
  useEffect(() => backend.service.subscribe((l) => {
    const old = linkRef.current;
    linkRef.current = l;
    setPrevStatus(old.kind === 'up' ? old.status : null);
    setLink(l);
  }), [backend]);

  useEffect(() => {
    const onVis = () => setVisible(!document.hidden);
    document.addEventListener('visibilitychange', onVis);
    return () => document.removeEventListener('visibilitychange', onVis);
  }, []);

  useEffect(() => {
    if (!visible || testPattern) return;
    const poll = () => {
      backend.service.requestStatus();
      setTick((t) => t + 1);
    };
    poll();
    const id = setInterval(poll, POLL_MS);
    return () => clearInterval(id);
  }, [visible, testPattern, backend]);

  const hosts = link.kind === 'up' && link.status.esp32.enabled ? link.status.esp32.hosts : link.kind === 'up' ? [] : null;
  const { controllers, actions } = useControllers(backend, hosts, visible && !testPattern, tick);
  const problems = useMemo(() => findProblems(link, controllers), [link, controllers]);

  // ---------------------------------------------------------------- remote
  const exit = () => backend.platform.exit();
  const open = (id: ScreenId) => {
    setHomeFocus(null);
    setScreen(id);
    setRailIndex(RAIL.findIndex((r) => r.id === id));
    keys.current = null; // the new screen registers its own handler when it renders
    setZone('content');
  };

  // The listener is registered once and calls the handler of the latest render, so the
  // 1 s status poll doesn't re-subscribe it on every render.
  const onKeyRef = useRef<(e: KeyboardEvent) => void>(() => {});
  useEffect(() => {
    const listener = (e: KeyboardEvent) => onKeyRef.current(e);
    document.addEventListener('keydown', listener);
    return () => document.removeEventListener('keydown', listener);
  }, []);
  {
    const onKey = (e: KeyboardEvent) => {
      const key = keyOf(e);
      if (!key) return;
      e.preventDefault();
      if (testPattern) {
        if (key === 'back' || key === 'ok') {
          setHomeFocus('test-pattern');
          setTestPattern(false);
        } else keys.current?.(key);
        return;
      }
      if (zone === 'rail') {
        if (key === 'up') setRailIndex((i) => Math.max(0, i - 1));
        else if (key === 'down') setRailIndex((i) => Math.min(RAIL.length - 1, i + 1));
        else if (key === 'ok') open(RAIL[railIndex].id);
        else if (key === 'right') setZone('content');
        else if (key === 'back') { if (screen === 'home') exit(); else open('home'); }
        return;
      }
      const r = keys.current ? keys.current(key) : false;
      if (r === 'rail') {
        setRailIndex(RAIL.findIndex((it) => it.id === screen));
        setZone('rail');
      } else if (!r && key === 'back') {
        if (screen === 'home') exit(); else open('home');
      }
    };
    onKeyRef.current = onKey;
  }

  // ---------------------------------------------------------------- view
  const bindKeys = (h: KeyHandler) => { keys.current = h; };
  if (testPattern) return <TestPattern bindKeys={bindKeys} />;

  const screenProps: ScreenProps = {
    backend, link, controllers, actions, problems, active: zone === 'content', bindKeys, open,
  };

  return (
    <div class="shell">
      <Rail current={screen} focused={zone === 'rail' ? railIndex : -1} problems={problems.length} version={backend.platform.appVersion()} />
      {screen === 'home' ? (
        <Home
          backend={backend}
          link={link}
          prev={prevStatus}
          controllers={controllers}
          actions={actions}
          problems={problems}
          model={backend.platform.modelName()}
          initialFocus={homeFocus}
          active={zone === 'content'}
          bindKeys={bindKeys}
          onTestPattern={() => setTestPattern(true)}
          onControllers={() => open('outputs')}
        />
      ) : screen === 'picture' ? (
        <Picture {...screenProps} />
      ) : screen === 'layout' ? (
        <Layout {...screenProps} />
      ) : screen === 'outputs' ? (
        <Outputs {...screenProps} />
      ) : (
        <Diagnostics {...screenProps} />
      )}
    </div>
  );
}
