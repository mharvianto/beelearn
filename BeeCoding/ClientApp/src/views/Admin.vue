<script setup>
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../lib/api';
import { withBase } from '../lib/base';
import { useAuth } from '../stores/auth';
import { useUndoToast } from '../stores/undoToast';

const auth = useAuth();
const router = useRouter();
const undoToast = useUndoToast();
const tabDefs = [
  ['ai', 'AI'], ['users', 'Users'], ['boards', 'Boards'], ['problems', 'Problems'],
  ['review', 'AI review'], ['reports', 'Reports'], ['trash', 'Trash'], ['audit', 'Audit log'],
];
const tab = ref('ai');
const err = ref('');

const aiRows = ref(null);
const users = ref(null);
const userQ = ref('');
const usersPage = ref(1);
const usersPageSize = ref(25);
const usersTotal = ref(0);

const fmt = (n) => (n ?? 0).toLocaleString();
const when = (d) => new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString();

function switchTab(id) {
  tab.value = id;
  if (id === 'ai') {
    if (!aiSettings.value) loadAiSettings();
    if (!aiOverrides.value) loadAiOverrides();
  } else if (id === 'users') {
    if (!users.value) loadUsers();
    if (!boards.value) loadBoards();   // populates the CSV-import board picker too
  } else if (id === 'boards' && !boards.value) loadBoards();
  else if (id === 'review') loadAiReview();   // queue changes often — always refresh
  else if (id === 'reports' && !systemStatus.value) loadSystemStatus();
  else if (id === 'trash') loadTrash();     // state changes often — always refresh
  else if (id === 'audit' && !auditRows.value) loadAudit();
}

// ---- AI settings: kill-switch, quotas, per-user overrides ----
const aiSettings = ref(null);
const aiSettingsSaving = ref(false);
const aiOverrides = ref(null);
const newOverride = ref({ userId: null, quota: null, banned: false });

async function loadAiSettings() {
  err.value = '';
  try { aiSettings.value = await api.get('/api/admin-ui/ai-settings'); }
  catch (e) { err.value = e.message; }
}
async function saveAiSettings() {
  err.value = ''; aiSettingsSaving.value = true;
  try { aiSettings.value = await api.put('/api/admin-ui/ai-settings', aiSettings.value); }
  catch (e) { err.value = e.message; }
  finally { aiSettingsSaving.value = false; }
}
async function loadAiOverrides() {
  err.value = '';
  try { aiOverrides.value = await api.get('/api/admin-ui/ai-settings/overrides'); }
  catch (e) { err.value = e.message; }
}
async function saveOverride(row) {
  err.value = '';
  try {
    await api.put(`/api/admin-ui/ai-settings/overrides/${row.userId}`, { dailyQuotaOverride: row.dailyQuotaOverride, banned: row.banned });
    await loadAiOverrides();
  } catch (e) { err.value = e.message; }
}
async function clearOverride(row) {
  err.value = '';
  try { await api.del(`/api/admin-ui/ai-settings/overrides/${row.userId}`); await loadAiOverrides(); }
  catch (e) { err.value = e.message; }
}
async function addOverride() {
  if (!newOverride.value.userId) return;
  err.value = '';
  try {
    await api.put(`/api/admin-ui/ai-settings/overrides/${newOverride.value.userId}`, {
      dailyQuotaOverride: newOverride.value.quota, banned: newOverride.value.banned,
    });
    newOverride.value = { userId: null, quota: null, banned: false };
    await loadAiOverrides();
  } catch (e) { err.value = e.message; }
}

// ---- AI review queue: AI-generated problems held back until approved ----
const aiReview = ref(null);
const expandedReview = ref(null);   // slug of the row currently expanded
const rejectReasonDraft = ref({});  // slug -> reason text while rejecting

async function loadAiReview() {
  err.value = '';
  try { aiReview.value = await api.get('/api/admin-ui/ai-review'); }
  catch (e) { err.value = e.message; }
}
async function approveAiReview(row) {
  err.value = '';
  try {
    await api.post(`/api/admin-ui/ai-review/${row.slug}/approve`);
    aiReview.value = aiReview.value.filter((r) => r.slug !== row.slug);
  } catch (e) { err.value = e.message; }
}
async function rejectAiReview(row) {
  err.value = '';
  try {
    await api.post(`/api/admin-ui/ai-review/${row.slug}/reject`, { reason: rejectReasonDraft.value[row.slug] || null });
    aiReview.value = aiReview.value.filter((r) => r.slug !== row.slug);
    delete rejectReasonDraft.value[row.slug];
  } catch (e) { err.value = e.message; }
}

