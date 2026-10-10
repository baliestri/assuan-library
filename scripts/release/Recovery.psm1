function Invoke-ReleaseSimulation {
  param([System.Collections.IDictionary] $Scenario)
  $sha = [string]$Scenario.preparedSha
  if ($sha -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Mock prepared SHA is invalid.' }
  $result = [ordered]@{
    lastPhase = 'Preflight'
    requiresManualInspection = $false
    attemptedSecondUpload = $false
    attemptedForce = $false
    nextStep = 'Candidate may be prepared.'
  }
  if ($Scenario.branchExists -eq $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Inspect the existing release branch before preparing anything.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'Prepared'
  if ($Scenario.checkedOutSha -cne $sha -or $Scenario.ciSha -cne $sha -or
      $Scenario.interopSha -cne $sha -or $Scenario.ciResult -cne 'success' -or
      $Scenario.interopResult -cne 'success') {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Inspect both gates and reuse the same prepared candidate SHA.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'Gated'
  if ($Scenario.artifactsValid -ne $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Inspect artifact hashes and package contents before promotion.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'Built'
  if ($Scenario.mainStillAncestor -ne $true -or $Scenario.tagConflict -eq $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Inspect main and the release tag; never force a ref.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'PublishingNuGet'
  if ($Scenario.uploadResult -cne 'success' -or $Scenario.registryContainsVersion -ne $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Inspect nuget.org and the original artifact before any upload retry.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'PublishedNuGet'
  if ($Scenario.githubAssetsMatch -ne $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Compare existing GitHub Release assets; do not replace divergent bytes.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'GithubRelease'
  if ($Scenario.pagesBundlePresent -ne $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Recover the original documentation ZIP and checksum before Pages.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'Pages'
  if ($Scenario.developCanFastForward -ne $true) {
    $result.requiresManualInspection = $true
    $result.nextStep = 'Keep the published release intact and reconcile develop manually.'
    return [pscustomobject]$result
  }

  $result.lastPhase = 'Complete'
  $result.nextStep = 'No recovery is required.'
  return [pscustomobject]$result
}

Export-ModuleMember -Function Invoke-ReleaseSimulation
