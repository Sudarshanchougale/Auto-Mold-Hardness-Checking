/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        zanvar: {
          red: '#DD1D21', // Official Zanvar Group Red
          dark: '#111827',
          gray: '#F3F4F6'
        }
      }
    },
  },
  plugins: [],
}