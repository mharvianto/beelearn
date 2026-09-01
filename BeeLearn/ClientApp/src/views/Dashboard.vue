<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../lib/api';
import { useAuth } from '../stores/auth';

const auth = useAuth();
const boards = ref([]);
const newTitle = ref('');
const joinCode = ref('');
const error = ref('');

async function load() {
  boards.value = await api.get('/api/boards');
}
onMounted(load);

async function createBoard() {
  error.value = '';
  try {
    await api.post('/api/boards', { title: newTitle.value });
    newTitle.value = '';
    await load();
  } catch (e) { error.value = e.message; }
}

async function join() {
  error.value = '';
  try {
    await api.post('/api/boards/join', { code: joinCode.value });
    joinCode.value = '';
    await load();
  } catch (e) { error.value = e.message; }
}
</script>

<template>
  <div class="max-w-6xl mx-auto px-4 py-8">
    <h1 class="text-xl font-bold mb-6">Your boards</h1>

    <div class="grid sm:grid-cols-2 gap-4 mb-8">
      <div v-if="auth.isTeacher" class="bg-white border border-slate-200 rounded-xl p-4">
        <h2 class="font-semibold mb-2 text-sm text-slate-600">Create a board</h2>
        <form @submit.prevent="createBoard" class="flex gap-2">
          <input v-model="newTitle" placeholder="Board title" required
                 class="flex-1 border border-slate-300 rounded-lg px-3 py-2" />
          <button class="bg-amber-500 hover:bg-amber-600 text-white rounded-lg px-4 font-medium">Create</button>
        </form>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl p-4">
        <h2 class="font-semibold mb-2 text-sm text-slate-600">Join a board</h2>
        <form @submit.prevent="join" class="flex gap-2">
          <input v-model="joinCode" placeholder="Join code" required
                 class="flex-1 border border-slate-300 rounded-lg px-3 py-2 uppercase" />
          <button class="bg-slate-800 hover:bg-slate-900 text-white rounded-lg px-4 font-medium">Join</button>
        </form>
      </div>
    </div>

    <p v-if="error" class="text-sm text-red-600 mb-4">{{ error }}</p>

    <div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <RouterLink v-for="b in boards" :key="b.id" :to="`/boards/${b.id}`"
                  class="bg-white border border-slate-200 rounded-xl p-4 hover:border-amber-400 transition">
        <div class="flex items-center justify-between">
          <h3 class="font-semibold">{{ b.title }}</h3>
          <span class="text-xs px-2 py-0.5 rounded-full"
                :class="b.role === 'Student' ? 'bg-sky-100 text-sky-700' : 'bg-amber-100 text-amber-700'">
            {{ b.role }}
          </span>
        </div>
        <div class="text-sm text-slate-500 mt-2 flex gap-4">
          <span>{{ b.problemCount }} problems</span>
          <span>{{ b.memberCount }} students</span>
        </div>
        <div v-if="b.role !== 'Student'" class="text-xs text-slate-400 mt-2">
          Code: <span class="font-mono font-semibold text-slate-600">{{ b.joinCode }}</span>
        </div>
      </RouterLink>
    </div>
    <p v-if="!boards.length" class="text-slate-400 text-sm">Nothing here yet.</p>
  </div>
</template>
