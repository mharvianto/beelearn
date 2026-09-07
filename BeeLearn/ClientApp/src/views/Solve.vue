<script setup>
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue';
import { api } from '../lib/api';
import { useAuth } from '../stores/auth';
import { useProgress } from '../stores/progress';
import { createBoardConnection } from '../lib/signalr';
import MonacoEditor from '../components/MonacoEditor.vue';
import MarkdownBlock from '../components/MarkdownBlock.vue';
import VerdictBadge from '../components/VerdictBadge.vue';
import LevelBadge from '../components/LevelBadge.vue';
import ContentGuard from '../components/ContentGuard.vue';
import StatementImage from '../components/StatementImage.vue';

const props = defineProps({ slug: { type: String, required: true }, problemId: [String, Number] });
const auth = useAuth();
const progress = useProgress();

const board = ref(null);
const problem = ref(null);
const protectOn = computed(() => !!board.value?.protectContent && auth.user?.role === 'Student');
const watermark = computed(() =>
  `${auth.user?.email || auth.user?.displayName || ''} · ${new Date().toLocaleString()}`);
const code = ref('');
const stdin = ref('');
const runOut = ref(null);
const running = ref(false);
const submitting = ref(false);
const submissions = ref([]);
const error = ref('');
const myPost = ref({ postId: null, hiddenByStudent: false });
let conn = null;

const mine = computed(() => submissions.value.filter((s) => s.mine));
const latestMine = computed(() => mine.value[0]);

async function load() {
  board.value = await api.get(`/api/boards/${props.slug}`);
  problem.value = await api.get(`/api/boards/${props.slug}/problems/${props.problemId}`);
  code.value = problem.value.starterCode || '';
  if (problem.value.sampleTests?.[0]) stdin.value = problem.value.sampleTests[0].stdin;
  await loadSubs();
}
async function loadSubs() {
  submissions.value = await api.get(`/api/problems/${props.problemId}/submissions`);
}

async function run() {
  error.value = ''; running.value = true; runOut.value = null;
  try {
    runOut.value = await api.post('/api/run', { language: problem.value.language, code: code.value, stdin: stdin.value });
  } catch (e) { error.value = e.message; }
  finally { running.value = false; }
}

async function submit() {
  error.value = ''; submitting.value = true;
  try {
    await api.post(`/api/problems/${props.problemId}/submit`, { code: code.value });
    await loadSubs();
  } catch (e) { error.value = e.message; }
  finally { submitting.value = false; }
}

async function toggleHiddenFromPeers() {
  const next = !myPost.value.hiddenByStudent;
  await api.patch(`/api/posts/${myPost.value.postId}/visibility`, { hiddenByStudent: next });
  myPost.value.hiddenByStudent = next;
  await loadSubs();
}

let draftTimer = null;
const isStudent = () => auth.user?.role === 'Student';

function pushDraftSoon() {
  if (!conn || conn.state !== 'Connected' || !isStudent()) return;
  clearTimeout(draftTimer);
  draftTimer = setTimeout(() => {
    conn.invoke('PushDraft', board.value.id, Number(props.problemId), code.value).catch(() => {});
  }, 900);
}
watch(code, pushDraftSoon);

onMounted(async () => {
  try { await load(); } catch (e) { error.value = e.message; return; }
  // Make sure a wall card exists for this student even before they run/submit.
  if (isStudent()) {
    try { myPost.value = await api.post(`/api/problems/${props.problemId}/post`); } catch { /* ignore */ }
  }

  conn = createBoardConnection();
  conn.on('submissionResult', (dto) => {
    if (dto.problemId === Number(props.problemId)) { loadSubs(); progress.refresh(); }
  });
  conn.on('progressBumped', (p) => progress.$patch({ ...p, ready: true }));
  try {
    await conn.start();
    await conn.invoke('JoinBoard', board.value.id);
    if (isStudent()) conn.invoke('PushDraft', board.value.id, Number(props.problemId), code.value).catch(() => {});
  } catch {}
});
onBeforeUnmount(async () => {
  clearTimeout(draftTimer);
  try { await conn?.stop(); } catch {}
});
</script>

<template>
  <div v-if="problem" class="h-full grid lg:grid-cols-2 gap-0">
    <!-- Left: statement + submissions -->
    <div class="p-5 overflow-y-auto border-r border-slate-200 dark:border-slate-800">
      <RouterLink :to="`/boards/${props.slug}`" class="text-sm text-slate-400 dark:text-slate-500">&larr; back to board</RouterLink>
      <div class="flex items-center gap-2 mt-2 mb-1 flex-wrap">
        <h1 class="text-lg font-bold">{{ problem.title }}</h1>
        <LevelBadge :level="problem.level" />
        <span v-for="t in (problem.tags ? problem.tags.split(',') : [])" :key="t"
              class="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">{{ t }}</span>
      </div>
      <div class="text-xs text-slate-400 dark:text-slate-500 mb-3">
        {{ problem.language.toUpperCase() }} · limit {{ problem.timeLimitMs }} ms · {{ problem.memoryLimitKb }} KB
      </div>
      <p v-if="protectOn" class="text-[11px] text-amber-600 dark:text-amber-400 mb-2">
        🔒 Protected problem — served as an encrypted image watermarked with your identity.
      </p>

      <ContentGuard v-if="protectOn" :active="true" :watermark="''">
        <StatementImage :problem-id="props.problemId" />
      </ContentGuard>

      <template v-else>
        <MarkdownBlock :text="problem.statementMarkdown" />
        <div v-if="problem.sampleTests?.length" class="mt-4">
          <h3 class="font-semibold text-sm mb-1">Samples</h3>
          <div v-for="(t, i) in problem.sampleTests" :key="i" class="grid grid-cols-2 gap-2 mb-2 text-xs">
            <pre class="bg-slate-100 dark:bg-slate-800 rounded p-2 overflow-x-auto">{{ t.stdin }}</pre>
            <pre class="bg-slate-100 dark:bg-slate-800 rounded p-2 overflow-x-auto">{{ t.expectedStdout }}</pre>
          </div>
        </div>
      </template>

      <button v-if="isStudent() && myPost.postId" @click="toggleHiddenFromPeers"
              class="mt-4 w-full text-sm px-3 py-2 rounded-lg border flex items-center justify-center gap-2"
              :class="myPost.hiddenByStudent
                ? 'bg-purple-100 text-purple-700 border-purple-200 dark:bg-purple-500/15 dark:text-purple-300 dark:border-purple-500/30'
                : 'text-slate-500 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-slate-400'">
        {{ myPost.hiddenByStudent
          ? "🔒 Live code & progress hidden from classmates"
          : "👥 Hide live code & progress from classmates" }}
      </button>

      <h3 class="font-semibold text-sm mt-5 mb-2">Submissions</h3>
      <div class="space-y-1">
        <div v-for="s in submissions" :key="s.id"
             class="flex items-center gap-2 text-sm border border-slate-100 dark:border-slate-800 rounded-lg px-2 py-1.5">
          <VerdictBadge :verdict="s.status === 'Done' ? s.verdict : s.status" small />
          <span class="text-slate-500 dark:text-slate-400">{{ s.authorName }}</span>
          <span v-if="s.status === 'Done'" class="text-xs text-slate-400 dark:text-slate-500">
            {{ s.runtimeMs }}ms · {{ s.memoryKb }}KB · {{ Math.round(s.score * 100) }}%
          </span>
        </div>
        <p v-if="!submissions.length" class="text-slate-400 dark:text-slate-500 text-sm">No submissions yet.</p>
      </div>
    </div>

    <!-- Right: editor + console -->
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
