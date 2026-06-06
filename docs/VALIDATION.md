# AvaScope Validation

Run these commands from the repository root before marking a development slice complete:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
git status --short
```

Run build and test commands sequentially. Parallel build/test invocations can contend for the same `bin/` and `obj/` outputs.

For protocol-only work, also run:

```powershell
dotnet test AvaScope.slnx --filter Protocol
```

For core-only work, also run:

```powershell
dotnet test AvaScope.slnx --filter Core
```

For MCP adapter work, also run:

```powershell
dotnet test AvaScope.slnx --filter Mcp
```

For Avalonia bridge work, also run:

```powershell
dotnet test AvaScope.slnx --filter Bridge
```
