import { createRouter, createWebHistory } from 'vue-router';
import { useAuth } from './stores/auth';

const routes = [
  { path: '/', component: () => import('./views/Landing.vue'), meta: { anon: true } },
  { path: '/login', component: () => import('./views/Login.vue'), meta: { anon: true } },
  { path: '/register', component: () => import('./views/Register.vue'), meta: { anon: true } },
  { path: '/privacy', component: () => import('./views/Privacy.vue'), meta: { public: true } },
  { path: '/terms', component: () => import('./views/Terms.vue'), meta: { public: true } },
  { path: '/boards', component: () => import('./views/Dashboard.vue') },
  { path: '/bank', component: () => import('./views/Bank.vue') },
  { path: '/practice', component: () => import('./views/Practice.vue') },
  { path: '/playground', component: () => import('./views/Playground.vue') },
  { path: '/practice/:id', component: () => import('./views/PracticeSolve.vue'), props: true },
  { path: '/leaderboard', component: () => import('./views/Leaderboard.vue') },
  { path: '/account', component: () => import('./views/Account.vue') },
  { path: '/admin', component: () => import('./views/Admin.vue') },
  { path: '/boards/:slug', component: () => import('./views/Board.vue'), props: true },
  { path: '/boards/:slug/live', component: () => import('./views/LiveCode.vue'), props: true },
  { path: '/boards/:slug/problems/:problemId', component: () => import('./views/Solve.vue'), props: true },
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async (to) => {
  const auth = useAuth();
  if (!auth.ready) await auth.fetchMe();
  if (to.meta.public) return true;                       // privacy / terms — anyone
  if (!to.meta.anon && !auth.user) return { path: '/login', query: { r: to.fullPath } };
  if (to.meta.anon && auth.user) return { path: '/boards' };
  return true;
});
