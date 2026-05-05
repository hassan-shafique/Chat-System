$ErrorActionPreference = "Stop"

# Set working directory to the API folder
$apiPath = Join-Path $PSScriptRoot "AI-Generated-Chat-System.API"
Set-Location $apiPath

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. Building the Project..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Exiting." -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "2. Starting the API in the background..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
# Start the API and capture the process
$apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru -WindowStyle Hidden
Write-Host "Waiting 8 seconds for the application to start..."
Start-Sleep -Seconds 8

$baseUrl = "http://localhost:5022"
$username = "testuser_$(Get-Random)"
$password = "Password123!"

try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "3. Executing Auth.http Requests" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    Write-Host "[POST] /api/auth/register" -ForegroundColor Yellow
    $registerBody = @{
        username = $username
        email = "$username@example.com"
        password = $password
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/auth/register" -Method Post -ContentType "application/json" -Body $registerBody
    Write-Host " -> User $username registered successfully." -ForegroundColor Green

    Write-Host "`n[POST] /api/auth/login" -ForegroundColor Yellow
    $loginBody = @{
        username = $username
        password = $password
    } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
    $token = $loginResponse.token
    $refreshToken = $loginResponse.refreshToken
    Write-Host " -> Login successful. Token received." -ForegroundColor Green

    $authHeader = @{ Authorization = "Bearer $token" }

    Write-Host "`n[POST] /api/auth/$username/2fa/enable" -ForegroundColor Yellow
    $enable2faResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/$username/2fa/enable" -Method Post -Headers $authHeader
    Write-Host " -> 2FA Enabled." -ForegroundColor Green

    # Note: We can't easily script the exact 2FA code verification without the secret logic, 
    # but we can try to hit the endpoint (it might fail if the code is invalid, which is expected)
    Write-Host "`n[POST] /api/auth/$username/2fa/verify-setup (Testing with dummy code)" -ForegroundColor Yellow
    $verifyBody = @{ code = "123456" } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri "$baseUrl/api/auth/$username/2fa/verify-setup" -Method Post -Headers $authHeader -ContentType "application/json" -Body $verifyBody | Out-Null
    } catch {
        Write-Host " -> Verify failed with dummy code (Expected behavior: $($_.Exception.Message))" -ForegroundColor DarkGray
    }

    Write-Host "`n[POST] /api/auth/$username/2fa/disable" -ForegroundColor Yellow
    Invoke-RestMethod -Uri "$baseUrl/api/auth/$username/2fa/disable" -Method Post -Headers $authHeader
    Write-Host " -> 2FA Disabled." -ForegroundColor Green

    Write-Host "`n[POST] /api/auth/refresh-token" -ForegroundColor Yellow
    $refreshBody = @{
        accessToken = $token
        refreshToken = $refreshToken
    } | ConvertTo-Json
    $refreshResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/refresh-token" -Method Post -ContentType "application/json" -Body $refreshBody
    $token = $refreshResponse.token
    Write-Host " -> Token refreshed successfully." -ForegroundColor Green
    $authHeader = @{ Authorization = "Bearer $token" }


    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "4. Executing RBAC.http Requests" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    Write-Host "[POST] /api/auth/$username/assign-role" -ForegroundColor Yellow
    $roleBody = '"Admin"'
    Invoke-RestMethod -Uri "$baseUrl/api/auth/$username/assign-role" -Method Post -ContentType "application/json" -Body $roleBody
    Write-Host " -> Role 'Admin' assigned successfully." -ForegroundColor Green

    # We must login again to refresh the claims to include the new role
    Write-Host "`n[POST] /api/auth/login (Re-authenticating to get Admin role claims)" -ForegroundColor Yellow
    $loginResponse2 = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
    $token2 = $loginResponse2.token
    $authHeader2 = @{ Authorization = "Bearer $token2" }
    Write-Host " -> Re-authenticated successfully." -ForegroundColor Green

    Write-Host "`n[GET] /api/auth/admin-only" -ForegroundColor Yellow
    $adminResp = Invoke-RestMethod -Uri "$baseUrl/api/auth/admin-only" -Method Get -Headers $authHeader2
    Write-Host " -> Admin endpoint response: $adminResp" -ForegroundColor Green

    Write-Host "`n[GET] /api/auth/finance-only (Expected to fail since we are Admin, not Finance)" -ForegroundColor Yellow
    try {
        Invoke-RestMethod -Uri "$baseUrl/api/auth/finance-only" -Method Get -Headers $authHeader2 | Out-Null
        Write-Host " -> Access granted (Unexpected)" -ForegroundColor Red
    } catch {
        Write-Host " -> Access denied (Expected behavior: $($_.Exception.Message))" -ForegroundColor Green
    }

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "All HTTP requests executed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan

} catch {
    Write-Host "`nAn error occurred during script execution:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
} finally {
    Write-Host "`nStopping the API process..." -ForegroundColor Cyan
    if ($apiProcess) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
        Write-Host "API process stopped." -ForegroundColor Green
    }
}
