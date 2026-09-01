<script setup>
import { ref, onMounted, onBeforeUnmount, computed } from 'vue';
import { api } from '../lib/api';
import { useAuth } from '../stores/auth';
import { createBoardConnection } from '../lib/signalr';
import ProgressGrid from '../components/ProgressGrid.vue';
import ProblemEditor from '../components/ProblemEditor.vue';
import VerdictBadge from '../components/VerdictBadge.vue';

const props = defineProps({ id: [String, Number] });
const auth = useAuth();

const board = ref(null);
const problems = ref([]);
const progress = ref({ students: [], problems: [], cells: [], examMode: false, viewerIsStaff: false });
const presence = ref([]);
const editing = ref(null);       // problem object or {} for new, null = closed
const error = ref('');
let conn = null;
let refreshTimer = null;

const isStaff = computed(() => board.value && board.value.role !== 'Student');

async function loadAll() {
  board.value = await api.get(`/api/boards/${props.id}`);
  problems.value = await api.get(`/api/boards/${props.id}/problems`);
  await loadProgress();
}
async function loadProgress() {
  progress.value = await api.get(`/api/boards/${props.id}/progress`);
}
function scheduleRefresh() {
  clearTimeout(refreshTimer);
  refreshTimer = setTimeout(loadProgress, 250);
}

async function toggleExam() {
  board.value = await api.patch(`/api/boards/${props.id}`, { examMode: !progress.value.examMode });
  await loadProgress();
}
async function toggleHide(student) {
  await api.patch(`/api/boards/${props.id}/members/${student.userId}`, { hiddenByTeacher: !student.hiddenByTeacher });
  await loadProgress();
}

async function onEditorSaved() { editing.value = null; await loadAll(); }

onMounted(async () => {
  try { await loadAll(); } catch (e) { error.value = e.message; return; }

  conn = createBoardConnection();
  conn.on('progressChanged', scheduleRefresh);
  conn.on('memberVisibilityChanged', scheduleRefresh);
  conn.on('examModeChanged', async () => { await loadAll(); });
  conn.on('problemChanged', async () => { problems.value = await api.get(`/api/boards/${props.id}/problems`); scheduleRefresh(); });
  conn.on('presence', (list) => { presence.value = list; });
  try {
    await conn.start();
    await conn.invoke('JoinBoard', Number(props.id));
  } catch (e) { /* realtime is best-effort */ }
});

onBeforeUnmount(async () => {
  clearTimeout(refreshTimer);
  try { await conn?.invoke('LeaveBoard', Number(props.id)); } catch {}
  await conn?.stop();
});
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-6" v-if="board">
    <div class="flex items-center justify-between mb-1">
      <h1 class="text-xl font-bold">{{ board.title }}</h1>
      <div class="text-sm text-slate-500 flex items-center gap-3">
        <span v-if="presence.length">🟢 {{ presence.length }} online</span>
        <span v-if="isStaff">Join code: <span class="font-mono font-semibold text-slate-700">{{ board.joinCode }}</span></span>
      </div>
    </div>
    <p v-if="error" class="text-red-600 text-sm">{{ error }}</p>

    <!-- Staff controls -->
    <div v-if="isStaff" class="flex items-center gap-3 my-4">
      <button @click="toggleExam"
              class="px-3 py-1.5 rounded-lg text-sm font-medium border"
              :class="progress.examMode
                ? 'bg-purple-600 text-white border-purple-600'
                : 'bg-white text-slate-600 border-slate-300'">
        {{ progress.examMode ? '🔒 Exam mode ON — peers hidden' : 'Exam mode off' }}
      </button>
      <button @click="editing = {}" class="px-3 py-1.5 rounded-lg text-sm font-medium bg-amber-500 text-white">
        + Add problem
      </button>
    </div>

    <!-- Student: exam-mode notice -->
    <div v-else-if="progress.examMode" class="my-4 text-sm bg-purple-50 text-purple-700 rounded-lg px-3 py-2">
      🔒 Exam mode is on — you can’t see other students’ progress.
    </div>

    <!-- Problem list -->
    <div class="grid gap-2 my-4">
      <div v-for="p in problems" :key="p.id"
           class="bg-white border border-slate-200 rounded-xl px-4 py-3 flex items-center justify-between">
        <div>
          <div class="font-medium">{{ p.title }}</div>
          <div class="text-xs text-slate-400">{{ p.language.toUpperCase() }} · {{ p.timeLimitMs }}ms · {{ p.memoryLimitKb }}KB</div>
        </div>
        <div class="flex items-center gap-2">
          <button v-if="isStaff" @click="editing = p" class="text-sm text-slate-500 hover:text-slate-900">edit</button>
          <RouterLink :to="`/boards/${board.id}/problems/${p.id}`"
                      class="text-sm bg-slate-800 text-white rounded-lg px-3 py-1.5">
            {{ isStaff ? 'View' : 'Solve' }}
          </RouterLink>
        </div>
      </div>
      <p v-if="!problems.length" class="text-slate-400 text-sm">No problems yet.</p>
    </div>

    <!-- Live board -->
    <h2 class="font-semibold text-slate-600 text-sm mt-8 mb-2">Live progress</h2>
    <ProgressGrid
      :students="progress.students"
      :problems="progress.problems"
      :cells="progress.cells"
      :is-staff="progress.viewerIsStaff"
      :current-user-id="auth.user?.id"
      @toggle-hide="toggleHide" />

    <ProblemEditor v-if="editing !== null"
      :board-id="board.id"
      :problem="editing.id ? editing : null"
      @saved="onEditorSaved" @deleted="onEditorSaved" @cancel="editing = null" />
  </div>
</template>
