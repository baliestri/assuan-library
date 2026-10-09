# Continuous integration gates

`ci.yml` validates one exact, lowercase 40-character commit SHA. A direct push, pull request or manual run uses the event SHA; a reusable-workflow call requires `source_sha`. Every build job checks out that commit without persisted checkout credentials and verifies `HEAD` before running checks. The final gate publishes the SHA only when preparation, the three-OS matrix, documentation, formatting and workflow validation all succeed. CI has read-only repository access and does not publish a package or a website.

`interop.yml` selects and verifies the same kind of SHA independently. Its final gate rejects a failed, cancelled, skipped or missing platform matrix. A future release must compare **both** gate outputs with its prepared commit, not treat two green result labels as proof of matching source. `Assert-Gate.ps1` accepts `-UpstreamShasJson` for that explicit three-way comparison.

## Reproduce the checks locally

Use the repository's pinned .NET 10 SDK. Set `NUGET_PACKAGES` and `DOTNET_CLI_HOME` to absolute paths under this checkout's ignored `artifacts/` directory, then run from the repository root:

```shell
dotnet restore AssuanLibrary.slnx --locked-mode
dotnet build AssuanLibrary.slnx -c Release --no-restore
dotnet test AssuanLibrary.slnx -c Release --no-restore
dotnet format AssuanLibrary.slnx --verify-no-changes --no-restore
```

The test suites include the main package's analyzer and NuGet README, four installed-package consumers (no Microsoft.Extensions references, DI only, Logging only, and both), platform-specific transport checks, and deterministic documentation bundles. The documentation job builds two fresh `2.0.0` bundles with the same URL base and compares every relative file hash. To inspect a new bundle yourself, use the commands in the documentation operations guide; the builder refuses to overwrite an existing version directory.

`scripts/ci/Verify-Workflows.ps1` validates every `.yml` and `.yaml` file under `.github/workflows` in sorted order. Supply either a repository-local `actionlint` executable in ignored `artifacts/tools/`, or the digest-pinned, read-only container used by CI. The workflow validator does not modify the checkout.

## What each environment proves

The hosted matrix runs on Windows, Linux and macOS. Platform-specific test cases run on their applicable OS; another platform skipping such a case is not evidence for it. A local Windows or Linux pass cannot establish macOS behavior. The separate `interop.yml` gate provisions `gpgconf`, `gpg-agent` and `gpg-connect-agent` from one installation on each OS. It sets `ASSUAN_GNUPG_REQUIRED=1` and passes their absolute paths to tests that create and remove a private homedir in the runner workspace. Missing tools or an agent connection failure are test failures; deterministic fixtures in the ordinary suite are not a substitute. The real-agent cases are deliberately not launched by local checks without an isolated hosted workspace. No local command here publishes NuGet, deploys Pages, creates a tag or changes repository permissions.
