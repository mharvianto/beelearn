<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../lib/api';

const rows = ref([]);
const error = ref('');

onMounted(async () => {
  try { rows.value = await api.get('/api/leaderboard?limit=100'); }
  catch (e) { error.value = e.message; }
});

const medal = (r) => (r === 1 ? '🥇' : r === 2 ? '🥈' : r === 3 ? '🥉' : '');
</script>

<template>
  <div class="max-w-3xl mx-auto px-4 py-8">
    <h1 class="text-xl font-bold mb-4">Leaderboard</h1>
    <p v-if="error" class="text-sm text-red-600 dark:text-red-400 mb-3">{{ error }}</p>

    <div class="border border-slate-200 dark:border-slate-800 rounded-xl overflow-hidden divide-y divide-slate-100 dark:divide-slate-800">
      <div v-for="r in rows" :key="r.userId"
           class="flex items-center gap-3 px-4 py-2.5 bg-white dark:bg-slate-900"
           :class="{ 'bg-amber-50 dark:bg-amber-500/10': r.me }">
        <span class="w-8 text-center text-sm text-slate-400 dark:text-slate-500">{{ medal(r.rank) || r.rank }}</span>
        <span class="flex-1 text-sm font-medium truncate">
          {{ r.displayName }}
          <span v-if="r.me" class="text-xs text-amber-600 dark:text-amber-400"> (you)</span>
        </span>
        <span v-if="r.role === 'Teacher'" class="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">teacher</span>
        <span class="text-xs font-semibold text-amber-600 dark:text-amber-400">Lv {{ r.level }}</span>
        <span class="text-sm text-slate-500 dark:text-slate-400 w-20 text-right">{{ r.xp }} XP</span>
      </div>
      <p v-if="!rows.length" class="px-4 py-6 text-sm text-slate-400 dark:text-slate-500">
        No XP earned yet. Solve problems in <RouterLink to="/practice" class="text-amber-600">Practice</RouterLink>.
      </p>
    </div>
  </div>
</template>
