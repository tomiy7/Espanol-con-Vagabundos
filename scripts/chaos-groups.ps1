<#
Chaos provera Groups servisa nad pokrenutim Docker stackom: gasi delove sistema (broker,
bazu, sam API) i proverava da li se servis ponasa kako treba i da li se sam oporavi.

Preduslovi:
  - .env u korenu repoa (JWT_SECRET, RABBITMQ_PASSWORD, opciono RABBITMQ_USER, GROUPS_API_PORT,
    RABBITMQ_UI_PORT)
  - podignut stack:  docker compose up -d --build groups.api

Pokretanje (iz korena repoa):
  pwsh ./scripts/chaos-groups.ps1
  pwsh ./scripts/chaos-groups.ps1 -ComposeArgs '-p','moj-projekat'   # drugi compose projekat

Sta radi (svaki korak ispisuje sta ocekuje i PASS/FAIL, na kraju je zbir; exit code = broj
neuspelih provera):
  1. RabbitMQ stoji: korisnici i dalje rade, /health je 503, dogadjaji cekaju u outbox-u;
     kad se broker vrati, sve odlazi bez restarta API-ja.
  2. Posle restarta brokera potrosac i dalje prima poruke (CourseCompleted zavrsava upis).
  3. Necitljiva poruka zavrsava u groups.dead, a sledeca ispravna se obradi.
  4. API stoji dok poruka stigne: poruka ceka u redu i obradi se kad se API podigne.
  5. Baza stoji: 500 sa kodom (bez internih detalja), /health 503; kad se baza vrati,
     API radi bez restarta.

Dira samo servise groups.api, groupsdb i rabbitmq iz zadatog compose projekta. Pravi test
podatke (nova grupa i upisi sa slucajnim id-jevima).
#>
param(
    [string]$EnvFile = ".env",
    [string[]]$ComposeArgs = @(),
    [string]$BaseUrl,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ---------- konfiguracija iz .env ----------

$envValues = @{}
if (-not (Test-Path $EnvFile)) { throw "Env file '$EnvFile' not found." }
foreach ($line in Get-Content $EnvFile) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') { $envValues[$Matches[1]] = $Matches[2] }
}
function Get-Setting([string]$name, [string]$default) {
    if ($envValues.ContainsKey($name) -and $envValues[$name]) { return $envValues[$name] }
    if ($null -ne $default) { return $default }
    throw "$name is missing in $EnvFile."
}

$jwtSecret = Get-Setting "JWT_SECRET" $null
$rabbitUser = Get-Setting "RABBITMQ_USER" "coursesite"
$rabbitPassword = Get-Setting "RABBITMQ_PASSWORD" $null
$rabbitUi = "http://localhost:$(Get-Setting 'RABBITMQ_UI_PORT' '15672')"
if (-not $BaseUrl) { $BaseUrl = "http://localhost:$(Get-Setting 'GROUPS_API_PORT' '5003')" }
$exchange = "coursesite.events"

# ---------- pomocne ----------

$results = [System.Collections.Generic.List[object]]::new()

function Step([string]$text) { Write-Host "`n== $text" -ForegroundColor Cyan }

function Check([string]$name, [bool]$ok, [string]$detail = "") {
    $results.Add([pscustomobject]@{ Check = $name; Result = $(if ($ok) { "PASS" } else { "FAIL" }); Detail = $detail })
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host ("   [{0}] {1} {2}" -f $(if ($ok) { "PASS" } else { "FAIL" }), $name, $detail) -ForegroundColor $color
}

function Compose([string[]]$arguments) {
    # stderr (upozorenja compose-a) ne sme da se pomesa sa izlazom koji se parsira.
    $errors = [IO.Path]::GetTempFileName()
    try {
        $output = & docker compose @ComposeArgs @arguments 2>$errors
        if ($LASTEXITCODE -ne 0) { throw "docker compose $($arguments -join ' ') failed: $(Get-Content $errors -Raw)" }
        return $output
    }
    finally { Remove-Item $errors -ErrorAction SilentlyContinue }
}

function Wait-Until([scriptblock]$condition, [int]$seconds = $TimeoutSeconds) {
    $until = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $until) {
        try { if (& $condition) { return $true } } catch { }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Wait-Healthy([string]$service) {
    Wait-Until {
        $id = (Compose @("ps", "-q", $service) | Select-Object -First 1)
        $id -and ((docker inspect $id --format '{{.State.Health.Status}}') -eq "healthy")
    }
}

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Token potpisan istim kljucem kao Auth (claim-ovi kao u Auth tokenu: nameid, role).
function New-Token([string]$role) {
    $header = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $payload = @{ nameid = [guid]::NewGuid().ToString(); role = $role; exp = [DateTimeOffset]::UtcNow.AddHours(1).ToUnixTimeSeconds() } |
        ConvertTo-Json -Compress
    $body = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($payload))
    $hmac = [Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($jwtSecret))
    $signature = ConvertTo-Base64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$header.$body")))
    [pscustomobject]@{ Id = ($payload | ConvertFrom-Json).nameid; Token = "$header.$body.$signature" }
}

