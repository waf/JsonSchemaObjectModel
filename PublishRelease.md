# Release Steps

If you want to make a new release of JsonSchemaObjectModel:

1. Pull the latest `main` branch.
2. Increment the `<Version>` in `src/JsonSchemaObjectModel/JsonSchemaObjectModel.csproj`.
3. Add a `# Release <version>` section to the top of `CHANGELOG.md` describing the changes.
4. Make a pull request with the above, and merge it into `main`.
5. From an up-to-date `main`, run `./publish-release.ps1`.

The script verifies your checkout is clean and up to date, reads the version from the csproj,
and (after a confirmation prompt) creates and pushes a `v<version>` git tag.

Pushing the tag triggers `.github/workflows/release.yml`, which:

- builds and pushes the NuGet package to nuget.org, and
- creates a GitHub release for the tag, using the matching `# Release <version>` section of
  `CHANGELOG.md` as the release notes.

Nothing is published until you push the tag, so bumping the version in a PR is safe on its own.
