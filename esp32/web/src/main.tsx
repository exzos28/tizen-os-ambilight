import { render } from 'preact'
import '@ambilight/ui/tokens.css'
import '@ambilight/ui/base.css'
import { App } from './App'
// After App: its imports bring the kit's component styles, which app.css adjusts.
import './app.css'

render(<App />, document.getElementById('app')!)
