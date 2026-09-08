<script setup>
import { ref, onMounted, onBeforeUnmount, watch } from 'vue';
import * as monaco from 'monaco-editor';
import { theme as appTheme } from '../lib/theme';
import { CppLsp } from '../lib/cpplsp';

function editorTheme() {
  const dark = appTheme.value === 'dark'
    || (appTheme.value === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  return dark ? 'vs-dark' : 'vs';
}

const props = defineProps({
  modelValue: { type: String, default: '' },
  language: { type: String, default: 'cpp' },
  // when set to 'c' | 'cpp', connect the clangd LSP bridge for this editor
  lsp: { type: [String, Boolean], default: false },
  readOnly: { type: Boolean, default: false },
});
const emit = defineEmits(['update:modelValue']);

const el = ref(null);
let editor = null;
let selfEmit = false;   // true while we're emitting our own change — don't echo it back

// Touch devices: Monaco's virtual-keyboard/IME handling is fragile. The browser
// EditContext API fixes most of the "typed char lands wrong / deletes the wrong
// one" desync; auto-closing pairs and Enter-accepts-suggestion make it worse on a
// phone, so turn those off there.
const coarse = typeof window !== 'undefined' && window.matchMedia?.('(pointer: coarse)').matches;
const hasEditContext = typeof window !== 'undefined' && 'EditContext' in window;

// ---- clangd LSP wiring --------------------------------------------------------
let lspClient = null;
let lspDisposables = [];
let changeTimer = null;

const COMPLETION_KIND = {           // LSP CompletionItemKind -> monaco
  1: 18, 2: 1, 3: 1, 4: 0, 5: 3, 6: 4, 7: 6, 8: 7, 9: 8, 10: 9, 11: 12, 12: 13,
  13: 15, 14: 17, 15: 27, 16: 19, 17: 20, 18: 18, 19: 23, 20: 16, 21: 14, 22: 5,
  23: 22, 24: 10, 25: 24,
};
const MARKER_SEV = { 1: 8, 2: 4, 3: 2, 4: 1 };   // LSP DiagnosticSeverity -> monaco.MarkerSeverity

const toLspPos = (p) => ({ line: p.lineNumber - 1, character: p.column - 1 });
const asText = (c) => (typeof c === 'string' ? c : Array.isArray(c) ? c.map(asText).join('\n\n') : (c?.value ?? ''));

function toMonacoCompletion(it, fallbackRange) {
  const edit = it.textEdit;
  let range = fallbackRange;
  const e = edit?.range || edit?.replace;
  if (e) {
    range = new monaco.Range(e.start.line + 1, e.start.character + 1, e.end.line + 1, e.end.character + 1);
  }
  return {
    label: (it.label || '').replace(/^\s+/, ''),   // clangd left-pads labels
    kind: COMPLETION_KIND[it.kind] ?? 0,
    detail: it.detail || (it.labelDetails?.detail ?? ''),
    documentation: it.documentation ? { value: asText(it.documentation) } : undefined,
    insertText: edit?.newText ?? it.insertText ?? it.label,
    insertTextRules: it.insertTextFormat === 2
      ? monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet : 0,
    range,
    sortText: it.sortText,
    filterText: it.filterText,
    commitCharacters: it.commitCharacters,
  };
}

async function initLsp() {
  const lang = props.lsp === true ? props.language : props.lsp;
  if (lang !== 'c' && lang !== 'cpp') return;

  try {
    lspClient = new CppLsp(lang);
    await lspClient.connect();
    await lspClient.open(editor.getValue());
  } catch {
    lspClient = null;               // bridge disabled / clangd missing -> word-based only
    return;
  }

  lspDisposables.push(editor.onDidChangeModelContent(() => {
    clearTimeout(changeTimer);
    changeTimer = setTimeout(() => lspClient?.didChange(editor.getValue()), 250);
  }));

  const mine = () => lspClient && editor && editor.getModel();

  lspDisposables.push(monaco.languages.registerCompletionItemProvider(props.language, {
    triggerCharacters: ['.', '>', ':', '<', '"', '/', ' '],
    async provideCompletionItems(model, position, ctx) {
      if (!mine() || model !== editor.getModel()) return { suggestions: [] };
      const r = await lspClient.completion(toLspPos(position), ctx?.triggerCharacter);
      const items = r?.items ?? (Array.isArray(r) ? r : []);
      const w = model.getWordUntilPosition(position);
      const range = new monaco.Range(position.lineNumber, w.startColumn, position.lineNumber, w.endColumn);
      return { suggestions: items.map((it) => toMonacoCompletion(it, range)), incomplete: !!r?.isIncomplete };
    },
  }));

  lspDisposables.push(monaco.languages.registerHoverProvider(props.language, {
    async provideHover(model, position) {
      if (!mine() || model !== editor.getModel()) return null;
      const h = await lspClient.hover(toLspPos(position));
      if (!h?.contents) return null;
      return { contents: [{ value: asText(h.contents) }] };
    },
  }));

  lspDisposables.push(monaco.languages.registerSignatureHelpProvider(props.language, {
    signatureHelpTriggerCharacters: ['(', ','],
    signatureHelpRetriggerCharacters: [','],
    async provideSignatureHelp(model, position) {
      if (!mine() || model !== editor.getModel()) return null;
      const s = await lspClient.signatureHelp(toLspPos(position));
      if (!s?.signatures?.length) return null;
      return {
        value: {
          signatures: s.signatures.map((sig) => ({
            label: sig.label,
            documentation: sig.documentation ? { value: asText(sig.documentation) } : undefined,
            parameters: (sig.parameters || []).map((p) => ({ label: p.label })),
          })),
          activeSignature: s.activeSignature ?? 0,
          activeParameter: s.activeParameter ?? 0,
        },
        dispose() {},
      };
    },
  }));

  lspClient.onDiagnostics((diags) => {
    const model = editor?.getModel();
    if (!model) return;
    monaco.editor.setModelMarkers(model, 'clangd', (diags || []).map((d) => ({
      severity: MARKER_SEV[d.severity] ?? 4,
      message: d.message,
      source: d.source || 'clangd',
      startLineNumber: d.range.start.line + 1,
      startColumn: d.range.start.character + 1,
      endLineNumber: d.range.end.line + 1,
      endColumn: d.range.end.character + 1,
    })));
  });
}

function disposeLsp() {
  clearTimeout(changeTimer);
  lspDisposables.forEach((d) => { try { d.dispose(); } catch { /* ignore */ } });
  lspDisposables = [];
  try { if (editor?.getModel()) monaco.editor.setModelMarkers(editor.getModel(), 'clangd', []); } catch { /* ignore */ }
  lspClient?.close();
  lspClient = null;
}

// ---- editor lifecycle -------------------------------------------------------
onMounted(() => {
  editor = monaco.editor.create(el.value, {
    value: props.modelValue,
    language: props.language,
    theme: editorTheme(),
    fontSize: coarse ? 14 : 13,
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    automaticLayout: true,
    tabSize: 4,
    readOnly: props.readOnly,
    experimentalEditContext: hasEditContext,
    ...(coarse ? {
      wordWrap: 'on',
      autoClosingBrackets: 'never',
      autoClosingQuotes: 'never',
      autoClosingOvertype: 'never',
      autoSurround: 'never',
      acceptSuggestionOnEnter: 'off',
      tabCompletion: 'off',
      contextmenu: false,
    } : {}),
  });
  editor.onDidChangeModelContent(() => {
    selfEmit = true;
    emit('update:modelValue', editor.getValue());
    selfEmit = false;
  });
  if (!props.readOnly) initLsp();
});

watch(() => props.readOnly, (ro) => editor?.updateOptions({ readOnly: ro }));

watch(() => props.modelValue, (v) => {
  // Only for genuinely external changes (template switch, "copy into my editor").
  // Never yank text out from under someone who is typing — that's what scrambled
  // input on mobile: setValue resets the cursor mid-keystroke.
  if (!editor || selfEmit) return;
  if (v === editor.getValue() || editor.hasTextFocus()) return;
  const model = editor.getModel();
  editor.executeEdits('external', [{ range: model.getFullModelRange(), text: v || '' }]);
  editor.pushUndoStop();
});
watch(() => props.language, (l) => {
  if (editor) monaco.editor.setModelLanguage(editor.getModel(), l);
  disposeLsp();
  initLsp();
});
watch(appTheme, () => monaco.editor.setTheme(editorTheme()));

onBeforeUnmount(() => { disposeLsp(); editor?.dispose(); });
</script>

<template>
  <div ref="el" class="h-full w-full"></div>
</template>
