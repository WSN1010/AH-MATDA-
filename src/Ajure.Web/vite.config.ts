import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: process.env.API_HTTP
    ? {
        proxy: {
          '/api': {
            target: process.env.API_HTTP,
            changeOrigin: true,
          },
        },
      }
    : undefined,
})
