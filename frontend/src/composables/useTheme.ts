import { computed, ref } from 'vue'

export type ThemeMode = 'auto' | 'light' | 'dark'

const STORAGE_KEY = 'theme-mode'
const mode = ref<ThemeMode>((localStorage.getItem(STORAGE_KEY) as ThemeMode) || 'auto')
const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
let initialized = false

const resolvedTheme = computed<'light' | 'dark'>(() => {
  if (mode.value === 'auto') {
    return mediaQuery.matches ? 'dark' : 'light'
  }

  return mode.value
})

const applyTheme = () => {
  document.documentElement.setAttribute('data-theme', resolvedTheme.value)
}

const handleSystemThemeChange = () => {
  if (mode.value === 'auto') {
    applyTheme()
  }
}

export function useTheme() {
  if (!initialized) {
    initialized = true
    applyTheme()
    mediaQuery.addEventListener('change', handleSystemThemeChange)
  }

  const setThemeMode = (nextMode: ThemeMode) => {
    mode.value = nextMode
    localStorage.setItem(STORAGE_KEY, nextMode)
    applyTheme()
  }

  return {
    mode,
    resolvedTheme,
    setThemeMode,
  }
}
