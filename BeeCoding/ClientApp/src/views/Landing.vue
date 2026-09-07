<script setup>
import ThemeToggle from '../components/ThemeToggle.vue';

const features = [
  ['🧑‍🏫', 'Teacher posts problems', 'Build problems with samples and hidden tests, or pull from a shared problem bank.'],
  ['📡', 'Live classroom board', 'A Padlet-style wall and a grid update in real time as students work — with controls over who sees whose answers.'],
  ['⚙️', 'Sandboxed C / C++ judge', 'Run and submit code against real tests with CPU, memory and wall-clock limits. Students pick C or C++.'],
  ['🎯', 'Practice + XP', 'A public bank of problems to solve for XP and a leaderboard, independent of any class.'],
  ['💡', 'C/C++ IntelliSense', 'Optional clangd-powered autocomplete, hover docs and inline diagnostics in the editor.'],
  ['🤖', 'AI tutor (hints only)', 'Optional. Points at the bug and suggests an approach — it will not write the solution for you.'],
];
</script>

<template>
  <div class="min-h-full flex flex-col bg-slate-50 dark:bg-slate-950 text-slate-800 dark:text-slate-100">
    <header class="max-w-6xl w-full mx-auto px-4 h-14 flex items-center justify-between">
      <span class="font-bold text-lg text-amber-600 dark:text-amber-400">🐝 BeeCoding</span>
      <div class="flex items-center gap-3 text-sm">
        <ThemeToggle />
        <RouterLink to="/login" class="text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100">Sign in</RouterLink>
        <RouterLink to="/register"
                    class="bg-amber-500 hover:bg-amber-600 text-white rounded-lg px-3 py-1.5 font-medium">
          Get started
        </RouterLink>
      </div>
    </header>

    <main class="flex-1">
      <section class="max-w-3xl mx-auto px-4 pt-16 pb-14 text-center">
        <h1 class="text-3xl sm:text-4xl font-bold tracking-tight">
          A live board and a C/C++ judge for the classroom
        </h1>
        <p class="mt-4 text-slate-500 dark:text-slate-400 text-lg">
          Teachers post problems, students solve them in the browser and submit against a
          sandboxed judge, and the whole class sees progress in real time.
        </p>
        <div class="mt-8 flex items-center justify-center gap-3">
          <RouterLink to="/register"
                      class="bg-amber-500 hover:bg-amber-600 text-white rounded-lg px-5 py-2.5 font-medium">
            Create an account
          </RouterLink>
          <RouterLink to="/login"
                      class="border border-slate-300 dark:border-slate-700 rounded-lg px-5 py-2.5 font-medium hover:border-amber-400">
            Sign in
          </RouterLink>
        </div>
      </section>

      <section class="max-w-5xl mx-auto px-4 pb-20 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div v-for="[icon, title, body] in features" :key="title"
             class="border border-slate-200 dark:border-slate-800 rounded-xl p-4 bg-white dark:bg-slate-900">
          <div class="text-2xl">{{ icon }}</div>
          <h3 class="font-semibold mt-2">{{ title }}</h3>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ body }}</p>
        </div>
      </section>
    </main>
  </div>
</template>
