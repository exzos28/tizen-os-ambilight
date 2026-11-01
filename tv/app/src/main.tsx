import { render } from 'preact';
import { App } from './App';
import { createBackend } from './data';
import { fitStage } from './fit';
import './style.css';

const stage = document.getElementById('app')!;
fitStage(stage);
render(<App backend={createBackend()} />, stage);
