# Contributing

This page covers how to build, validate and send changes. What the library does and how to install it are described in
the [README](README.md); how each part behaves is described in the [wiki](wiki/Home.md).

Search existing issues and pull requests first, and discuss a substantial protocol or API change before implementing it.
Report a suspected vulnerability privately, as described in the [security policy](SECURITY.md).

## Prerequisites

- The .NET SDK pinned in `global.json` (10.0.403, rolling forward to later patches only).
- PowerShell 7 (`pwsh`) for the scripts under `scripts/`.
- Git. The release tests create disposable repositories under the ignored `artifacts/` directory.
- Optional: the ReSharper command-line tools (`jb`) for the cleanup and inspection described below.

## Project layout

| Path                              | Responsibility                                                                             |
| :-------------------------------- | :----------------------------------------------------------------------------------------- |
| `src/AssuanLibrary/`              | The `net10.0` runtime and its public XML documentation. `PACKAGE.md` is the NuGet README.  |
| `src/AssuanLibrary.Generators/`   | The `netstandard2.0` incremental generator shipped as an analyzer inside the main package. |
| `examples/AssuanLibrary.Examples/` | One console application that hosts every selectable example.                               |
| `tests/AssuanLibrary.Tests/`      | Runtime, documentation, CI-contract and release-fixture tests.                             |
| `tests/AssuanLibrary.Generators.Tests/` | Generator and installed-package consumer tests.                                      |
| `tests/AssuanLibrary.GpgconfFixture/` | A deterministic stand-in for `gpgconf` used by the discovery tests.                    |
| `scripts/ci/`, `scripts/release/` | The PowerShell checks called by the workflows.                                             |
| `wiki/`                           | Reference pages, one per part of the library.                                              |

The solution is `AssuanLibrary.slnx`.

## Build and test

Run these from the repository root:

```shell
dotnet restore AssuanLibrary.slnx --locked-mode
dotnet build AssuanLibrary.slnx -c Release --no-restore
dotnet test --solution AssuanLibrary.slnx -c Release --no-restore
pwsh ./scripts/ci/Verify-Format.ps1 -Repository .
dotnet run --project examples/AssuanLibrary.Examples -- --example custom-transport
```

The test runner is Microsoft.Testing.Platform, so `dotnet test` takes an explicit `--solution` or `--project` argument
and VSTest filters do not apply. Each project has a `packages.lock.json`; a change to a package version must regenerate
the affected lock files with `dotnet restore --force-evaluate`.

To keep package caches and tool state inside the checkout, set `NUGET_PACKAGES` and `DOTNET_CLI_HOME` to absolute paths
under the ignored `artifacts/` directory before running the commands.

## What automated tests do not cover

- **Real GnuPG.** The ordinary suite uses a deterministic `gpgconf` fixture. Interoperability with a real agent runs only
  in the hosted `interop.yml` workflow, which provisions GnuPG on Windows, Linux and macOS.
- **Other platforms.** Named-pipe, access-control and native agent-nonce tests run on Windows only; Unix peer-credential
  and cleanup tests run on Linux and macOS only. A pass on one system says nothing about the others.
- **Intel macOS.** The hosted macOS runner is Apple silicon, so the x64 native layout is not exercised.
- **Publication.** No local command publishes a package, creates a tag or changes repository settings.

If a change touches one of these paths, say in the pull request what you exercised by hand and what you did not.

## Continuous integration

`ci.yml` and `interop.yml` run for pull requests and for pushes to `main` and `develop`. Each validates one exact
40-character commit SHA: every job checks out that commit without persisted credentials and verifies `HEAD` before
running. `ci.yml` runs the build and tests on Windows, Linux and macOS, the package and documentation checks, formatting
and a lint of the workflow files; its final gate publishes the SHA only when all of them succeed. A release compares the
SHA reported by both gates with its prepared commit instead of trusting two green labels.

`.github/dependabot.yml` proposes weekly NuGet and GitHub Actions updates against `develop`. Four pins stay manual
because they are bound to reviewed values: the .NET SDK in `global.json`, the release tool archives in
`scripts/release/tools.json`, the actionlint container digest in `ci.yml`, and the Scoop installer commit and SHA-256 in
`interop.yml`. The generator's `Microsoft.CodeAnalysis.CSharp` reference is updated automatically for patch versions
only, because a newer minor version raises the minimum compiler a consumer needs.

`scripts/ci/Verify-Workflows.ps1` lints every workflow with either a local `actionlint` executable under `artifacts/` or
the digest-pinned container that CI uses.

## Releasing

