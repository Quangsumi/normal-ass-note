import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'


// https://vite.dev/config/
export default defineConfig({
  base: './',
  plugins: [
    react(),
    basicSsl()
  ],
  server: {
    proxy: {
      '/api': 'https://localhost:7130',
    },
  },
})
