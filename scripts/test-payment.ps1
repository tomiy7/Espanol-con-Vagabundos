$token = "eyJhbGciOiUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6ImMwYWM5MTRlLWUzM2QtNGU3Yi04ODQxLWIwNDE5MWRmYTEzNCIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6InVzZXJAZXhhbXBsZS5jb20iLCJ1c2VybmFtZSI6InRvbWl5NyIsImZpcnN0TmFtZSI6InJhZGUiLCJsYXN0TmFtZSI6Im5pa29saWMiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJzdHVkZW50IiwiZXhwIjoxNzg3OTM3Nzk4fQ.iiwcAp_7g5Y2X6JfgD6jAuKlYB7e6nZ_Lf93dq9a6j0"

$body = @{
    courseId = "3fa85f64-5717-4562-b3fc-2c963f66afaa"
    payerName = "Testic"
    amount = 15000.00
    currency = "RSD"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5176/payments" `
    -Method Post `
    -Headers @{ Authorization = "Bearer $token" } `
    -ContentType "application/json" `
    -Body $body

[System.IO.File]::WriteAllBytes("$PWD\qr.png", [System.Convert]::FromBase64String($response.qrCodeImageBase64))
[System.IO.File]::WriteAllBytes("$PWD\uplatnica.pdf", [System.Convert]::FromBase64String($response.uplatnicaPdfBase64))

Write-Host "Sacuvano: qr.png i uplatnica.pdf u $PWD"