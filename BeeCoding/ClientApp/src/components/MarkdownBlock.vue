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
  <div class="max-w-none text-sm leading-relaxed text-slate-700 dark:text-slate-200
              [&_p]:my-2 [&_h1]:font-bold [&_h1]:text-lg [&_h1]:mt-3 [&_h2]:font-bold [&_h2]:mt-3 [&_h3]:font-semibold [&_h3]:mt-3
              [&_ul]:list-disc [&_ul]:pl-5 [&_ul]:my-2 [&_ol]:list-decimal [&_ol]:pl-5 [&_ol]:my-2 [&_li]:my-0.5
              [&_a]:text-amber-600 dark:[&_a]:text-amber-400 [&_a]:underline
              [&_blockquote]:border-l-2 [&_blockquote]:border-slate-300 dark:[&_blockquote]:border-slate-600 [&_blockquote]:pl-3 [&_blockquote]:text-slate-500
              [&_table]:block [&_table]:overflow-x-auto [&_td]:border [&_th]:border [&_td]:border-slate-200 [&_th]:border-slate-200 dark:[&_td]:border-slate-700 dark:[&_th]:border-slate-700 [&_td]:px-2 [&_th]:px-2
              [&_:not(pre)>code]:bg-slate-100 dark:[&_:not(pre)>code]:bg-slate-800 [&_:not(pre)>code]:text-[0.85em] [&_:not(pre)>code]:px-1 [&_:not(pre)>code]:py-0.5 [&_:not(pre)>code]:rounded [&_:not(pre)>code]:font-mono
              [&_pre]:bg-slate-900 [&_pre]:text-slate-100 [&_pre]:p-3 [&_pre]:my-2 [&_pre]:rounded-lg [&_pre]:overflow-x-auto [&_pre]:text-[13px] [&_pre]:leading-snug [&_pre]:whitespace-pre-wrap [&_pre]:break-words
              [&_pre_code]:!bg-transparent [&_pre_code]:!p-0 [&_pre_code]:!rounded-none [&_pre_code]:!text-inherit [&_pre_code]:!text-[13px] [&_pre_code]:font-mono"
       v-html="html"></div>
</template>
