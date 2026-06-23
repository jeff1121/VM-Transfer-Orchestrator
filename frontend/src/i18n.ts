import { createI18n } from 'vue-i18n'
import zhTW from './locales/zh-TW'
import enUS from './locales/en-US'
import zhCN from './locales/zh-CN'

export const i18n = createI18n({
  legacy: false,
  locale: localStorage.getItem('locale') || 'zh-TW',
  fallbackLocale: 'zh-TW',
  messages: {
    'zh-TW': zhTW,
    'en-US': enUS,
    'zh-CN': zhCN,
  },
})
