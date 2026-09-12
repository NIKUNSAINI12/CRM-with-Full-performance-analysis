/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  darkMode: 'class', // We'll force dark mode initially or rely on OS
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
      },
      colors: {
        // Plane.so specific color palette mapped to 'brand'
        brand: {
          dark: {
            950: '#0F1115', // Main workspace background
            900: '#1A1D24', // Sidebar & Header surface
            800: '#2B303B', // Borders and separators
            700: '#343B49', // Hover states
          },
          text: {
            100: '#F3F4F6', // High contrast text
            400: '#9CA3AF', // Muted text
          },
          primary: '#3F76FF', // Vibrant blue primary
          secondary: '#6366f1' // Deep indigo secondary
        }
      }
    },
  },
  plugins: [],
}
