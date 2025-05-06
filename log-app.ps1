# PowerShell script to run the MBV application with logging enabled

# Set up log directories
$logsDir = "$PSScriptRoot\logs"
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$sessionDir = "$logsDir\session_$timestamp"

Write-Host "Creating session log directory: $sessionDir"
New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null

# Build and run the application
Write-Host "Building the application..."
dotnet build

# Run the app with output redirected
Write-Host "Running the application with logging enabled..."
$processOutput = "$sessionDir\process_output.log"
Start-Process -FilePath "dotnet" -ArgumentList "run --project Frontend.Desktop" -NoNewWindow -Wait -RedirectStandardOutput $processOutput -RedirectStandardError "$sessionDir\process_error.log"

# Wait for logs to be generated
Start-Sleep -Seconds 1

# Copy all logs to session directory
Write-Host "Collecting logs..."
if (Test-Path "$PSScriptRoot\Frontend.Desktop\bin\Debug\net9.0\logs") {
    Copy-Item -Path "$PSScriptRoot\Frontend.Desktop\bin\Debug\net9.0\logs\*" -Destination $sessionDir -Recurse
}

# Create a combined log for easier analysis
Write-Host "Creating combined log file..."
$combinedLog = "$sessionDir\combined.log"
"=== COMBINED MBV APPLICATION LOGS ===`r`n" | Out-File -FilePath $combinedLog

Get-ChildItem -Path $sessionDir -Filter "*.log" | ForEach-Object {
    "`r`n=== $($_.Name) ===`r`n" | Out-File -FilePath $combinedLog -Append
    Get-Content $_.FullName | Out-File -FilePath $combinedLog -Append
}

Write-Host "Done! All logs saved to: $sessionDir"
Write-Host "View combined log at: $combinedLog" 