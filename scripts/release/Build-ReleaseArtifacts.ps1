[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $Root,
  [Parameter(Mandatory = $true)] [string] $PreparedSha,
  [Parameter(Mandatory = $true)] [string] $PackagePath,
  [Parameter(Mandatory = $true)] [string] $BundlePath,
  [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
try {
  $rootPath = [IO.Path]::GetFullPath($Root)
  if ($PreparedSha -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid prepared SHA.' }
  $head = & git -C $rootPath rev-parse HEAD 2>$null
  if ($LASTEXITCODE -ne 0 -or $head -cne $PreparedSha) { throw 'Artifact build checkout differs from the prepared SHA.' }
  $dirty = & git -C $rootPath status --porcelain=v1 --untracked-files=all 2>$null
  if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Artifact build checkout is dirty.' }
  $artifactRoot = Join-Path $rootPath 'artifacts'
  foreach ($path in @($PackagePath, $BundlePath, $OutputDirectory)) {
    $relative = [IO.Path]::GetRelativePath($artifactRoot, [IO.Path]::GetFullPath($path))
    if ($relative -eq '..' -or $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or
        [IO.Path]::IsPathRooted($relative)) { throw 'Release output must remain under ignored artifacts.' }
  }
  Import-Module (Join-Path $PSScriptRoot 'Artifacts.psm1') -Force
  $built = New-ReleaseArtifacts -PreparedSha $PreparedSha -PackagePath $PackagePath `
    -BundlePath $BundlePath -OutputDirectory $OutputDirectory
  $built | ConvertTo-Json -Compress
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
