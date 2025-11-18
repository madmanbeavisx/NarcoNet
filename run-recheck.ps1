# NarcoNet Server Endpoint Test Script
# Run this after starting the SPT server

$baseUrl = 'https://localhost:6969'
$endpoint = "$($baseUrl.TrimEnd('/'))/narconet/recheck"

Write-Host "Invoking recheck endpoint: $endpoint" -ForegroundColor Cyan

# Bypass SSL certificate validation for self-signed certs
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            System.Net.WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$exitCode = 0
try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -TimeoutSec 120 -Headers @{ 'Accept' = 'application/json' }
    Write-Host "Response:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | Write-Host
    $exitCode = 0
}
catch {
    Write-Host "Request failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
}

# Pause so a double-clicked window doesn't close immediately
Write-Host "`nPress Enter to exit..." -ForegroundColor DarkCyan
try {
    Read-Host | Out-Null
} catch {
    # In non-interactive contexts Read-Host can fail; ignore
}

exit $exitCode
