import { defineConfig } from 'vite'
import { livePlugin } from './vite-plugins/live.ts'

export default defineConfig({
  plugins: [livePlugin()],
  server: {
    port: 5815,
    strictPort: true,
  },
  build: {
    rollupOptions: {
      input: {
        main: 'index.html',
        gallery: 'gallery.html',
      },
    },
  },
})
