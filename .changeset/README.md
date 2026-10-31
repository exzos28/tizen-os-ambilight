# Changesets

Every change that users should know about gets a changeset — a small Markdown file in this folder that says how big the change is and what it does. They are collected into `CHANGELOG.md` and the next release.

```sh
npm install          # once, at the repo root
npx changeset        # pick patch / minor / major, write one or two lines
```

Commit the generated `.changeset/*.md` with your change. Docs-only or CI-only changes don't need one.

| Bump | When |
|------|------|
| `patch` | Bug fixes, small improvements |
| `minor` | New features, new settings — existing setups keep working |
| `major` | Something users must redo: reflash over USB, reinstall the TV app, changed protocol between TV and ESP32 |

## Release flow

1. Changesets land on `main`.
2. The *Changesets* workflow opens (or updates) a **chore: release** pull request that bumps the version in `package.json` and writes `CHANGELOG.md`.
3. Merging that PR tags `vX.Y.Z`, creates the GitHub release with the changelog entry, and the firmware images are built and attached to it.

The version is the project's version as a whole (ESP32 firmware, TV apps, tools).