async function loadAi() {
  err.value = '';
  try { aiRows.value = await api.get('/api/admin-ui/ai-usage'); }
  catch (e) { err.value = e.message; }
}
async function loadUsers() {
  err.value = '';
  selectedUsers.value = new Set();
  try {
    const p = new URLSearchParams({ page: String(usersPage.value), pageSize: String(usersPageSize.value) });
    if (userQ.value.trim()) p.set('q', userQ.value.trim());
    const result = await api.get(`/api/admin-ui/users?${p}`);
    users.value = result.rows;
    usersTotal.value = result.total;
  } catch (e) { err.value = e.message; }
}
function searchUsers() { usersPage.value = 1; loadUsers(); }
function usersPrevPage() { if (usersPage.value > 1) { usersPage.value--; loadUsers(); } }
function usersNextPage() { if (usersPage.value * usersPageSize.value < usersTotal.value) { usersPage.value++; loadUsers(); } }

async function deleteUser(u) {
  err.value = '';
  try {
    await api.del(`/api/admin-ui/users/${u.id}`);
    await loadUsers();
    undoToast.show(`"${u.displayName}" deleted.`, async () => {
      await api.post(`/api/admin-ui/users/${u.id}/restore`);
      await loadUsers();
    });
  } catch (e) { err.value = e.message; }
}

// ---- bulk-delete users ----
const selectedUsers = ref(new Set());
function toggleUserSelect(id) {
  const next = new Set(selectedUsers.value);
  next.has(id) ? next.delete(id) : next.add(id);
  selectedUsers.value = next;
}
function selectAllUsers(checked) {
  selectedUsers.value = checked
    ? new Set((users.value || []).filter((u) => u.id !== auth.user?.id).map((u) => u.id))
    : new Set();
}
const userDeleteResultMsg = ref('');
async function deleteSelectedUsers() {
  const ids = [...selectedUsers.value];
  if (!ids.length) return;
  if (!confirm(`Delete ${ids.length} user(s)? They'll move to Trash and can be restored there.`)) return;
  err.value = ''; userDeleteResultMsg.value = '';
  try {
    const result = await api.post('/api/admin-ui/users/bulk-delete', { ids });
    selectedUsers.value = new Set();
    await loadUsers();
    userDeleteResultMsg.value = `Deleted ${result.deleted} user(s).` + (result.errors.length ? ` Errors: ${result.errors.join(', ')}` : '');
  } catch (e) { err.value = e.message; }
}

async function changeRole(u, role) {
  if (role === u.role) return;
  err.value = '';
  try {
    await api.patch(`/api/admin-ui/users/${u.id}/role`, { role });
    u.role = role;
  } catch (e) { err.value = e.message; }
}
async function grantAdmin(u) {
  err.value = '';
  try { await api.post(`/api/admin-ui/users/${u.id}/admin`); u.isAdmin = true; }
  catch (e) { err.value = e.message; }
}
async function revokeAdmin(u) {
  err.value = '';
  try { await api.del(`/api/admin-ui/users/${u.id}/admin`); u.isAdmin = false; }
  catch (e) { err.value = e.message; }
}

// ---- bulk user import (CSV) ----
const importingUsers = ref(false);
const userImportResult = ref(null);
const importBoardSlug = ref('');
const importDefaultRole = ref('Student');

async function importUsersCsv(ev) {
  const file = ev.target.files?.[0];
  ev.target.value = '';
  if (!file) return;
  err.value = ''; userImportResult.value = null; importingUsers.value = true;
  try {
    const csv = await file.text();
    userImportResult.value = await api.post('/api/admin-ui/users/import', {
      csv, boardSlug: importBoardSlug.value || null, defaultRole: importDefaultRole.value,
    });
    if (users.value) await loadUsers();
  } catch (e) { err.value = e.message; }
  finally { importingUsers.value = false; }
}

// ---- boards (browse all) ----
const boards = ref(null);
const boardQ = ref('');
async function loadBoards() {
  err.value = '';
  try { boards.value = await api.get('/api/admin-ui/boards' + (boardQ.value.trim() ? `?q=${encodeURIComponent(boardQ.value.trim())}` : '')); }
  catch (e) { err.value = e.message; }
}
async function deleteBoard(b) {
  err.value = '';
  try {
    await api.del(`/api/boards/${b.slug}`);
    boards.value = boards.value.filter((x) => x.slug !== b.slug);
    undoToast.show(`"${b.title}" deleted.`, async () => {
      await api.post(`/api/boards/${b.slug}/restore`);
      await loadBoards();
    });
  } catch (e) { err.value = e.message; }
}

