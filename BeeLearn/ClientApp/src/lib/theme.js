import { ref, watchEffect } from 'vue';

const KEY = 'beelearn.theme';
const mq = window.matchMedia('(prefers-color-scheme: dark)');

/** 'light' | 'dark' | 'system' */
export const theme = ref(localStorage.getItem(KEY) || 'system');

function apply() {
  const dark = theme.value === 'dark' || (theme.value === 'system' && mq.matches);
  document.documentElement.classList.toggle('dark', dark);
}
watchEffect(apply);
mq.addEventListener?.('change', apply);

export function setTheme(t) {
  theme.value = t;
  localStorage.setItem(KEY, t);
}

export function cycleTheme() {
  setTheme(theme.value === 'light' ? 'dark' : theme.value === 'dark' ? 'system' : 'light');
}

export function isDark() {
  return document.documentElement.classList.contains('dark');
}