Releases are made only by the manually dispatched `release.yml` workflow; a local build or a passing test run does not
authorize a tag or an upload. For a read-only local preview from a clean, synchronized checkout, run
`pwsh -NoProfile -File scripts/release/Prepare-Version.ps1 -Root <absolute-repository-path> -Version <version> -DryRun`.
It pins git-cliff and typos by archive SHA-256 and reports the proposed version edits and changelog section without
changing a tracked file.

1. **Prepare.** Dispatch the workflow from `develop` with, for example, `version=2.0.1`. One bot commit on
   `release/v<version>` sets the package version, the install commands and the generated changelog. Its full SHA is the
   **prepared SHA** that every later job must match.
2. **Gate.** CI and Interop run for that exact SHA and must both report it.
3. **Build and attest.** The single `AssuanLibrary.<version>.nupkg` is packed once from that commit and bound to it by a
   hash manifest; a separate job signs a build-provenance attestation. No later job rebuilds the package.
4. **Promote.** `main` advances to the prepared SHA and the tag `v<version>` is created in one atomic push. Nothing is
   ever force-pushed.
5. **Publish.** The protected `release` job obtains a short-lived NuGet credential through OIDC trusted publishing and
   uploads the verified package once. It does nothing else.
6. **Confirm.** A separate job waits up to 45 minutes for nuget.org to expose the version, compares the published
   package with the original, and creates the GitHub Release with the original `.nupkg` attached. `develop` is then
   fast-forwarded to the published commit.

nuget.org accepts an upload some time before the version becomes visible. If `confirm` runs out of time, choose
**Re-run failed jobs** on the same run: only `confirm` and the jobs after it run again, and the upload is never repeated.

For recovery, the safe next step depends on the last phase that completed:

| Last completed phase                     | Safe next step                                                                                     |
| :--------------------------------------- | :------------------------------------------------------------------------------------------------- |
| Before preparation                       | Fix the preflight condition and dispatch again.                                                    |
| Prepared branch, gates failed            | Re-run the checks for the same prepared SHA; do not create a replacement commit silently.          |
| `main` and tag promoted, upload uncertain | Inspect nuget.org and the original artifact before any retry; `--skip-duplicate` proves nothing.  |
| Upload accepted, version not yet visible | Re-run only the failed `confirm` job. Do not dispatch a new release.                               |
| Version visible, GitHub Release missing  | Re-run `confirm`; it accepts an existing release only when its asset is identical to the original. |
| Published, `develop` not synchronized    | Leave the release intact and integrate the branch with a human review.                             |

If a prepared branch needs a reviewed fix, fast-forward `release/v<version>` to it and dispatch again with
`resume_sha=<that SHA>`; resuming creates no new preparation commit. The first release of this workflow was v2.0.0.

## Repository conventions

- **Warnings are errors.** `Directory.Build.props` treats compiler and analyzer warnings as errors and applies
  `Meziantou.Analyzer` to every project.
- **Package versions are central.** Put versions in `Directory.Packages.props`, not on a project's `PackageReference`.
  Packages restore from nuget.org only (`NuGet.config`).
- **Code style comes from `.editorconfig`**, including its ReSharper options. After changing C# files, run the ReSharper
  command-line tools and review both the cleanup diff and the inspection report:

  ```shell
  jb cleanupcode --profile="Built-in: Full Cleanup" AssuanLibrary.slnx
  jb inspectcode AssuanLibrary.slnx --stdout --format=Text
  ```

  These tools are the formatter of record; `dotnet format` disagrees with the ReSharper options and is not used.
  `scripts/ci/Verify-Format.ps1` runs both commands with the version pinned in `.config/dotnet-tools.json` and fails
  when the cleanup changes a file or the inspection reports anything. CI runs the same script.

- **Diagnostics stay redacted.** Never print command payloads, secrets, nonce bytes or private homedir paths in code,
  tests or logs.
- **Documentation is in English** and lives in three places: the README says what the project is, this page says how to
  change it, and `wiki/` says how each part behaves. Do not state a validation that was not run. Every C# block in the
  README, in `wiki/Getting-Started.md` and in `src/AssuanLibrary/PACKAGE.md` is compiled by the test suite.

## Sending changes

- `develop` is the integration branch and `main` holds releases. Branch from `develop` and open pull requests against
  it.
- Write commit messages in the [Conventional Commits](https://www.conventionalcommits.org/) format, for example
  `fix(transport): reject an invalid endpoint` or `docs(wiki): clarify handler lifetime`. Mark a breaking change with
  `!`. `git-cliff` generates `CHANGELOG.md` from them; do not rewrite historical 1.x entries by hand.
- Add or update tests with a behavior change, update the affected wiki page, and keep the commands above passing.
