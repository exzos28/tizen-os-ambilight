import { type Esp32Client, createEsp32Client } from '@ambilight/protocol'
import { createMockClient, mockScenario } from './mock'

// The web app is served by the controller itself, so the API is on the page's own origin.
//
// Mock controller (src/mock.ts) for development and screenshots: only in `vite` (dev) and
// `vite build --mode mock`, chosen with ?mock=ok|ap|noframes|limited|offline. In the flash
// build both conditions are the constants `false` and `"production" === "mock"`, so the branch
// and the whole mock module are dropped from the bundle.
const scenario = import.meta.env.DEV || import.meta.env.MODE === 'mock'
  ? mockScenario(location.search, import.meta.env.MODE === 'mock')
  : null

export const esp: Esp32Client = scenario ? createMockClient(scenario) : createEsp32Client('')

export const errorText = (e: unknown) => (e instanceof Error ? e.message : String(e))
