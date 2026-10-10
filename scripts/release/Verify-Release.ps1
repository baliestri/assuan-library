[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $Root,
  [Parameter(Mandatory = $true)] [string] $PreparedSha,
  [Parameter(Mandatory = $true)] [string] $CiSha,
  [Parameter(Mandatory = $true)] [string] $InteropSha,
  [Parameter(Mandatory = $true)] [string] $PackagePath,
  [Parameter(Mandatory = $true)] [string] $DocsArchive,
  [Parameter(Mandatory = $true)] [string] $ChecksumPath,
  [Parameter(Mandatory = $true)] [string] $ManifestPath
)

$ErrorActionPreference = 'Stop'
try {
  if ($PreparedSha -cnotmatch '\A[0-9a-f]{40}\z' -or
      $PreparedSha -cne $CiSha -or $PreparedSha -cne $InteropSha) {
    throw 'Release gates did not validate the prepared SHA.'
  }
  $rootPath = [IO.Path]::GetFullPath($Root)
  $head = & git -C $rootPath rev-parse HEAD 2>$null
  if ($LASTEXITCODE -ne 0 -or $head -cne $PreparedSha) {
    throw 'Release checkout does not match the prepared SHA.'
  }
  $dirty = & git -C $rootPath status --porcelain=v1 --untracked-files=all 2>$null
  if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Release checkout is dirty.' }
  $artifactRoot = Join-Path $rootPath 'artifacts'
  foreach ($path in @($PackagePath, $DocsArchive, $ChecksumPath, $ManifestPath)) {
    $relative = [IO.Path]::GetRelativePath($artifactRoot, [IO.Path]::GetFullPath($path))
    if ($relative -eq '..' -or $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or
        [IO.Path]::IsPathRooted($relative)) { throw 'Release artifact is outside the checkout.' }
  }
  Import-Module (Join-Path $PSScriptRoot 'Artifacts.psm1') -Force
  $verified = Assert-ReleaseArtifacts -PreparedSha $PreparedSha -PackagePath $PackagePath `
    -DocsArchive $DocsArchive -ChecksumPath $ChecksumPath -ManifestPath $ManifestPath
  $verified | ConvertTo-Json -Compress
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
