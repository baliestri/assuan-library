[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $EventName,
  [AllowEmptyString()]
  [string] $SourceSha = '',
  [AllowEmptyString()]
  [string] $EventSha = '',
  [AllowEmptyString()]
  [string] $CallInputsJson
)

$events = @('workflow_call', 'push', 'pull_request', 'workflow_dispatch')
if ($EventName -cnotin $events) {
  [Console]::Error.WriteLine('Unsupported workflow event.')
  exit 1
}

if ($PSBoundParameters.ContainsKey('CallInputsJson')) {
  try {
    $call = ConvertFrom-Json -InputObject $CallInputsJson -AsHashtable -NoEnumerate
    if ($null -eq $call) { $call = @{} }
  }
  catch {
    [Console]::Error.WriteLine('Invalid reusable workflow inputs.')
    exit 1
  }
  if ($call -isnot [System.Collections.IDictionary]) {
    [Console]::Error.WriteLine('Invalid reusable workflow inputs.')
    exit 1
  }
  if ($call.Contains('source_sha')) {
    $selected = $call['source_sha']
  }
  elseif ($EventName -ceq 'workflow_call') {
    [Console]::Error.WriteLine('Required reusable source SHA is missing.')
    exit 1
  }
  else { $selected = $EventSha }
}
else {
  $selected = if ($EventName -ceq 'workflow_call') { $SourceSha } else { $EventSha }
}

if ($selected -isnot [string] -or $selected -cnotmatch '\A[0-9a-f]{40}\z') {
  [Console]::Error.WriteLine('Invalid source SHA.')
  exit 1
}

Write-Output $selected
