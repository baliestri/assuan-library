[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $Root,
  [Parameter(Mandatory = $true)] [string] $ManifestPath,
  [Parameter(Mandatory = $true)] [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
try {
  $artifactRoot = Join-Path ([IO.Path]::GetFullPath($Root)) 'artifacts'
  foreach ($path in @($ManifestPath, $OutputDirectory)) {
    $relative = [IO.Path]::GetRelativePath($artifactRoot, [IO.Path]::GetFullPath($path))
    if ($relative -eq '.' -or $relative -eq '..' -or
        $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or
        [IO.Path]::IsPathRooted($relative)) { throw 'Pages inputs and output must remain under ignored artifacts.' }
  }
  $releases = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable -NoEnumerate
  foreach ($release in $releases) {
    foreach ($path in @($release.archive, $release.checksum)) {
      if (-not $path) { continue }
      $relative = [IO.Path]::GetRelativePath($artifactRoot, [IO.Path]::GetFullPath($path))
      if ($relative -eq '..' -or $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or
          [IO.Path]::IsPathRooted($relative)) { throw 'Release asset is outside ignored artifacts.' }
    }
  }
  Import-Module (Join-Path $PSScriptRoot 'Pages.psm1') -Force
  $site = New-PagesSite -ManifestPath $ManifestPath -OutputDirectory $OutputDirectory
  $site | ConvertTo-Json -Compress
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
