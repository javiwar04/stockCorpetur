import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      // Redirige las llamadas /api al backend en desarrollo.
      '/api': {
        target: 'http://localhost:5093',
        changeOrigin: true,
      },
    },
  },
})
