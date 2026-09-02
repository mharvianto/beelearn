<script setup>
import { ref, reactive, onMounted, onBeforeUnmount, computed } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../lib/api';
import { useAuth } from '../stores/auth';
import { createBoardConnection } from '../lib/signalr';
import ProgressGrid from '../components/ProgressGrid.vue';
import PadletWall from '../components/PadletWall.vue';
import ProblemEditor from '../components/ProblemEditor.vue';
import BankPicker from '../components/BankPicker.vue';
import LevelBadge from '../components/LevelBadge.vue';
import VerdictBadge from '../components/VerdictBadge.vue';

const props = defineProps({ slug: { type: String, required: true } });
const auth = useAuth();
const router = useRouter();

const view = ref(localStorage.getItem('beelearn.boardView') || 'wall');
function setView(v) { view.value = v; localStorage.setItem('beelearn.boardView', v); }
function openCard({ problemId }) { router.push(`/boards/${props.slug}/problems/${problemId}`); }

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
  board.value = await api.get(`/api/boards/${props.slug}`);
  problems.value = await api.get(`/api/boards/${props.slug}/problems`);
  await loadProgress();
}
async function loadProgress() {
  progress.value = await api.get(`/api/boards/${props.slug}/progress`);
}
const wallSignal = ref(0);
const drafts = reactive({});   // "problemId:userId" -> { code, updatedAt, authorName }
function scheduleRefresh() {
  clearTimeout(refreshTimer);
  refreshTimer = setTimeout(loadProgress, 250);
  wallSignal.value++;
}

async function toggleExam() {
  board.value = await api.patch(`/api/boards/${props.slug}`, { examMode: !progress.value.examMode });
  await loadProgress();
}
async function toggleProtect() {
  board.value = await api.patch(`/api/boards/${props.slug}`, { protectContent: !board.value.protectContent });
}
async function toggleHide(student) {
  await api.patch(`/api/boards/${props.slug}/members/${student.userId}`, { hiddenByTeacher: !student.hiddenByTeacher });
  await loadProgress();
}

const picking = ref(false);
const editError = ref('');
const editBusy = ref(false);

async function saveProblem(form) {
  editError.value = ''; editBusy.value = true;
  try {
    const body = { ...(form.value ?? form) };
    if (editing.value?.id) await api.put(`/api/boards/${props.slug}/problems/${editing.value.id}`, body);
    else await api.post(`/api/boards/${props.slug}/problems`, body);
    editing.value = null;
    await loadAll();
  } catch (e) { editError.value = e.message; }
  finally { editBusy.value = false; }
}

async function deleteProblem() {
  if (!editing.value?.id || !confirm('Delete this problem?')) return;
  try {
    await api.del(`/api/boards/${props.slug}/problems/${editing.value.id}`);
    editing.value = null;
    await loadAll();
  } catch (e) { editError.value = e.message; }
}

async function saveToBank(p) {
  try {
    await api.post(`/api/boards/${props.slug}/problems/${p.id}/to-bank`);
    error.value = '';
    alert(`"${p.title}" disimpan ke bank soal.`);
  } catch (e) { error.value = e.message; }
}

async function onBankAdded() { picking.value = false; await loadAll(); }

// Authoritative live-draft snapshot (server filters by visibility for students).
async function refreshDrafts() {
  if (!conn || conn.state !== 'Connected') return;
  try {
    const list = await conn.invoke('GetDrafts', board.value.id);
    for (const k of Object.keys(drafts)) delete drafts[k];
    for (const d of list || [])
      drafts[`${d.problemId}:${d.userId}`] = { code: d.code, updatedAt: d.updatedAt, authorName: d.authorName };
  } catch { /* ignore */ }
}

onMounted(async () => {
  try { await loadAll(); } catch (e) { error.value = e.message; return; }

  conn = createBoardConnection();
  conn.on('progressChanged', scheduleRefresh);
  conn.on('wallChanged', () => { wallSignal.value++; refreshDrafts(); });
  conn.on('memberVisibilityChanged', () => { scheduleRefresh(); refreshDrafts(); });
  conn.on('examModeChanged', async () => { await loadAll(); refreshDrafts(); });
  conn.on('problemChanged', async () => { problems.value = await api.get(`/api/boards/${props.slug}/problems`); scheduleRefresh(); });
  conn.on('presence', (list) => { presence.value = list; });
  conn.on('draftUpdated', (d) => {
    drafts[`${d.problemId}:${d.userId}`] = { code: d.code, updatedAt: d.updatedAt, authorName: d.authorName };
  });
  try {
    await conn.start();
    await conn.invoke('JoinBoard', board.value.id);
    await refreshDrafts();
  } catch (e) { /* realtime is best-effort */ }
});

