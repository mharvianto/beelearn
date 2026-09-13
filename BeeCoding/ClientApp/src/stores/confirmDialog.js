import { defineStore } from 'pinia';

// A single global confirm modal — styled like the rest of the app, instead of the
// browser's native confirm() (which can't be themed and blocks the whole tab).
export const useConfirmDialog = defineStore('confirmDialog', {
  state: () => ({
    visible: false,
    message: '',
    confirmLabel: 'Confirm',
    cancelLabel: 'Cancel',
    danger: true,          // red confirm button — every current use site is a delete
    resolver: null,        // (bool) => void
  }),
  actions: {
    /** Returns a Promise<boolean> — true if the user confirmed. */
    ask(message, opts = {}) {
      this.resolver?.(false);   // an unresolved prior call loses, same as native confirm()
      this.message = message;
      this.confirmLabel = opts.confirmLabel ?? 'Confirm';
      this.cancelLabel = opts.cancelLabel ?? 'Cancel';
      this.danger = opts.danger ?? true;
      this.visible = true;
      return new Promise((resolve) => { this.resolver = resolve; });
    },
    confirm() {
      this.visible = false;
      this.resolver?.(true);
      this.resolver = null;
    },
    cancel() {
      this.visible = false;
      this.resolver?.(false);
      this.resolver = null;
    },
  },
});
