import { render } from 'preact';
import { App } from './App';
import { createBackend } from './data';
import './style.css';

render(<App backend={createBackend()} />, document.getElementById('app')!);
