Import-Module (Join-Path $PSScriptRoot 'Artifacts.psm1') -Force

function New-PagesSite {
  param([string] $ManifestPath, [string] $OutputDirectory)
  $releases = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable -NoEnumerate
  if ($releases -isnot [array]) { throw 'Release manifest must be an array.' }
  $stable = [Collections.Generic.List[object]]::new()
  $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach ($release in $releases) {
    if ($release -isnot [System.Collections.IDictionary] -or $release.tagName -isnot [string]) {
      throw 'Release metadata is malformed.'
    }
    if ($release.draft -eq $true -or $release.prerelease -eq $true -or
        $release.tagName -cnotmatch '\Av(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
      continue
    }
    $version = $release.tagName.Substring(1)
    $parsed = [Version]::Parse($version)
    if ($parsed.Major -lt 2) { continue }
    if (-not $seen.Add($version)) { throw 'A stable release version is duplicated.' }
    $archive = [IO.Path]::GetFullPath([string]$release.archive)
    $checksum = [IO.Path]::GetFullPath([string]$release.checksum)
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf) -or
        -not (Test-Path -LiteralPath $checksum -PathType Leaf) -or
        [IO.Path]::GetFileName($checksum) -cne "docs-v$version.sha256") {
      throw 'A stable release is missing its original documentation assets.'
    }
    Assert-ReleaseDocsArchive $archive $version
    $digest = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
    if ([IO.File]::ReadAllText($checksum) -cne "$digest  docs-v$version.zip`n") {
      throw 'A stable release documentation checksum is invalid.'
    }
    $stable.Add([pscustomobject]@{ Version = $version; Parsed = $parsed; Archive = $archive })
  }
  if ($stable.Count -eq 0) { throw 'No stable documentation release is available.' }
  if (Test-Path -LiteralPath $OutputDirectory) { throw 'Pages output already exists.' }
  $parent = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputDirectory))
  [IO.Directory]::CreateDirectory($parent) | Out-Null
  $staging = Join-Path $parent ('pages-stage-' + [Guid]::NewGuid().ToString('N'))
  [IO.Directory]::CreateDirectory($staging) | Out-Null
  try {
    $ordered = @($stable | Sort-Object Parsed)
    foreach ($item in $ordered) {
      $archive = [IO.Compression.ZipFile]::OpenRead($item.Archive)
      try {
        $prefix = "$($item.Version)/site/"
        foreach ($entry in $archive.Entries) {
          if (-not $entry.FullName.StartsWith($prefix, [StringComparison]::Ordinal)) { continue }
          $relative = $entry.FullName.Substring($prefix.Length)
          $destination = Join-Path $staging "$($item.Version)/$relative"
          [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
          $source = $entry.Open()
          try {
            $target = [IO.File]::Create($destination)
            try { $source.CopyTo($target) }
            finally { $target.Dispose() }
          }
          finally { $source.Dispose() }
        }
      }
      finally { $archive.Dispose() }
      foreach ($required in @('index.html', 'api/toc.html', 'llms.txt', 'llms-full.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $staging "$($item.Version)/$required") -PathType Leaf)) {
          throw 'A verified release site is incomplete.'
        }
      }
    }
    $latest = $ordered[-1].Version
    foreach ($name in @('llms.txt', 'llms-full.txt')) {
      [IO.File]::Copy((Join-Path $staging "$latest/$name"), (Join-Path $staging $name))
    }
    $links = ($ordered | ForEach-Object { '<li><a href="./' + $_.Version + '/index.html">' + $_.Version + '</a></li>' }) -join ''
    $index = '<!doctype html><html lang="en"><meta charset="utf-8"><title>AssuanLibrary documentation</title>' +
      '<meta http-equiv="refresh" content="0; url=./' + $latest + '/index.html">' +
      '<h1>AssuanLibrary documentation</h1><p>Latest stable: <a href="./' + $latest + '/index.html">' +
      $latest + '</a></p><ul>' + $links + '</ul></html>'
    [IO.File]::WriteAllText((Join-Path $staging 'index.html'), $index, [Text.UTF8Encoding]::new($false))
    [IO.Directory]::Move($staging, [IO.Path]::GetFullPath($OutputDirectory))
    return [pscustomobject]@{ Latest = $latest; Versions = @($ordered.Version);
      OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory) }
  }
  finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
  }
}

Export-ModuleMember -Function New-PagesSite
