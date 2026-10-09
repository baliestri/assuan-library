[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Repository,
  [string] $ActionlintPath,
  [string] $ContainerImage
)

$ErrorActionPreference = 'Stop'
$workflowRoot = Join-Path $Repository '.github/workflows'
if (-not (Test-Path -LiteralPath $workflowRoot -PathType Container)) {
  throw 'Workflow directory is missing.'
}

$files = @(Get-ChildItem -LiteralPath $workflowRoot -File |
  Where-Object { $_.Extension -in '.yml', '.yaml' } |
  Sort-Object Name)
if ($files.Count -eq 0) {
  throw 'No workflows were found.'
}

if ($ActionlintPath -and -not $ContainerImage) {
  if (-not (Test-Path -LiteralPath $ActionlintPath -PathType Leaf)) {
    throw 'The selected workflow validator is missing.'
  }

  & $ActionlintPath @($files.FullName)
}
elseif ($ContainerImage -and -not $ActionlintPath) {
  if ($ContainerImage -cnotmatch '\Arhysd/actionlint@sha256:[0-9a-f]{64}\z') {
    throw 'The workflow validator image must be digest-pinned.'
  }

  $paths = @($files | ForEach-Object { '.github/workflows/' + $_.Name })
  & docker run --rm --mount "type=bind,source=$Repository,target=/work,readonly" `
    --workdir /work $ContainerImage @paths
}
else {
  throw 'Select exactly one workflow validator.'
}

if ($LASTEXITCODE -ne 0) {
  throw 'Workflow validation failed.'
}
