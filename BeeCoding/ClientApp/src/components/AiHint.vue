<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../lib/api';
import MarkdownBlock from './MarkdownBlock.vue';

const props = defineProps({
  problemId: [String, Number],
  bankProblemId: [String, Number],
  language: { type: String, default: 'cpp' },
  code: { type: String, default: '' },
  stdin: { type: String, default: '' },
  verdict: { type: String, default: '' },
  compilerOutput: { type: String, default: '' },
  stderr: { type: String, default: '' },
});

const enabled = ref(false);
const open = ref(false);
const question = ref('');
const reply = ref('');
const busy = ref(false);
const error = ref('');

onMounted(async () => {
  try { enabled.value = (await api.get('/api/ai/enabled'))?.enabled === true; } catch { enabled.value = false; }
});

async function ask() {
  error.value = ''; busy.value = true; reply.value = '';
  try {
    const res = await api.post('/api/ai/hint', {
      problemId: props.problemId ? Number(props.problemId) : null,
      bankProblemId: props.bankProblemId ? Number(props.bankProblemId) : null,
      language: props.language,
      code: props.code,
      stdin: props.stdin,
      verdict: props.verdict,
      compilerOutput: props.compilerOutput,
      stderr: props.stderr,
      question: question.value.trim() || null,
    });
    reply.value = res.reply || '';
  } catch (e) {
    error.value = e.message;
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div v-if="enabled" class="mt-5 border border-violet-200 dark:border-violet-500/30 rounded-xl overflow-hidden">
    <button @click="open = !open"
            class="w-full flex items-center justify-between px-3 py-2 text-sm font-semibold bg-violet-50 dark:bg-violet-500/10 text-violet-700 dark:text-violet-300">
      <span>🤖 AI tutor — hints only</span>
      <span class="text-xs">{{ open ? '▾' : '▸' }}</span>
    </button>

    <div v-if="open" class="p-3 space-y-2">
      <p class="text-[11px] text-slate-400 dark:text-slate-500">
        It looks at your current code and errors, points out bugs, and suggests an approach — it
        will <b>not</b> write the solution for you.
      </p>
      <textarea v-model="question" rows="2" placeholder="Optional: ask something specific (e.g. “why does test 3 fail?”)"
                class="w-full border border-slate-300 dark:border-slate-700 dark:bg-slate-800 rounded-lg px-2 py-1.5 text-xs"></textarea>
      <button @click="ask" :disabled="busy || !code"
              class="bg-violet-600 hover:bg-violet-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium disabled:opacity-50">
        {{ busy ? 'Thinking…' : 'Ask for a hint' }}
      </button>

      <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
      <div v-if="reply" class="text-sm bg-slate-50 dark:bg-slate-800/60 rounded-lg p-3">
        <MarkdownBlock :text="reply" />
      </div>
    </div>
  </div>
</template>
