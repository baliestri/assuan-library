[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $OriginalPath,
  [Parameter(Mandatory = $true)] [string] $RegistryPath
)

$ErrorActionPreference = 'Stop'
try {
  Import-Module (Join-Path $PSScriptRoot 'Artifacts.psm1') -Force
  Assert-RegistryPackage -OriginalPath $OriginalPath -RegistryPath $RegistryPath
  Write-Output 'Package identity and required entries match.'
}
catch {
  [Console]::Error.WriteLine('Published package verification failed.')
  exit 1
}
