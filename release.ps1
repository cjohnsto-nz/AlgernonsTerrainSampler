param(
    [switch]$SkipJsonValidation
)

$args = @("--configuration=Release")
if ($SkipJsonValidation) {
    $args += "--skipJsonValidation=true"
}

Write-Host "Building AlgernonsTerrainSampler mod (Release)..." -ForegroundColor Cyan
dotnet run --project CakeBuild/CakeBuild.csproj -- @args

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful! Release package is in: Releases/" -ForegroundColor Green
    Get-ChildItem Releases/*.zip | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
} else {
    Write-Host "`nBuild failed with exit code $LASTEXITCODE" -ForegroundColor Red
}

exit $LASTEXITCODE
