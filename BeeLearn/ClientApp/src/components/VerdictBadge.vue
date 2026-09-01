<script setup>
const props = defineProps({ verdict: String, small: Boolean });

const map = {
  Accepted: ['bg-emerald-100 text-emerald-700', 'AC'],
  WrongAnswer: ['bg-red-100 text-red-700', 'WA'],
  TimeLimit: ['bg-orange-100 text-orange-700', 'TLE'],
  MemoryLimit: ['bg-purple-100 text-purple-700', 'MLE'],
  RuntimeError: ['bg-rose-100 text-rose-700', 'RE'],
  CompileError: ['bg-slate-200 text-slate-700', 'CE'],
  Queued: ['bg-slate-100 text-slate-500', '…'],
  Running: ['bg-sky-100 text-sky-700', 'run'],
  Hidden: ['bg-slate-100 text-slate-400', '•'],
  None: ['bg-slate-50 text-slate-300', '–'],
};
</script>

<template>
  <span class="inline-flex items-center justify-center rounded font-semibold"
        :class="[map[verdict]?.[0] || map.None[0], small ? 'text-[10px] px-1.5 py-0.5 min-w-[34px]' : 'text-xs px-2 py-1']">
    {{ map[verdict]?.[1] || verdict }}
  </span>
</template>
