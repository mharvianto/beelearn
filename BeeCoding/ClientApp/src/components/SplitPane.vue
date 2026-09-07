<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue';

const props = defineProps({
  direction: { type: String, default: 'horizontal' },  // 'horizontal' = side by side, 'vertical' = stacked
  initial: { type: Number, default: 50 },               // starting % for pane A
  min: { type: Number, default: 120 },                  // px min for either pane
  storageKey: { type: String, default: '' },
  collapseBelow: { type: Number, default: 1024 },       // horizontal splits stack (no drag) under this px
});

const isRow = computed(() => props.direction === 'horizontal');

const pct = ref(props.initial);
try {
  if (props.storageKey) {
    const v = parseFloat(localStorage.getItem(props.storageKey));
    if (!Number.isNaN(v) && v > 4 && v < 96) pct.value = v;
  }
} catch { /* ignore */ }

const el = ref(null);
const wide = ref(typeof window === 'undefined' || window.innerWidth >= props.collapseBelow);
const stacked = computed(() => isRow.value && !wide.value);
function updateWide() { wide.value = window.innerWidth >= props.collapseBelow; }

let dragging = false;
function onDown() {
  dragging = true;
  document.body.style.userSelect = 'none';
  document.body.style.cursor = isRow.value ? 'col-resize' : 'row-resize';
}
function onMove(e) {
  if (!dragging || !el.value) return;
  const r = el.value.getBoundingClientRect();
  const total = isRow.value ? r.width : r.height;
  if (total <= 0) return;
  const pos = isRow.value ? e.clientX - r.left : e.clientY - r.top;
  const minPct = (props.min / total) * 100;
  pct.value = Math.max(minPct, Math.min(100 - minPct, (pos / total) * 100));
}
function onUp() {
  if (!dragging) return;
  dragging = false;
  document.body.style.userSelect = '';
  document.body.style.cursor = '';
  try { if (props.storageKey) localStorage.setItem(props.storageKey, String(Math.round(pct.value * 10) / 10)); } catch { /* ignore */ }
}

onMounted(() => {
  updateWide();
  window.addEventListener('resize', updateWide);
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
  window.addEventListener('pointercancel', onUp);
});
onBeforeUnmount(() => {
  window.removeEventListener('resize', updateWide);
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
  window.removeEventListener('pointercancel', onUp);
});
</script>

<template>
  <div ref="el" class="min-h-0 min-w-0 h-full w-full flex"
       :class="stacked ? 'flex-col' : isRow ? 'flex-row' : 'flex-col'">
    <div class="min-h-0 min-w-0 overflow-hidden"
         :class="stacked ? 'flex-1' : ''"
         :style="stacked ? null : isRow ? { width: pct + '%' } : { height: pct + '%' }">
      <slot name="a" />
    </div>

    <div v-if="!stacked" @pointerdown.prevent="onDown"
         class="shrink-0 z-10 bg-slate-200 dark:bg-slate-800 hover:bg-amber-400 dark:hover:bg-amber-500 transition-colors touch-none"
         :class="isRow ? 'w-1.5 cursor-col-resize' : 'h-1.5 cursor-row-resize'"
         title="Drag to resize"></div>

    <div class="flex-1 min-h-0 min-w-0 overflow-hidden">
      <slot name="b" />
    </div>
  </div>
</template>
