const defaultTheme = require('tailwindcss/defaultTheme')

module.exports = {
  purge: {
    enabled: true,
    content: [ 
        './Pages/**/*.cshtml',
        './Views/**/*.cshtml'
    ]
  },
  darkMode: false, // or 'media' or 'class'
  theme: {
    screens: {
      'xs': '450px',
      ...defaultTheme.screens,
    },
  },
  variants: {
    extend: {
      fontFamily: {
        sans: ['Inter var', ...defaultTheme.fontFamily.sans],
      },
    },
  },
  plugins: [
    require('@tailwindcss/aspect-ratio'),
    require('@tailwindcss/forms'),
    require('@tailwindcss/typography'),
  ],
}
