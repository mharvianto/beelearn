<script setup>
import { watch } from 'vue';
import { useRouter } from 'vue-router';
import { useAuth } from './stores/auth';
import { useProgress } from './stores/progress';
import ThemeToggle from './components/ThemeToggle.vue';

const auth = useAuth();
const progress = useProgress();
const router = useRouter();

// keep the header XP in sync with who's logged in
watch(() => auth.user?.id, (id) => (id ? progress.refresh() : progress.reset()), { immediate: true });

async function logout() {
  await auth.logout();
  progress.reset();
  router.push('/login');
}
</script>

<template>
  <div class="h-full min-h-0 flex flex-col">
    <header v-if="auth.user" class="shrink-0 bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-800">
      <div class="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between gap-3">
        <div class="flex items-center gap-4 min-w-0">
          <RouterLink to="/boards" class="font-bold text-lg text-amber-600 dark:text-amber-400 shrink-0">🐝 BeeLearn</RouterLink>
          <RouterLink to="/practice"
                      class="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
            Practice
          </RouterLink>
          <RouterLink to="/leaderboard"
                      class="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
            Leaderboard
          </RouterLink>
          <RouterLink v-if="auth.isTeacher" to="/bank"
                      class="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
            Problem bank
          </RouterLink>
        </div>
        <div class="flex items-center gap-3 text-sm shrink-0">
          <RouterLink to="/leaderboard" v-if="progress.ready"
                      class="hidden sm:flex items-center gap-2" title="Your XP">
            <span class="text-xs font-semibold text-amber-600 dark:text-amber-400">Lv {{ progress.level }}</span>
            <span class="w-20 h-1.5 rounded-full bg-slate-200 dark:bg-slate-700 overflow-hidden">
              <span class="block h-full bg-amber-400" :style="{ width: (progress.pct * 100) + '%' }"></span>
            </span>
            <span class="text-xs text-slate-400 dark:text-slate-500">{{ progress.xp }} XP</span>
          </RouterLink>
          <span class="text-slate-500 dark:text-slate-400 truncate max-w-[10rem]">{{ auth.user.displayName }}</span>
          <span class="px-2 py-0.5 rounded-full text-xs"
                :class="auth.isTeacher
                  ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300'
                  : 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300'">
            {{ auth.user.role }}
          </span>
          <ThemeToggle />
          <button @click="logout" class="text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
            Sign out
          </button>
        </div>
      </div>
    </header>
    <main class="flex-1 min-h-0 overflow-y-auto">
      <RouterView />
    </main>
  </div>
</template>
