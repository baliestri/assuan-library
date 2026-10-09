[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $EventName,
  [AllowEmptyString()]
  [string] $SourceSha = '',
  [AllowEmptyString()]
  [string] $EventSha = ''
)

$selected = switch -CaseSensitive ($EventName) {
  'workflow_call' { $SourceSha; break }
  'push' { $EventSha; break }
  'pull_request' { $EventSha; break }
  'workflow_dispatch' { $EventSha; break }
  default {
    [Console]::Error.WriteLine('Unsupported workflow event.')
    exit 1
  }
}

if ($selected -cnotmatch '\A[0-9a-f]{40}\z') {
  [Console]::Error.WriteLine('Invalid source SHA.')
  exit 1
}

Write-Output $selected
