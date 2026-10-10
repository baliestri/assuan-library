[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Root,
  [Parameter(Mandatory = $true)]
  [string] $Version,
  [string] $ExpectedSha,
  [string] $ToolsRoot,
  [switch] $DryRun,
  [switch] $Fixture
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Version.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Changelog.psm1') -Force

try {
  $rootPath = [IO.Path]::GetFullPath($Root)
  $sourceSha = Assert-ReleaseSource -Root $rootPath -Version $Version -ExpectedSha $ExpectedSha
  $changes = Get-ReleaseVersionEdits -Root $rootPath -Version $Version
  $cacheRoot = if ($ToolsRoot) { [IO.Path]::GetFullPath($ToolsRoot) } else { $rootPath }
  $tools = & (Join-Path $PSScriptRoot 'Get-ReleaseTools.ps1') -Root $cacheRoot
  $changelog = New-ReleaseChangelog -Root $rootPath -Version $Version -GitCliff $tools.GitCliff
  $changes['CHANGELOG.md'] = $changelog.Content

  if (-not $DryRun) {
    $checkout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $fixtureRoot = Join-Path $checkout 'artifacts/release-tests'
    $relative = [IO.Path]::GetRelativePath($fixtureRoot, $rootPath)
    $isFixture = $Fixture -and $relative -ne '.' -and $relative -ne '..' -and
      -not $relative.StartsWith('..' + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal) -and
      -not [IO.Path]::IsPathRooted($relative)
    $isHosted = $env:GITHUB_ACTIONS -eq 'true' -and $env:ASSUAN_RELEASE_PREPARE -eq '1' -and
      $rootPath -eq [IO.Path]::GetFullPath($env:GITHUB_WORKSPACE)
    if (-not $isFixture -and -not $isHosted) {
      throw 'Release writes require an isolated fixture or an authorized hosted preparation job.'
    }
    foreach ($path in $changes.Keys) {
      [IO.File]::WriteAllText((Join-Path $rootPath $path), $changes[$path], [Text.UTF8Encoding]::new($false))
    }
  }

  [ordered]@{
    version = $Version
    sourceSha = $sourceSha
    files = @($changes.Keys | Sort-Object)
    generatedSection = $changelog.Section
  } | ConvertTo-Json -Depth 4 -Compress
}
catch {
  [Console]::Error.WriteLine($_.Exception.Message)
  exit 1
}
