[CmdletBinding()]
param([string] $Root = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))))

$ErrorActionPreference = 'Stop'
try {
  $rootPath = [IO.Path]::GetFullPath($Root)
  if ($rootPath -cne [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) {
    throw 'Local release dry-run must use this checkout.'
  }
  $env:NUGET_PACKAGES = Join-Path $rootPath 'artifacts/nuget-cache'
  $env:DOTNET_CLI_HOME = Join-Path $rootPath 'artifacts/dotnet-home'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  $workspace = Join-Path $rootPath ('artifacts/release-tests/dry-run-' + [Guid]::NewGuid().ToString('N'))
  [IO.Directory]::CreateDirectory($workspace) | Out-Null
  $completed = [Collections.Generic.List[string]]::new()

  & dotnet restore (Join-Path $rootPath 'AssuanLibrary.slnx') --locked-mode
  if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
  $completed.Add('restore')
  & dotnet build (Join-Path $rootPath 'AssuanLibrary.slnx') -c Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
  $completed.Add('build')
  & dotnet test (Join-Path $rootPath 'AssuanLibrary.slnx') -c Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Release test suite failed.' }
  $completed.Add('tests')
  & dotnet format (Join-Path $rootPath 'AssuanLibrary.slnx') --verify-no-changes --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Source formatting check failed.' }
  $completed.Add('format')

  $actionlint = 'rhysd/actionlint@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667'
  $validated = & pwsh -NoProfile -NonInteractive -File (Join-Path $rootPath 'scripts/ci/Verify-Workflows.ps1') `
    -Repository $rootPath -ContainerImage $actionlint
  if ($LASTEXITCODE -ne 0) { throw 'Workflow validation failed.' }
  $completed.Add('workflows')

  $packageRoot = Join-Path $workspace 'package'
  [IO.Directory]::CreateDirectory($packageRoot) | Out-Null
  & dotnet pack (Join-Path $rootPath 'src/AssuanLibrary/AssuanLibrary.csproj') -c Release --no-build `
    --no-restore -p:PackageVersion=2.0.0 -o $packageRoot
  if ($LASTEXITCODE -ne 0) { throw 'Local package build failed.' }
  Import-Module (Join-Path $rootPath 'scripts/release/Artifacts.psm1') -Force
  Assert-ReleasePackage (Join-Path $packageRoot 'AssuanLibrary.2.0.0.nupkg') '2.0.0'
  $completed.Add('package')

  foreach ($name in @('one', 'two')) {
    & dotnet run --project (Join-Path $rootPath 'tools/AssuanLibrary.Docs') -c Release --no-restore -- `
      build --version 2.0.0 --base-url https://baliestri.github.io/assuan-library/ `
      --output (Join-Path $workspace "docs-$name")
    if ($LASTEXITCODE -ne 0) { throw 'Local documentation bundle failed.' }
  }
  $first = Join-Path $workspace 'docs-one/2.0.0'
  $second = Join-Path $workspace 'docs-two/2.0.0'
  $left = @(Get-ChildItem -LiteralPath $first -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($first, $_.FullName)
    "${relative}:$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
  } | Sort-Object)
  $right = @(Get-ChildItem -LiteralPath $second -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($second, $_.FullName)
    "${relative}:$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
  } | Sort-Object)
  if (Compare-Object $left $right) { throw 'Local documentation bundles are not equivalent.' }
  $completed.Add('documentation')

  Import-Module (Join-Path $rootPath 'scripts/release/Recovery.psm1') -Force
  $head = (& git -C $rootPath rev-parse HEAD).Trim()
  if ($LASTEXITCODE -ne 0 -or $head -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Checkout HEAD is unavailable.' }
  $scenario = @{ preparedSha = $head; checkedOutSha = $head; ciSha = $head; interopSha = $head;
    ciResult = 'success'; interopResult = 'success'; branchExists = $false; mainStillAncestor = $true;
    tagConflict = $false; artifactsValid = $true; uploadResult = 'success'; registryContainsVersion = $true;
    githubAssetsMatch = $true; pagesBundlePresent = $true; developCanFastForward = $true }
  $success = Invoke-ReleaseSimulation $scenario
  if ($success.lastPhase -cne 'Complete') { throw 'Mocked success path did not complete.' }
  $scenario.uploadResult = 'timeout'
  $uncertain = Invoke-ReleaseSimulation $scenario
  if ($uncertain.lastPhase -cne 'PublishingNuGet' -or -not $uncertain.requiresManualInspection -or
      $uncertain.attemptedSecondUpload -or $uncertain.attemptedForce) {
    throw 'Mocked uncertain upload did not stop safely.'
  }
  $completed.Add('recovery')

  [ordered]@{ dryRunOnly = $true; version = '2.0.0'; checkoutHead = $head; output = $workspace;
    checks = $completed.ToArray(); lastMockedFailure = $uncertain.lastPhase;
    requiresManualInspection = $uncertain.requiresManualInspection;
    recovery = $uncertain.nextStep } | ConvertTo-Json -Compress
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
