// What Outputs and the setup wizard share for HyperHDR: the editable host / port / priority
// fields (real <input>s, so the TV opens its on-screen keyboard), the draft they edit and the
// connection status line.

import { useEffect, useRef } from 'preact/hooks';
import type { HyperHdrOutputSettings, HyperHdrOutputSettingsInEffect, OutputsStatus, ServiceOutputs } from '@ambilight/protocol';
import type { ServiceLink } from '../data';
import type { Tone } from '../status';
import './outputs.css';

/** The HyperHDR settings as typed: strings until they are sent. */
export interface HyperHdrDraft {
  host: string;
  port: string;
  priority: string;
}

export type FieldId = keyof HyperHdrDraft;

export const draftOf = (h: HyperHdrOutputSettingsInEffect): HyperHdrDraft => ({
  host: h.host, port: String(h.port), priority: String(h.priority),
});

/** The draft differs from the settings in effect. */
export const draftDirty = (d: HyperHdrDraft, h: HyperHdrOutputSettingsInEffect) =>
  d.host.trim() !== h.host || d.port.trim() !== String(h.port) || d.priority.trim() !== String(h.priority);

/**
 * The draft as set-outputs settings, or the reason it can't be sent. Only the number format is
 * checked here; ranges and the required host are the service's (its error text is shown).
 */
export function settingsOf(d: HyperHdrDraft, enabled: boolean): HyperHdrOutputSettings | string {
  const port = d.port.trim();
  const priority = d.priority.trim();
  if (!/^\d+$/.test(port)) return 'The port must be a number, 19400 by default.';
  if (!/^\d+$/.test(priority)) return 'The priority must be a number from 50 to 250.';
  return { enabled, host: d.host.trim(), port: +port, priority: +priority };
}

/** The outputs in effect with one part replaced, as set-outputs takes them. */
export const outputsWith = (o: OutputsStatus, change: Partial<ServiceOutputs>): ServiceOutputs => ({
  esp32: { enabled: o.esp32.enabled },
  hyperhdr: { ...o.hyperhdr },
  ...change,
});

// ---------------------------------------------------------------- input

/** How an edit ended: OK / Done, Back / Cancel, or ▲▼ out of the field. */
export type EditEnd = 'commit' | 'cancel' | 'up' | 'down';

// Tizen IME keys: Done and Cancel of the on-screen keyboard.
const KEY_DONE = 65376;
const KEY_CANCEL = 65385;

export interface FieldProps {
  id: string;
  label: string;
  hint: string;
  value: string;
  numeric?: boolean;
  focused: boolean;
  editing: boolean;
  /** The value is not the one in effect yet. */
  pending?: boolean;
  onInput: (value: string) => void;
  onEnd: (how: EditEnd) => void;
}

/**
 * A settings row with a text input. The focus manager highlights it; OK (editing = true) gives
 * the input the real focus, which opens the TV's keyboard. While it has the focus the input
 * keeps every key from the app's remote handler (Backspace deletes, ◀▶ move the cursor).
 */
export function Field(p: FieldProps) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (p.editing && document.activeElement !== el) {
      el.focus();
      const end = el.value.length;
      try { el.setSelectionRange(end, end); } catch { /* not a text input */ }
    } else if (!p.editing && document.activeElement === el) {
      el.blur();
    }
  }, [p.editing]);

  const onKeyDown = (e: KeyboardEvent) => {
    e.stopPropagation();
    if (!p.editing) return;
    let how: EditEnd | null = null;
    switch (e.keyCode) {
      case 13: case KEY_DONE: how = 'commit'; break;
      case 27: case 10009: case KEY_CANCEL: how = 'cancel'; break;
      case 38: how = 'up'; break;
      case 40: how = 'down'; break;
    }
    if (how) {
      e.preventDefault();
      p.onEnd(how);
    }
  };

  return (
    <div class={`out-field${p.focused ? ' focused' : ''}${p.editing ? ' editing' : ''}`}>
      <div class="out-field-text">
        <label for={p.id} class="out-field-label">{p.label}{p.pending && <span class="out-pending"> · not applied</span>}</label>
        <span id={`${p.id}-hint`} class="out-field-hint">{p.hint}</span>
      </div>
      <input
        ref={ref}
        id={p.id}
        class="out-input ui-num"
        type="text"
        inputMode={p.numeric ? 'numeric' : 'url'}
        autocomplete="off"
        spellcheck={false}
        maxLength={p.numeric ? 5 : 253}
        value={p.value}
        tabIndex={-1}
        aria-describedby={`${p.id}-hint`}
        onInput={(e) => p.onInput((e.currentTarget as HTMLInputElement).value)}
        onKeyDown={onKeyDown}
        // The keyboard closed some other way: keep what was typed.
        onBlur={() => { if (p.editing) p.onEnd('commit'); }}
      />
    </div>
  );
}

// ---------------------------------------------------------------- status

export interface ConnectionLine {
  tone: Tone;
  title: string;
  detail: string;
}

/**
 * The HyperHDR connection as the service reports it. lastReplyAt: when the reply count last
 * grew (tracked by the screen), 0 = not seen. watching: a test is running (the first seconds
 * after set-outputs), so "not connected yet" is not an error yet.
 */
export function connectionLine(link: ServiceLink, lastReplyAt: number, watching: boolean): ConnectionLine {
  if (link.kind !== 'up') return { tone: 'off', title: 'Unknown', detail: 'The Ambilight service is not running' };
  const h = link.status.hyperhdr;
  if (!h.enabled) return { tone: 'off', title: 'Off', detail: 'Nothing is sent to the server' };
  if (h.connected && h.replies > 0) {
    const s = lastReplyAt ? Math.max(0, Math.round((Date.now() - lastReplyAt) / 1000)) : -1;
    return { tone: 'ok', title: 'Connected · answering', detail: s >= 0 ? `${h.endpoint} · last reply ${s} s ago` : h.endpoint };
  }
  if (h.connected) return { tone: 'warn', title: 'Connected · no reply yet', detail: h.endpoint };
  if (h.lastError && !watching) return { tone: 'bad', title: 'Not connected', detail: `${h.endpoint} · ${h.lastError}` };
  return { tone: 'warn', title: `Connecting…`, detail: h.endpoint };
}

/** Remembers when HyperHDR's reply count last grew, for "last reply N s ago". */
export function useLastReply(link: ServiceLink): number {
  const ref = useRef({ replies: -1, at: 0 });
  const h = link.kind === 'up' ? link.status.hyperhdr : null;
  const replies = h && h.enabled ? h.replies : -1;
  const r = ref.current;
  if (replies < 0) { r.replies = -1; r.at = 0; }
  else if (replies !== r.replies) {
    // The first count seen is no news (it may be old); a change is a reply that just came.
    if (r.replies >= 0 && replies > r.replies) r.at = Date.now();
    r.replies = replies;
  }
  return r.at;
}
