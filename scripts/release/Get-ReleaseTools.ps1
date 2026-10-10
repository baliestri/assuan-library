[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Root,
  [string] $Manifest = (Join-Path $PSScriptRoot 'tools.json')
)

$ErrorActionPreference = 'Stop'
$rootPath = [IO.Path]::GetFullPath($Root)
if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) { throw 'Release root is missing.' }
$catalog = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json -AsHashtable
if ($catalog.schemaVersion -ne 1) { throw 'Unsupported release tool manifest.' }
$platform = if ($IsWindows) { 'win-x64' }
  elseif ($IsLinux) { 'linux-x64' }
  elseif ($IsMacOS -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'osx-arm64' }
  elseif ($IsMacOS) { 'osx-x64' }
  else { throw 'Unsupported release tool platform.' }
$target = Join-Path $rootPath "artifacts/tools/release/$platform"
$downloads = Join-Path $target 'downloads'
$bin = Join-Path $target 'bin'
[IO.Directory]::CreateDirectory($downloads) | Out-Null
[IO.Directory]::CreateDirectory($bin) | Out-Null

$resolved = @{}
foreach ($name in @('gitCliff', 'typos')) {
  $tool = $catalog[$name]
  $archive = $tool.archives[$platform]
  if ($tool.version -notmatch '\A[0-9]+\.[0-9]+\.[0-9]+\z' -or
      $archive.name -notmatch '\A[A-Za-z0-9._-]+\z' -or
      $archive.sha256 -notmatch '\A[0-9a-f]{64}\z') {
    throw 'Invalid release tool pin.'
  }

  $download = Join-Path $downloads $archive.name
  if (-not (Test-Path -LiteralPath $download -PathType Leaf)) {
    $uri = "https://github.com/$($tool.repository)/releases/download/$($tool.tag)/$($archive.name)"
    Invoke-WebRequest -Uri $uri -OutFile $download
  }
  if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -cne $archive.sha256) {
    throw 'Release tool archive checksum mismatch.'
  }

  $commandName = if ($name -eq 'gitCliff') { 'git-cliff' } else { 'typos' }
  $executableName = $commandName + $(if ($IsWindows) { '.exe' } else { '' })
  $executable = Join-Path $bin $executableName
  if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    $staging = Join-Path $target ('extract-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($staging) | Out-Null
    try {
      if ($archive.name.EndsWith('.zip', [StringComparison]::Ordinal)) {
        Expand-Archive -LiteralPath $download -DestinationPath $staging
      }
      else {
        & tar -xzf $download -C $staging
        if ($LASTEXITCODE -ne 0) { throw 'Release tool extraction failed.' }
      }
      $matches = @(Get-ChildItem -LiteralPath $staging -File -Recurse -Filter $executableName)
      if ($matches.Count -ne 1) { throw 'Release tool executable was not unique.' }
      Copy-Item -LiteralPath $matches[0].FullName -Destination $executable
      if (-not $IsWindows) {
        & chmod +x $executable
        if ($LASTEXITCODE -ne 0) { throw 'Release tool executable could not be enabled.' }
      }
    }
    finally { Remove-Item -LiteralPath $staging -Recurse -Force }
  }
  $versionLines = & $executable --version
  $version = @($versionLines)[0]
  if ($LASTEXITCODE -ne 0 -or $version -notmatch [regex]::Escape($tool.version)) {
    throw "Release tool version mismatch for $name."
  }
  $resolved[$name] = $executable
}

$env:PATH = $bin + [IO.Path]::PathSeparator + $env:PATH
[pscustomobject]@{ GitCliff = $resolved.gitCliff; Typos = $resolved.typos; Bin = $bin }