// ---- bulk-archive boards (e.g. "everything from last semester") ----
const selectedBoards = ref(new Set());
const archiveBeforeDate = ref('');
function toggleBoardSelect(slug) {
  const next = new Set(selectedBoards.value);
  next.has(slug) ? next.delete(slug) : next.add(slug);
  selectedBoards.value = next;
}
function selectAllBoards(checked) {
  selectedBoards.value = checked ? new Set((boards.value || []).map((b) => b.slug)) : new Set();
}
function selectBoardsBeforeDate() {
  if (!archiveBeforeDate.value) return;
  const cutoff = new Date(archiveBeforeDate.value).getTime();
  selectedBoards.value = new Set((boards.value || []).filter((b) => new Date(b.createdAt).getTime() < cutoff).map((b) => b.slug));
}
const archiveResultMsg = ref('');
async function archiveSelectedBoards() {
  const slugs = [...selectedBoards.value];
  if (!slugs.length) return;
  if (!confirm(`Archive ${slugs.length} board(s)? They'll move to Trash and can be restored there (no 30s limit for admins).`)) return;
  err.value = ''; archiveResultMsg.value = '';
  try {
    const result = await api.post('/api/admin-ui/boards/archive', { slugs });
    selectedBoards.value = new Set();
    await loadBoards();
    archiveResultMsg.value = `Archived ${result.archived} board(s).` + (result.errors.length ? ` Errors: ${result.errors.join(', ')}` : '');
  } catch (e) { err.value = e.message; }
}

// ---- trash: browse + restore/purge soft-deleted rows (no time limit for admins) ----
const trash = ref(null);
async function loadTrash() {
  err.value = '';
  try { trash.value = await api.get('/api/admin-ui/trash'); }
  catch (e) { err.value = e.message; }
}
async function restoreTrash(kind, row) {
  err.value = '';
  try {
    if (kind === 'users') await api.post(`/api/admin-ui/users/${row.id}/restore`);
    else if (kind === 'boards') await api.post(`/api/boards/${row.slug}/restore`);
    else if (kind === 'problems') await api.post(`/api/boards/${row.boardSlug}/problems/${row.slug}/restore`);
    else if (kind === 'bank') await api.post(`/api/bank/${row.slug}/restore`);
    await loadTrash();
  } catch (e) { err.value = e.message; }
}
async function purge(kind, row, label) {
  if (!confirm(`Permanently delete "${label}"? This cannot be undone.`)) return;
  err.value = '';
  try {
    await api.del(`/api/admin-ui/trash/${kind}/${kind === 'users' ? row.id : row.slug}`);
    await loadTrash();
  } catch (e) { err.value = e.message; }
}

// ---- audit log ----
const auditRows = ref(null);
async function loadAudit() {
  err.value = '';
  try { auditRows.value = await api.get('/api/admin-ui/audit-log'); }
  catch (e) { err.value = e.message; }
}

// ---- problems export / import ----
const importing = ref(false);
const importResult = ref(null);
const replaceExisting = ref(true);

async function downloadFile(url, fallbackName) {
  err.value = '';
  try {
    const res = await fetch(url.startsWith('/') ? withBase(url) : url, { credentials: 'include' });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const blob = await res.blob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = (res.headers.get('content-disposition') || '').match(/filename=([^;]+)/)?.[1] || fallbackName;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  } catch (e) { err.value = e.message; }
}
const exportProblems = () => downloadFile('/api/admin-ui/problems/export', 'beecoding-problems.json');

