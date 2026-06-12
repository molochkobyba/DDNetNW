# GitHub release guide

This guide is for publishing DDNetNW without terminal commands.

## 1. Upload source files

1. Open the repository on GitHub.
2. Click `Add file`.
3. Click `Upload files`.
4. Drag the updated project files into the page.
5. Do not upload `bin`, `obj`, `.vs` or `publish` folders.
6. Commit directly to `main` if this is your main development branch.

Suggested commit message:

```text
Update DDNetNW to v1.35
```

## 2. Create a release

1. Open the repository on GitHub.
2. Click `Releases`.
3. Click `Draft a new release`.
4. Create a new tag: `v1.35`.
5. Release title: `DDNetNW v1.35`.
6. Paste the release description from `docs/RELEASE_DESCRIPTION_v1.35.md`.
7. Attach the compiled release zip, for example `DDNetNW-v1.35-win-x64.zip`.
8. Click `Publish release`.

## 3. What to attach

Attach the compiled app archive from the publish output folder. Do not attach the source zip as the main user download unless you only want to share code.

Good asset name:

```text
DDNetNW-v1.35-win-x64.zip
```

Bad asset names:

```text
final.zip
new version.zip
program really final 2.zip
```
