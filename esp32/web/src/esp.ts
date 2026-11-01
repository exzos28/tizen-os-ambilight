import { createEsp32Client } from '@ambilight/protocol'

// The web UI is served by the controller itself, so the API is on the page's own origin.
export const esp = createEsp32Client('')
