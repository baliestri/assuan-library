Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-ArtifactVersion {
  param([string] $Version)
  if ($Version -cnotmatch '\A2\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
    throw 'Invalid stable 2.x.x artifact version.'
  }
}

function Get-ReleaseFileHash {
  param([string] $Path)
  return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Assert-ReleasePackage {
  param([string] $Path, [string] $Version)
  Assert-ArtifactVersion $Version
  if ([IO.Path]::GetFileName($Path) -cne "AssuanLibrary.$Version.nupkg") {
    throw 'The package filename does not match the selected release.'
  }
  $archive = [IO.Compression.ZipFile]::OpenRead($Path)
  try {
    $entries = @($archive.Entries)
    foreach ($entry in @('lib/net10.0/AssuanLibrary.dll', 'lib/net10.0/AssuanLibrary.xml',
      'analyzers/dotnet/cs/AssuanLibrary.Generators.dll', 'README.md', 'LICENSE.md')) {
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
    $fields = @{
      authors = 'Bruno Sales'
      projectUrl = 'https://github.com/baliestri/assuan-library'
      readme = 'README.md'
      tags = 'assuan gpg gpgme gnupg'
    }
    foreach ($field in $fields.Keys) {
      $node = $metadata[0].SelectSingleNode("*[local-name()='$field']")
      if ($null -eq $node -or $node.InnerText -cne $fields[$field]) {
        throw "Package $field metadata differs from the release."
      }
    }
    foreach ($field in @('description', 'copyright')) {
      $node = $metadata[0].SelectSingleNode("*[local-name()='$field']")
      if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "Package $field metadata is missing."
      }
    }
    $license = $metadata[0].SelectSingleNode('*[local-name()="license"]')
    if ($null -eq $license -or $license.InnerText -cne 'LICENSE.md' -or
        $license.GetAttribute('type') -cne 'file') { throw 'Package license metadata is invalid.' }
    $repository = $metadata[0].SelectSingleNode('*[local-name()="repository"]')
    if ($null -eq $repository -or $repository.GetAttribute('type') -cne 'git' -or
        $repository.GetAttribute('url') -cne 'https://github.com/baliestri/assuan-library') {
      throw 'Package repository metadata is invalid.'
    }
    foreach ($dependency in $xml.SelectNodes('//*[local-name()="dependency"]')) {
      if ($dependency.GetAttribute('id').StartsWith('Microsoft.Extensions', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The package requires an optional Microsoft.Extensions dependency.'
      }
    }
  }
  finally { $archive.Dispose() }
}

function Assert-RegistryPackage {
  param([string] $OriginalPath, [string] $RegistryPath, [string] $Version)
  Assert-ArtifactVersion $Version
  Assert-ReleasePackage $OriginalPath $Version
  Assert-ReleasePackage $RegistryPath $Version
  $original = [IO.Compression.ZipFile]::OpenRead($OriginalPath)
  $published = [IO.Compression.ZipFile]::OpenRead($RegistryPath)
  try {
    foreach ($name in @('lib/net10.0/AssuanLibrary.dll', 'lib/net10.0/AssuanLibrary.xml',
      'analyzers/dotnet/cs/AssuanLibrary.Generators.dll', 'README.md', 'LICENSE.md')) {
      $first = $original.GetEntry($name).Open()
      $second = $published.GetEntry($name).Open()
      try {
        $before = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($first))
        $after = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($second))
        if ($before -cne $after) { throw 'Published package content differs from the validated artifact.' }
      }
      finally { $first.Dispose(); $second.Dispose() }
    }
  }
  finally { $original.Dispose(); $published.Dispose() }
}

function Assert-ReleaseArtifacts {
  param([string] $Version, [string] $PreparedSha, [string] $PackagePath, [string] $ManifestPath)
  Assert-ArtifactVersion $Version
  if ($PreparedSha -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid prepared SHA.' }
  foreach ($path in @($PackagePath, $ManifestPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'A release artifact is missing.' }
  }
  Assert-ReleasePackage $PackagePath $version
  $packageHash = Get-ReleaseFileHash $PackagePath
  $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
  if ($manifest.version -cne $version -or $manifest.sourceSha -cne $PreparedSha -or
      $manifest.packageName -cne [IO.Path]::GetFileName($PackagePath) -or
      $manifest.packageSha256 -cne $packageHash) {
    throw 'Artifact provenance does not match the prepared commit.'
  }
  return [pscustomobject]@{ Version = $version; SourceSha = $PreparedSha; PackageSha256 = $packageHash }
}

# Binds the already packed nupkg to the prepared commit. The package is never rebuilt after this point.
function New-ReleaseArtifacts {
  param([string] $Version, [string] $PreparedSha, [string] $PackagePath, [string] $OutputDirectory)
  Assert-ArtifactVersion $Version
  if ($PreparedSha -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid prepared SHA.' }
  Assert-ReleasePackage $PackagePath $version
  [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
  $manifest = Join-Path $OutputDirectory 'release-artifacts.json'
  if (Test-Path -LiteralPath $manifest) { throw 'A release artifact already exists.' }
  $record = [ordered]@{ version = $version; sourceSha = $PreparedSha;
    packageName = [IO.Path]::GetFileName($PackagePath); packageSha256 = Get-ReleaseFileHash $PackagePath }
  [IO.File]::WriteAllText($manifest, ($record | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
  return Assert-ReleaseArtifacts -Version $Version -PreparedSha $PreparedSha -PackagePath $PackagePath `
    -ManifestPath $manifest
}

Export-ModuleMember -Function Assert-ReleasePackage, Assert-RegistryPackage, Assert-ReleaseArtifacts,
  New-ReleaseArtifacts
