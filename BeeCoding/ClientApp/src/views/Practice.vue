<script setup>
import { ref, computed, onMounted } from 'vue';
import { api } from '../lib/api';
import { useProgress } from '../stores/progress';
import LevelBadge from '../components/LevelBadge.vue';
import VerdictBadge from '../components/VerdictBadge.vue';

const progress = useProgress();
const items = ref([]);
const q = ref('');
const level = ref('');
const status = ref('');
const error = ref('');

async function load() {
  error.value = '';
  try {
    const p = new URLSearchParams();
    if (q.value.trim()) p.set('q', q.value.trim());
    if (level.value) p.set('level', level.value);
    if (status.value) p.set('status', status.value);
    items.value = await api.get(`/api/practice?${p}`);
  } catch (e) { error.value = e.message; }
}
onMounted(() => { load(); progress.refresh(); });

const solvedCount = computed(() => items.value.filter((x) => x.solved).length);
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-1">
      <h1 class="text-xl font-bold">Practice</h1>
      <div class="text-sm text-slate-500 dark:text-slate-400">
        Lv {{ progress.level }} · {{ progress.xp }} XP · {{ progress.toNext }} XP to next level
      </div>
    </div>
    <p class="text-sm text-slate-400 dark:text-slate-500 mb-4">
      Solve anything. Your first full solve of a problem earns XP (Easy 10 · Medium 20 · Hard 40).
    </p>

    <div class="flex gap-2 mb-4 flex-wrap">
      <input v-model="q" @keyup.enter="load" placeholder="Search title or tag…"
             class="flex-1 min-w-48 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
      <select v-model="level" @change="load"
              class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 text-sm">
        <option value="">All levels</option>
        <option>Easy</option><option>Medium</option><option>Hard</option>
      </select>
      <select v-model="status" @change="load"
              class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 text-sm">
        <option value="">All statuses</option>
        <option value="unsolved">Unsolved</option>
        <option value="attempted">Attempted</option>
        <option value="solved">Solved</option>
      </select>
      <button @click="load" class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4">Search</button>
    </div>

    <p v-if="error" class="text-sm text-red-600 dark:text-red-400 mb-3">{{ error }}</p>
    <p class="text-xs text-slate-400 dark:text-slate-500 mb-2">
      {{ items.length }} problems · {{ solvedCount }} solved
    </p>

    <div class="border border-slate-200 dark:border-slate-800 rounded-xl overflow-hidden divide-y divide-slate-100 dark:divide-slate-800">
      <RouterLink v-for="p in items" :key="p.id" :to="`/practice/${p.id}`"
                  class="flex items-center gap-3 px-4 py-2.5 bg-white dark:bg-slate-900 hover:bg-slate-50 dark:hover:bg-slate-800/60">
        <span class="w-5 text-center">
          <span v-if="p.solved" class="text-emerald-500">✓</span>
          <span v-else-if="p.myVerdict !== 'None'" class="inline-block w-2 h-2 rounded-full bg-amber-400"></span>
        </span>
        <span class="flex-1 text-sm font-medium truncate">{{ p.title }}</span>
        <VerdictBadge v-if="p.myVerdict !== 'None' && !p.solved" :verdict="p.myVerdict" small />
        <span v-if="p.tags" class="hidden md:block text-[11px] text-slate-400 dark:text-slate-500 truncate max-w-[14rem]">{{ p.tags }}</span>
        <LevelBadge :level="p.level" />
        <span class="text-[11px] text-slate-400 dark:text-slate-500 w-8 text-right">{{ p.language.toUpperCase() }}</span>
      </RouterLink>
      <p v-if="!items.length" class="px-4 py-6 text-sm text-slate-400 dark:text-slate-500">No problems.</p>
    </div>
  </div>
</template>
