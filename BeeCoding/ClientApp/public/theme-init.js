// Applied before the app renders to avoid a flash of the wrong theme.
try {
  var t = localStorage.getItem('beecoding.theme') || 'system';
  if (t === 'dark' || (t === 'system' && matchMedia('(prefers-color-scheme: dark)').matches)) {
    document.documentElement.classList.add('dark');
  }
} catch (e) { /* ignore */ }
