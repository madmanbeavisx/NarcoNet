# NarcoNet Server Endpoint Test Script
# Run this after starting the SPT server

# Bypass SSL certificate validation for self-signed certs
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$baseUrl = "https://127.0.0.1:6969"
$endpoints = @(
    "/narconet/version",
    "/narconet/syncpaths",
    "/narconet/exclusions"
)

Write-Host "Testing NarcoNet Server Endpoints..." -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl" -ForegroundColor Gray
Write-Host ""

foreach ($endpoint in $endpoints) {
    $url = "$baseUrl$endpoint"
    Write-Host "Testing: $endpoint" -ForegroundColor Yellow

    try {
        $response = Invoke-WebRequest -Uri $url -Method GET -Headers @{
            "narconet-version" = "1.0.0"
        } -TimeoutSec 5 -UseBasicParsing

        Write-Host "  Status: $($response.StatusCode)" -ForegroundColor Green
        Write-Host "  Raw Response:" -ForegroundColor Gray
        Write-Host "  $($response.Content)" -ForegroundColor White
    }
    catch {
        Write-Host "  Status: FAILED" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
}

Write-Host "Test complete!" -ForegroundColor Cyan
