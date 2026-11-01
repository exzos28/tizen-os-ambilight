import { defineConfig } from 'vite'
import { viteSingleFile } from 'vite-plugin-singlefile'

// One self-contained HTML file (scripts and styles inlined) that scripts/embed_web.py gzips into
// the firmware. `vite build --mode mock` (npm run build:mock) builds the same page with the mock
// controller (src/mock.ts) into dist-mock/ for screenshots; the normal build leaves it out.
export default defineConfig({
  plugins: [viteSingleFile()],
  esbuild: {
    jsx: 'automatic',
    jsxImportSource: 'preact',
  },
  resolve: {
    // @ambilight/ui is linked from packages/ui and may have its own node_modules/preact (its
    // devDependency): always use this app's copy, one Preact in the bundle.
    dedupe: ['preact'],
  },
  server: {
    fs: { allow: ['.', '../../packages/protocol', '../../packages/ui'] },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    target: 'es2020',
    cssTarget: ['chrome87', 'safari14'],
  },
})
