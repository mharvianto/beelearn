<script setup>
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../lib/api';
import { useAuth } from '../stores/auth';
import { useProgress } from '../stores/progress';

const auth = useAuth();
const progress = useProgress();
const router = useRouter();

// AI usage (only shown if the tutor is enabled on this instance)
const aiUsage = ref(null);
onMounted(async () => {
  try {
    if ((await api.get('/api/ai/enabled'))?.enabled) aiUsage.value = await api.get('/api/ai/usage');
  } catch { /* ignore */ }
});
const fmt = (n) => (n ?? 0).toLocaleString();

// change password
const cur = ref('');
const next = ref('');
const next2 = ref('');
const pwMsg = ref('');
const pwErr = ref('');
const pwBusy = ref(false);

async function changePassword() {
  pwMsg.value = ''; pwErr.value = '';
  if (next.value.length < 6) { pwErr.value = 'New password must be at least 6 characters.'; return; }
  if (next.value !== next2.value) { pwErr.value = 'New passwords do not match.'; return; }
  pwBusy.value = true;
  try {
    await auth.changePassword(cur.value, next.value);
    cur.value = next.value = next2.value = '';
    pwMsg.value = 'Password updated.';
  } catch (e) { pwErr.value = e.message; }
  finally { pwBusy.value = false; }
}

// delete account
const showDelete = ref(false);
const delPw = ref('');
const delErr = ref('');
const delBusy = ref(false);
const ownedBoards = ref(null);   // set when the server asks for confirmation

async function deleteAccount(force = false) {
  delErr.value = ''; delBusy.value = true;
  try {
    await auth.deleteAccount(delPw.value, force);
    progress.reset();
    router.push('/register');
  } catch (e) {
    if (e.status === 409 && e.boards) { ownedBoards.value = e.boards; }
    else { delErr.value = e.message; }
  } finally { delBusy.value = false; }
}
</script>

<template>
  <div class="max-w-md mx-auto px-4 py-10 space-y-10">
    <div>
      <h1 class="text-xl font-bold mb-1">Account</h1>
      <p class="text-sm text-slate-500 dark:text-slate-400">
        {{ auth.user?.displayName }} · {{ auth.user?.email }} · {{ auth.user?.role }}
      </p>
    </div>

    <!-- AI usage -->
    <section v-if="aiUsage" class="space-y-2">
      <h2 class="font-semibold text-sm">AI tutor usage</h2>
      <table class="w-full text-sm">
        <thead>
          <tr class="text-xs text-slate-400 dark:text-slate-500 text-left">
            <th class="font-normal py-1"></th><th class="font-normal">Calls</th>
            <th class="font-normal">Prompt</th><th class="font-normal">Reply</th><th class="font-normal">Total tokens</th>
          </tr>
        </thead>
        <tbody class="[&_td]:py-1 [&_td:not(:first-child)]:tabular-nums">
          <tr><td class="text-slate-500 dark:text-slate-400">Today</td>
            <td>{{ fmt(aiUsage.today.calls) }}</td><td>{{ fmt(aiUsage.today.promptTokens) }}</td>
            <td>{{ fmt(aiUsage.today.completionTokens) }}</td><td class="font-medium">{{ fmt(aiUsage.today.totalTokens) }}</td></tr>
          <tr><td class="text-slate-500 dark:text-slate-400">This month</td>
            <td>{{ fmt(aiUsage.month.calls) }}</td><td>{{ fmt(aiUsage.month.promptTokens) }}</td>
            <td>{{ fmt(aiUsage.month.completionTokens) }}</td><td class="font-medium">{{ fmt(aiUsage.month.totalTokens) }}</td></tr>
          <tr><td class="text-slate-500 dark:text-slate-400">All time</td>
            <td>{{ fmt(aiUsage.allTime.calls) }}</td><td>{{ fmt(aiUsage.allTime.promptTokens) }}</td>
            <td>{{ fmt(aiUsage.allTime.completionTokens) }}</td><td class="font-medium">{{ fmt(aiUsage.allTime.totalTokens) }}</td></tr>
        </tbody>
      </table>
    </section>

    <!-- change password -->
    <section class="space-y-3">
      <h2 class="font-semibold text-sm">Change password</h2>
      <input v-model="cur" type="password" placeholder="Current password" autocomplete="current-password"
             class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
      <input v-model="next" type="password" placeholder="New password (min 6 chars)" autocomplete="new-password"
             class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
      <input v-model="next2" type="password" placeholder="Repeat new password" autocomplete="new-password"
             @keyup.enter="changePassword"
             class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
      <p v-if="pwErr" class="text-sm text-red-600 dark:text-red-400">{{ pwErr }}</p>
      <p v-if="pwMsg" class="text-sm text-emerald-600 dark:text-emerald-400">{{ pwMsg }}</p>
      <button @click="changePassword" :disabled="pwBusy || !cur || !next"
              class="bg-amber-500 hover:bg-amber-600 text-white rounded-lg px-4 py-2 text-sm font-medium disabled:opacity-50">
        {{ pwBusy ? '…' : 'Update password' }}
      </button>
    </section>

    <!-- delete account -->
    <section class="space-y-3 border border-red-200 dark:border-red-500/30 rounded-xl p-4">
      <h2 class="font-semibold text-sm text-red-600 dark:text-red-400">Delete account</h2>
      <p class="text-sm text-slate-500 dark:text-slate-400">
        Permanently removes your account, your submissions, and your practice progress.
        <span v-if="auth.isTeacher">Boards you own — and everyone's work on them — go too.</span>
        This cannot be undone.
      </p>

      <button v-if="!showDelete" @click="showDelete = true"
              class="border border-red-300 dark:border-red-500/40 text-red-600 dark:text-red-400 rounded-lg px-4 py-2 text-sm font-medium">
        Delete my account…
      </button>

      <template v-else>
        <input v-model="delPw" type="password" placeholder="Confirm with your password" autocomplete="current-password"
               class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />

        <div v-if="ownedBoards" class="text-sm bg-red-50 dark:bg-red-500/10 rounded-lg p-3 space-y-1">
          <p class="text-red-700 dark:text-red-300 font-medium">These boards will be deleted with all their submissions:</p>
          <ul class="list-disc pl-5 text-slate-600 dark:text-slate-300">
            <li v-for="b in ownedBoards" :key="b.slug">{{ b.title }}</li>
          </ul>
        </div>

        <p v-if="delErr" class="text-sm text-red-600 dark:text-red-400">{{ delErr }}</p>

        <div class="flex gap-2">
          <button @click="deleteAccount(!!ownedBoards)" :disabled="delBusy || !delPw"
                  class="bg-red-600 hover:bg-red-700 text-white rounded-lg px-4 py-2 text-sm font-medium disabled:opacity-50">
            {{ delBusy ? '…' : ownedBoards ? 'Yes, delete everything' : 'Delete account' }}
          </button>
          <button @click="showDelete = false; ownedBoards = null; delPw = ''; delErr = ''"
                  class="text-slate-500 dark:text-slate-400 rounded-lg px-4 py-2 text-sm">
            Cancel
          </button>
        </div>
      </template>
    </section>
  </div>
</template>
