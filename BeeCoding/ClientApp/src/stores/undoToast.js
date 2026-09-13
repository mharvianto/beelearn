import { defineStore } from 'pinia';

// A single global "Deleted. Undo" toast — soft-deletes across the app (boards, problems,
// bank problems, users) share this so the affordance survives a route navigation away
// from the page that triggered the delete (e.g. back to the list after deleting a problem).
export const UNDO_SECONDS = 30;

export const useUndoToast = defineStore('undoToast', {
  state: () => ({
    visible: false,
    message: '',
    deadline: 0,       // Date.now() ms when the undo window closes
    onUndo: null,       // async () => void
  }),
  actions: {
    show(message, onUndo) {
      this.message = message;
      this.onUndo = onUndo;
      this.deadline = Date.now() + UNDO_SECONDS * 1000;
      this.visible = true;
    },
    dismiss() {
      this.visible = false;
      this.onUndo = null;
    },
    async undo() {
      const fn = this.onUndo;
      if (!fn) return;
      this.dismiss();
      try { await fn(); } catch { /* best effort — the item just stays deleted */ }
    },
  },
});
