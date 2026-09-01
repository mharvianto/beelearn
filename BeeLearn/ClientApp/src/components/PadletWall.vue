<script setup>
import { ref, watch, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../lib/api';
import VerdictBadge from './VerdictBadge.vue';

const props = defineProps({
  boardId: { type: [String, Number], required: true },
  currentUserId: Number,
  refreshSignal: { type: Number, default: 0 },
});
const router = useRouter();

const wall = ref({ problems: [], posts: [], examMode: false, viewerIsStaff: false });
const activeProblem = ref(null);
const noteDraft = ref({});          // postId -> string while editing
const commentDraft = ref({});       // postId -> string
const openComments = ref({});       // postId -> bool
const error = ref('');

const EMOJIS = ['👍', '⭐', '🎉', '🔥', '👀'];

async function load() {
  try {
    wall.value = await api.get(`/api/boards/${props.boardId}/wall`);
    if (!activeProblem.value && wall.value.problems.length)
      activeProblem.value = wall.value.problems[0].id;
  } catch (e) { error.value = e.message; }
}
onMounted(load);
watch(() => props.refreshSignal, load);

const visiblePosts = computed(() =>
  wall.value.posts.filter((p) => p.problemId === activeProblem.value));

const pastels = ['bg-rose-50', 'bg-amber-50', 'bg-lime-50', 'bg-sky-50', 'bg-violet-50', 'bg-teal-50', 'bg-orange-50'];
const avatarColors = ['bg-rose-400', 'bg-amber-400', 'bg-lime-500', 'bg-sky-400', 'bg-violet-400', 'bg-teal-400', 'bg-orange-400'];
function hash(n) { return ((n * 2654435761) >>> 0) % pastels.length; }
function initials(name) {
  return (name || '?').split(/\s+/).slice(0, 2).map((w) => w[0]?.toUpperCase() || '').join('');
}
function ago(iso) {
  if (!iso) return '';
  const t = Date.parse(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : iso + 'Z');
  if (!t) return '';
  const s = Math.max(1, Math.floor((Date.now() - t) / 1000));
  if (s < 60) return 'just now';
  if (s < 3600) return Math.floor(s / 60) + 'm';
  if (s < 86400) return Math.floor(s / 3600) + 'h';
  return Math.floor(s / 86400) + 'd';
}

async function toggleReaction(post, emoji) {
  try {
    post.reactions = await api.post(`/api/posts/${post.postId}/reactions`, { emoji });
  } catch (e) { error.value = e.message; }
}
function reactionCount(post, emoji) {
  return post.reactions.find((r) => r.emoji === emoji);
}

function startNote(post) { noteDraft.value[post.postId] = post.note || ''; }
async function saveNote(post) {
  try {
    await api.put(`/api/posts/${post.postId}/note`, { note: noteDraft.value[post.postId] });
    post.note = noteDraft.value[post.postId].trim();
    delete noteDraft.value[post.postId];
  } catch (e) { error.value = e.message; }
}

async function addComment(post) {
  const body = (commentDraft.value[post.postId] || '').trim();
  if (!body) return;
  try {
    const c = await api.post(`/api/posts/${post.postId}/comments`, { body });
    post.comments.push(c);
    commentDraft.value[post.postId] = '';
  } catch (e) { error.value = e.message; }
}
async function delComment(post, c) {
  try {
    await api.del(`/api/posts/${post.postId}/comments/${c.id}`);
    post.comments = post.comments.filter((x) => x.id !== c.id);
  } catch (e) { error.value = e.message; }
}

function openPost(post) {
  router.push(`/boards/${props.boardId}/problems/${post.problemId}`);
}
</script>

<template>
  <div>
    <p v-if="error" class="text-sm text-red-600 mb-2">{{ error }}</p>

    <!-- problem tabs -->
    <div class="flex gap-1 flex-wrap mb-4">
      <button v-for="p in wall.problems" :key="p.id" @click="activeProblem = p.id"
              class="px-3 py-1.5 rounded-full text-sm border transition"
              :class="activeProblem === p.id
                ? 'bg-amber-500 text-white border-amber-500'
                : 'bg-white text-slate-600 border-slate-200 hover:border-amber-300'">
        {{ p.title }}
      </button>
    </div>

    <div v-if="wall.examMode && !wall.viewerIsStaff"
         class="text-sm bg-purple-50 text-purple-700 rounded-lg px-3 py-2 mb-3">
      🔒 Exam mode — only your own posts are shown.
    </div>

    <!-- masonry wall -->
    <div class="[column-fill:_balance] columns-1 sm:columns-2 xl:columns-3 gap-4">
      <article v-for="post in visiblePosts" :key="post.postId"
               class="mb-4 break-inside-avoid rounded-2xl border border-slate-200 shadow-sm relative"
               :class="post.redacted ? 'bg-white border-dashed' : pastels[hash(post.userId)]">
        <!-- header -->
        <div class="flex items-center gap-2 px-4 pt-3">
          <span class="w-7 h-7 rounded-full text-white text-xs font-bold grid place-items-center shrink-0"
                :class="avatarColors[hash(post.userId)]">{{ initials(post.authorName) }}</span>
          <div class="min-w-0">
            <div class="text-sm font-semibold truncate">
              {{ post.redacted ? 'Hidden' : post.authorName }}
              <span v-if="post.mine" class="text-xs text-slate-400 font-normal">· you</span>
            </div>
            <div class="text-[11px] text-slate-400">{{ ago(post.updatedAt) }} ago</div>
          </div>
          <VerdictBadge v-if="!post.redacted" :verdict="post.verdict" small class="ml-auto" />
        </div>

        <div v-if="post.redacted" class="px-4 py-4 text-sm text-slate-400">
          🔒 This student’s answer is hidden from peers.
        </div>

        <template v-else>
          <!-- note -->
          <div class="px-4 pt-2">
            <div v-if="noteDraft[post.postId] !== undefined" class="flex gap-1">
              <input v-model="noteDraft[post.postId]" @keyup.enter="saveNote(post)"
                     placeholder="Add a note…" maxlength="500"
                     class="flex-1 text-sm border border-slate-300 rounded-lg px-2 py-1 bg-white" />
              <button @click="saveNote(post)" class="text-xs text-amber-600 px-1">save</button>
            </div>
            <p v-else-if="post.note" @click="post.mine && startNote(post)"
               class="text-sm text-slate-700" :class="{ 'cursor-text': post.mine }">{{ post.note }}</p>
            <button v-else-if="post.mine" @click="startNote(post)" class="text-xs text-slate-400 hover:text-slate-600">
              + add a note
            </button>
          </div>

          <!-- code preview -->
          <pre v-if="post.codePreview"
               class="mx-4 mt-2 bg-slate-900 text-slate-100 text-[11px] leading-snug rounded-lg p-2 overflow-x-auto max-h-44">{{ post.codePreview }}</pre>
          <div class="px-4 mt-1 text-[11px] text-slate-400">
            <span v-if="post.attempts">{{ post.attempts }} attempt{{ post.attempts === 1 ? '' : 's' }}</span>
            <span v-if="post.runtimeMs"> · {{ post.runtimeMs }}ms · {{ post.memoryKb }}KB</span>
            <span v-if="post.score"> · {{ Math.round(post.score * 100) }}%</span>
          </div>

          <!-- reactions -->
          <div class="flex flex-wrap gap-1 px-4 mt-2">
            <button v-for="e in EMOJIS" :key="e" @click="toggleReaction(post, e)"
                    class="text-xs rounded-full px-2 py-0.5 border transition"
                    :class="reactionCount(post, e)?.mine
                      ? 'bg-amber-100 border-amber-300'
                      : 'bg-white/70 border-slate-200 hover:border-slate-300'">
              {{ e }}<span v-if="reactionCount(post, e)" class="ml-1 text-slate-500">{{ reactionCount(post, e).count }}</span>
            </button>
          </div>

          <!-- comments -->
          <div class="px-4 mt-2 pb-3">
            <button @click="openComments[post.postId] = !openComments[post.postId]"
                    class="text-xs text-slate-500 hover:text-slate-800">
              💬 {{ post.comments.length }} comment{{ post.comments.length === 1 ? '' : 's' }}
            </button>
            <div v-if="openComments[post.postId]" class="mt-2 space-y-1.5">
              <div v-for="c in post.comments" :key="c.id" class="text-xs bg-white/70 rounded-lg px-2 py-1">
                <span class="font-semibold">{{ c.authorName }}</span>
                <span class="text-slate-400"> · {{ ago(c.createdAt) }} ago</span>
                <button v-if="c.canDelete" @click="delComment(post, c)"
                        class="text-slate-300 hover:text-red-500 float-right">×</button>
                <div class="text-slate-700 whitespace-pre-wrap">{{ c.body }}</div>
              </div>
              <div class="flex gap-1">
                <input v-model="commentDraft[post.postId]" @keyup.enter="addComment(post)"
                       placeholder="Write a comment…"
                       class="flex-1 text-xs border border-slate-300 rounded-lg px-2 py-1 bg-white" />
                <button @click="addComment(post)" class="text-xs text-amber-600 px-1">send</button>
              </div>
            </div>
          </div>
        </template>

        <button @click="openPost(post)"
                class="absolute bottom-2 right-3 text-[11px] text-slate-400 hover:text-slate-700">open ↗</button>
      </article>
    </div>

    <p v-if="!visiblePosts.length" class="text-slate-400 text-sm">
      No posts yet — a card appears here when a student runs their first submission.
    </p>
  </div>
</template>
