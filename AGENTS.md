# OpenMU Agent Instructions

## .NET Verification On Mac

- Do not assume a local .NET SDK is installed. Check `dotnet --info` first; if it is unavailable, use Docker.
- OpenMU targets `net10.0`, so use a .NET 10 SDK image for build and test commands:

```bash
docker run --rm \
  -v "$PWD:/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test <test-project-or-solution>
```

- Prefer running the smallest relevant test project instead of the whole solution. The solution includes Windows-targeted projects such as `net10.0-windows`, which may not build cleanly inside a Linux container.
- For server/gameplay/plugin features, start with the related test projects, for example:

```bash
dotnet test tests/MUnique.OpenMU.Tests/MUnique.OpenMU.Tests.csproj
dotnet test tests/MUnique.OpenMU.PlugIns.Tests/MUnique.OpenMU.PlugIns.Tests.csproj
dotnet test tests/MUnique.OpenMU.Persistence.Initialization.Tests/MUnique.OpenMU.Persistence.Initialization.Tests.csproj
```

- When Docker daemon access is blocked by sandbox permissions, request approval to run Docker instead of falling back silently.
- If build or tests cannot be executed, state that clearly in the final response and describe which static checks were completed.
