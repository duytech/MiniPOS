# Starts the API on the pos_e2e database: the dev connection string from user-secrets, ending in Database=pos_e2e.
# Used by the "api-e2e" entry in .claude/launch.json; run from the repo root.
$line = dotnet user-secrets list --project src/Pos.Api | Where-Object { $_ -like 'ConnectionStrings:Pos = *' } | Select-Object -First 1
if (-not $line) { throw 'ConnectionStrings:Pos is not in the user-secrets of src/Pos.Api.' }
# Added last, not replaced: the last key wins in Npgsql, whatever form the earlier ones have (Database=, DB=, none).
$env:ConnectionStrings__Pos = ($line -replace '^ConnectionStrings:Pos = ', '') + ';Database=pos_e2e'
dotnet run --project src/Pos.Api --launch-profile http
