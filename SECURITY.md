# Security policy

## Supported versions

Security fixes are released for the latest stable 2.x version of the `AssuanLibrary` NuGet package. The 1.x line has a different API and is no longer
maintained.

## Reporting a vulnerability

Report a suspected vulnerability privately through a
[GitHub Security Advisory](https://github.com/baliestri/assuan-library/security/advisories/new). If that is unavailable, contact the maintainer listed
in [LICENSE.md](LICENSE.md).

Do not open a public issue for a vulnerability, and do not include secrets, nonce bytes, private agent paths or captured protocol payloads in any
report. A description of the affected API, the platform and a minimal reproduction with public placeholder data is enough to start.

## Scope and boundaries

[Ownership and security boundaries](wiki/Security.md) describes what the library protects and what remains the application's responsibility,
including unauthenticated TCP listeners, local IPC peer checks and the limits of secret wiping in managed memory.
