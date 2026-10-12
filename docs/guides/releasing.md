# Release and recovery operations

The first release of this workflow was **v2.0.0**, a complete break from the published 1.x API. The next patch, **v2.0.1**, restores missing NuGet
metadata and includes the MIT license file. This guide describes the protected process; reading it or running a local dry-run does not create a
branch, tag, package publication, GitHub Release or Pages deployment.

## Preconditions before a human dispatch

- The release workflow is available for manual dispatch from the appropriate default/develop branch, and the intended `develop` commit is fixed.
  `main` must be an ancestor; remote `release/v<version>` and `v<version>` must not already exist. The selected version must be stable 2.x.x and newer
  than the latest stable 2.x.x tag reachable from `main`.
- Both reusable CI and Interop workflows are available and reviewed. After preparation, their hosted Windows, Linux and macOS jobs must pass for the
  **prepared SHA** before promotion. Deterministic GnuPG fixtures do not replace the required real-agent gate.
- The package owner has configured **NuGet Trusted Publishing** for this repository, `release.yml` and the protected `release` environment. The
  short-lived credential comes from OIDC immediately before publishing; no permanent NuGet key or bootstrap-token fallback is used.
- GitHub environments `release` and `github-pages`, branch permissions/protections and Pages deployment by GitHub Actions are configured by an
  operator. The automation does not change those settings.

For a read-only local preview from a clean, synchronized checkout, set tool caches under this repository's ignored `artifacts/` directory and run
`pwsh -NoProfile -File scripts/release/Prepare-Version.ps1 -Root <absolute-repository-path> -Version 2.0.1 -DryRun`. The preflight validates refs and
the stable version, pins git-cliff and typos by archive SHA-256, and reports proposed package/installation edits plus a changelog section covering
only commits since the previous published tag. It preserves earlier `CHANGELOG.md` entries; the unusual `vv1.1.0` tag is not a stable release. Tool
downloads may populate `artifacts/`, but dry-run never edits tracked files.

## One validated candidate

1. **Prepare:** Dispatch `release.yml` manually from `develop` with `version=2.0.1` (or the next reviewed stable 2.x.x version). A single bot commit
   on `release/v<version>` sets the main package version, NuGet README and generated changelog. Record its full lowercase 40-character SHA. If the
   branch or tag already exists, inspect it rather than replacing it.
2. **Gate:** Call CI and Interop with that exact SHA. Failed, cancelled, skipped or missing jobs fail the release. Compare *both* output SHAs to the
   prepared SHA; green labels alone are insufficient.
3. **Build and verify:** From that same commit, pack the single `AssuanLibrary.<version>.nupkg` with `lib/net10.0`, the analyzer once under
   `analyzers/dotnet/cs`, XML docs, root NuGet `README.md` and `LICENSE.md`. Generate deterministic versioned documentation with DocFX, `llms.txt` and
   `llms-full.txt`; keep the original `docs-v<version>.zip` and `docs-v<version>.sha256`. Hashes and manifests bind the exact artifacts to the
   prepared SHA. No publish job rebuilds them.
4. **Promote:** Recheck remote release branch, `main` ancestry and tag absence immediately before advancing `main` and creating `v<version>`. Never
   force-push or move an existing tag.
5. **Publish:** In the protected `release` job only, obtain NuGet OIDC credentials and upload the previously verified nupkg once. Confirm the registry
   version and package identity. Then create the GitHub Release with the original documentation ZIP and checksum, trigger the separate Pages workflow
   explicitly, and synchronize `develop` with the published commit.

Pages began at `/2.0.0/`; the patch adds `/2.0.1/`. Its independent workflow verifies archived bundles from stable GitHub Releases 2.0.0+ before
assembling the site; it does not regenerate historical bundles or reconstruct documentation for 1.x. Root navigation and LLM indexes select the
greatest stable SemVer version.

## Recovery by last completed phase

| Last completed phase                          | Safe next step                                                                                                                                              |
|-----------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Before preparation                            | Correct the preflight condition, then dispatch again.                                                                                                       |
| Prepared branch, gates failed                 | Inspect and rerun checks for the **same candidate SHA**; do not silently create a replacement commit.                                                       |
| `main`/tag promoted, NuGet result uncertain   | Inspect nuget.org and the original artifact identity before any retry. Do not assume a timeout means no upload, and do not use `--skip-duplicate` as proof. |
| NuGet confirmed, GitHub Release incomplete    | Reuse the existing tag, nupkg identity and original ZIP/hash; compare any existing assets instead of replacing divergent ones.                              |
| GitHub Release complete, Pages failed         | Rerun `docs.yml` manually from verified release assets; do not rebuild an old bundle from `develop`.                                                        |
| Package/site published, `develop` sync failed | Leave the published release intact and resolve the branch integration with a human review.                                                                  |

If a prepared branch failed because reusable gates validated the caller's SHA instead of its candidate, fix the selectors on `develop` and inspect the
existing release branch. After a **reviewed, fast-forward** integration of the fix into `release/v<version>`, record its new full SHA and dispatch
`release.yml` from `develop` with `version=<version>` and `resume_sha=<that SHA>`. The resume preflight requires the remote branch, checkout, version,
changelog and main ancestry to match; it creates no new preparation commit. Omitting `resume_sha` while the branch exists fails closed. Never delete
or force-update the candidate as a shortcut.

Local release tests use disposable Git repositories and fake registry/GitHub responses under `artifacts/release-tests/`. They cannot prove the
account's OIDC policy, protected environments, branch permissions, hosted macOS or real GnuPG success. Actual publication requires a separate explicit
maintainer authorization.

Run the complete local check from this checkout with `pwsh -NoProfile -File scripts/release/Test-Release.ps1 -Root <absolute-repository-path>`. It
performs locked restore, Release build/tests, formatting, actionlint, package inspection and two equivalent documentation builds, then exercises safe
mocked recovery transitions. The JSON result identifies the checkout HEAD and ignored dry-run output directory; it is **not** a prepared release SHA
or an authorization to dispatch `release.yml`. A failed dry-run leaves only its own ignored diagnostic files for inspection.
