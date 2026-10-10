Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ReleaseFileHash {
  param([string] $Path)
  return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Assert-ReleasePackage {
  param([string] $Path, [string] $Version)
  if ([IO.Path]::GetFileName($Path) -cne "AssuanLibrary.$Version.nupkg") {
    throw 'The package filename does not match the selected release.'
  }
  $archive = [IO.Compression.ZipFile]::OpenRead($Path)
  try {
    $entries = @($archive.Entries)
    foreach ($entry in @('lib/net10.0/AssuanLibrary.dll', 'lib/net10.0/AssuanLibrary.xml',
      'analyzers/dotnet/cs/AssuanLibrary.Generators.dll', 'README.md')) {
      if (@($entries | Where-Object FullName -CEQ $entry).Count -ne 1) {
        throw 'The package is missing a required unique entry.'
      }
    }
    if (@($entries | Where-Object { $_.FullName -match '\AAssuanLibrary\.Generators\.' }).Count -ne 0 -or
        @($entries | Where-Object { $_.FullName -match '\Alib/.+Generators.+\.dll\z' }).Count -ne 0) {
      throw 'A separate generator package or runtime assembly was included.'
    }
    $nuspec = @($entries | Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::Ordinal) })
    if ($nuspec.Count -ne 1) { throw 'Package metadata is missing or duplicated.' }
    $stream = $nuspec[0].Open()
    try { $xml = [xml]([IO.StreamReader]::new($stream)).ReadToEnd() }
    finally { $stream.Dispose() }
    $metadata = @($xml.SelectNodes('//*[local-name()="metadata"]'))
    if ($metadata.Count -ne 1 -or
        $metadata[0].SelectSingleNode('*[local-name()="id"]').InnerText -cne 'AssuanLibrary' -or
        $metadata[0].SelectSingleNode('*[local-name()="version"]').InnerText -cne $Version) {
      throw 'Package identity does not match the release.'
    }
    foreach ($dependency in $xml.SelectNodes('//*[local-name()="dependency"]')) {
      if ($dependency.GetAttribute('id').StartsWith('Microsoft.Extensions', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The package requires an optional Microsoft.Extensions dependency.'
      }
    }
  }
  finally { $archive.Dispose() }
}

