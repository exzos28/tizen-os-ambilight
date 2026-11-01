import { render } from 'preact';
import '@ambilight/ui/tokens.css';
import '@ambilight/ui/base.css';
import { App } from './App';
import { createBackend } from './data';
import { fitStage } from './fit';
// After App: its imports bring the kit's component styles, which style.css places and adjusts.
import './style.css';

const stage = document.getElementById('app')!;
fitStage(stage);
render(<App backend={createBackend()} />, stage);
