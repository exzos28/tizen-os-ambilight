import { render } from 'preact';
import '@ambilight/ui/tokens.css';
import '@ambilight/ui/base.css';
import { App } from './App';
import { createBackend } from './data';
// After App: its imports bring the kit's component styles, which style.css places and adjusts.
import './style.css';

render(<App backend={createBackend()} />, document.getElementById('app')!);
