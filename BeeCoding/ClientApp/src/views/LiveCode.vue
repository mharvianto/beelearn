<script setup>
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue';
import { RouterLink } from 'vue-router';
import { api } from '../lib/api';
import { createBoardConnection } from '../lib/signalr';
import MonacoEditor from '../components/MonacoEditor.vue';
import SplitPane from '../components/SplitPane.vue';
import AiHint from '../components/AiHint.vue';
import { CODE_TEMPLATES } from '../lib/templates';

// A board-wide live-coding scratchpad — lecturing mode without a problem.
// Streamed with problemId 0 so it never collides with a real problem's lecture buffer.
const SCRATCH = 0;

const props = defineProps({ slug: { type: String, required: true } });

const board = ref(null);
const error = ref('');
const isStaff = computed(() => board.value && board.value.role !== 'Student');
const lecturingOn = computed(() => !!board.value?.lecturingMode);

const storeKey = computed(() =>
  !board.value ? '' : `beecoding.livecode.${isStaff.value ? '' : 'student.'}${board.value.id}`);
const code = ref(CODE_TEMPLATES.cpp);
const liveLang = ref('cpp');

// student view of the teacher's buffer
const lecture = ref(null);
const showTeacher = ref(true);

// run panel
const stdin = ref('');
const running = ref(false);
const runOut = ref(null);

let conn = null;
let pushTimer = null;
let saveTimer = null;

function pushSoon() {
  if (!conn || conn.state !== 'Connected' || !isStaff.value || !lecturingOn.value) return;
  clearTimeout(pushTimer);
  pushTimer = setTimeout(pushNow, 600);
}
function pushNow() {
  if (conn?.state === 'Connected' && isStaff.value && lecturingOn.value)
    conn.invoke('PushLecture', board.value.id, SCRATCH, code.value, liveLang.value).catch(() => {});
}

function setLang(l) {
  if (l === liveLang.value) return;
  liveLang.value = l;
}
function loadTeacherCode() {
  if (!lecture.value) return;
  code.value = lecture.value.code || '';
  if (lecture.value.language === 'c' || lecture.value.language === 'cpp') liveLang.value = lecture.value.language;
}

async function toggleLecturing() {
  try { board.value = await api.patch(`/api/boards/${props.slug}`, { lecturingMode: !lecturingOn.value }); }
  catch (e) { error.value = e.message; }
}

async function run() {
  error.value = ''; running.value = true; runOut.value = null;
  try { runOut.value = await api.post('/api/run', { language: liveLang.value, code: code.value, stdin: stdin.value }); }
  catch (e) { error.value = e.message; }
  finally { running.value = false; }
}

function ago(ts) {
  if (!ts) return '';
  const s = Math.max(0, (Date.now() - new Date(ts + (ts.endsWith('Z') ? '' : 'Z')).getTime()) / 1000);
  return s < 60 ? `${s | 0}s` : s < 3600 ? `${(s / 60) | 0}m` : `${(s / 3600) | 0}h`;
}

const compilerOutput = computed(() => (runOut.value && !runOut.value.compileOk ? runOut.value.compilerOutput : ''));

watch(code, () => {
  pushSoon();
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    try { if (storeKey.value) localStorage.setItem(storeKey.value, code.value); } catch { /* ignore */ }
  }, 400);
});
watch(liveLang, pushSoon);
watch(lecturingOn, (on) => { if (on) pushNow(); });

onMounted(async () => {
  try { board.value = await api.get(`/api/boards/${props.slug}`); }
  catch (e) { error.value = e.message; return; }

  try {
    const saved = localStorage.getItem(storeKey.value);
    if (saved && saved.trim()) code.value = saved;
  } catch { /* ignore */ }

  conn = createBoardConnection();
  conn.on('boardSettingsChanged', async () => {
    try { board.value = await api.get(`/api/boards/${props.slug}`); } catch { /* ignore */ }
  });
  conn.on('lectureUpdated', (l) => {
    if (l.problemId === SCRATCH && !isStaff.value) lecture.value = l;
  });
  try {
    await conn.start();
    await conn.invoke('JoinBoard', board.value.id);
    if (isStaff.value) {
      pushNow();
    } else {
      const l = await conn.invoke('GetLecture', board.value.id, SCRATCH);
      if (l) lecture.value = l;
    }
  } catch { /* ignore */ }
});
onBeforeUnmount(async () => {
  clearTimeout(pushTimer);
  clearTimeout(saveTimer);
  try { await conn?.stop(); } catch { /* ignore */ }
});
</script>