function Api([string]$method, [string]$path, $user, $body = $null) {
    $headers = @{}
    if ($user) { $headers.Authorization = "Bearer $($user.Token)" }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $params = @{ Method = $method; Uri = "$BaseUrl$path"; Headers = $headers; SkipHttpErrorCheck = $true; TimeoutSec = 60 }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 5); $params.ContentType = "application/json" }
    try { $response = Invoke-WebRequest @params } catch { return [pscustomobject]@{ Status = 0; Body = $_.Exception.Message; Ms = $timer.ElapsedMilliseconds } }
    # application/problem+json PowerShell vraca kao bajtove, pa se tekst cita eksplicitno.
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $text; Ms = $timer.ElapsedMilliseconds }
}

function Health { (Api GET "/health" $null).Status }

# Polje iz JSON odgovora, ili null ako odgovor nije ono sto se ocekuje.
function Field($response, [string]$name) { try { ($response.Body | ConvertFrom-Json).$name } catch { $null } }

function Sql([string]$query) {
    (Compose @("exec", "-T", "groupsdb", "psql", "-U", "postgres", "-d", "groupsdb", "-tAc", $query) | Out-String).Trim()
}

function PendingOutbox { [int](Sql "SELECT count(*) FROM outbox_messages WHERE processed_at IS NULL") }

function QueueLength([string]$queue) {
    $line = Compose @("exec", "-T", "rabbitmq", "rabbitmqctl", "list_queues", "-q", "--no-table-headers", "name", "messages") |
        Where-Object { $_ -match "^$([regex]::Escape($queue))\s+(\d+)" } | Select-Object -First 1
    if ($line -match '\s(\d+)\s*$') { [int]$Matches[1] } else { -1 }
}

