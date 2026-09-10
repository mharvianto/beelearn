<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../lib/api';
import ProblemEditor from '../components/ProblemEditor.vue';

// One page for all four routes:
//   /bank/new  /bank/:id/edit  /boards/:slug/problems/new  /boards/:slug/problems/:id/edit
const props = defineProps({ id: [String, Number], slug: String });
const router = useRouter();

const isBoard = computed(() => !!props.slug);
const editId = computed(() => (props.id ? Number(props.id) : null));
const listPath = computed(() => (isBoard.value ? `/boards/${props.slug}` : '/bank'));
const apiBase = computed(() => (isBoard.value ? `/api/boards/${props.slug}/problems` : '/api/bank'));

const problem = ref(null);        // null = new / not loaded yet
const loaded = ref(false);
const error = ref('');
const busy = ref(false);
const aiEnabled = ref(false);
const regenBusy = ref(false);

async function load() {
  error.value = '';
  try {
    problem.value = editId.value ? await api.get(`${apiBase.value}/${editId.value}`) : null;
  } catch (e) { error.value = e.message; }
  loaded.value = true;
}

async function save(form) {
  error.value = ''; busy.value = true;
  try {
    const body = { ...(form.value ?? form) };
    if (editId.value) await api.put(`${apiBase.value}/${editId.value}`, body);
    else await api.post(apiBase.value, body);
    router.push(listPath.value);
  } catch (e) { error.value = e.message; }
  finally { busy.value = false; }
}

async function remove() {
  if (!editId.value || !confirm('Delete this problem?')) return;
  error.value = '';
  try {
    await api.del(`${apiBase.value}/${editId.value}`);
    router.push(listPath.value);
  } catch (e) { error.value = e.message; }
}

function cancel() {
  if (window.history.length > 1) router.back();
  else router.push(listPath.value);
}

// regenerate hidden tests — bank problems only (background job, poll)
async function regenerateTests() {
  if (isBoard.value || !editId.value) return;
  error.value = ''; regenBusy.value = true;
  try {
    const { jobId } = await api.post(`/api/ai/regenerate-tests/${editId.value}`);
    for (let n = 0; n < 240; n++) {
      await new Promise((r) => setTimeout(r, 2000));
      let r;
      try { r = await api.get(`/api/ai/generate-problem/${jobId}`); }
      catch (e) { if (e.status === 404) { error.value = 'The job expired.'; break; } continue; }
      if (r.status === 'running') continue;
      if (r.status === 'done') problem.value = r.problem;
      else error.value = (r.message || 'Regeneration failed.') +
        (r.compilerOutput ? '\n\n' + r.compilerOutput : '') + (r.stderr ? '\n\n' + r.stderr : '');
      break;
    }
  } catch (e) { error.value = e.message; }
  finally { regenBusy.value = false; }
}

onMounted(async () => {
  await load();
  if (!isBoard.value) {
    try { aiEnabled.value = (await api.get('/api/ai/enabled'))?.enabled === true; } catch { /* ignore */ }
  }
});
</script>

<template>
  <div>
    <p v-if="!loaded" class="max-w-5xl mx-auto px-4 py-10 text-sm text-slate-400 dark:text-slate-500">Loading…</p>
    <ProblemEditor v-else
      :problem="problem"
      :show-bank-fields="!isBoard"
      :error="error"
      :busy="busy"
      :can-regen-tests="!isBoard && aiEnabled && !!editId"
      :regen-busy="regenBusy"
      @save="save" @delete="remove" @cancel="cancel" @regenerate-tests="regenerateTests" />
  </div>
</template>
