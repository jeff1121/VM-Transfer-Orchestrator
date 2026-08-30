import { ref } from 'vue'

export type ThemeMode = 'light' | 'dark'

const currentTheme = ref<ThemeMode>(
  (localStorage.getItem('theme_mode') as ThemeMode) ||
    (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
)

export function useTheme() {
  const applyTheme = (theme: ThemeMode) => {
    currentTheme.value = theme
    localStorage.setItem('theme_mode', theme)
    if (theme === 'dark') {
      document.documentElement.setAttribute('data-theme', 'dark')
    } else {
      document.documentElement.removeAttribute('data-theme')
    }
  }

  const setThemeMode = (mode: ThemeMode) => {
    applyTheme(mode)
  }

  const toggleTheme = () => {
    applyTheme(currentTheme.value === 'dark' ? 'light' : 'dark')
  }

  return {
    currentTheme,
    resolvedTheme: currentTheme,
    setThemeMode,
    toggleTheme,
  }
}