# Objava preko RabbitMQ management API-ja, kao da salje drugi servis (npr. Courses).
function Publish([string]$routingKey, [string]$payload) {
    $pair = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${rabbitUser}:${rabbitPassword}"))
    $request = @{ properties = @{ delivery_mode = 2 }; routing_key = $routingKey; payload = $payload; payload_encoding = "string" } | ConvertTo-Json
    $result = Invoke-RestMethod -Method Post -Uri "$rabbitUi/api/exchanges/%2F/$exchange/publish" `
        -Headers @{ Authorization = "Basic $pair" } -ContentType "application/json" -Body $request
    if (-not $result.routed) { throw "Message $routingKey was not routed to any queue." }
}

function PublishCourseCompleted($enrollmentId, $userId, $courseId) {
    $envelope = @{
        eventId = [guid]::NewGuid(); eventType = "CourseCompleted"; occurredAt = [DateTime]::UtcNow.ToString("o")
        data = @{ enrollmentId = $enrollmentId; userId = $userId; courseId = $courseId }
    } | ConvertTo-Json -Compress
    Publish "CourseCompleted" $envelope
}

function EnrollmentStatus($id) { Field (Api GET "/enrollments/$id" $admin) "status" }

# ---------- priprema ----------

Step "Preflight: stack is up"
$admin = New-Token "admin"
$professor = New-Token "professor"
$courseId = [guid]::NewGuid()
if ((Health) -ne 200) { throw "Groups API at $BaseUrl is not healthy. Start the stack first (docker compose up -d --build groups.api)." }
Write-Host "   API $BaseUrl is healthy"

try {
    # ---------- 1. broker stoji ----------
    Step "1. RabbitMQ is down: users keep working, events wait in the outbox"
    Compose @("stop", "rabbitmq") | Out-Null

    $slots = @("Monday", "Wednesday", "Friday") | ForEach-Object { @{ dayOfWeek = $_; time = "18:00:00"; professorId = $professor.Id } }
    $created = Api POST "/groups" $professor @{ courseId = $courseId; slots = $slots }
    Check "create group answers 201" ($created.Status -eq 201) "($($created.Status), $($created.Ms) ms)"
    $groupId = Field $created "groupId"

    $students = @(); $enrollments = @()
    for ($i = 0; $i -lt 3; $i++) {
        $student = New-Token "student"
        $added = Api POST "/groups/$groupId/members" $professor @{ userId = $student.Id }
        $students += $student; $enrollments += Field $added "enrollmentId"
    }
    Check "adding members answers 201 (group becomes Ready)" ($null -notin $enrollments)
    $started = Api POST "/groups/$groupId/start" $professor @{}
    Check "start answers 204" ($started.Status -eq 204) "($($started.Status))"

    Check "/health reports 503" (Wait-Until { (Health) -eq 503 } 30)
    Check "events wait in the outbox" ((PendingOutbox) -ge 2) "(pending: $(PendingOutbox))"

    Compose @("start", "rabbitmq") | Out-Null
    Check "rabbitmq is healthy again" (Wait-Healthy "rabbitmq")
    Check "outbox drains without restarting the API" (Wait-Until { (PendingOutbox) -eq 0 }) "(pending: $(PendingOutbox))"
    Check "/health is 200 again" (Wait-Until { (Health) -eq 200 })

    # ---------- 2. potrosac posle restarta brokera ----------
    Step "2. Consumer still receives messages after the broker restart"
    Check "consumer is attached to queue groups" (Wait-Until {
        (Compose @("exec", "-T", "rabbitmq", "rabbitmqctl", "list_queues", "-q", "--no-table-headers", "name", "consumers") |
            Where-Object { $_ -match '^groups\s+([1-9]\d*)' }) -ne $null } 30)
    PublishCourseCompleted $enrollments[0] $students[0].Id $courseId
    Check "CourseCompleted completes the enrollment" (Wait-Until { (EnrollmentStatus $enrollments[0]) -eq "Completed" } 30) "(status: $(EnrollmentStatus $enrollments[0]))"

    # ---------- 3. necitljiva poruka ----------
    Step "3. Unreadable message goes to groups.dead, the queue keeps flowing"
    $deadBefore = QueueLength "groups.dead"
    Publish "CourseCompleted" "this is not json"
    Check "message lands in groups.dead" (Wait-Until { (QueueLength "groups.dead") -eq $deadBefore + 1 } 30) "(dead: $(QueueLength 'groups.dead'))"
    PublishCourseCompleted $enrollments[1] $students[1].Id $courseId
    Check "next valid message is processed" (Wait-Until { (EnrollmentStatus $enrollments[1]) -eq "Completed" } 30)

    # ---------- 4. API stoji ----------
    Step "4. API is down while a message arrives: it waits in the queue"
    Compose @("stop", "groups.api") | Out-Null
    PublishCourseCompleted $enrollments[2] $students[2].Id $courseId
    Check "message waits in queue groups" ((QueueLength "groups") -ge 1) "(queued: $(QueueLength 'groups'))"
    Compose @("start", "groups.api") | Out-Null
    Check "API is healthy again" (Wait-Until { (Health) -eq 200 })
    Check "queued message is processed after the restart" (Wait-Until { (EnrollmentStatus $enrollments[2]) -eq "Completed" } 60)
    Check "group completes when every member finished" (Wait-Until {
        (Field (Api GET "/groups/$groupId" $admin) "status") -eq "Completed" } 30)

    # ---------- 5. baza stoji ----------
    Step "5. Database is down: fast 500 with a code, recovery without restart"
    Compose @("stop", "groupsdb") | Out-Null
    $failed = Api GET "/groups" $admin
    $code = Field $failed "code"
    Check "request answers 500 INTERNAL_ERROR" ($failed.Status -eq 500 -and $code -eq "INTERNAL_ERROR") "($($failed.Status) $code, $($failed.Ms) ms)"
    Check "answer is fast (< 20 s)" ($failed.Ms -lt 20000) "($($failed.Ms) ms)"
    Check "no internal details in the answer" (-not ($failed.Body -match 'Npgsql|Host=|Password|   at '))
    Check "/health reports 503" (Wait-Until { (Health) -eq 503 } 30)

    Compose @("start", "groupsdb") | Out-Null
    Check "groupsdb is healthy again" (Wait-Healthy "groupsdb")
    Check "API works again without restart" (Wait-Until { (Api GET "/groups" $admin).Status -eq 200 } 60)
    Check "/health is 200 again" (Wait-Until { (Health) -eq 200 })
}
finally {
    # Sta god da pukne, stack ostaje podignut.
    Compose @("start", "rabbitmq", "groupsdb", "groups.api") | Out-Null
}

# ---------- zbir ----------

$failedChecks = @($results | Where-Object Result -eq "FAIL")
Write-Host "`n== Summary: $($results.Count - $failedChecks.Count)/$($results.Count) checks passed" -ForegroundColor $(if ($failedChecks) { "Red" } else { "Green" })
$failedChecks | Format-Table -AutoSize | Out-String | Write-Host
exit $failedChecks.Count
