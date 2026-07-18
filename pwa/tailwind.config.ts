import type { Config } from 'tailwindcss'
import forms from '@tailwindcss/forms'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        'rush-red': {
          DEFAULT: '#E63946',
          hover:   '#C1121F',
        },
        'rush-dark': {
          DEFAULT: '#1D3557',
          light:   '#2D4A6E',
        },
        'rush-light': '#F1FAEE',
        'rush-blue':  '#457B9D',
      },
      fontFamily: {
        sans: ['Poppins', 'system-ui', 'sans-serif'],
      },
      screens: {
        sm: '640px',
        md: '768px',
      },
    },
  },
  plugins: [forms],
} satisfies Config
