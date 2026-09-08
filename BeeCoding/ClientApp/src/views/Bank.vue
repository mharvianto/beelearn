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

// AI problem generator
const aiEnabled = ref(false);
const genOpen = ref(false);
const genBusy = ref(false);
const genError = ref('');
const gen = ref({ idea: '', level: 'Medium', language: 'cpp', count: 10, lang: 'id' });

async function generate() {
  genError.value = ''; genBusy.value = true;
  try {
    const p = await api.post('/api/ai/generate-problem', {
      idea: gen.value.idea.trim(),
      level: gen.value.level,
      language: gen.value.language,
      count: Number(gen.value.count),
      lang: gen.value.lang,
    });
    genOpen.value = false;
    gen.value.idea = '';
    await load();
    editing.value = p;   // open it for review / tweak / publish
  } catch (e) {
    genError.value = e.message + (e.compilerOutput ? '\n\n' + e.compilerOutput : '') + (e.stderr ? '\n\n' + e.stderr : '');
  } finally { genBusy.value = false; }
}

async function load() {
  error.value = '';
  try {
    const p = new URLSearchParams({ scope: scope.value });
    if (q.value.trim()) p.set('q', q.value.trim());
    if (level.value) p.set('level', level.value);
    items.value = await api.get(`/api/bank?${p}`);
  } catch (e) { error.value = e.message; }
}
onMounted(async () => {
  load();
  try { aiEnabled.value = (await api.get('/api/ai/enabled'))?.enabled === true; } catch { /* ignore */ }
});

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
  if (!editing.value?.id || !confirm("Delete this problem from the bank?")) return;
  try {
    await api.del(`/api/bank/${editing.value.id}`);
    editing.value = null;
    await load();
  } catch (e) { editError.value = e.message; }
}
</script>

<template>
  <div class="max-w-5xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-4 gap-2 flex-wrap">
      <h1 class="text-xl font-bold">Problem bank</h1>
      <div class="flex items-center gap-2">
        <button v-if="aiEnabled" @click="genOpen = !genOpen"
                class="px-3 py-1.5 rounded-lg text-sm font-medium border border-violet-300 dark:border-violet-500/40 text-violet-700 dark:text-violet-300">
          ✨ Generate with AI
        </button>
        <button @click="editing = {}" class="px-3 py-1.5 rounded-lg text-sm font-medium bg-amber-500 text-white">
          + New problem
        </button>
      </div>
    </div>

    <div v-if="genOpen" class="mb-4 border border-violet-200 dark:border-violet-500/30 rounded-xl p-4 space-y-3 bg-violet-50/40 dark:bg-violet-500/5">
      <p class="text-sm font-semibold text-violet-700 dark:text-violet-300">✨ Generate a problem from an idea</p>
      <textarea v-model="gen.idea" rows="3"
                placeholder="e.g. 'jumlah elemen array yang habis dibagi k', 'cek graf bipartit', 'prefix sum kueri rentang'"
                class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm"></textarea>
      <div class="flex flex-wrap gap-2 text-sm">
        <select v-model="gen.level" class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1">
          <option>Easy</option><option>Medium</option><option>Hard</option>
        </select>
        <select v-model="gen.language" class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1">
          <option value="cpp">C++</option><option value="c">C</option>
        </select>
        <label class="flex items-center gap-1">tests
          <input v-model.number="gen.count" type="number" min="3" max="15"
                 class="w-14 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1" />
        </label>
        <select v-model="gen.lang" class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1">
          <option value="id">Statement: Indonesia</option><option value="en">Statement: English</option>
        </select>
      </div>
      <p class="text-[11px] text-slate-400 dark:text-slate-500">
        The AI writes the statement + a reference solution; the judge runs that solution against the
        inputs, so the stored expected outputs are the real program output. Saved to your bank as a
        private draft — review and publish it yourself.
      </p>
      <pre v-if="genError" class="text-xs text-red-600 dark:text-red-400 whitespace-pre-wrap max-h-40 overflow-auto">{{ genError }}</pre>
      <div class="flex gap-2">
        <button @click="generate" :disabled="genBusy || !gen.idea.trim()"
                class="bg-violet-600 hover:bg-violet-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
          {{ genBusy ? 'Writing & checking…' : 'Generate' }}
        </button>
        <button @click="genOpen = false" class="text-slate-500 dark:text-slate-400 text-sm px-3">Cancel</button>
      </div>
    </div>

    <div class="flex gap-2 mb-4">
      <input v-model="q" @keyup.enter="load" placeholder="Search title or tag…"
             class="flex-1 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 text-sm" />
      <select v-model="scope" @change="load"
              class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 text-sm">
        <option value="mine">Mine</option>
        <option value="public">Shared by others</option>
        <option value="all">All</option>
      </select>
      <select v-model="level" @change="load"
              class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 text-sm">
        <option value="">All levels</option>
        <option>Easy</option><option>Medium</option><option>Hard</option>
      </select>
      <button @click="load" class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4">Search</button>
    </div>

    <p v-if="error" class="text-sm text-red-600 dark:text-red-400 mb-3">{{ error }}</p>

    <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
      <div v-for="b in items" :key="b.id"
           class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl p-3">
        <div class="flex items-start justify-between gap-2">
          <h3 class="font-semibold text-sm">{{ b.title }}</h3>
          <span v-if="b.isPublic" class="text-[10px] shrink-0 px-1.5 py-0.5 rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
            shared
          </span>
        </div>
        <div class="mt-1"><LevelBadge :level="b.level" /></div>
        <div class="text-[11px] text-slate-400 dark:text-slate-500 mt-1">
          {{ b.language.toUpperCase() }} · {{ b.testCount }} tests ({{ b.sampleCount }} sample)
          <span v-if="!b.mine"> · by {{ b.ownerName }}</span>
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
    <p v-if="!items.length" class="text-slate-400 dark:text-slate-500 text-sm">Nothing here yet.</p>

    <ProblemEditor v-if="editing !== null"
      :problem="editing.id ? editing : null"
      :show-bank-fields="true"
      :error="editError"
      :busy="busy"
      @save="save" @delete="remove" @cancel="editing = null" />
  </div>
</template>
