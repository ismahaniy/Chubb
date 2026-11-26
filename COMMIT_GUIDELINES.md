# Git Commit Message Guidelines

Follow these rules every time you write a git commit message in this repository.
They are based on "The Art of Writing Meaningful Git Commit Messages" and the
Conventional Commits standard.

> If the repository already has its own commit convention (see existing `git log`,
> CONTRIBUTING.md or commitlint config), that convention wins. Otherwise use this file.

---

## 1. Why this matters

- The history is a roadmap for future developers. They should understand how the
  project changed and why, without asking anyone.
- Assume the reader has **no context**: no ticket, no Slack thread, no memory of the task.
- Do **not** assume the code explains itself. The diff shows *what* changed in the code;
  the message must explain *why*.

---

## 2. Structure

```
<type>(<optional scope>): <subject>

<optional body>

<optional footer(s)>
```

- **Subject line** (required): one line summary.
- **Blank line** between subject and body (required if a body exists).
- **Body** (optional, but expected for anything non-trivial): what changed and why.
- **Footer** (optional): breaking changes and issue references.

Example command when a body is needed:

```bash
git commit -m "fix(auth): prevent session expiry during active use" \
           -m "Sessions expired after 30 minutes even when the user was active
because the refresh timer was never reset on API calls. Reset the timer
in the request interceptor so active users stay signed in.

Closes #142"
```

Or prefer writing the full message with `git commit` (no `-m`) / `git commit -F <file>`
so the body can be wrapped properly.

---

## 3. Commit types

| Type       | Use it for                                                                  |
|------------|-----------------------------------------------------------------------------|
| `feat`     | A new feature                                                               |
| `fix`      | A bug fix                                                                   |
| `refactor` | Code restructuring that neither fixes a bug nor adds a feature              |
| `perf`     | A performance improvement                                                   |
| `style`    | Formatting only: whitespace, semicolons, linting — no change in meaning     |
| `docs`     | Documentation only (README, Markdown, comments)                            |
| `test`     | Adding or correcting tests                                                  |
| `build`    | Build system or external dependency changes                                 |
| `ci`       | CI configuration and scripts                                                |
| `chore`    | Maintenance that doesn't touch source or test files (e.g. bumping a package)|
| `revert`   | Reverting a previous commit                                                 |

Pick the type that best describes the change and move on — consistency matters
more than the "perfect" label. **Scope** is optional and names the area affected,
e.g. `feat(api):`, `fix(navbar):`, `docs(readme):`.

---

## 4. Subject line rules

1. **Imperative mood** — write it as a command. It should complete the sentence
   *"If applied, this commit will …"*
   - ✅ `fix: add margin to nav items`
   - ❌ `fixed margin`, `adds margin`, `adding margin`
2. **Lowercase** after the type prefix (Conventional Commits style).
3. **No trailing period** or other ending punctuation.
4. **~50 characters** for the description; never exceed 72 characters for the whole line.
5. **Be specific.** Say what changed *and* the purpose when it fits.
   - ❌ `fix: add margin`
   - ✅ `fix(nav): add margin to prevent items overlapping logo`

---

## 5. Body rules

- Wrap lines at **72 characters**.
- Answer the two key questions, like a journalist:
  - **What** changed, and what effect does it have?
  - **Why** was the change needed? What problem or request is it in reference to?
- Explain the *what* and *why*, not a line-by-line *how* — the diff already shows how.
- Write like a news story: the subject is the headline, the body gives the
  organised details that matter.
- Be direct. Remove filler and hedging words such as "maybe", "I think",
  "kind of", "though", "just", "basically".
- Bullet points (`-`) are fine for listing several related changes.
- Use of imperative mood is only strict for the subject; the body can use
  normal prose.

---

## 6. Footer rules

- **Breaking changes**: add `BREAKING CHANGE: <description>` explaining the old
  behaviour, the new behaviour, and why. You may also mark the subject with `!`,
  e.g. `feat(api)!: remove v1 endpoints`.
- **Issue references**: `Closes #123`, `Fixes #45`, `Refs PROJ-678`.
- Keep any trailers required by the tool environment (e.g. `Co-Authored-By:`)
  at the very end of the footer.

---

## 7. One commit = one logical change

- Commit small, focused changes. If the subject needs "and" to describe two
  unrelated things, split it into two commits.
- Don't bundle formatting/style changes with logic changes.

---

## 8. Examples

### Good

```
feat(gallery): lazy-load images to improve page load time
```

```
chore(deps): bump axios from 1.6.0 to 1.7.2
```

```
fix(subscribe): allow form submission when email has a plus sign

The email validator rejected addresses containing "+", which stopped
some users from subscribing. Replace the custom regex with the
built-in validator, which accepts all RFC 5322 addresses.

Closes #87
```

```
docs(footer): update support phone number

The client changed their support line and asked for the site to show
the new number.
```

```
refactor(api)!: rename getUser to fetchUser

Aligns naming with the other fetch* helpers in the client module.

BREAKING CHANGE: getUser() no longer exists. Call fetchUser() instead;
the signature and return value are unchanged.
```

### Bad (never write these)

```
fixed bug on landing page      ← vague, past tense, which bug?
Changed style                  ← past tense, says nothing
oops                           ← meaningless
I think I fixed it this time?  ← hedging, no information
update                         ← no type, no detail
WIP / misc / stuff             ← filler
(empty message)
```

---

## 9. Checklist before committing

- [ ] Type is one of the allowed types (scope optional)
- [ ] Subject is imperative, lowercase, no trailing period, ≤ 50 chars (hard max 72)
- [ ] Subject completes "If applied, this commit will …"
- [ ] Blank line between subject and body
- [ ] Body (if any) wrapped at 72 chars and explains **what** and **why**
- [ ] No filler or hedging words
- [ ] Breaking changes flagged with `!` and/or `BREAKING CHANGE:` footer
- [ ] Related issue referenced in the footer when there is one
- [ ] The commit contains one logical change
