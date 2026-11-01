import { rmSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig, type Plugin } from 'vite';

// The web app is built into ../ui, next to the tracked config.xml, icon.png and
// tizen_web_project.yaml, so the VS Code Tizen extension and make_package.sh pack it as is.
const outDir = fileURLToPath(new URL('../ui', import.meta.url));

// The Tizen web runtime loads the widget from file://, where module scripts and
// crossorigin requests are refused: ship one classic deferred script instead.
function tizenWidget(): Plugin {
  return {
    name: 'tizen-widget',
    apply: 'build',
    buildStart() {
      // emptyOutDir would delete config.xml: remove only what this build writes.
      rmSync(resolve(outDir, 'assets'), { recursive: true, force: true });
      rmSync(resolve(outDir, 'index.html'), { force: true });
    },
    transformIndexHtml: {
      order: 'post',
      handler: (html) => html.replace(/<script type="module" crossorigin/g, '<script defer').replace(/ crossorigin/g, ''),
    },
  };
}

export default defineConfig({
  base: './',
  plugins: [tizenWidget()],
  oxc: {
    jsx: { runtime: 'automatic', importSource: 'preact' },
  },
  build: {
    outDir,
    emptyOutDir: false,
    // Tizen 7.0 (2023 TVs), the oldest supported: Chromium M94 (Samsung web engine specifications).
    target: 'chrome94',
    cssTarget: 'chrome94',
    modulePreload: false,
    // One stylesheet next to the script (with an iife bundle Vite would inject it from JS).
    cssCodeSplit: false,
    assetsInlineLimit: 0,
    reportCompressedSize: true,
    rolldownOptions: {
      output: {
        format: 'iife',
        entryFileNames: 'assets/app.js',
        assetFileNames: 'assets/app[extname]',
      },
    },
  },
});
