// Generic starter templates per language. Used when the student picks a language the
// problem's teacher-authored starter code isn't written in.
export const CODE_TEMPLATES = {
  cpp: `#include <bits/stdc++.h>
using namespace std;

int main() {
    ios::sync_with_stdio(false);
    cin.tie(nullptr);

    return 0;
}
`,
  c: `#include <stdio.h>
#include <stdlib.h>

int main(void) {

    return 0;
}
`,
};

// True when `code` is still an untouched template / starter — i.e. safe to replace when
// the student switches language. `extra` = the problem's own starter code.
export function isPristine(code, ...extra) {
  const t = (code || '').trim();
  if (t === '') return true;
  return [CODE_TEMPLATES.c, CODE_TEMPLATES.cpp, ...extra].some((s) => (s || '').trim() === t);
}
