[CmdletBinding()]
param([Parameter(Mandatory = $true)] [string] $InputJson)

$ErrorActionPreference = 'Stop'
try {
  $scenario = ConvertFrom-Json -InputObject $InputJson -AsHashtable -NoEnumerate
  if ($scenario -isnot [System.Collections.IDictionary]) { throw 'Invalid mocked release response.' }
  Import-Module (Join-Path $PSScriptRoot 'Recovery.psm1') -Force
  $result = Invoke-ReleaseSimulation -Scenario $scenario
  $result | ConvertTo-Json -Compress
}
catch {
  [Console]::Error.WriteLine('Release simulation input is invalid.')
  exit 1
}
