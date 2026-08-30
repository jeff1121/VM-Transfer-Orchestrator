<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'

export interface SelectOption {
  value: string | number
  label: string
  icon?: string
  disabled?: boolean
}

const props = withDefaults(
  defineProps<{
    modelValue: string | number
    options: SelectOption[]
    placeholder?: string
    disabled?: boolean
    fullWidth?: boolean
  }>(),
  {
    placeholder: '請選擇',
    disabled: false,
    fullWidth: false,
  }
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string | number): void
  (e: 'change', value: string | number): void
}>()

const isOpen = ref(false)
const selectContainer = ref<HTMLElement | null>(null)

const selectedOption = computed(() => {
  return props.options.find((opt) => opt.value === props.modelValue)
})

const displayText = computed(() => {
  return selectedOption.value ? selectedOption.value.label : props.placeholder
})

const displayIcon = computed(() => {
  return selectedOption.value?.icon
})

const toggleDropdown = () => {
  if (props.disabled) return
  isOpen.value = !isOpen.value
}

const selectOption = (opt: SelectOption) => {
  if (opt.disabled) return
  emit('update:modelValue', opt.value)
  emit('change', opt.value)
  isOpen.value = false
}

const handleClickOutside = (event: MouseEvent) => {
  if (selectContainer.value && !selectContainer.value.contains(event.target as Node)) {
    isOpen.value = false
  }
}

const handleKeyDown = (event: KeyboardEvent) => {
  if (props.disabled) return

  if (event.key === 'Escape') {
    isOpen.value = false
    return
  }

  if (event.key === 'Enter' || event.key === ' ') {
    if (!isOpen.value) {
      event.preventDefault()
      isOpen.value = true
    }
  }

  if (isOpen.value && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) {
    event.preventDefault()
    const currentIndex = props.options.findIndex((opt) => opt.value === props.modelValue)
    let nextIndex = currentIndex

    if (event.key === 'ArrowDown') {
      nextIndex = currentIndex < props.options.length - 1 ? currentIndex + 1 : 0
    } else if (event.key === 'ArrowUp') {
      nextIndex = currentIndex > 0 ? currentIndex - 1 : props.options.length - 1
    }

    const nextOption = props.options[nextIndex]
    if (nextOption && !nextOption.disabled) {
      emit('update:modelValue', nextOption.value)
      emit('change', nextOption.value)
    }
  }
}

onMounted(() => {
  window.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  window.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <div
    ref="selectContainer"
    :class="[
      'neu-select-wrapper',
      { 'is-open': isOpen, 'is-disabled': disabled, 'full-width': fullWidth },
    ]"
    tabindex="0"
    @keydown="handleKeyDown"
  >
    <!-- Trigger Box -->
    <div
      class="neu-select-trigger"
      :class="{ active: isOpen }"
      @click="toggleDropdown"
    >
      <div class="selected-content">
        <span v-if="displayIcon" class="opt-icon">{{ displayIcon }}</span>
        <span :class="['selected-label', { 'is-placeholder': !selectedOption }]">
          {{ displayText }}
        </span>
      </div>
      <div class="chevron-indicator">
        <span class="chevron-icon">▼</span>
      </div>
    </div>

    <!-- Floating Glassmorphic Popover Menu -->
    <transition name="popover-slide">
      <div v-if="isOpen" class="neu-select-menu">
        <div class="menu-scroll-container">
          <div
            v-for="opt in options"
            :key="opt.value"
            :class="[
              'neu-select-option',
              {
                selected: opt.value === modelValue,
                disabled: opt.disabled,
              },
            ]"
            @click.stop="selectOption(opt)"
          >
            <div class="opt-left">
              <span v-if="opt.icon" class="opt-icon">{{ opt.icon }}</span>
              <span class="opt-label">{{ opt.label }}</span>
            </div>
            <span v-if="opt.value === modelValue" class="check-icon">✓</span>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<style scoped>
.neu-select-wrapper {
  position: relative;
  display: inline-block;
  min-width: 220px;
  user-select: none;
  outline: none;
  font-family: inherit;
}

.neu-select-wrapper.full-width {
  width: 100%;
  min-width: 100%;
}

.neu-select-wrapper.is-disabled {
  opacity: 0.55;
  cursor: not-allowed;
  pointer-events: none;
}

/* Trigger Box */
.neu-select-trigger {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  box-shadow: var(--neu-inset);
  color: var(--text-primary);
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.neu-select-trigger:hover {
  border-color: var(--primary);
}

.neu-select-trigger.active {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-glow), var(--neu-inset);
}

.selected-content {
  display: flex;
  align-items: center;
  gap: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.selected-label.is-placeholder {
  color: var(--text-muted);
}

.opt-icon {
  font-size: 1.1rem;
  line-height: 1;
}

.chevron-indicator {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
  font-size: 0.7rem;
  transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1), color 0.2s ease;
}

.neu-select-trigger.active .chevron-indicator {
  transform: rotate(180deg);
  color: var(--primary);
}

/* Floating Glassmorphic Popover Menu */
.neu-select-menu {
  position: absolute;
  top: calc(100% + 6px);
  left: 0;
  right: 0;
  background: var(--bg-surface-solid);
  backdrop-filter: var(--glass-blur);
  -webkit-backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 14px;
  box-shadow: var(--neu-shadow-hover);
  padding: 6px;
  z-index: 150;
  min-width: 100%;
  max-height: 260px;
  overflow: hidden;
}

.menu-scroll-container {
  max-height: 248px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

/* Options */
.neu-select-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 10px 14px;
  border-radius: 10px;
  color: var(--text-secondary);
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s ease;
}

.opt-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.neu-select-option:hover:not(.disabled) {
  background: var(--bg-surface-elevated);
  color: var(--text-primary);
  transform: translateX(4px);
}

.neu-select-option.selected {
  background: var(--primary-gradient);
  color: #ffffff;
  font-weight: 700;
  box-shadow: 0 4px 12px var(--primary-glow);
}

.neu-select-option.disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.check-icon {
  font-size: 0.9rem;
  font-weight: 800;
}

/* Animations */
.popover-slide-enter-active,
.popover-slide-leave-active {
  transition: opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1), transform 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.popover-slide-enter-from,
.popover-slide-leave-to {
  opacity: 0;
  transform: translateY(-8px) scale(0.97);
}
</style>
