[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Sha,
  [Parameter(Mandatory = $true)]
  [string] $Repository
)

if ($Sha -cnotmatch '\A[0-9a-f]{40}\z') {
  [Console]::Error.WriteLine('Invalid source SHA.')
  exit 1
}

$head = & git -C $Repository rev-parse HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or $head -cne $Sha) {
  [Console]::Error.WriteLine('Checkout does not match the selected SHA.')
  exit 1
}

Write-Output $Sha
