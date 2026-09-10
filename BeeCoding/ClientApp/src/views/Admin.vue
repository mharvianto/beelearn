<script setup>
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../lib/api';
import { useAuth } from '../stores/auth';

const auth = useAuth();
const router = useRouter();
const tab = ref('ai');           // 'ai' | 'users' | 'problems'
const err = ref('');

const aiRows = ref(null);
const users = ref(null);
const userQ = ref('');

const fmt = (n) => (n ?? 0).toLocaleString();

async function loadAi() {
  err.value = '';
  try { aiRows.value = await api.get('/api/admin-ui/ai-usage'); }
  catch (e) { err.value = e.message; }
}
async function loadUsers() {
  err.value = '';
  try { users.value = await api.get('/api/admin-ui/users' + (userQ.value.trim() ? `?q=${encodeURIComponent(userQ.value.trim())}` : '')); }
  catch (e) { err.value = e.message; }
}

// ---- problems export / import ----
const importing = ref(false);
const importResult = ref(null);
const replaceExisting = ref(true);

async function exportProblems() {
  err.value = '';
  try {
    const res = await fetch('/api/admin-ui/problems/export', { credentials: 'include' });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const blob = await res.blob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = (res.headers.get('content-disposition') || '').match(/filename=([^;]+)/)?.[1] || 'beecoding-problems.json';
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  } catch (e) { err.value = e.message; }
}

async function importProblems(ev) {
  const file = ev.target.files?.[0];
  ev.target.value = '';
  if (!file) return;
  err.value = ''; importResult.value = null; importing.value = true;
  try {
    const bundle = JSON.parse(await file.text());
    importResult.value = await api.post(
      `/api/admin-ui/problems/import?replaceExisting=${replaceExisting.value}`, bundle);
  } catch (e) { err.value = e.message; }
  finally { importing.value = false; }
}

onMounted(async () => {
  if (!auth.user?.isAdmin) { router.replace('/boards'); return; }
  await loadAi();
});
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-8">
    <h1 class="text-xl font-bold mb-4">Admin</h1>

    <div class="inline-flex rounded-lg border border-slate-300 dark:border-slate-700 overflow-hidden text-sm mb-4">
      <button v-for="t in [['ai','AI usage'],['users','Users'],['problems','Problems']]" :key="t[0]"
              @click="tab = t[0]; t[0]==='users' && !users ? loadUsers() : null"
              class="px-3 py-1.5"
              :class="tab === t[0] ? 'bg-slate-800 text-white dark:bg-slate-600' : 'text-slate-500 dark:text-slate-400'">
        {{ t[1] }}
      </button>
    </div>

    <p v-if="err" class="text-sm text-red-600 dark:text-red-400 mb-3">{{ err }}</p>

    <!-- AI usage -->
    <section v-show="tab === 'ai'">
      <button @click="loadAi" class="text-xs text-slate-500 dark:text-slate-400 mb-2">↻ refresh</button>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-xs text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
              <th class="font-normal py-1.5 pr-3">User</th>
              <th class="font-normal px-2">Today (calls / tokens)</th>
              <th class="font-normal px-2">This month</th>
              <th class="font-normal px-2">All time</th>
            </tr>
          </thead>
          <tbody class="[&_td]:py-1.5 [&_td]:pr-3 [&_td]:align-top">
            <tr v-for="r in aiRows" :key="r.userId" class="border-b border-slate-100 dark:border-slate-800/60">
              <td><div class="font-medium">{{ r.displayName }}</div><div class="text-[11px] text-slate-400">{{ r.email }}</div></td>
              <td class="tabular-nums">{{ fmt(r.today.calls) }} / {{ fmt(r.today.totalTokens) }}</td>
              <td class="tabular-nums">{{ fmt(r.month.calls) }} / {{ fmt(r.month.totalTokens) }}</td>
              <td class="tabular-nums font-medium">{{ fmt(r.allTime.calls) }} / {{ fmt(r.allTime.totalTokens) }}</td>
            </tr>
            <tr v-if="aiRows && !aiRows.length"><td colspan="4" class="text-slate-400 dark:text-slate-500 py-3">No AI usage yet.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Users -->
    <section v-show="tab === 'users'">
      <div class="flex gap-2 mb-3">
        <input v-model="userQ" @keyup.enter="loadUsers" placeholder="Search name or email…"
               class="flex-1 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
        <button @click="loadUsers" class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4">Search</button>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-xs text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
              <th class="font-normal py-1.5 pr-3">#</th><th class="font-normal pr-3">Name / email</th>
              <th class="font-normal pr-3">Role</th><th class="font-normal pr-3">XP</th>
              <th class="font-normal pr-3">Boards</th><th class="font-normal pr-3">Subs</th>
              <th class="font-normal pr-3">Joined</th>
            </tr>
          </thead>
          <tbody class="[&_td]:py-1.5 [&_td]:pr-3">
            <tr v-for="u in users" :key="u.id" class="border-b border-slate-100 dark:border-slate-800/60">
              <td class="text-slate-400">{{ u.id }}</td>
              <td><div class="font-medium">{{ u.displayName }}</div><div class="text-[11px] text-slate-400">{{ u.email }}</div></td>
              <td>
                <span class="text-[11px] px-1.5 py-0.5 rounded-full"
                      :class="u.role === 'Teacher' ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300' : 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300'">{{ u.role }}</span>
                <span v-if="u.isAdmin" class="ml-1 text-[11px] px-1.5 py-0.5 rounded-full bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300">admin</span>
              </td>
              <td class="tabular-nums">{{ fmt(u.xp) }}</td>
              <td class="tabular-nums">{{ u.ownedBoards }}</td>
              <td class="tabular-nums">{{ u.submissions }}</td>
              <td class="text-[11px] text-slate-400">{{ new Date(u.createdAt).toLocaleDateString() }}</td>
            </tr>
            <tr v-if="users && !users.length"><td colspan="7" class="text-slate-400 dark:text-slate-500 py-3">No users.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Problems -->
    <section v-show="tab === 'problems'" class="space-y-4">
      <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-4">
        <h2 class="font-semibold text-sm mb-1">Export</h2>
        <p class="text-xs text-slate-400 dark:text-slate-500 mb-2">
          Downloads every bank problem (all owners) with its test cases as a JSON file.
        </p>
        <button @click="exportProblems"
                class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium">
          Download problems.json
        </button>
      </div>

      <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-4">
        <h2 class="font-semibold text-sm mb-1">Import</h2>
        <p class="text-xs text-slate-400 dark:text-slate-500 mb-2">
          Upserts by (owner email, title). Owner resolved by email, falling back to the first teacher.
        </p>
        <label class="flex items-center gap-2 text-xs mb-2">
          <input type="checkbox" v-model="replaceExisting" /> Replace existing (overwrite a problem with the same owner + title)
        </label>
        <input type="file" accept="application/json,.json" @change="importProblems" :disabled="importing"
               class="text-sm" />
        <p v-if="importing" class="text-xs text-slate-400 mt-2">Importing…</p>
        <div v-if="importResult" class="mt-3 text-sm">
          <p class="text-emerald-600 dark:text-emerald-400">
            Created {{ importResult.created }} · Updated {{ importResult.updated }} · Skipped {{ importResult.skipped }}
          </p>
          <ul v-if="importResult.errors?.length" class="mt-1 text-xs text-red-600 dark:text-red-400 list-disc pl-5">
            <li v-for="(e, i) in importResult.errors" :key="i">{{ e }}</li>
          </ul>
        </div>
      </div>
    </section>
  </div>
</template>
