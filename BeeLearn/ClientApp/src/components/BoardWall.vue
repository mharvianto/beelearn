<script setup>
import { computed } from 'vue';
import VerdictBadge from './VerdictBadge.vue';

const props = defineProps({
  students: Array,
  problems: Array,
  cells: Array,
  isStaff: Boolean,
  currentUserId: Number,
});
const emit = defineEmits(['open']);

const studentName = computed(() => Object.fromEntries(props.students.map((s) => [s.userId, s.displayName])));

function cardsFor(problemId) {
  return props.cells
    .filter((c) => c.problemId === problemId)
    .slice()
    .sort((a, b) => new Date(b.lastAt) - new Date(a.lastAt));
}
function solvedFor(problemId) {
  return props.cells.filter((c) => c.problemId === problemId && c.latest).length;
}

const accent = {
  Accepted: 'border-l-emerald-400',
  WrongAnswer: 'border-l-red-400',
  TimeLimit: 'border-l-orange-400',
  MemoryLimit: 'border-l-purple-400',
  RuntimeError: 'border-l-rose-400',
  CompileError: 'border-l-slate-400',
};

function ago(iso) {
  const s = Math.max(1, Math.floor((Date.now() - new Date(iso + (iso.endsWith('Z') ? '' : 'Z'))) / 1000));
  if (s < 60) return s + 's ago';
  if (s < 3600) return Math.floor(s / 60) + 'm ago';
  if (s < 86400) return Math.floor(s / 3600) + 'h ago';
  return Math.floor(s / 86400) + 'd ago';
}
</script>

<template>
  <div class="overflow-x-auto pb-2">
    <div class="flex gap-4 min-w-min">
      <section v-for="p in problems" :key="p.id" class="w-64 shrink-0">
        <header class="flex items-center justify-between px-1 mb-2">
          <h3 class="font-semibold text-sm truncate">{{ p.title }}</h3>
          <span class="text-xs text-slate-400 shrink-0">{{ solvedFor(p.id) }}/{{ students.length }} ✓</span>
        </header>

        <div class="space-y-2">
          <button
            v-for="c in cardsFor(p.id)" :key="c.userId"
            @click="emit('open', { userId: c.userId, problemId: p.id })"
            class="w-full text-left bg-white rounded-xl border border-slate-200 border-l-4 px-3 py-2 shadow-sm hover:shadow transition"
            :class="c.redacted ? 'border-l-slate-300 border-dashed' : (accent[c.verdict] || 'border-l-slate-300')">
            <div class="flex items-center justify-between">
              <span class="text-sm font-medium truncate"
                    :class="{ 'text-amber-600': c.userId === currentUserId }">
                {{ c.redacted ? '🔒 hidden' : studentName[c.userId] }}
                <span v-if="c.userId === currentUserId" class="text-xs text-slate-400">(you)</span>
              </span>
              <VerdictBadge v-if="!c.redacted" :verdict="c.verdict" small />
              <span v-else class="w-2.5 h-2.5 rounded-full bg-slate-300 inline-block"></span>
            </div>
            <div v-if="!c.redacted" class="mt-1.5">
              <div class="h-1 rounded-full bg-slate-100 overflow-hidden">
                <div class="h-full bg-emerald-400" :style="{ width: Math.round(c.score * 100) + '%' }"></div>
              </div>
              <div class="flex justify-between text-[11px] text-slate-400 mt-1">
                <span>{{ c.attempts }} attempt{{ c.attempts === 1 ? '' : 's' }}</span>
                <span>{{ ago(c.lastAt) }}</span>
              </div>
            </div>
            <div v-else class="text-[11px] text-slate-400 mt-1">attempted · hidden from peers</div>
          </button>

          <p v-if="!cardsFor(p.id).length" class="text-xs text-slate-300 px-1">no submissions</p>
        </div>
      </section>

      <p v-if="!problems.length" class="text-slate-400 text-sm">No problems yet.</p>
    </div>
  </div>
</template>
