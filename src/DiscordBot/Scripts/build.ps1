# Build script for Discord Bot
# Usage: ./Scripts/build.ps1 [-Configuration Debug|Release] [-Verbose] [-Clean]

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [switch]$Clean,
    [switch]$Verbose
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get script directory
$ScriptDir = $PSScriptRoot
$ProjectDir = Split-Path -Parent $ScriptDir
$SolutionDir = Split-Path -Parent -Parent $ProjectDir

Write-Host "🔧 Discord Bot Build Script" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Project Directory: $ProjectDir" -ForegroundColor Gray

try {
    # Change to project directory
    Push-Location $ProjectDir
    
    # Clean if requested
    if ($Clean) {
        Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
        dotnet clean --configuration $Configuration --verbosity minimal
        
        # Remove bin and obj directories
        Get-ChildItem -Path $ProjectDir -Include "bin", "obj" -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # Restore packages
    Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
    $RestoreArgs = @("restore")
    if ($Verbose) { $RestoreArgs += "--verbosity", "normal" }
    & dotnet @RestoreArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Package restore failed"
    }
    
    # Build project
    Write-Host "🔨 Building project..." -ForegroundColor Yellow
    $BuildArgs = @("build", "--configuration", $Configuration, "--no-restore")
    if ($Verbose) { $BuildArgs += "--verbosity", "normal" }
    & dotnet @BuildArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    
    # Run tests if they exist
    $TestDir = Join-Path $ProjectDir "Tests"
    if (Test-Path $TestDir) {
        Write-Host "🧪 Running tests..." -ForegroundColor Yellow
        $TestArgs = @("test", "--configuration", $Configuration, "--no-build", "--logger", "console;verbosity=minimal")
        & dotnet @TestArgs
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Tests failed but continuing with build"
        }
    }
    
    Write-Host "✅ Build completed successfully!" -ForegroundColor Green
    
    # Display build output information
    $OutputPath = Join-Path $ProjectDir "bin" $Configuration "net9.0"
    if (Test-Path $OutputPath) {
        Write-Host ""
        Write-Host "📁 Build Output:" -ForegroundColor Cyan
        Write-Host "  Location: $OutputPath" -ForegroundColor Gray
        
        $ExePath = Join-Path $OutputPath "DiscordBot.exe"
        if (Test-Path $ExePath) {
            $FileInfo = Get-Item $ExePath
            Write-Host "  Executable: $($FileInfo.Name) ($([math]::Round($FileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Error "❌ Build failed: $($_.Exception.Message)"
    exit 1
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "🚀 To run the bot:" -ForegroundColor Cyan
Write-Host "  dotnet run --project `"$ProjectDir`" --configuration $Configuration" -ForegroundColor Gray