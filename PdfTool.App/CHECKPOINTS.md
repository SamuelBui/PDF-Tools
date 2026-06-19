# Git Checkpoints for PdfTool.App

## First-time setup in Visual Studio

1. Open `PdfTool.App` in Visual Studio.
2. Open `Git Changes`.
3. If Visual Studio asks for Git identity, set:
   - `user.name`: your display name
   - `user.email`: your email
4. Create the first commit:

```text
checkpoint: initial v0.3 base
```

## Recommended lightweight workflow

Use one stable commit per milestone and one tag on the same commit.

### Milestone commits

- `checkpoint: stable v0.3`
- `checkpoint: stable v0.4`
- `checkpoint: stable v0.5`

### Matching tags

- `v0.3-stable`
- `v0.4-stable`
- `v0.5-stable`

## Visual Studio flow

1. Finish a stable feature set.
2. Open `Git Changes`.
3. Review changed files.
4. Commit with a checkpoint message.
5. Open `Git Repository`.
6. Right-click that commit and create a tag:
   - `v0.3-stable`
   - `v0.4-stable`
   - `v0.5-stable`

## Command-line flow

Create a checkpoint commit:

```powershell
git add .
git commit -m "checkpoint: stable v0.3"
```

Create a tag on that checkpoint:

```powershell
git tag v0.3-stable
```

## Restore options

### View an old version without changing history

```powershell
git checkout v0.3-stable
```

### Start a new branch from an old checkpoint

```powershell
git checkout -b codex/restore-v0.3 v0.3-stable
```

### Roll current branch back to an old checkpoint

Only do this when you are sure:

```powershell
git reset --hard v0.3-stable
```

## Safe day-to-day pattern

- `main`: always keep working stable here
- `codex/v0.4-work`: use for larger feature work
- tag only stable milestones

Example:

```powershell
git checkout -b codex/v0.4-work
```

When finished:

```powershell
git add .
git commit -m "checkpoint: stable v0.4"
git tag v0.4-stable
git checkout main
git merge --ff-only codex/v0.4-work
```

## What to checkpoint

Create a checkpoint when one of these is true:

- the app builds cleanly
- a tab is stable end-to-end
- a UI redesign is complete
- before a risky refactor
- before adding a new package

## Suggested milestones for this project

- `v0.3-stable`: current working state after Protect and Merge improvements
- `v0.4-stable`: session persistence + validation engine + major Split/Merge upgrades
- `v0.5-stable`: UI polish and advanced workflow features