// ---- reports: analytics export + system status ----
const systemStatus = ref(null);
async function loadSystemStatus() {
  err.value = '';
  try { systemStatus.value = await api.get('/api/admin-ui/system-status'); }
  catch (e) { err.value = e.message; }
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
  await Promise.all([loadAi(), loadAiSettings(), loadAiOverrides()]);
});
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-8">
    <h1 class="text-xl font-bold mb-4">Admin</h1>

    <div class="inline-flex flex-wrap rounded-lg border border-slate-300 dark:border-slate-700 overflow-hidden text-sm mb-4">
      <button v-for="t in tabDefs" :key="t[0]" @click="switchTab(t[0])"
              class="px-3 py-1.5"
              :class="tab === t[0] ? 'bg-slate-800 text-white dark:bg-slate-600' : 'text-slate-500 dark:text-slate-400'">
        {{ t[1] }}
      </button>
    </div>

    <p v-if="err" class="text-sm text-red-600 dark:text-red-400 mb-3">{{ err }}</p>

    <!-- AI usage -->
    <section v-show="tab === 'ai'" class="space-y-5">
      <!-- Kill-switch + default daily quotas -->
      <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-4" v-if="aiSettings">
        <h2 class="font-semibold text-sm mb-2">Global AI settings</h2>
        <label class="flex items-center gap-2 text-sm mb-2">
          <input type="checkbox" v-model="aiSettings.paused" />
          <span :class="aiSettings.paused ? 'text-rose-600 dark:text-rose-400 font-medium' : ''">
            Pause AI platform-wide {{ aiSettings.paused ? '(paused — no AI feature works right now)' : '' }}
          </span>
        </label>
        <input v-model="aiSettings.pausedReason" placeholder="Reason (shown in the audit log only)"
               class="w-full mb-3 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-1.5 text-sm" />
        <div class="flex flex-wrap gap-4 mb-3 text-sm">
          <label class="flex items-center gap-2">Daily quota · students
            <input v-model.number="aiSettings.dailyQuotaStudent" type="number" min="0"
                   class="w-20 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1" />
          </label>
          <label class="flex items-center gap-2">Daily quota · teachers
            <input v-model.number="aiSettings.dailyQuotaTeacher" type="number" min="0"
                   class="w-20 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1" />
          </label>
        </div>
        <p class="text-[11px] text-slate-400 dark:text-slate-500 mb-3">
          Requests per user per calendar day (UTC), across hints + problem generation combined.
          Takes effect on the next AI request platform-wide — no restart needed.
        </p>
        <button @click="saveAiSettings" :disabled="aiSettingsSaving"
                class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
          {{ aiSettingsSaving ? 'Saving…' : 'Save' }}
        </button>
      </div>

      <!-- Per-user overrides / bans -->
      <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-4">
        <h2 class="font-semibold text-sm mb-2">Per-user overrides</h2>
        <div class="overflow-x-auto mb-3">
          <table class="w-full text-sm">
            <thead>
              <tr class="text-xs text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
                <th class="font-normal py-1.5 pr-3">User</th><th class="font-normal pr-3">Daily quota</th>
                <th class="font-normal pr-3">Banned</th><th class="font-normal pr-3"></th>
              </tr>
            </thead>
            <tbody class="[&_td]:py-1.5 [&_td]:pr-3">
              <tr v-for="row in aiOverrides" :key="row.userId" class="border-b border-slate-100 dark:border-slate-800/60">
                <td><div class="font-medium">{{ row.displayName }}</div><div class="text-[11px] text-slate-400">{{ row.email }}</div></td>
                <td>
                  <input v-model.number="row.dailyQuotaOverride" type="number" min="0" placeholder="role default"
                         class="w-24 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1 text-xs" />
                </td>
                <td><input type="checkbox" v-model="row.banned" /></td>
                <td class="whitespace-nowrap">
                  <button @click="saveOverride(row)" class="text-[11px] text-emerald-600 dark:text-emerald-400 hover:underline mr-3">Save</button>
                  <button @click="clearOverride(row)" class="text-[11px] text-slate-500 dark:text-slate-400 hover:underline">Clear</button>
                </td>
              </tr>
              <tr v-if="aiOverrides && !aiOverrides.length"><td colspan="4" class="text-slate-400 dark:text-slate-500 py-3">No overrides set.</td></tr>
            </tbody>
          </table>
        </div>
        <div class="flex flex-wrap items-end gap-2 text-sm">
          <label class="flex flex-col text-xs text-slate-400 dark:text-slate-500">User ID
            <input v-model.number="newOverride.userId" type="number" placeholder="see Users tab"
                   class="w-28 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1 text-sm" />
          </label>
          <label class="flex flex-col text-xs text-slate-400 dark:text-slate-500">Daily quota
            <input v-model.number="newOverride.quota" type="number" min="0" placeholder="role default"
                   class="w-28 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1 text-sm" />
          </label>
          <label class="flex items-center gap-1.5"><input type="checkbox" v-model="newOverride.banned" /> Banned</label>
          <button @click="addOverride" :disabled="!newOverride.userId"
                  class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
            Add / update
          </button>
        </div>
      </div>

      <!-- Usage -->
      <div>
        <div class="flex items-center gap-2 mb-2">
          <h2 class="font-semibold text-sm">Usage</h2>
          <button @click="loadAi" class="text-xs text-slate-500 dark:text-slate-400">↻ refresh</button>
        </div>
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
      </div>
    </section>

    <!-- Users -->
    <section v-show="tab === 'users'">
      <div class="flex gap-2 mb-3">
        <input v-model="userQ" @keyup.enter="searchUsers" placeholder="Search name or email…"
               class="flex-1 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
        <button @click="searchUsers" class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4">Search</button>
      </div>
      <div v-if="selectedUsers.size" class="flex items-center gap-2 mb-2 text-sm">
        <span>{{ selectedUsers.size }} selected</span>
        <button @click="deleteSelectedUsers" class="bg-rose-600 hover:bg-rose-700 text-white rounded-lg px-3 py-1 text-xs font-medium">
          Delete selected
        </button>
      </div>
      <p v-if="userDeleteResultMsg" class="text-xs text-emerald-600 dark:text-emerald-400 mb-2">{{ userDeleteResultMsg }}</p>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-xs text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
              <th class="font-normal py-1.5 pr-3">
                <input type="checkbox" :checked="!!users?.length && selectedUsers.size === users.filter((u) => u.id !== auth.user?.id).length"
                       @change="selectAllUsers($event.target.checked)" />
              </th>
              <th class="font-normal py-1.5 pr-3">#</th><th class="font-normal pr-3">Name / email</th>
              <th class="font-normal pr-3">Role</th><th class="font-normal pr-3">XP</th>
              <th class="font-normal pr-3">Boards</th><th class="font-normal pr-3">Subs</th>
              <th class="font-normal pr-3">Joined</th><th class="font-normal pr-3"></th>
            </tr>
          </thead>
          <tbody class="[&_td]:py-1.5 [&_td]:pr-3">
            <tr v-for="u in users" :key="u.id" class="border-b border-slate-100 dark:border-slate-800/60">
              <td><input v-if="u.id !== auth.user?.id" type="checkbox" :checked="selectedUsers.has(u.id)" @change="toggleUserSelect(u.id)" /></td>
              <td class="text-slate-400">{{ u.id }}</td>
              <td><div class="font-medium">{{ u.displayName }}</div><div class="text-[11px] text-slate-400">{{ u.email }}</div></td>
              <td>
                <select v-if="u.id !== auth.user?.id" :value="u.role" @change="changeRole(u, $event.target.value)"
                        class="text-[11px] border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-1 py-0.5">
                  <option value="Student">Student</option>
                  <option value="Teacher">Teacher</option>
                </select>
                <span v-else class="text-[11px] px-1.5 py-0.5 rounded-full bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300">{{ u.role }}</span>
                <span v-if="u.isAdmin" class="ml-1 text-[11px] px-1.5 py-0.5 rounded-full bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300">admin</span>
              </td>
              <td class="tabular-nums">{{ fmt(u.xp) }}</td>
              <td class="tabular-nums">{{ u.ownedBoards }}</td>
              <td class="tabular-nums">{{ u.submissions }}</td>
              <td class="text-[11px] text-slate-400">{{ new Date(u.createdAt).toLocaleDateString() }}</td>
              <td class="whitespace-nowrap">
                <template v-if="u.id !== auth.user?.id">
                  <button v-if="!u.isAdmin" @click="grantAdmin(u)"
                          class="text-[11px] text-violet-600 dark:text-violet-400 hover:underline mr-3">Make admin</button>
                  <button v-else @click="revokeAdmin(u)"
                          class="text-[11px] text-slate-500 dark:text-slate-400 hover:underline mr-3">Revoke admin</button>
                  <button @click="deleteUser(u)" class="text-[11px] text-rose-600 dark:text-rose-400 hover:underline">Delete</button>
                </template>
              </td>
            </tr>
            <tr v-if="users && !users.length"><td colspan="9" class="text-slate-400 dark:text-slate-500 py-3">No users.</td></tr>
          </tbody>
        </table>
      </div>
      <div v-if="usersTotal" class="flex items-center gap-3 mt-3 text-sm">
        <span class="text-slate-400 dark:text-slate-500">
          {{ (usersPage - 1) * usersPageSize + 1 }}–{{ Math.min(usersPage * usersPageSize, usersTotal) }} of {{ usersTotal }}
        </span>
        <div class="ml-auto flex gap-2">
          <button @click="usersPrevPage" :disabled="usersPage === 1"
                  class="px-3 py-1 rounded-lg border border-slate-300 dark:border-slate-700 disabled:opacity-40">Prev</button>
          <button @click="usersNextPage" :disabled="usersPage * usersPageSize >= usersTotal"
                  class="px-3 py-1 rounded-lg border border-slate-300 dark:border-slate-700 disabled:opacity-40">Next</button>
        </div>
      </div>

      <div class="mt-5 border border-slate-200 dark:border-slate-800 rounded-xl p-4">
        <h2 class="font-semibold text-sm mb-1">Bulk import (CSV)</h2>
        <p class="text-xs text-slate-400 dark:text-slate-500 mb-2">
          Header row required — at least <code>email</code> and <code>name</code> (or
          <code>displayname</code>); optional <code>role</code> and <code>password</code>
          columns. An email that already exists is left alone (just optionally added to the
          board below). A missing password is auto-generated and shown once — copy it down.
        </p>
        <div class="flex flex-wrap gap-2 mb-2 text-sm">
          <select v-model="importDefaultRole" class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1 text-sm">
            <option value="Student">Default role: Student</option>
            <option value="Teacher">Default role: Teacher</option>
          </select>
          <select v-model="importBoardSlug" class="flex-1 min-w-48 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1 text-sm">
            <option value="">Don't assign to a board</option>
            <option v-for="b in boards" :key="b.slug" :value="b.slug">Add to: {{ b.title }}</option>
          </select>
        </div>
        <input type="file" accept="text/csv,.csv" @change="importUsersCsv" :disabled="importingUsers" class="text-sm" />
        <p v-if="importingUsers" class="text-xs text-slate-400 mt-2">Importing…</p>
        <div v-if="userImportResult" class="mt-3 text-sm">
          <p class="text-emerald-600 dark:text-emerald-400">
            Created {{ userImportResult.created }} · Already existed {{ userImportResult.existing }} · Errors {{ userImportResult.errors }}
          </p>
          <div class="overflow-x-auto mt-2">
            <table class="w-full text-xs">
              <thead>
                <tr class="text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
                  <th class="font-normal py-1 pr-3">Email</th><th class="font-normal pr-3">Name</th>
                  <th class="font-normal pr-3">Password</th><th class="font-normal pr-3">Note</th>
                </tr>
              </thead>
              <tbody class="[&_td]:py-1 [&_td]:pr-3">
                <tr v-for="(row, i) in userImportResult.rows" :key="i" class="border-b border-slate-100 dark:border-slate-800/60">
                  <td>{{ row.email }}</td>
                  <td>{{ row.displayName }}</td>
                  <td class="font-mono">{{ row.generatedPassword || '—' }}</td>
                  <td :class="row.error ? 'text-red-600 dark:text-red-400' : 'text-slate-400'">
                    {{ row.error || (row.addedToBoard ? 'added to board' : (row.created ? 'created' : 'already existed')) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>

    <!-- Boards (browse all, across every teacher) -->
    <section v-show="tab === 'boards'">
      <div class="flex flex-wrap gap-2 mb-3 items-center">
        <input v-model="boardQ" @keyup.enter="loadBoards" placeholder="Search title or slug…"
               class="flex-1 min-w-40 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
        <button @click="loadBoards" class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-2">Search</button>
        <span class="text-slate-300 dark:text-slate-700">|</span>
        <label class="text-xs text-slate-500 dark:text-slate-400 flex items-center gap-1">Select boards created before
          <input type="date" v-model="archiveBeforeDate" class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1 text-sm" />
        </label>
        <button @click="selectBoardsBeforeDate" class="text-xs text-slate-500 dark:text-slate-400 hover:underline">select</button>
      </div>
      <div v-if="selectedBoards.size" class="flex items-center gap-2 mb-2 text-sm">
        <span>{{ selectedBoards.size }} selected</span>
        <button @click="archiveSelectedBoards" class="bg-rose-600 hover:bg-rose-700 text-white rounded-lg px-3 py-1 text-xs font-medium">
          🗄️ Archive selected
        </button>
      </div>
      <p v-if="archiveResultMsg" class="text-xs text-emerald-600 dark:text-emerald-400 mb-2">{{ archiveResultMsg }}</p>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-xs text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
              <th class="font-normal py-1.5 pr-3">
                <input type="checkbox" :checked="!!boards?.length && selectedBoards.size === boards.length"
                       @change="selectAllBoards($event.target.checked)" />
              </th>
              <th class="font-normal pr-3">Title</th><th class="font-normal pr-3">Owner</th>
              <th class="font-normal pr-3">Students</th><th class="font-normal pr-3">Problems</th>
              <th class="font-normal pr-3">Created</th><th class="font-normal pr-3"></th>
            </tr>
          </thead>
          <tbody class="[&_td]:py-1.5 [&_td]:pr-3">
            <tr v-for="b in boards" :key="b.slug" class="border-b border-slate-100 dark:border-slate-800/60">
              <td><input type="checkbox" :checked="selectedBoards.has(b.slug)" @change="toggleBoardSelect(b.slug)" /></td>
              <td><RouterLink :to="`/boards/${b.slug}`" class="font-medium hover:underline">{{ b.title }}</RouterLink>
                <div class="text-[11px] text-slate-400 font-mono">{{ b.slug }}</div></td>
              <td><div class="font-medium">{{ b.ownerName }}</div><div class="text-[11px] text-slate-400">{{ b.ownerEmail }}</div></td>
              <td class="tabular-nums">{{ b.memberCount }}</td>
              <td class="tabular-nums">{{ b.problemCount }}</td>
              <td class="text-[11px] text-slate-400">{{ new Date(b.createdAt).toLocaleDateString() }}</td>
              <td><button @click="deleteBoard(b)" class="text-[11px] text-rose-600 dark:text-rose-400 hover:underline">Delete</button></td>
            </tr>
            <tr v-if="boards && !boards.length"><td colspan="7" class="text-slate-400 dark:text-slate-500 py-3">No boards.</td></tr>
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

    <!-- AI review: AI-generated bank problems held back until approved -->
    <section v-show="tab === 'review'">
      <div class="flex items-center gap-2 mb-3">
        <button @click="loadAiReview" class="text-xs text-slate-500 dark:text-slate-400">↻ refresh</button>
        <span class="text-xs text-slate-400 dark:text-slate-500">{{ aiReview?.length ?? 0 }} waiting</span>
      </div>
      <div v-for="row in aiReview" :key="row.slug" class="border border-slate-200 dark:border-slate-800 rounded-xl p-4 mb-3">
        <div class="flex items-start justify-between gap-2 flex-wrap">
          <div>
            <div class="font-semibold text-sm">{{ row.title }}</div>
            <div class="text-[11px] text-slate-400 dark:text-slate-500">
              by {{ row.ownerName }} ({{ row.ownerEmail }}) · {{ row.level }} · {{ row.tags || 'no tags' }} ·
              {{ row.allowedLanguages ? row.allowedLanguages : 'any language' }} ·
              {{ row.tests.length }} tests ({{ row.tests.filter(t => t.isSample).length }} sample) ·
              generated {{ when(row.createdAt) }}
            </div>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <button @click="expandedReview = expandedReview === row.slug ? null : row.slug"
                    class="text-xs text-slate-500 dark:text-slate-400 hover:underline">
              {{ expandedReview === row.slug ? 'hide' : 'view' }} statement &amp; tests
            </button>
            <button @click="approveAiReview(row)" class="text-xs bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg px-3 py-1.5 font-medium">
              ✓ Approve &amp; share
            </button>
          </div>
        </div>

        <div v-if="expandedReview === row.slug" class="mt-3 space-y-3">
          <div class="text-sm bg-slate-50 dark:bg-slate-800/40 rounded-lg p-3 whitespace-pre-wrap font-mono text-xs max-h-64 overflow-auto">{{ row.statementMarkdown }}</div>
          <div class="grid sm:grid-cols-2 gap-2">
            <div v-for="(t, i) in row.tests" :key="i" class="text-xs border border-slate-100 dark:border-slate-800 rounded-lg p-2">
              <div class="text-slate-400 mb-1">{{ t.isSample ? 'sample' : 'hidden' }} #{{ i + 1 }}</div>
              <pre class="bg-slate-100 dark:bg-slate-800 rounded p-1.5 overflow-x-auto">stdin: {{ t.stdin }}</pre>
              <pre class="bg-slate-100 dark:bg-slate-800 rounded p-1.5 overflow-x-auto mt-1">expected: {{ t.expectedStdout }}</pre>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <input v-model="rejectReasonDraft[row.slug]" placeholder="Reason (optional, goes in the audit log)"
                   class="flex-1 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-1.5 text-sm" />
            <button @click="rejectAiReview(row)" class="text-xs bg-rose-600 hover:bg-rose-700 text-white rounded-lg px-3 py-1.5 font-medium">
              ✕ Reject (stays private)
            </button>
          </div>
        </div>
      </div>
      <p v-if="aiReview && !aiReview.length" class="text-slate-400 dark:text-slate-500 text-sm">Nothing waiting for review.</p>
    </section>

    <!-- Reports: analytics export + system status -->
    <section v-show="tab === 'reports'" class="space-y-4">
      <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-4">
        <h2 class="font-semibold text-sm mb-1">Analytics export</h2>
        <p class="text-xs text-slate-400 dark:text-slate-500 mb-3">
          CSV, ready to open in a spreadsheet — also useful as evidence for an impact/grant report.
        </p>
        <div class="flex flex-wrap gap-2">
          <button @click="downloadFile('/api/admin-ui/analytics/topics.csv', 'topic-stats.csv')"
                  class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium">
            Topic solve rates
          </button>
          <button @click="downloadFile('/api/admin-ui/analytics/users.csv', 'user-xp.csv')"
                  class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium">
            User XP
          </button>
          <button @click="downloadFile('/api/admin-ui/analytics/engagement.csv', 'weekly-engagement.csv')"
                  class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium">
            Weekly engagement
          </button>
        </div>
        <div class="mt-3 pt-3 border-t border-slate-100 dark:border-slate-800">
          <button @click="downloadFile('/api/admin-ui/analytics/report.xlsx', 'beecoding-report.xlsx')"
                  class="bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium">
            📊 Export all as Excel (.xlsx)
          </button>
          <p class="text-[11px] text-slate-400 dark:text-slate-500 mt-1">
            One workbook, three sheets — Topics, Users, Weekly engagement.
          </p>
        </div>
      </div>

      <div class="border border-slate-200 dark:border-slate-800 rounded-xl p-4">
        <div class="flex items-center gap-2 mb-2">
          <h2 class="font-semibold text-sm">System status</h2>
          <button @click="loadSystemStatus" class="text-xs text-slate-500 dark:text-slate-400">↻ refresh</button>
        </div>
        <div v-if="systemStatus" class="grid sm:grid-cols-2 gap-x-6 gap-y-1.5 text-sm">
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Judge sandbox</span>
            <span :class="systemStatus.bwrapUsable ? 'text-emerald-600 dark:text-emerald-400' : 'text-amber-600 dark:text-amber-400'">
              {{ systemStatus.sandboxMode }}
            </span>
          </div>
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Sandbox required (fail closed)</span>
            <span>{{ systemStatus.sandboxRequired ? 'yes' : 'no' }}</span>
          </div>
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Judge queue backend</span>
            <span>{{ systemStatus.judgeQueueBackend }}</span>
          </div>
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Pending judge jobs</span>
            <span>{{ systemStatus.pendingJudgeJobs ?? 'n/a' }}</span>
          </div>
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Realtime backend</span>
            <span>{{ systemStatus.realtimeBackend }}</span>
          </div>
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Redis</span>
            <span v-if="!systemStatus.redisConfigured" class="text-slate-400">not used</span>
            <span v-else :class="systemStatus.redisConnected ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
              {{ systemStatus.redisConnected ? 'connected' : 'disconnected' }}
            </span>
          </div>
          <div class="flex justify-between border-b border-slate-100 dark:border-slate-800/60 py-1">
            <span class="text-slate-400 dark:text-slate-500">Database</span>
            <span :class="systemStatus.databaseOk ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
              {{ systemStatus.databaseOk ? 'reachable' : 'unreachable' }}
            </span>
          </div>
          <div class="flex justify-between py-1">
            <span class="text-slate-400 dark:text-slate-500">Checked</span>
            <span class="text-[11px] text-slate-400">{{ when(systemStatus.checkedAt) }}</span>
          </div>
        </div>
      </div>
    </section>

    <!-- Trash: soft-deleted rows. Restore has no time limit here; Purge is permanent. -->
    <section v-show="tab === 'trash'" class="space-y-5">
      <button @click="loadTrash" class="text-xs text-slate-500 dark:text-slate-400">↻ refresh</button>

      <div v-for="group in [
        ['Users', 'users', trash?.users, (r) => r.displayName],
        ['Boards', 'boards', trash?.boards, (r) => r.title],
        ['Problems', 'problems', trash?.problems, (r) => `${r.title} (${r.boardTitle})`],
        ['Bank problems', 'bank', trash?.bankProblems, (r) => r.title],
      ]" :key="group[1]">
        <h2 class="font-semibold text-sm mb-1.5">{{ group[0] }} · {{ group[2]?.length ?? 0 }}</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <tbody class="[&_td]:py-1.5 [&_td]:pr-3">
              <tr v-for="row in group[2]" :key="row.slug ?? row.id" class="border-b border-slate-100 dark:border-slate-800/60">
                <td class="font-medium">{{ group[3](row) }}</td>
                <td class="text-[11px] text-slate-400">{{ row.email || row.ownerEmail || '' }}</td>
                <td class="text-[11px] text-slate-400">deleted {{ when(row.deletedAt) }}</td>
                <td class="text-right">
                  <button @click="restoreTrash(group[1], row)" class="text-[11px] text-emerald-600 dark:text-emerald-400 hover:underline mr-3">Restore</button>
                  <button @click="purge(group[1], row, group[3](row))" class="text-[11px] text-rose-600 dark:text-rose-400 hover:underline">Purge</button>
                </td>
              </tr>
              <tr v-if="group[2] && !group[2].length"><td class="text-slate-400 dark:text-slate-500 py-1.5 text-xs">Empty.</td></tr>
            </tbody>
          </table>
        </div>
      </div>
      <p v-if="!trash" class="text-slate-400 dark:text-slate-500 text-sm">Loading…</p>
    </section>

    <!-- Audit log -->
    <section v-show="tab === 'audit'">
      <button @click="loadAudit" class="text-xs text-slate-500 dark:text-slate-400 mb-2">↻ refresh</button>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="text-xs text-left text-slate-400 dark:text-slate-500 border-b border-slate-200 dark:border-slate-800">
              <th class="font-normal py-1.5 pr-3">When</th><th class="font-normal pr-3">Actor</th>
              <th class="font-normal pr-3">Action</th><th class="font-normal pr-3">Target</th>
            </tr>
          </thead>
          <tbody class="[&_td]:py-1.5 [&_td]:pr-3">
            <tr v-for="r in auditRows" :key="r.id" class="border-b border-slate-100 dark:border-slate-800/60">
              <td class="text-[11px] text-slate-400 whitespace-nowrap">{{ when(r.createdAt) }}</td>
              <td class="text-[11px]">{{ r.actorEmail }}</td>
              <td>
                <span class="text-[11px] px-1.5 py-0.5 rounded-full"
                      :class="{
                        'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300': ['delete', 'admin-revoke', 'ai-ban', 'ai-pause', 'ai-review-reject'].includes(r.action),
                        'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300': ['restore', 'create', 'ai-resume', 'ai-review-approve'].includes(r.action),
                        'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-300': ['purge', 'ai-override-clear'].includes(r.action),
                        'bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300': ['admin-grant', 'role-change', 'ai-quota-set'].includes(r.action),
                      }">{{ r.action }}</span>
              </td>
              <td class="text-[11px]">{{ r.targetType }} · {{ r.targetLabel }}</td>
            </tr>
            <tr v-if="auditRows && !auditRows.length"><td colspan="4" class="text-slate-400 dark:text-slate-500 py-3">No activity yet.</td></tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
