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
import AiHint from '../components/AiHint.vue';
import SplitPane from '../components/SplitPane.vue';
import { CODE_TEMPLATES, isPristine } from '../lib/templates';
import { loadDraft, saveDraft, clearDraft } from '../lib/draft';

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
const solveLang = ref('cpp');   // 'c' | 'cpp' — student's choice of compiler

// local autosave so an accidental refresh doesn't wipe the editor
const draftScope = computed(() => `board:${props.problemId}`);
const restored = ref(false);
const restoredAt = ref('');
let saveTimer = null;
function saveDraftNow() {
  if (problem.value) saveDraft(auth.user?.id, draftScope.value, code.value, solveLang.value);
}
function useTemplate() {
  code.value = templateFor(solveLang.value);
  clearDraft(auth.user?.id, draftScope.value);
  restored.value = false;
}

// The teacher's starter if it's in this language, otherwise the generic template.
function templateFor(l) {
  const authored = problem.value?.language === 'c' ? 'c' : 'cpp';
  return l === authored && problem.value?.starterCode ? problem.value.starterCode : CODE_TEMPLATES[l];
}
function setLang(l) {
  if (l === solveLang.value) return;
  solveLang.value = l;
  // swap the boilerplate only if the student hasn't written their own code
  if (isPristine(code.value, problem.value?.starterCode)) code.value = templateFor(l);
  try { localStorage.setItem('beecoding.lang', l); } catch { /* ignore */ }
}
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
  let pref = null;
  try { pref = localStorage.getItem('beecoding.lang'); } catch { /* ignore */ }
  solveLang.value = pref === 'c' || pref === 'cpp' ? pref : (problem.value.language === 'c' ? 'c' : 'cpp');
  code.value = templateFor(solveLang.value) || '';

  // bring back an unsaved draft from a previous visit / refresh
  const d = loadDraft(auth.user?.id, draftScope.value);
  if (d && d.code.trim() && !isPristine(d.code, problem.value.starterCode)) {
    code.value = d.code;
    if (d.lang === 'c' || d.lang === 'cpp') solveLang.value = d.lang;
    restored.value = true;
    restoredAt.value = new Date(d.ts).toLocaleString();
  }

  if (problem.value.sampleTests?.[0]) stdin.value = problem.value.sampleTests[0].stdin;
  await loadSubs();
}
async function loadSubs() {
  submissions.value = await api.get(`/api/problems/${props.problemId}/submissions`);
}

async function run() {
  error.value = ''; running.value = true; runOut.value = null;
  try {
    runOut.value = await api.post('/api/run', { language: solveLang.value, code: code.value, stdin: stdin.value });
  } catch (e) { error.value = e.message; }
  finally { running.value = false; }
}

async function submit() {
  error.value = ''; submitting.value = true;
  try {
    await api.post(`/api/problems/${props.problemId}/submit`, { code: code.value, language: solveLang.value });
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
watch(code, () => {
  pushDraftSoon();
  clearTimeout(saveTimer);
  saveTimer = setTimeout(saveDraftNow, 500);
});
watch(solveLang, saveDraftNow);

onMounted(async () => {
  try { await load(); } catch (e) { error.value = e.message; return; }
  window.addEventListener('beforeunload', saveDraftNow);
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
  clearTimeout(saveTimer);
  saveDraftNow();
  window.removeEventListener('beforeunload', saveDraftNow);
  try { await conn?.stop(); } catch {}
});
</script>

<template>
  <div v-if="problem" class="h-full">
   <SplitPane direction="horizontal" storage-key="beecoding.split.solve-main" :initial="42" :min="260">
    <template #a>
    <!-- Left: statement + submissions -->
    <div class="h-full overflow-y-auto p-5 border-r border-slate-200 dark:border-slate-800">
      <RouterLink :to="`/boards/${props.slug}`" class="text-sm text-slate-400 dark:text-slate-500">&larr; back to board</RouterLink>
      <div class="flex items-center gap-2 mt-2 mb-1 flex-wrap">
        <h1 class="text-lg font-bold">{{ problem.title }}</h1>
        <LevelBadge :level="problem.level" />
        <span v-for="t in (problem.tags ? problem.tags.split(',') : [])" :key="t"
              class="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">{{ t }}</span>
      </div>
      <div class="text-xs text-slate-400 dark:text-slate-500 mb-3">
        {{ solveLang === 'c' ? 'C' : 'C++' }} · limit {{ problem.timeLimitMs }} ms · {{ problem.memoryLimitKb }} KB
      </div>

      <div v-if="restored" class="mb-3 text-xs bg-amber-100 dark:bg-amber-500/15 text-amber-800 dark:text-amber-200 rounded-lg px-3 py-2 flex items-center gap-2 flex-wrap">
        <span>↩︎ Restored your unsaved code from {{ restoredAt }}.</span>
        <button @click="useTemplate" class="underline hover:no-underline">Use the template instead</button>
        <button @click="restored = false" class="ml-auto text-amber-600 dark:text-amber-300" title="Dismiss">✕</button>
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

      <AiHint :problem-id="props.problemId" :language="solveLang" :code="code" :stdin="stdin"
              :verdict="latestMine?.status === 'Done' ? latestMine?.verdict : ''"
              :compiler-output="runOut && !runOut.compileOk ? runOut.compilerOutput : (latestMine?.compilerOutput || '')"
              :stderr="runOut?.stderr || ''" />

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
    </template>

    <template #b>
    <!-- Right: editor + console -->
    <SplitPane direction="vertical" storage-key="beecoding.split.solve-console" :initial="66" :min="110">
      <template #a>
        <MonacoEditor v-model="code" :language="solveLang" :lsp="solveLang" />
      </template>
      <template #b>
      <div class="h-full overflow-y-auto border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-3 space-y-2">
        <div class="flex gap-2 items-center">
          <button @click="run" :disabled="running"
                  class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
            {{ running ? 'Running…' : 'Run' }}
          </button>
          <button @click="submit" :disabled="submitting"
                  class="bg-amber-500 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
            {{ submitting ? 'Submitting…' : 'Submit' }}
          </button>
          <span class="ml-auto inline-flex rounded-lg border border-slate-300 dark:border-slate-700 overflow-hidden text-xs">
            <button v-for="l in ['c', 'cpp']" :key="l" @click="setLang(l)"
                    class="px-2.5 py-1"
                    :class="solveLang === l
                      ? 'bg-slate-800 text-white dark:bg-slate-600'
                      : 'text-slate-500 dark:text-slate-400'">
              {{ l === 'c' ? 'C' : 'C++' }}
            </button>
          </span>
        </div>
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400 dark:text-slate-500">stdin</label>
            <textarea v-model="stdin"
                      class="w-full h-20 resize-y border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1 font-mono text-xs"></textarea>
          </div>
          <div>
            <label class="text-xs text-slate-400 dark:text-slate-500">output</label>
            <pre class="w-full h-20 bg-slate-900 text-slate-100 dark:bg-black dark:border dark:border-slate-800 rounded-lg px-2 py-1 font-mono text-xs overflow-auto whitespace-pre-wrap">{{
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
      </template>
    </SplitPane>
    </template>
   </SplitPane>
  </div>
</template>
