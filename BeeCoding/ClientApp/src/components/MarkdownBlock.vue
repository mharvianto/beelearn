<script setup>
import { computed } from 'vue';
import { marked } from 'marked';
import DOMPurify from 'dompurify';

const props = defineProps({ text: String });

// Problem statements are authored by teachers (and the admin ingest endpoint), i.e.
// not fully trusted. marked emits raw HTML as-is, so sanitise before v-html.
const html = computed(() =>
  DOMPurify.sanitize(marked.parse(props.text || '', { breaks: true }), {
    ALLOWED_TAGS: [
      'p', 'br', 'hr', 'span', 'div',
      'strong', 'b', 'em', 'i', 'del', 's', 'mark', 'sub', 'sup',
      'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
      'ul', 'ol', 'li', 'blockquote',
      'code', 'pre', 'kbd', 'samp',
      'table', 'thead', 'tbody', 'tr', 'th', 'td',
      'a', 'img',
    ],
    ALLOWED_ATTR: ['href', 'title', 'alt', 'src', 'colspan', 'rowspan', 'align'],
    ALLOW_DATA_ATTR: false,
    // block javascript:, data: (except images), etc.
    ALLOWED_URI_REGEXP: /^(?:https?:|mailto:|#|\/(?!\/))/i,
    ADD_ATTR: ['target', 'rel'],
  }),
);
</script>

<template>
  <div class="prose-sm max-w-none leading-relaxed text-slate-700 dark:text-slate-200 [&_code]:bg-slate-100 dark:[&_code]:bg-slate-800 [&_code]:px-1 [&_code]:rounded [&_h2]:font-bold [&_h2]:mt-3 [&_pre]:bg-slate-900 [&_pre]:text-slate-100 [&_pre]:p-3 [&_pre]:rounded-lg [&_pre]:overflow-x-auto [&_ul]:list-disc [&_ul]:pl-5"
       v-html="html"></div>
</template>
