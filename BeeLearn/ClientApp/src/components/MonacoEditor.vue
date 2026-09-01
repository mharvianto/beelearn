<script setup>
import { ref, onMounted, onBeforeUnmount, watch } from 'vue';
import * as monaco from 'monaco-editor';
import { theme as appTheme } from '../lib/theme';

function editorTheme() {
  const dark = appTheme.value === 'dark'
    || (appTheme.value === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  return dark ? 'vs-dark' : 'vs';
}

const props = defineProps({
  modelValue: { type: String, default: '' },
  language: { type: String, default: 'cpp' },
});
const emit = defineEmits(['update:modelValue']);

const el = ref(null);
let editor = null;

onMounted(() => {
  editor = monaco.editor.create(el.value, {
    value: props.modelValue,
    language: props.language,
    theme: editorTheme(),
    fontSize: 13,
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    automaticLayout: true,
    tabSize: 4,
  });
  editor.onDidChangeModelContent(() => emit('update:modelValue', editor.getValue()));
});

watch(() => props.modelValue, (v) => {
  if (editor && v !== editor.getValue()) editor.setValue(v || '');
});
watch(() => props.language, (l) => {
  if (editor) monaco.editor.setModelLanguage(editor.getModel(), l);
});
watch(appTheme, () => monaco.editor.setTheme(editorTheme()));

onBeforeUnmount(() => editor?.dispose());
</script>

<template>
  <div ref="el" class="h-full w-full"></div>
</template>
