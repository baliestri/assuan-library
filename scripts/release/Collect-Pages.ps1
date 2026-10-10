[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $Root,
  [Parameter(Mandatory = $true)] [string] $Repository,
  [Parameter(Mandatory = $true)] [string] $ManifestPath
)

$ErrorActionPreference = 'Stop'
try {
  if ($Repository -cnotmatch '\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z') {
    throw 'Invalid release repository.'
  }
  $rootPath = [IO.Path]::GetFullPath($Root)
  $artifactRoot = Join-Path $rootPath 'artifacts'
  $relative = [IO.Path]::GetRelativePath($artifactRoot, [IO.Path]::GetFullPath($ManifestPath))
  if ($relative -eq '.' -or $relative -eq '..' -or
      $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar) -or
      [IO.Path]::IsPathRooted($relative) -or (Test-Path -LiteralPath $ManifestPath)) {
    throw 'Release collection output is not a new path under ignored artifacts.'
  }

  $selected = [Collections.Generic.List[object]]::new()
  for ($page = 1; $page -le 100; $page++) {
    $json = & gh api "repos/$Repository/releases?per_page=100&page=$page"
    if ($LASTEXITCODE -ne 0) { throw 'Release metadata could not be read.' }
    $batch = ($json -join "`n") | ConvertFrom-Json -AsHashtable -NoEnumerate
    if ($batch -isnot [array]) { throw 'Release metadata is malformed.' }
    foreach ($release in $batch) {
      $tag = [string]$release.tag_name
      if ($release.draft -eq $true -or $release.prerelease -eq $true -or
          $tag -cnotmatch '\Av(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') { continue }
      $version = $tag.Substring(1)
      if ([Version]::Parse($version).Major -lt 2) { continue }
      $archiveName = "docs-v$version.zip"
      $checksumName = "docs-v$version.sha256"
      if (@($release.assets | Where-Object name -CEQ $archiveName).Count -ne 1 -or
          @($release.assets | Where-Object name -CEQ $checksumName).Count -ne 1) {
        throw 'A stable release lacks its original documentation archive or checksum.'
      }
      $directory = Join-Path $artifactRoot "pages-input/$version"
      if (Test-Path -LiteralPath $directory) { throw 'A release version was downloaded twice.' }
      [IO.Directory]::CreateDirectory($directory) | Out-Null
      & gh release download $tag --repo $Repository --dir $directory --pattern $archiveName --pattern $checksumName
      if ($LASTEXITCODE -ne 0) { throw 'Release documentation assets could not be downloaded.' }
      $archive = Join-Path $directory $archiveName
      $checksum = Join-Path $directory $checksumName
      if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or
          -not (Test-Path -LiteralPath $checksum -PathType Leaf)) {
        throw 'Downloaded release documentation is incomplete.'
      }
      $selected.Add([ordered]@{ tagName = $tag; draft = $false; prerelease = $false;
        archive = $archive; checksum = $checksum })
    }
    if ($batch.Count -lt 100) { break }
    if ($page -eq 100) { throw 'Release listing exceeded its pagination budget.' }
  }
  if ($selected.Count -eq 0) { throw 'No stable 2.0.0+ release has documentation.' }
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ManifestPath))) | Out-Null
  [IO.File]::WriteAllText($ManifestPath, (ConvertTo-Json -InputObject $selected.ToArray() -Depth 4),
    [Text.UTF8Encoding]::new($false))
  Write-Output $ManifestPath
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