<template>
  <div v-if="board" class="h-full flex flex-col">
    <div class="flex items-center gap-3 px-4 py-2 border-b border-slate-200 dark:border-slate-800 flex-wrap">
      <RouterLink :to="`/boards/${props.slug}`" class="text-sm text-slate-400 dark:text-slate-500">&larr; back to board</RouterLink>
      <h1 class="text-sm font-bold">🎥 Live code · {{ board.title }}</h1>
      <span v-if="isStaff"
            class="text-[11px] px-2 py-0.5 rounded-full"
            :class="lecturingOn
              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300'
              : 'bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400'">
        {{ lecturingOn ? 'students are watching' : 'not broadcasting' }}
      </span>
      <div class="ml-auto flex items-center gap-2">
        <button v-if="isStaff" @click="toggleLecturing"
                class="text-xs px-2.5 py-1 rounded-lg font-medium"
                :class="lecturingOn
                  ? 'bg-amber-500 text-white'
                  : 'border border-slate-300 dark:border-slate-700 text-slate-600 dark:text-slate-300'">
          {{ lecturingOn ? 'Stop broadcasting' : 'Start broadcasting' }}
        </button>
        <span class="inline-flex rounded-lg border border-slate-300 dark:border-slate-700 overflow-hidden text-xs">
          <button v-for="l in ['c', 'cpp']" :key="l" @click="setLang(l)" class="px-2.5 py-1"
                  :class="liveLang === l ? 'bg-slate-800 text-white dark:bg-slate-600' : 'text-slate-500 dark:text-slate-400'">
            {{ l === 'c' ? 'C' : 'C++' }}
          </button>
        </span>
      </div>
    </div>

    <p v-if="error" class="text-sm text-red-600 dark:text-red-400 px-4 py-2">{{ error }}</p>

    <!-- Teacher: editable pad + run console -->
    <div v-if="isStaff" class="flex-1 min-h-0">
      <SplitPane direction="vertical" storage-key="beecoding.split.livecode" :initial="70" :min="110">
        <template #a>
          <MonacoEditor v-model="code" :language="liveLang" :lsp="liveLang" />
        </template>
        <template #b>
          <div class="h-full overflow-y-auto bg-white dark:bg-slate-900 p-3 space-y-2">
            <div class="flex gap-2 items-center">
              <button @click="run" :disabled="running"
                      class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
                {{ running ? 'Running…' : 'Run' }}
              </button>
              <span class="text-xs text-slate-400 dark:text-slate-500">No problem attached — Run only, nothing is graded.</span>
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
          </div>
        </template>
      </SplitPane>
    </div>

    <!-- Student: follow along in their own editor -->
    <div v-else class="flex-1 min-h-0 flex flex-col">
      <!-- teacher's live code (peek) -->
      <div class="border-b border-slate-200 dark:border-slate-800">
        <div class="flex items-center gap-2 px-4 py-1.5 text-xs"
             :class="lecturingOn && lecture
               ? 'bg-sky-50 dark:bg-sky-500/10 text-sky-700 dark:text-sky-300'
               : 'bg-slate-50 dark:bg-slate-800/50 text-slate-400 dark:text-slate-500'">
          <template v-if="lecturingOn && lecture">
            <span>👨‍🏫 {{ lecture.teacherName }} · live · {{ lecture.language === 'c' ? 'C' : 'C++' }} · {{ ago(lecture.updatedAt) }} ago</span>
            <button @click="loadTeacherCode" class="ml-auto bg-sky-600 hover:bg-sky-700 text-white rounded-lg px-2.5 py-0.5">Load into my editor</button>
            <button @click="showTeacher = !showTeacher" class="rounded-lg px-2 py-0.5 border border-sky-300 dark:border-sky-500/40">
              {{ showTeacher ? 'Hide' : 'Show' }}
            </button>
          </template>
          <span v-else>{{ lecturingOn ? 'Waiting for the teacher to start typing…' : 'The teacher hasn’t started a live session yet.' }}</span>
        </div>
        <pre v-if="lecturingOn && lecture && showTeacher"
             class="bg-slate-900 text-slate-100 dark:bg-black px-4 py-2 font-mono text-xs overflow-auto max-h-56 whitespace-pre">{{ lecture.code || '(empty)' }}</pre>
      </div>

      <!-- student's own editor + console + AI tutor -->
      <div class="flex-1 min-h-0">
        <SplitPane direction="vertical" storage-key="beecoding.split.livecode-student" :initial="58" :min="110">
          <template #a>
            <MonacoEditor v-model="code" :language="liveLang" :lsp="liveLang" />
          </template>
          <template #b>
            <div class="h-full overflow-y-auto bg-white dark:bg-slate-900 p-3 space-y-2">
              <div class="flex gap-2 items-center">
                <button @click="run" :disabled="running"
                        class="bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
                  {{ running ? 'Running…' : 'Run' }}
                </button>
                <span class="text-xs text-slate-400 dark:text-slate-500">Your own scratch editor — nothing is graded or shared.</span>
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

              <AiHint :board-slug="props.slug" :teacher-code="lecture?.code || ''"
                      :language="liveLang" :code="code" :stdin="stdin"
                      :compiler-output="compilerOutput" :stderr="runOut?.stderr || ''" />
            </div>
          </template>
        </SplitPane>
      </div>
    </div>
  </div>
</template>