onBeforeUnmount(async () => {
  clearTimeout(refreshTimer);
  try { await conn?.invoke('LeaveBoard', board.value?.id); } catch {}
  await conn?.stop();
});
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-6" v-if="board">
    <div class="flex items-center justify-between mb-1">
      <h1 class="text-xl font-bold">{{ board.title }}</h1>
      <div class="text-sm text-slate-500 dark:text-slate-400 flex items-center gap-3">
        <span v-if="presence.length">🟢 {{ presence.length }} online</span>
        <span v-if="isStaff">Join code: <span class="font-mono font-semibold text-slate-700 dark:text-slate-200">{{ board.joinCode }}</span></span>
      </div>
    </div>
    <p v-if="error" class="text-red-600 dark:text-red-400 text-sm">{{ error }}</p>

    <!-- Staff controls -->
    <div v-if="isStaff" class="flex items-center gap-3 my-4">
      <button @click="toggleExam"
              class="px-3 py-1.5 rounded-lg text-sm font-medium border"
              :class="progress.examMode
                ? 'bg-purple-600 text-white border-purple-600'
                : 'bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-300 border-slate-300 dark:border-slate-700'">
        {{ progress.examMode ? '🔒 Exam mode ON — peers hidden' : 'Exam mode off' }}
      </button>
      <button @click="toggleProtect"
              class="px-3 py-1.5 rounded-lg text-sm font-medium border"
              :class="board.protectContent
                ? 'bg-slate-800 text-white border-slate-800 dark:bg-slate-200 dark:text-slate-900 dark:border-slate-200'
                : 'bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-300 border-slate-300 dark:border-slate-700'">
        {{ board.protectContent ? '🔒 Content protected' : 'Protect content' }}
      </button>
      <button @click="editing = {}" class="px-3 py-1.5 rounded-lg text-sm font-medium bg-amber-500 text-white">
        + Add problem
      </button>
      <button @click="picking = true"
              class="px-3 py-1.5 rounded-lg text-sm font-medium border bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-300 border-slate-300 dark:border-slate-700">
        📚 Dari bank
      </button>
    </div>

    <!-- Staff: per-student visibility (feature 5, per student) -->
    <div v-if="isStaff && progress.students.length" class="flex flex-wrap gap-1.5 mb-4">
      <span class="text-xs text-slate-400 dark:text-slate-500 self-center mr-1">Hide from peers:</span>
      <button v-for="s in progress.students" :key="s.userId" @click="toggleHide(s)"
              class="text-xs px-2 py-0.5 rounded-full border"
              :class="s.hiddenByTeacher
                ? 'bg-purple-100 text-purple-700 border-purple-200 dark:bg-purple-500/15 dark:text-purple-300 dark:border-purple-500/30'
                : 'text-slate-500 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:border-slate-400'">
        {{ s.displayName }} {{ s.hiddenByTeacher ? '🔒' : '' }}
      </button>
    </div>

    <!-- Student: exam-mode notice -->
    <div v-else-if="progress.examMode" class="my-4 text-sm bg-purple-50 text-purple-700 dark:bg-purple-500/10 dark:text-purple-300 rounded-lg px-3 py-2">
      🔒 Exam mode is on — you can’t see other students’ progress.
    </div>

    <!-- Problem list -->
    <div class="grid gap-2 my-4">
      <div v-for="p in problems" :key="p.id"
           class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl px-4 py-3 flex items-center justify-between">
        <div>
          <div class="font-medium flex items-center gap-2 flex-wrap">
            {{ p.title }}
            <LevelBadge :level="p.level" />
            <span v-for="t in (p.tags ? p.tags.split(',') : [])" :key="t"
                  class="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">{{ t }}</span>
          </div>
          <div class="text-xs text-slate-400 dark:text-slate-500">{{ p.language.toUpperCase() }} · {{ p.timeLimitMs }}ms · {{ p.memoryLimitKb }}KB</div>
        </div>
        <div class="flex items-center gap-2">
          <button v-if="isStaff" @click="saveToBank(p)"
                  class="text-sm text-slate-400 dark:text-slate-500 hover:text-slate-900 dark:hover:text-slate-100"
                  title="Simpan ke bank soal">📚</button>
          <button v-if="isStaff" @click="editing = p" class="text-sm text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">edit</button>
          <RouterLink :to="`/boards/${board.slug}/problems/${p.id}`"
                      class="text-sm bg-slate-800 dark:bg-slate-700 text-white rounded-lg px-3 py-1.5">
            {{ isStaff ? 'View' : 'Solve' }}
          </RouterLink>
        </div>
      </div>
      <p v-if="!problems.length" class="text-slate-400 dark:text-slate-500 text-sm">No problems yet.</p>
    </div>

    <!-- Live board -->
    <div class="flex items-center justify-between mt-8 mb-2">
      <h2 class="font-semibold text-slate-600 dark:text-slate-300 text-sm">Live progress</h2>
      <div class="flex rounded-lg border border-slate-300 dark:border-slate-700 overflow-hidden text-xs">
        <button @click="setView('wall')" class="px-3 py-1"
                :class="view === 'wall' ? 'bg-amber-500 text-white' : 'bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400'">Wall</button>
        <button @click="setView('grid')" class="px-3 py-1 border-l border-slate-300 dark:border-slate-700"
                :class="view === 'grid' ? 'bg-amber-500 text-white' : 'bg-white dark:bg-slate-900 text-slate-500 dark:text-slate-400'">Grid</button>
      </div>
    </div>

    <PadletWall v-if="view === 'wall'"
      :board-slug="board.slug"
      :current-user-id="auth.user?.id"
      :refresh-signal="wallSignal"
      :drafts="drafts" />

    <ProgressGrid v-else
      :students="progress.students"
      :problems="progress.problems"
      :cells="progress.cells"
      :is-staff="progress.viewerIsStaff"
      :current-user-id="auth.user?.id"
      @toggle-hide="toggleHide" />

    <ProblemEditor v-if="editing !== null"
      :problem="editing.id ? editing : null"
      :error="editError"
      :busy="editBusy"
      @save="saveProblem" @delete="deleteProblem" @cancel="editing = null" />

    <BankPicker v-if="picking"
      :board-slug="board.slug"
      @added="onBankAdded" @cancel="picking = false" />
  </div>
</template>
