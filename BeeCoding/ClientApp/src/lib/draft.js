// Local autosave of the code editor so an accidental refresh / tab close doesn't lose work.
// Keyed per user + problem. Best-effort — silently no-ops if localStorage is unavailable.

const key = (uid, scope) => `beecoding.draft.${uid || 0}.${scope}`;
const INDEX = 'beecoding.draft.index';
const MAX_DRAFTS = 50;

export function loadDraft(uid, scope) {
  try {
    const raw = localStorage.getItem(key(uid, scope));
    if (!raw) return null;
    const d = JSON.parse(raw);
    return d && typeof d.code === 'string' ? d : null;   // { code, lang, ts }
  } catch {
    return null;
  }
}

export function saveDraft(uid, scope, code, lang) {
  try {
    const k = key(uid, scope);
    localStorage.setItem(k, JSON.stringify({ code, lang, ts: Date.now() }));
    touchIndex(k);
  } catch {
    /* quota exceeded / private mode — nothing we can do */
  }
}

export function clearDraft(uid, scope) {
  try {
    const k = key(uid, scope);
    localStorage.removeItem(k);
    let idx = JSON.parse(localStorage.getItem(INDEX) || '[]');
    idx = idx.filter((x) => x !== k);
    localStorage.setItem(INDEX, JSON.stringify(idx));
  } catch {
    /* ignore */
  }
}

// keep the newest MAX_DRAFTS entries, drop the rest
function touchIndex(k) {
  try {
    let idx = JSON.parse(localStorage.getItem(INDEX) || '[]');
    idx = idx.filter((x) => x !== k);
    idx.push(k);
    while (idx.length > MAX_DRAFTS) localStorage.removeItem(idx.shift());
    localStorage.setItem(INDEX, JSON.stringify(idx));
  } catch {
    /* ignore */
  }
}
