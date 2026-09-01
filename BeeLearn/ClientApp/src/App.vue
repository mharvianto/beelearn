<script setup>
import { useRouter } from 'vue-router';
import { useAuth } from './stores/auth';

const auth = useAuth();
const router = useRouter();

async function logout() {
  await auth.logout();
  router.push('/login');
}
</script>

<template>
  <div class="h-full min-h-0 flex flex-col">
    <header v-if="auth.user" class="shrink-0 bg-white border-b border-slate-200">
      <div class="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">
        <RouterLink to="/boards" class="font-bold text-lg text-amber-600">🐝 BeeLearn</RouterLink>
        <div class="flex items-center gap-3 text-sm">
          <span class="text-slate-500">{{ auth.user.displayName }}</span>
          <span class="px-2 py-0.5 rounded-full text-xs"
                :class="auth.isTeacher ? 'bg-amber-100 text-amber-700' : 'bg-sky-100 text-sky-700'">
            {{ auth.user.role }}
          </span>
          <button @click="logout" class="text-slate-500 hover:text-slate-900">Sign out</button>
        </div>
      </div>
    </header>
    <main class="flex-1 min-h-0 overflow-y-auto">
      <RouterView />
    </main>
  </div>
</template>