function Assert-ReleaseDocsArchive {
  param([string] $Path, [string] $Version)
  if ([IO.Path]::GetFileName($Path) -cne "docs-v$Version.zip") {
    throw 'The documentation archive filename is invalid.'
  }
  $archive = [IO.Compression.ZipFile]::OpenRead($Path)
  try {
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$total = 0
    foreach ($entry in $archive.Entries) {
      $name = $entry.FullName
      $segments = $name.Split('/')
      if ($name.StartsWith('/', [StringComparison]::Ordinal) -or $name.Contains('\') -or
          $name.Contains(':') -or $segments.Count -lt 2 -or $segments[0] -cne $Version -or
          @($segments | Where-Object { $_ -in '', '.', '..' }).Count -ne 0 -or
          -not $names.Add($name) -or
          (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
        throw 'Documentation archive path is unsafe or duplicated.'
      }
      $total += $entry.Length
      if ($total -gt 128MB) { throw 'Documentation archive exceeds its budget.' }
    }
    foreach ($required in @('site/index.html', 'site/api/toc.html', 'llms.txt', 'llms-full.txt', 'metadata.json')) {
      if (-not $names.Contains("$Version/$required")) { throw 'Documentation archive is incomplete.' }
    }
  }
  finally { $archive.Dispose() }
}

function Assert-ReleaseArtifacts {
  param([string] $PreparedSha, [string] $PackagePath, [string] $DocsArchive,
    [string] $ChecksumPath, [string] $ManifestPath)
  $version = '2.0.0'
  if ($PreparedSha -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid prepared SHA.' }
  foreach ($path in @($PackagePath, $DocsArchive, $ChecksumPath, $ManifestPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'A release artifact is missing.' }
  }
  Assert-ReleasePackage $PackagePath $version
  Assert-ReleaseDocsArchive $DocsArchive $version
  if ([IO.Path]::GetFileName($ChecksumPath) -cne "docs-v$version.sha256") {
    throw 'Documentation checksum filename is invalid.'
  }
  $packageHash = Get-ReleaseFileHash $PackagePath
  $docsHash = Get-ReleaseFileHash $DocsArchive
  $checksum = [IO.File]::ReadAllText($ChecksumPath)
  if ($checksum -cne "$docsHash  docs-v$version.zip`n") { throw 'Documentation checksum differs.' }
  $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
  if ($manifest.version -cne $version -or $manifest.sourceSha -cne $PreparedSha -or
      $manifest.packageName -cne [IO.Path]::GetFileName($PackagePath) -or
      $manifest.docsName -cne [IO.Path]::GetFileName($DocsArchive) -or
      $manifest.packageSha256 -cne $packageHash -or $manifest.docsSha256 -cne $docsHash) {
    throw 'Artifact provenance does not match the prepared commit.'
  }
  return [pscustomobject]@{ Version = $version; SourceSha = $PreparedSha;
    PackageSha256 = $packageHash; DocsSha256 = $docsHash }
}

function New-ReleaseArtifacts {
  param([string] $PreparedSha, [string] $PackagePath, [string] $BundlePath, [string] $OutputDirectory)
  $version = '2.0.0'
  if ($PreparedSha -cnotmatch '\A[0-9a-f]{40}\z' -or
      [IO.Path]::GetFileName($BundlePath) -cne $version -or
      -not (Test-Path -LiteralPath $BundlePath -PathType Container)) {
    throw 'Invalid release candidate bundle.'
  }
  Assert-ReleasePackage $PackagePath $version
  [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
  $zip = Join-Path $OutputDirectory "docs-v$version.zip"
  $checksum = Join-Path $OutputDirectory "docs-v$version.sha256"
  $manifest = Join-Path $OutputDirectory 'release-artifacts.json'
  if ((Test-Path -LiteralPath $zip) -or (Test-Path -LiteralPath $checksum) -or
      (Test-Path -LiteralPath $manifest)) { throw 'A release artifact already exists.' }
  $files = @(Get-ChildItem -LiteralPath $BundlePath -File -Recurse | Sort-Object FullName)
  if ($files.Count -eq 0) { throw 'Documentation bundle is empty.' }
  $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
  try {
    foreach ($file in $files) {
      if ($file.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw 'Documentation bundle contains a link.'
      }
      $relative = [IO.Path]::GetRelativePath($BundlePath, $file.FullName).Replace('\', '/')
      $entry = $archive.CreateEntry("$version/$relative", [IO.Compression.CompressionLevel]::NoCompression)
      $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
      $entry.ExternalAttributes = 0
      $source = [IO.File]::OpenRead($file.FullName)
      try {
        $destination = $entry.Open()
        try { $source.CopyTo($destination) }
        finally { $destination.Dispose() }
      }
      finally { $source.Dispose() }
    }
  }
  finally { $archive.Dispose() }
  $docsHash = Get-ReleaseFileHash $zip
  [IO.File]::WriteAllText($checksum, "$docsHash  docs-v$version.zip`n", [Text.UTF8Encoding]::new($false))
  $record = [ordered]@{ version = $version; sourceSha = $PreparedSha;
    packageName = [IO.Path]::GetFileName($PackagePath); packageSha256 = Get-ReleaseFileHash $PackagePath;
    docsName = [IO.Path]::GetFileName($zip); docsSha256 = $docsHash }
  [IO.File]::WriteAllText($manifest, ($record | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
  return Assert-ReleaseArtifacts $PreparedSha $PackagePath $zip $checksum $manifest
}

Export-ModuleMember -Function Assert-ReleasePackage, Assert-ReleaseDocsArchive,
  Assert-ReleaseArtifacts, New-ReleaseArtifacts
