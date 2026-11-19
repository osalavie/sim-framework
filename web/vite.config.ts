import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import vueDevTools from 'vite-plugin-vue-devtools'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { NaiveUiResolver } from 'unplugin-vue-components/resolvers'
import tailwindcss from '@tailwindcss/vite'
import { existsSync, readFileSync } from 'node:fs'

const devCert = new URL('./.cert/dev-cert.pem', import.meta.url)
const devKey = new URL('./.cert/dev-key.pem', import.meta.url)

function getDevHttps() {
  const hasCert = existsSync(devCert)
  const hasKey = existsSync(devKey)

  if (hasCert !== hasKey) {
    throw new Error('Local HTTPS requires both web/.cert/dev-cert.pem and web/.cert/dev-key.pem')
  }

  return hasCert
    ? { cert: readFileSync(devCert), key: readFileSync(devKey) }
    : undefined
}

// https://vite.dev/config/
export default defineConfig(({ command }) => ({
  plugins: [
    vue(),
    vueJsx(),
    ...(command === 'serve' ? [vueDevTools()] : []),
    tailwindcss(),
    AutoImport({
      imports: [
        'vue',
        {
          'naive-ui': [],
        },
      ],
    }),
    Components({
      resolvers: [NaiveUiResolver()],
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@modules': fileURLToPath(new URL('./src/modules', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    https: command === 'serve' ? getDevHttps() : undefined,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET || 'https://localhost:7777',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: process.env.VITE_API_PROXY_TARGET || 'https://localhost:7777',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
  build: {
    // vendor-ui is intentionally kept as a single chunk (see manualChunks below)
    // because splitting Naive UI caused circular chunk dependencies and a runtime
    // ReferenceError. At ~677 kB minified / ~175 kB gzip it is under the 1 MiB
    // async-chunk budget, so raise the advisory warning limit instead of re-splitting.
    chunkSizeWarningLimit: 700,
    manifest: true,
    rollupOptions: {
      // Suppress two categories of informational warnings that are not actionable
      // in this codebase. Keep all other warnings flowing through to defaultHandler.
      onwarn(warning, defaultHandler) {
        const message = warning.message ?? ''

        // 1) `@microsoft/signalr@10` ships with `/*#__PURE__*/` annotations in
        //    positions Rollup can't interpret. Rollup already drops them safely;
        //    no action available on our side. Upstream issue, not ours.
        if (message.includes('contains an annotation that Rollup cannot interpret')) {
          return
        }

        // 2) Several modules are BOTH statically imported (e.g. by main.ts,
        //    Header, route components) AND dynamically imported inside request.ts
        //    / dynamicRouteLoader.ts. The dynamic imports are intentional to
        //    break circular dependencies (request ↔ user store ↔ api ↔ request)
        //    and to defer loading of Layout/router in interceptor paths. At
        //    runtime the dynamic import still works — Rollup just can't move
        //    the module into its own chunk. Safe to silence.
        if (
          message.includes('dynamically imported by') &&
          message.includes('but also statically imported by')
        ) {
          return
        }

        defaultHandler(warning)
      },
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return

          // -- naive-ui: keep the whole component library in one chunk. Naive UI
          //    internally cross-imports shared utils from subpaths such as
          //    `es/_utils/vue/*` and `es/_utils/*`. Splitting it into
          //    `vendor-ui` / `vendor-ui-heavy` created circular chunk
          //    dependencies and caused `Cannot access 'Ue' before initialization`
          //    at runtime. Put this check before the Vue check so those internal
          //    `/vue/`-looking paths are not misclassified as Vue core.
          if (id.includes('/node_modules/naive-ui/')) {
            return 'vendor-ui'
          }

          // -- core framework (Vue ecosystem) --
          if (
            id.includes('/node_modules/vue/') ||
            id.includes('/node_modules/@vue/') ||
            id.includes('/node_modules/vue-router/') ||
            id.includes('/node_modules/pinia/')
          ) {
            return 'vendor-vue'
          }

          // -- rich-text editor (tiptap + prosemirror), only article-editor pages --
          if (
            id.includes('/@tiptap/') ||
            id.includes('/prosemirror-') ||
            id.includes('/orderedmap/') ||
            id.includes('/rope-sequence/') ||
            id.includes('/w3c-keyname/')
          ) {
            return 'vendor-tiptap'
          }

          // -- flowchart editor + canvas image export, only workflow/editor pages --
          //    html-to-image is used solely by the canvas export feature and must
          //    stay out of the eagerly loaded catch-all `vendor` chunk.
          if (id.includes('/@vue-flow/') || id.includes('/html-to-image/')) {
            return 'vendor-flow'
          }

          // -- charts, only analytics / dashboard pages --
          if (id.includes('/d3-')) {
            return 'vendor-d3'
          }

          // -- content rendering (markdown, syntax highlight, sanitisation) --
          if (
            id.includes('/marked/') ||
            id.includes('/highlight.js/') ||
            id.includes('/lowlight/') ||
            id.includes('/prismjs/') ||
            id.includes('/dompurify/')
          ) {
            return 'vendor-content'
          }

          // -- icon fonts --
          if (id.includes('/@vicons/') || id.includes('/vfonts/')) {
            return 'vendor-icons'
          }

          // -- realtime hub client, loaded on first connect (see utils/signalrLoader) --
          //    Needs its own chunk: the catch-all `vendor` is part of the entry
          //    graph, so a module assigned there would stay eagerly loaded even
          //    though every import of it is dynamic.
          if (id.includes('/node_modules/@microsoft/signalr/')) {
            return 'vendor-signalr'
          }

          // -- QR code generation, only the 2FA / share dialogs --
          //    Isolated so the eager page does not carry a library that a lazy
          //    route alone uses (the catch-all `vendor` chunk would pull it in).
          if (
            id.includes('/node_modules/qrcode/') ||
            id.includes('/node_modules/dijkstrajs/') ||
            id.includes('/node_modules/encode-utf8/') ||
            id.includes('/node_modules/pngjs/')
          ) {
            return 'vendor-qrcode'
          }

          // -- everything else --
          return 'vendor'
        },
      },
    },
  },
}))
