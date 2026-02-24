param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$AdminEmail = "",
    [string]$AdminPassword = "",
    [switch]$SkipTraining
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AdminEmail)) {
    if (-not [string]::IsNullOrWhiteSpace($env:HL_ADMIN_EMAIL)) {
        $AdminEmail = $env:HL_ADMIN_EMAIL
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:SEED_ADMIN_EMAIL)) {
        $AdminEmail = $env:SEED_ADMIN_EMAIL
    }
    else {
        $AdminEmail = "admin@hirelens.local"
    }
}

if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
    if (-not [string]::IsNullOrWhiteSpace($env:HL_ADMIN_PASSWORD)) {
        $AdminPassword = $env:HL_ADMIN_PASSWORD
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:SEED_ADMIN_PASSWORD)) {
        $AdminPassword = $env:SEED_ADMIN_PASSWORD
    }
    else {
        $AdminPassword = "Admin123!Abcd"
    }
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Invoke-Api {
    param(
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null,
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession = $null,
        [string]$ContentType = "application/json",
        [int]$TimeoutSec = 30
    )

    try {
        $params = @{
            Method     = $Method
            Uri        = $Uri
            TimeoutSec = $TimeoutSec
        }

        if ($null -ne $WebSession) {
            $params["WebSession"] = $WebSession
        }

        if ($null -ne $Body) {
            $params["Body"] = $Body
            $params["ContentType"] = $ContentType
        }

        return Invoke-RestMethod @params
    }
    catch {
        $errorMessage = $_.Exception.Message
        if ($_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $errorMessage = "$errorMessage | Response: $($_.ErrorDetails.Message)"
        }

        throw "HTTP $Method $Uri failed. $errorMessage"
    }
}

function To-ObjectArray {
    param(
        [object]$Value
    )

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return @($Value)
    }

    $valueProperty = $Value.PSObject.Properties["value"]
    if ($null -ne $valueProperty -and $null -ne $valueProperty.Value -and $valueProperty.Value -is [System.Collections.IEnumerable] -and -not ($valueProperty.Value -is [string])) {
        return @($valueProperty.Value)
    }

    return @($Value)
}

function Invoke-TestCase {
    param(
        [string]$Id,
        [string]$Name,
        [scriptblock]$Action,
        [System.Collections.Generic.List[object]]$Results
    )

    $started = Get-Date
    try {
        $details = & $Action
        $duration = [int]((Get-Date) - $started).TotalMilliseconds
        $Results.Add([pscustomobject]@{
                Id       = $Id
                Name     = $Name
                Status   = "PASS"
                Duration = $duration
                Details  = $details
            })
    }
    catch {
        $duration = [int]((Get-Date) - $started).TotalMilliseconds
        $Results.Add([pscustomobject]@{
                Id       = $Id
                Name     = $Name
                Status   = "FAIL"
                Duration = $duration
                Details  = $_.Exception.Message
            })
    }
}

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$results = New-Object System.Collections.Generic.List[object]

$jobs = @()
$candidates = @()
$trainedModel = $null
$latestAnalysis = $null
$matchResults = @()
$authPassed = $false
$dataLoaded = $false

Write-Host "Running HireLens ML API tests against $BaseUrl"

# Firm test case set:
# TC01 API health endpoint responds.
# TC02 Swagger contains ML endpoints.
# TC03 Admin authentication succeeds.
# TC04 Jobs and candidates are retrievable.
# TC05 Model training endpoint succeeds (optional if -SkipTraining).
# TC06 Active model endpoint returns active resume classifier.
# TC07 Candidate analysis endpoint returns valid category/confidence.
# TC08 Matching endpoint returns scored results with explanations.

Invoke-TestCase -Id "TC01" -Name "Health endpoint is reachable" -Results $results -Action {
    $health = Invoke-Api -Method GET -Uri "$BaseUrl/api/health" -TimeoutSec 15
    Assert-True ($null -ne $health) "Health response should not be null."
    return "Health endpoint responded."
}

Invoke-TestCase -Id "TC02" -Name "Swagger exposes ML endpoints" -Results $results -Action {
    $swagger = Invoke-Api -Method GET -Uri "$BaseUrl/swagger/v1/swagger.json" -TimeoutSec 15
    $paths = @($swagger.paths.PSObject.Properties.Name)
    Assert-True ($paths -contains "/api/models/train/resume-category") "Training endpoint path missing in swagger."
    Assert-True ($paths -contains "/api/analyses/candidate/{candidateId}/run") "Analysis endpoint path missing in swagger."
    Assert-True ($paths -contains "/api/matches/run") "Matching endpoint path missing in swagger."
    return "ML endpoints are present in swagger."
}

Invoke-TestCase -Id "TC03" -Name "Admin login works" -Results $results -Action {
    $body = @{ email = $AdminEmail; password = $AdminPassword } | ConvertTo-Json
    Invoke-Api -Method POST -Uri "$BaseUrl/api/auth/login?useCookies=true" -Body $body -WebSession $session | Out-Null
    Assert-True ($session.Cookies.Count -gt 0) "Expected auth cookie after login."
    $script:authPassed = $true
    return "Authenticated as $AdminEmail"
}

