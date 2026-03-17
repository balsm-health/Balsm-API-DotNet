import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/admin/',
  build: {
    outDir: '../src/Balsm.API/wwwroot/admin',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5050',
      '/connect': 'http://localhost:5050',
    },
  },
})
