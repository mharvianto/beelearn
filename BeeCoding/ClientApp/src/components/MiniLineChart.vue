<script setup>
import { ref, computed } from 'vue';

// A small single-series trend chart (area + line). Two measures of different scale
// (e.g. active users vs. submissions) render as two of these side by side rather
// than one dual-axis chart — see dataviz guidance: never a second y-axis.
const props = defineProps({
  title: { type: String, required: true },
  points: { type: Array, required: true },   // [{ label: string, value: number }]
  color: { type: String, default: '#f59e0b' },   // amber-500, the app's existing accent
});

const W = 300, H = 100, PAD_TOP = 14, PAD_BOTTOM = 4;
const maxValue = computed(() => Math.max(1, ...props.points.map((p) => p.value)));
const xAt = (i) => props.points.length > 1 ? (i / (props.points.length - 1)) * (W - 8) + 4 : W / 2;
const yAt = (v) => H - PAD_BOTTOM - (v / maxValue.value) * (H - PAD_TOP - PAD_BOTTOM);

const linePath = computed(() =>
  props.points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xAt(i)} ${yAt(p.value)}`).join(' '));
const areaPath = computed(() => {
  if (!props.points.length) return '';
  const top = props.points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${xAt(i)} ${yAt(p.value)}`).join(' ');
  return `${top} L ${xAt(props.points.length - 1)} ${H - PAD_BOTTOM} L ${xAt(0)} ${H - PAD_BOTTOM} Z`;
});
const gridY = computed(() => [0, 0.5, 1].map((f) => H - PAD_BOTTOM - f * (H - PAD_TOP - PAD_BOTTOM)));

const hoverIdx = ref(null);
function onMove(ev) {
  const rect = ev.currentTarget.getBoundingClientRect();
  const frac = (ev.clientX - rect.left) / rect.width;
  const i = Math.round(frac * (props.points.length - 1));
  hoverIdx.value = Math.min(props.points.length - 1, Math.max(0, i));
}

const fmt = (n) => (n ?? 0).toLocaleString();
const lastPoint = computed(() => props.points[props.points.length - 1]);
const activePoint = computed(() => hoverIdx.value === null ? lastPoint.value : props.points[hoverIdx.value]);
</script>

<template>
  <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-3">
    <div class="flex items-baseline justify-between mb-1">
      <div class="text-xs text-slate-400 dark:text-slate-500">{{ title }}</div>
      <div class="text-sm font-semibold tabular-nums">{{ fmt(activePoint?.value) }}</div>
    </div>
    <div class="relative">
      <svg :viewBox="`0 0 ${W} ${H}`" class="w-full h-20" preserveAspectRatio="none"
           @mousemove="onMove" @mouseleave="hoverIdx = null">
        <line v-for="y in gridY" :key="y" x1="0" :y1="y" :x2="W" :y2="y"
              class="text-slate-200 dark:text-slate-800" stroke="currentColor" stroke-width="1" />
        <path :d="areaPath" :fill="color" fill-opacity="0.1" stroke="none" />
        <path :d="linePath" :stroke="color" stroke-width="2" fill="none" stroke-linejoin="round" stroke-linecap="round" />
        <line v-if="hoverIdx !== null" :x1="xAt(hoverIdx)" y1="0" :x2="xAt(hoverIdx)" :y2="H"
              class="text-slate-300 dark:text-slate-600" stroke="currentColor" stroke-width="1" />
        <circle :cx="xAt(points.length - 1)" :cy="yAt(lastPoint?.value ?? 0)" r="4" :fill="color"
                stroke="white" stroke-width="2" class="dark:stroke-slate-900" />
        <circle v-if="hoverIdx !== null" :cx="xAt(hoverIdx)" :cy="yAt(points[hoverIdx].value)" r="4" :fill="color"
                stroke="white" stroke-width="2" class="dark:stroke-slate-900" />
      </svg>
    </div>
    <div class="flex items-center justify-between text-[10px] text-slate-400 dark:text-slate-500 mt-0.5">
      <span>{{ points[0]?.label }}</span>
      <span v-if="hoverIdx !== null">{{ points[hoverIdx].label }}</span>
      <span v-else>{{ lastPoint?.label }}</span>
    </div>
  </div>
</template>
