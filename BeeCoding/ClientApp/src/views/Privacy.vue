<script setup>
import LegalPage from './LegalPage.vue';
</script>

<template>
  <LegalPage title="Privacy Policy" updated="7 September 2026">
    <p>
      BeeCoding is a self-hosted classroom platform. This instance is operated by your
      school, teacher, or institution (the <strong>“Operator”</strong>), who is responsible
      for how your data is handled and is your point of contact for any privacy request.
      This page describes what the software collects and does by design.
    </p>

    <h2>1. Information we collect</h2>
    <ul>
      <li><strong>Account data</strong> — your email address, display name, chosen role
        (student or teacher), and a one-way hashed password. Plain passwords are never stored.</li>
      <li><strong>Learning activity</strong> — code you run and submit, judge results
        (verdict, runtime, memory), experience points, board and class memberships, wall
        posts, notes, comments and reactions you create.</li>
      <li><strong>Technical data</strong> — a single authentication cookie to keep you
        signed in, and standard server logs (timestamps, IP address, error traces) kept for
        security and debugging.</li>
      <li><strong>AI tutor prompts</strong> — only if the Operator has enabled the optional
        AI tutor. See section 4.</li>
    </ul>

    <h2>2. How we use it</h2>
    <ul>
      <li>To run the service: authenticate you, compile and judge your code, show class
        progress, award XP and build leaderboards.</li>
      <li>To let teachers see and grade the work of students in their classes.</li>
      <li>To keep the service secure (rate limiting, abuse prevention, debugging).</li>
    </ul>
    <p>We do not sell your data or use it for advertising.</p>

    <h2>3. Cookies</h2>
    <p>
      One first-party cookie (<code>beecoding.auth</code>) is used to keep you logged in. It
      is <code>HttpOnly</code> and, over HTTPS, <code>Secure</code>. Some preferences (theme,
      chosen language, panel sizes) are stored in your browser's local storage and never
      leave your device. No third-party or tracking cookies are used.
    </p>

    <h2>4. Third parties</h2>
    <p>
      The core platform sends your data to no third party. If the Operator enables the
      optional <strong>AI tutor</strong>, then when you request a hint the problem
      statement, sample tests, your current code and any error output are sent to the
      AI provider configured by the Operator (for example NVIDIA NIM or an OpenAI-compatible
      endpoint) to generate that hint. Ask your Operator which provider is in use and review
      that provider's own privacy terms.
    </p>

    <h2>5. Data retention and deletion</h2>
    <ul>
      <li>Your data is kept for as long as your account exists on this instance.</li>
      <li>You can delete your account from <strong>Account settings</strong>. This
        permanently removes your profile, submissions and practice progress; if you are a
        teacher it also deletes boards you own and the work submitted to them.</li>
      <li>The Operator may also remove data as part of end-of-term or backup policies.</li>
    </ul>

    <h2>6. Security</h2>
    <p>
      Passwords are hashed with PBKDF2. Submitted code runs in a resource-limited sandbox.
      Traffic should be served over HTTPS. No system is perfectly secure; the code judge is
      classroom-grade isolation, not a guarantee against a determined attacker.
    </p>

    <h2>7. Children and educational use</h2>
    <p>
      BeeCoding is intended for use within a class under the supervision of an educator. Where
      students are minors, the school or Operator acts as the data controller and is
      responsible for obtaining any consent required by local law.
    </p>

    <h2>8. Changes</h2>
    <p>
      This policy may change as the software evolves. The “last updated” date above reflects
      the current version shipped with this instance.
    </p>

    <h2>9. Contact</h2>
    <p>
      Direct privacy questions, access requests or deletion requests to the Operator of this
      instance (usually your teacher or school IT).
    </p>
  </LegalPage>
</template>
