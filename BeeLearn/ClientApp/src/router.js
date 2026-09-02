import { createRouter, createWebHistory } from 'vue-router';
import { useAuth } from './stores/auth';

const routes = [
  { path: '/', redirect: '/boards' },
  { path: '/login', component: () => import('./views/Login.vue'), meta: { anon: true } },
  { path: '/register', component: () => import('./views/Register.vue'), meta: { anon: true } },
  { path: '/boards', component: () => import('./views/Dashboard.vue') },
  { path: '/bank', component: () => import('./views/Bank.vue') },
  { path: '/boards/:slug', component: () => import('./views/Board.vue'), props: true },
  { path: '/boards/:slug/problems/:problemId', component: () => import('./views/Solve.vue'), props: true },
];

export const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async (to) => {
  const auth = useAuth();
  if (!auth.ready) await auth.fetchMe();
  if (!to.meta.anon && !auth.user) return { path: '/login', query: { r: to.fullPath } };
  if (to.meta.anon && auth.user) return { path: '/boards' };
  return true;
});