Invoke-TestCase -Id "TC04" -Name "Jobs and candidates exist" -Results $results -Action {
    Assert-True $authPassed "TC03 must pass before TC04."

    $jobsRaw = Invoke-Api -Method GET -Uri "$BaseUrl/api/jobs" -WebSession $session
    $candidatesRaw = Invoke-Api -Method GET -Uri "$BaseUrl/api/candidates" -WebSession $session
    $script:jobs = @(To-ObjectArray -Value $jobsRaw)
    $script:candidates = @(To-ObjectArray -Value $candidatesRaw)
    Assert-True ($jobs.Count -gt 0) "Expected at least one job posting."
    Assert-True ($candidates.Count -gt 0) "Expected at least one candidate."
    Assert-True ($null -ne $jobs[0].id) "First job must contain id."
    Assert-True ($null -ne $candidates[0].id) "First candidate must contain id."
    $script:dataLoaded = $true
    return "Jobs=$($jobs.Count), Candidates=$($candidates.Count)"
}

if (-not $SkipTraining) {
    Invoke-TestCase -Id "TC05" -Name "Resume category model training succeeds" -Results $results -Action {
        Assert-True $authPassed "TC03 must pass before TC05."
        $script:trainedModel = Invoke-Api -Method POST -Uri "$BaseUrl/api/models/train/resume-category" -WebSession $session
        Assert-True ($trainedModel.modelType -eq "ResumeCategoryClassifier") "ModelType should be ResumeCategoryClassifier."
        Assert-True ($trainedModel.isActive -eq $true) "Trained model should be active."
        Assert-True ($trainedModel.accuracy -ge 0 -and $trainedModel.accuracy -le 1) "Accuracy should be between 0 and 1."
        return "Model=$($trainedModel.version), Accuracy=$($trainedModel.accuracy)"
    }
}
else {
    $results.Add([pscustomobject]@{
            Id       = "TC05"
            Name     = "Resume category model training succeeds"
            Status   = "SKIP"
            Duration = 0
            Details  = "Skipped by -SkipTraining switch."
        })
}

Invoke-TestCase -Id "TC06" -Name "Active model endpoint returns classifier" -Results $results -Action {
    Assert-True $authPassed "TC03 must pass before TC06."
    $active = Invoke-Api -Method GET -Uri "$BaseUrl/api/models/active?modelType=ResumeCategoryClassifier" -WebSession $session
    Assert-True ($active.modelType -eq "ResumeCategoryClassifier") "Active model type mismatch."
    Assert-True ($active.isActive -eq $true) "Active model should be true."
    return "ActiveModel=$($active.version)"
}

Invoke-TestCase -Id "TC07" -Name "Candidate analysis returns valid output" -Results $results -Action {
    Assert-True $authPassed "TC03 must pass before TC07."
    Assert-True $dataLoaded "TC04 must pass before TC07."
    $candidate = $candidates | Select-Object -First 1
    $candidateId = [string]$candidate.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($candidateId)) "Candidate id should not be empty."
    $analysis = Invoke-Api -Method POST -Uri "$BaseUrl/api/analyses/candidate/$candidateId/run" -WebSession $session
    Assert-True (-not [string]::IsNullOrWhiteSpace($analysis.predictedCategory)) "PredictedCategory should not be empty."
    Assert-True ($analysis.confidenceScore -ge 0 -and $analysis.confidenceScore -le 1) "ConfidenceScore should be between 0 and 1."
    Assert-True ($analysis.candidateId -eq $candidateId) "Analysis candidate id mismatch."
    $script:latestAnalysis = $analysis
    return "Category=$($analysis.predictedCategory), Confidence=$($analysis.confidenceScore)"
}

Invoke-TestCase -Id "TC08" -Name "Matching returns scored explainable results" -Results $results -Action {
    Assert-True $authPassed "TC03 must pass before TC08."
    Assert-True $dataLoaded "TC04 must pass before TC08."
    $job = $jobs | Select-Object -First 1
    $jobId = [string]$job.id
    Assert-True (-not [string]::IsNullOrWhiteSpace($jobId)) "Job id should not be empty."
    $request = @{ jobPostingId = $jobId; candidateIds = @() } | ConvertTo-Json -Compress
    $matchesRaw = Invoke-Api -Method POST -Uri "$BaseUrl/api/matches/run" -Body $request -WebSession $session
    $script:matchResults = @(To-ObjectArray -Value $matchesRaw)

    Assert-True ($matchResults.Count -gt 0) "Expected at least one match result."
    $top = $matchResults[0]
    Assert-True ($top.matchScore -ge 0 -and $top.matchScore -le 100) "MatchScore should be between 0 and 100."
    Assert-True ($null -ne $top.matchedSkills) "MatchedSkills should not be null."
    Assert-True ($null -ne $top.missingSkills) "MissingSkills should not be null."
    Assert-True ($null -ne $top.topOverlappingKeywords) "TopOverlappingKeywords should not be null."

    return "MatchCount=$($matchResults.Count), TopScore=$($top.matchScore)"
}

Write-Host ""
Write-Host "Test Results"
$results | Format-Table Id, Name, Status, Duration, Details -AutoSize

$failed = @($results | Where-Object { $_.Status -eq "FAIL" })
if ($failed.Count -gt 0) {
    throw "$($failed.Count) test case(s) failed."
}

Write-Host ""
Write-Host "All ML API test cases passed."
