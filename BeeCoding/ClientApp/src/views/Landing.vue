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

const faqs = [
  ['What is BeeCoding?',
    'A classroom platform for teaching C and C++. A teacher posts problems, students solve them in the browser and submit against an automated judge, and the class sees progress on a live board.'],
  ['Do students need to install anything?',
    'No. Everything runs in the browser — the editor, running code, and submitting. Only a modern browser is needed.'],
  ['Can I write C or C++?',
    'Both. You choose the language per problem with a C / C++ toggle; the judge compiles your submission with gcc or g++ accordingly. The starter template switches to match.'],
  ['How is my code graded?',
    'Your program is compiled and run against sample and hidden test cases under CPU, memory and wall-clock limits. You get a verdict (Accepted, Wrong Answer, Time/Memory Limit, Runtime or Compile Error) and a score equal to the fraction of test points passed.'],
  ['Does the AI tutor give me the answer?',
    'No. When enabled by the operator, it points at where a bug likely is and suggests an approach or algorithm — it is instructed never to hand over a working solution. It is optional and off by default.'],
  ['Who can see my submissions?',
    'Teachers of the classes you belong to can see your submissions; other students see only what the visibility settings allow, and hidden tests are never shown to students. See the Privacy Policy for details.'],
  ['How do I get a teacher account?',
    'Choose the Teacher role at registration and enter the invite code from your school or whoever runs this instance. Without a code, registration creates a student account.'],
  ['Is it free? Where is my data?',
    'BeeCoding is open-source software that your school or teacher self-hosts. There are no ads and your data is not sold. The operator of the instance controls where it is stored and is your contact for any data request.'],
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

      <section class="max-w-5xl mx-auto px-4 pb-16 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div v-for="[icon, title, body] in features" :key="title"
             class="border border-slate-200 dark:border-slate-800 rounded-xl p-4 bg-white dark:bg-slate-900">
          <div class="text-2xl">{{ icon }}</div>
          <h3 class="font-semibold mt-2">{{ title }}</h3>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">{{ body }}</p>
        </div>
      </section>

      <section class="max-w-3xl mx-auto px-4 pb-20">
        <h2 class="text-xl font-bold text-center mb-6">Frequently asked questions</h2>
        <div class="divide-y divide-slate-200 dark:divide-slate-800 border-y border-slate-200 dark:border-slate-800">
          <details v-for="[q, a] in faqs" :key="q" class="group">
            <summary class="flex items-center justify-between gap-4 cursor-pointer list-none py-4 font-medium
                            [&::-webkit-details-marker]:hidden hover:text-amber-600 dark:hover:text-amber-400">
              <span>{{ q }}</span>
              <span class="shrink-0 text-slate-400 transition-transform group-open:rotate-45">+</span>
            </summary>
            <p class="pb-4 -mt-1 text-sm text-slate-500 dark:text-slate-400 leading-relaxed">{{ a }}</p>
          </details>
        </div>
      </section>
    </main>
  </div>
</template>
