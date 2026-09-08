Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

dotnet test ".\src\Yggdrasilnet.Server.Tests\Yggdrasilnet.Server.Tests.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet run --project ".\src\Yggdrasilnet.Server\Yggdrasilnet.Server.csproj" -c Release --no-build -- @args
