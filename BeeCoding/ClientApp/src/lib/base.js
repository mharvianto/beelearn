// The app's mount path when reverse-proxied under a subpath (see vite.config.js's
// VITE_BASE_PATH / Program.cs's PathBase). '/' at root -> no prefix.
const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

/** Prefix a root-relative path ("/api/...", "/hubs/board", ...) with the app's base path. */
export function withBase(path) {
  return BASE + path;
}
