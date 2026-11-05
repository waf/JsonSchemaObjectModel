$localChanges = git status --short
if ( $null -ne $localChanges ) {
    Write-Output "Uncommitted changes detected, aborting release."
    Exit 1
}

git fetch origin
$remoteChanges = git log HEAD..origin/main --oneline
if ( $null -ne $remoteChanges ) {
    Write-Output "The main branch is out of date, aborting release."
    Exit 2
}

$csproj = [xml](Get-Content ./src/JsonSchemaObjectModel/JsonSchemaObjectModel.csproj)
# Select the <Version> node directly. Using xpath allows the project to have more than one <PropertyGroup>
$version = $csproj.SelectSingleNode('//Project/PropertyGroup/Version').InnerText.Trim()

Write-Output "Reminder: Did you update the CHANGELOG.md with a '# Release $version' section?"
Write-Output "Press Enter to create tag ""v$version"" and publish to nuget.org"
Read-Host

git tag "v$version"
git push origin "v$version"
