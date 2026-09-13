<script setup>
import { computed } from 'vue';

// Ranking / magnitude chart — one sequential hue, values direct-labeled (no hover
// layer needed since nothing is hidden behind it).
const props = defineProps({
  items: { type: Array, required: true },   // [{ label, value, rate }] — rate is 0..1
  color: { type: String, default: '#f59e0b' },
});

const maxValue = computed(() => Math.max(1, ...props.items.map((i) => i.value)));
const pct = (v) => Math.max(2, (v / maxValue.value) * 100);
const fmt = (n) => (n ?? 0).toLocaleString();
</script>

<template>
  <div class="space-y-1.5">
    <div v-for="it in items" :key="it.label" class="flex items-center gap-2 text-xs">
      <div class="w-20 sm:w-24 truncate text-slate-500 dark:text-slate-400 shrink-0" :title="it.label">{{ it.label }}</div>
      <div class="flex-1 h-4 rounded bg-slate-100 dark:bg-slate-800/60 relative overflow-hidden">
        <div class="absolute inset-y-0 left-0 rounded-r" :style="{ width: pct(it.value) + '%', backgroundColor: color }"></div>
      </div>
      <div class="w-10 text-right tabular-nums font-medium shrink-0">{{ fmt(it.value) }}</div>
      <div class="w-9 text-right tabular-nums text-slate-400 dark:text-slate-500 shrink-0">{{ Math.round(it.rate * 100) }}%</div>
    </div>
    <p v-if="!items.length" class="text-slate-400 dark:text-slate-500 text-xs">No data yet.</p>
  </div>
</template>
