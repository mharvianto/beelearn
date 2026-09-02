<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../lib/api';
import ProblemEditor from '../components/ProblemEditor.vue';
import LevelBadge from '../components/LevelBadge.vue';

const items = ref([]);
const q = ref('');
const scope = ref('mine');
const level = ref('');
const editing = ref(null);   // {} = new, object = edit, null = closed
const error = ref('');
const editError = ref('');
const busy = ref(false);

async function load() {
  error.value = '';
  try {
    const p = new URLSearchParams({ scope: scope.value });
    if (q.value.trim()) p.set('q', q.value.trim());
    if (level.value) p.set('level', level.value);
    items.value = await api.get(`/api/bank?${p}`);
  } catch (e) { error.value = e.message; }
}
onMounted(load);

async function openEdit(item) {
  editError.value = '';
  try { editing.value = await api.get(`/api/bank/${item.id}`); }
  catch (e) { error.value = e.message; }
}

async function save(form) {
  editError.value = ''; busy.value = true;
  try {
    const body = { ...form.value ?? form };
    if (editing.value?.id) await api.put(`/api/bank/${editing.value.id}`, body);
    else await api.post('/api/bank', body);
    editing.value = null;
    await load();
  } catch (e) { editError.value = e.message; }
  finally { busy.value = false; }
}

async function remove() {
  if (!editing.value?.id || !confirm('Hapus soal ini dari bank?')) return;
  try {
    await api.del(`/api/bank/${editing.value.id}`);
    editing.value = null;
    await load();
  } catch (e) { editError.value = e.message; }
}
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-4">
      <h1 class="text-xl font-bold">Bank soal</h1>
      <button @click="editing = {}" class="px-3 py-1.5 rounded-lg text-sm font-medium bg-amber-500 text-white">
        + Soal baru
      </button>
    </div>

    <div class="flex gap-2 mb-4">
      <input v-model="q" @keyup.enter="load" placeholder="Cari judul atau tag…"
             class="flex-1 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
      <select v-model="scope" @change="load"
              class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 text-sm">
        <option value="mine">Milik saya</option>
        <option value="public">Dibagikan guru lain</option>
        <option value="all">Semua</option>
      </select>
      <select v-model="level" @change="load"
              class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 text-sm">
        <option value="">Semua level</option>
        <option>Easy</option><option>Medium</option><option>Hard</option>
      </select>
      <button @click="load" class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4">Cari</button>
    </div>

    <p v-if="error" class="text-sm text-red-600 dark:text-red-400 mb-3">{{ error }}</p>

    <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
      <div v-for="b in items" :key="b.id"
           class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl p-3">
        <div class="flex items-start justify-between gap-2">
          <h3 class="font-semibold text-sm">{{ b.title }}</h3>
          <span v-if="b.isPublic" class="text-[10px] shrink-0 px-1.5 py-0.5 rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
            dibagikan
          </span>
        </div>
        <div class="mt-1"><LevelBadge :level="b.level" /></div>
        <div class="text-[11px] text-slate-400 dark:text-slate-500 mt-1">
          {{ b.language.toUpperCase() }} · {{ b.testCount }} test ({{ b.sampleCount }} sample)
          <span v-if="!b.mine"> · oleh {{ b.ownerName }}</span>
        </div>
        <div v-if="b.tags" class="flex flex-wrap gap-1 mt-2">
          <span v-for="t in b.tags.split(',')" :key="t"
                class="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
            {{ t }}
          </span>
        </div>
        <button v-if="b.mine" @click="openEdit(b)"
                class="mt-3 text-xs text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">
          edit
        </button>
      </div>
    </div>
    <p v-if="!items.length" class="text-slate-400 dark:text-slate-500 text-sm">Belum ada soal di sini.</p>

    <ProblemEditor v-if="editing !== null"
      :problem="editing.id ? editing : null"
      :show-bank-fields="true"
      :error="editError"
      :busy="busy"
      @save="save" @delete="remove" @cancel="editing = null" />
  </div>
</template>
