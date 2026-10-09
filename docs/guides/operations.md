# Local documentation operations

Keep `README.adoc` as the repository entrance and edit the canonical Markdown guides under `docs/` for user workflows. `docs/nuget/README.md` is the package-specific entry point. `docs/site/sources.json` selects source order, destination and which guides enter `llms.txt` or `llms-full.txt`; this operational guide is indexed but intentionally excluded from the consolidated end-user text.

Build and test the .NET solution before creating a site bundle. From the repository root, set `NUGET_PACKAGES` to `artifacts/nuget-cache` and `DOTNET_CLI_HOME` to `artifacts/dotnet-home` (use absolute paths). The builder restores the pinned local DocFX tool; no global installation is needed. Then run:

```shell
dotnet run --project tools/AssuanLibrary.Docs -c Release -- build --version 2.0.0 --base-url https://baliestri.github.io/assuan-library/ --output artifacts/docs
dotnet run --project tools/AssuanLibrary.Docs -c Release -- verify --bundle artifacts/docs/2.0.0
```

The build accepts a version and base URL, resolves paths from the repository regardless of caller working directory, and creates a fresh `artifacts/docs/<version>` tree with `site/`, selected `_stage/` Markdown, LLM files, and metadata. It refuses to replace an existing version; use another output root to compare deterministic builds. Verification checks local Markdown and HTML links, fragment IDs, index URLs and required assets. External HTTP links are syntax-checked but never fetched; a passing local check does not prove that remote links or scripts work in production. The command does not deploy. Source specs/plans outside the Git repository are not user guides and must not enter the site or NuGet package.
