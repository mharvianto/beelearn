<script setup>
import { computed } from 'vue';
import { marked } from 'marked';

const props = defineProps({ text: String });
const html = computed(() => marked.parse(props.text || '', { breaks: true }));
</script>

<template>
  <div class="prose-sm max-w-none leading-relaxed text-slate-700 dark:text-slate-200 [&_code]:bg-slate-100 dark:[&_code]:bg-slate-800 [&_code]:px-1 [&_code]:rounded [&_h2]:font-bold [&_h2]:mt-3 [&_pre]:bg-slate-900 [&_pre]:text-slate-100 [&_pre]:p-3 [&_pre]:rounded-lg [&_pre]:overflow-x-auto [&_ul]:list-disc [&_ul]:pl-5"
       v-html="html"></div>
</template>
