<script setup>
import { ref, watch } from 'vue';
import { api } from '../lib/api';
import MonacoEditor from './MonacoEditor.vue';

const props = defineProps({ boardId: [String, Number], problem: Object });
const emit = defineEmits(['saved', 'cancel', 'deleted']);

const blank = () => ({
  title: '', statementMarkdown: '', language: 'cpp', starterCode: '',
  timeLimitMs: 1000, memoryLimitKb: 32768, position: 0, testCases: [],
});
const form = ref(blank());
const error = ref('');

watch(() => props.problem, (p) => {
  form.value = p ? JSON.parse(JSON.stringify(p)) : blank();
}, { immediate: true });

function addTest() {
  form.value.testCases.push({ id: null, stdin: '', expectedStdout: '', isSample: false, points: 1, position: form.value.testCases.length });
}
function removeTest(i) { form.value.testCases.splice(i, 1); }

async function save() {
  error.value = '';
  try {
    const body = { ...form.value };
    if (props.problem?.id) await api.put(`/api/boards/${props.boardId}/problems/${props.problem.id}`, body);
    else await api.post(`/api/boards/${props.boardId}/problems`, body);
    emit('saved');
  } catch (e) { error.value = e.message; }
}
async function del() {
  if (!props.problem?.id || !confirm('Delete this problem?')) return;
  await api.del(`/api/boards/${props.boardId}/problems/${props.problem.id}`);
  emit('deleted');
}
</script>

<template>
  <div class="fixed inset-0 bg-black/50 flex items-start justify-center p-4 overflow-y-auto z-50">
    <div class="bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 border border-transparent dark:border-slate-800 rounded-xl w-full max-w-2xl p-5 my-8">
      <h2 class="font-bold text-lg mb-3">{{ props.problem?.id ? 'Edit' : 'New' }} problem</h2>
      <div class="space-y-3">
        <input v-model="form.title" placeholder="Title" class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2" />
        <textarea v-model="form.statementMarkdown" placeholder="Statement (Markdown)" rows="4"
                  class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-3 py-2 font-mono text-sm"></textarea>
        <div class="flex gap-3 flex-wrap text-sm">
          <label class="flex items-center gap-1">Language
            <select v-model="form.language" class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1">
              <option value="cpp">C++</option><option value="c">C</option>
            </select>
          </label>
          <label class="flex items-center gap-1">Time (ms)
            <input v-model.number="form.timeLimitMs" type="number" class="w-24 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1" />
          </label>
          <label class="flex items-center gap-1">Memory (KB)
            <input v-model.number="form.memoryLimitKb" type="number" class="w-28 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1" />
          </label>
          <label class="flex items-center gap-1">Position
            <input v-model.number="form.position" type="number" class="w-16 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1" />
          </label>
        </div>
        <div>
          <label class="text-xs text-slate-400 dark:text-slate-500">Starter code</label>
          <div class="h-52 border border-slate-300 dark:border-slate-700 rounded-lg overflow-hidden mt-1">
            <MonacoEditor v-model="form.starterCode" :language="form.language === 'c' ? 'c' : 'cpp'" />
          </div>
        </div>

        <div>
          <div class="flex items-center justify-between mb-1">
            <h3 class="font-semibold text-sm">Test cases</h3>
            <button @click="addTest" class="text-xs text-amber-600">+ add test</button>
          </div>
          <div v-for="(t, i) in form.testCases" :key="i" class="border border-slate-200 dark:border-slate-800 rounded-lg p-2 mb-2">
            <div class="grid grid-cols-2 gap-2">
              <textarea v-model="t.stdin" placeholder="stdin" rows="2"
                        class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1 font-mono text-xs"></textarea>
              <textarea v-model="t.expectedStdout" placeholder="expected stdout" rows="2"
                        class="border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-2 py-1 font-mono text-xs"></textarea>
            </div>
            <div class="flex items-center gap-3 text-xs mt-1">
              <label class="flex items-center gap-1"><input type="checkbox" v-model="t.isSample" /> sample (visible to students)</label>
              <label class="flex items-center gap-1">points <input v-model.number="t.points" type="number" class="w-14 border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded px-1" /></label>
              <button @click="removeTest(i)" class="text-red-500 ml-auto">remove</button>
            </div>
          </div>
        </div>

        <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
        <div class="flex gap-2 pt-1">
          <button @click="save" class="bg-amber-500 hover:bg-amber-600 text-white rounded-lg px-4 py-2 font-medium">Save</button>
          <button @click="emit('cancel')" class="text-slate-500 dark:text-slate-400 px-3">Cancel</button>
          <button v-if="props.problem?.id" @click="del" class="text-red-600 dark:text-red-400 ml-auto px-3">Delete</button>
        </div>
      </div>
    </div>
  </div>
</template>
