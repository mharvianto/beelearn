<script setup>
import { theme, cycleTheme } from '../lib/theme';
const icon = { light: '☀️', dark: '🌙', system: '🖥️' };
</script>

<template>
  <button @click="cycleTheme" :title="`Theme: ${theme}`"
          class="text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
    {{ icon[theme] }}
  </button>
</template>
