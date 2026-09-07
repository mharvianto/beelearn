<script setup>
import ThemeToggle from '../components/ThemeToggle.vue';
defineProps({ title: String, updated: String });
</script>

<template>
  <div class="min-h-full flex flex-col bg-slate-50 dark:bg-slate-950 text-slate-800 dark:text-slate-100">
    <header class="max-w-3xl w-full mx-auto px-4 h-14 flex items-center justify-between">
      <RouterLink to="/" class="font-bold text-lg text-amber-600 dark:text-amber-400">🐝 BeeCoding</RouterLink>
      <ThemeToggle />
    </header>

    <main class="flex-1 max-w-3xl w-full mx-auto px-4 py-8">
      <h1 class="text-2xl font-bold">{{ title }}</h1>
      <p v-if="updated" class="text-xs text-slate-400 dark:text-slate-500 mt-1">Last updated: {{ updated }}</p>

      <article class="mt-6 space-y-5 text-sm leading-relaxed text-slate-600 dark:text-slate-300
                      [&_h2]:text-slate-900 [&_h2]:dark:text-slate-100 [&_h2]:font-semibold [&_h2]:text-base [&_h2]:mt-6
                      [&_ul]:list-disc [&_ul]:pl-5 [&_ul]:space-y-1 [&_a]:text-amber-600 [&_a]:dark:text-amber-400">
        <slot />
      </article>

      <p class="mt-10 text-sm">
        <RouterLink to="/" class="text-amber-600 dark:text-amber-400">&larr; Back to home</RouterLink>
      </p>
    </main>
  </div>
</template>
