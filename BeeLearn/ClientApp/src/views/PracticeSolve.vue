<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue';
import { api } from '../lib/api';
import { useProgress } from '../stores/progress';
import { createBoardConnection } from '../lib/signalr';
import MonacoEditor from '../components/MonacoEditor.vue';
import VerdictBadge from '../components/VerdictBadge.vue';
import LevelBadge from '../components/LevelBadge.vue';
import ContentGuard from '../components/ContentGuard.vue';
import StatementImage from '../components/StatementImage.vue';

const props = defineProps({ id: { type: [String, Number], required: true } });
const progress = useProgress();

const problem = ref(null);
const code = ref('');
const stdin = ref('');
const runOut = ref(null);
const running = ref(false);
const submitting = ref(false);
const submissions = ref([]);
const error = ref('');
const gained = ref(0);
let conn = null;

async function load() {
  problem.value = await api.get(`/api/practice/${props.id}`);
  code.value = problem.value.starterCode || '';
  if (problem.value.sampleTests?.[0]) stdin.value = problem.value.sampleTests[0].stdin;
  await loadSubs();
}
async function loadSubs() {
  submissions.value = await api.get(`/api/practice/${props.id}/submissions`);
  problem.value.solved = submissions.value.some((s) => s.verdict === 'Accepted' && s.score >= 1);
}

async function run() {
  error.value = ''; running.value = true; runOut.value = null;
  try {
    runOut.value = await api.post('/api/run', { language: problem.value.language, code: code.value, stdin: stdin.value });
  } catch (e) { error.value = e.message; }
  finally { running.value = false; }
}

async function submit() {
  error.value = ''; submitting.value = true; gained.value = 0;
  try {
    const before = progress.xp;
    const alreadySolved = problem.value.solved;
    await api.post(`/api/practice/${props.id}/submit`, { code: code.value });
    await loadSubs();
    // give the judge a moment, then reconcile XP
    setTimeout(async () => {
      await loadSubs();
      await progress.refresh();
      if (!alreadySolved && progress.xp > before) gained.value = progress.xp - before;
    }, 1500);
  } catch (e) { error.value = e.message; }
  finally { submitting.value = false; }
}

onMounted(async () => {
  try { await load(); } catch (e) { error.value = e.message; return; }
  progress.refresh();
  conn = createBoardConnection();
  conn.on('practiceResult', (dto) => {
    if (dto.bankProblemId === Number(props.id)) loadSubs();
  });
  conn.on('progressBumped', (p) => {
    const before = progress.xp;
    progress.$patch({ ...p, ready: true });
    if (p.xp > before) gained.value = p.xp - before;
  });
  try { await conn.start(); } catch { /* realtime best-effort */ }
});
onBeforeUnmount(async () => { try { await conn?.stop(); } catch {} });
</script>

<template>
  <div v-if="problem" class="h-full grid lg:grid-cols-2 gap-0">
    <div class="p-5 overflow-y-auto border-r border-slate-200 dark:border-slate-800">
      <RouterLink to="/practice" class="text-sm text-slate-400 dark:text-slate-500">&larr; back to Practice</RouterLink>
      <div class="flex items-center gap-2 mt-2 mb-1 flex-wrap">
        <h1 class="text-lg font-bold">{{ problem.title }}</h1>
        <LevelBadge :level="problem.level" />
        <span v-if="problem.solved" class="text-[10px] px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">✓ solved</span>
        <span v-for="t in (problem.tags ? problem.tags.split(',') : [])" :key="t"
              class="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">{{ t }}</span>
      </div>
      <div class="text-xs text-slate-400 dark:text-slate-500 mb-3">
        {{ problem.language.toUpperCase() }} · limit {{ problem.timeLimitMs }} ms · {{ problem.memoryLimitKb }} KB
      </div>

      <div v-if="gained" class="mb-3 text-sm bg-amber-100 text-amber-800 dark:bg-amber-500/15 dark:text-amber-200 rounded-lg px-3 py-2">
        🎉 +{{ gained }} XP! Now Lv {{ progress.level }} · {{ progress.xp }} XP
      </div>

      <p class="text-[11px] text-amber-600 dark:text-amber-400 mb-2">
        🔒 Protected problem — served as an encrypted image watermarked with your identity.
      </p>
      <ContentGuard :active="true" :watermark="''">
        <StatementImage :bank-id="props.id" />
      </ContentGuard>

      <div v-if="problem.sampleTests?.length" class="mt-3 flex items-center gap-2 flex-wrap">
        <span class="text-xs text-slate-400 dark:text-slate-500">Load sample input:</span>
        <button v-for="(t, i) in problem.sampleTests" :key="i" @click="stdin = t.stdin"
                class="text-xs px-2 py-0.5 rounded border border-slate-300 dark:border-slate-700 hover:border-amber-400">
          Sample {{ i + 1 }}
        </button>
      </div>

      <h3 class="font-semibold text-sm mt-5 mb-2">History</h3>
      <div class="space-y-1">
        <div v-for="s in submissions" :key="s.id"
             class="flex items-center gap-2 text-sm border border-slate-100 dark:border-slate-800 rounded-lg px-2 py-1.5">
          <VerdictBadge :verdict="s.status === 'Done' ? s.verdict : s.status" small />
          <span v-if="s.status === 'Done'" class="text-xs text-slate-400 dark:text-slate-500">
            {{ s.runtimeMs }}ms · {{ s.memoryKb }}KB · {{ Math.round(s.score * 100) }}%
          </span>
          <span class="text-xs text-slate-400 dark:text-slate-500 ml-auto">{{ new Date(s.createdAt + 'Z').toLocaleTimeString() }}</span>
        </div>
        <p v-if="!submissions.length" class="text-slate-400 dark:text-slate-500 text-sm">No submissions yet.</p>
      </div>
    </div>

    <div class="flex flex-col h-full min-h-0">
      <div class="flex-1 min-h-0">
        <MonacoEditor v-model="code" :language="problem.language === 'c' ? 'c' : 'cpp'" />
      </div>
      <div class="border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3 space-y-2">
        <div class="flex gap-2">
          <button @click="run" :disabled="running"
                  class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
            {{ running ? 'Running…' : 'Run' }}
          </button>
          <button @click="submit" :disabled="submitting"
                  class="bg-amber-500 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
            {{ submitting ? 'Submitting…' : 'Submit' }}
          </button>
        </div>
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400 dark:text-slate-500">stdin</label>
            <textarea v-model="stdin" rows="3"
                      class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1 font-mono text-xs"></textarea>
          </div>
          <div>
            <label class="text-xs text-slate-400 dark:text-slate-500">output</label>
            <pre class="w-full h-[76px] bg-slate-900 text-slate-100 dark:bg-black dark:border dark:border-slate-800 rounded-lg px-2 py-1 font-mono text-xs overflow-auto whitespace-pre-wrap">{{
              runOut
                ? (runOut.compileOk
                    ? (runOut.stdout || '') + (runOut.stderr ? '\n[stderr] ' + runOut.stderr : '') +
                      `\n— ${runOut.runtimeMs}ms, ${runOut.memoryKb}KB${runOut.timedOut ? ', TIMED OUT' : ''}`
                    : '[compile error]\n' + runOut.compilerOutput)
                : ''
            }}</pre>
          </div>
        </div>
        <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
      </div>
    </div>
  </div>
</template>
