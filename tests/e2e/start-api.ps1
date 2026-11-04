# Starts the API on the pos_e2e database: the dev connection string from user-secrets with Database=pos_e2e.
# Used by the "api-e2e" entry in .claude/launch.json; run from the repo root.
$line = dotnet user-secrets list --project src/Pos.Api | Where-Object { $_ -like 'ConnectionStrings:Pos = *' } | Select-Object -First 1
if (-not $line) { throw 'ConnectionStrings:Pos is not in the user-secrets of src/Pos.Api.' }
$env:ConnectionStrings__Pos = ($line -replace '^ConnectionStrings:Pos = ', '') -replace 'Database=[^;]*', 'Database=pos_e2e'
# Without a Database= key the replace changes nothing and the API would run on the dev database.
if ($env:ConnectionStrings__Pos -notmatch '(^|;)\s*Database=pos_e2e\s*(;|$)') { throw 'ConnectionStrings:Pos in the user-secrets of src/Pos.Api has no Database= key to point at pos_e2e.' }
dotnet run --project src/Pos.Api --launch-profile http
