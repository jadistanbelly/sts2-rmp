# RMP Lobby Full Maintenance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the fork maintainable, recover the deployed 0.1.7 behavior, and fix the loaded-run path that can reject the fifth player with "Lobby is full."

**Architecture:** Treat the Nexus/local 0.1.7 DLL as the current deployed baseline because the public GitHub source stops at 0.0.6. The durable fix should centralize host-capacity synchronization so every host path, including loaded-run and save-slot continuation paths, updates the Steam lobby member limit and RMP protocol state.

**Tech Stack:** Godot 4.5.1 C# / .NET 9, STS2 game assemblies from the local Steam install, Steamworks.NET, GitHub Releases, Nexus Mods upload action.

---

## Evidence

- Nexus lists version `0.1.7`, uploaded on `2026-05-15`.
- Upstream GitHub only has release/tag `0.0.6`, dated `2026-03-26`.
- The installed local mod manifest is `0.1.7` and says `Harmony-free rewrite with Steamworks.NET`.
- The installed local DLL is `125440` bytes and contains `HostBootstrapModule`, `ExtendedLobbyModule`, and `LobbyManagerModule`.
- This repo's built DLL is `69120` bytes and still contains Harmony patch classes such as `StartSteamHostPatch`.
- STS2 `LoadRunLobby` has no `MaxPlayers` check; loaded-run connection failure is therefore most likely the Steam lobby member limit or a bypassed host-bootstrap path, not `LoadRunLobby` rejecting the saved player directly.

## Suspected Root Cause

In 0.1.7, RMP stopped using Harmony patches for `NetHostGameService.StartSteamHost(int maxClients)` and instead rewired menu button handlers. That works for RMP-owned host buttons, but any mod or flow that calls vanilla host/load continuation directly can create a Steam lobby with vanilla capacity `4`.

`LobbyManagerModule` actively syncs `StartRunLobby` by binding `RmpProtocol`, updating `StartRunLobby.MaxPlayers`, and calling `SteamLobbyHelper.TryUpdateMemberLimit(...)`. For `LoadRunLobby`, it only logs state. That leaves a loaded-run or switched-run lobby without the same capacity repair.

## Task 1: Keep the Fork Buildable

**Files:**
- Modify: `RemoveMultiplayerPlayerLimit.csproj`
- Modify: `.gitignore`

- [ ] **Step 1: Verify the missing-reference failure**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet build sts2-RMP.sln -c Release
```

Expected before the project-file fix: build fails with missing `MegaCrit` and `HarmonyLib` namespaces because `libs/` was deleted and is ignored.

- [ ] **Step 2: Point references at the installed STS2 data folder**

Use OS-specific `STS2Path` and `STS2DataPath` MSBuild properties. Reference these assemblies with `<Private>false</Private>`:

```xml
<Reference Include="sts2">
  <HintPath>$(STS2DataPath)/sts2.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="GodotSharp">
  <HintPath>$(STS2DataPath)/GodotSharp.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="0Harmony">
  <HintPath>$(STS2DataPath)/0Harmony.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="Steamworks.NET">
  <HintPath>$(STS2DataPath)/Steamworks.NET.dll</HintPath>
  <Private>false</Private>
</Reference>
```

- [ ] **Step 3: Verify the build**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet build sts2-RMP.sln -c Release
```

Expected: `Build succeeded. 0 Warning(s), 0 Error(s)`.

## Task 2: Recover the Real 0.1.7 Baseline

**Files:**
- Create: `references/decompiled/0.1.7/`
- Modify: source files under `src/` after manual review

- [ ] **Step 1: Decompile the installed 0.1.7 DLL**

Run:

```bash
mkdir -p references/decompiled/0.1.7
~/.dotnet/tools/ilspycmd \
  -p \
  -o references/decompiled/0.1.7 \
  "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/mods/RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.dll"
```

Expected: generated C# files include `Network/HostBootstrapModule.cs`, `Network/ExtendedLobbyModule.cs`, `Network/LobbyManagerModule.cs`, and `Core/ModEntry.cs`.

- [ ] **Step 2: Port reviewed decompiled source into normal source files**

Use the decompiled source as a reference, but remove generated Godot bridge boilerplate and keep source organized by module:

```text
src/Core/ModEntry.cs
src/Core/ProtocolConfig.cs
src/Network/HostBootstrapModule.cs
src/Network/ExtendedLobbyModule.cs
src/Network/LobbyManagerModule.cs
src/Network/SteamLobbyHelper.cs
src/Infrastructure/SceneMonitor.cs
```

Expected: `strings -a` on the rebuilt DLL shows the same module names as the installed `0.1.7` DLL.

- [ ] **Step 3: Verify the port compiles**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet build sts2-RMP.sln -c Release
```

Expected: build succeeds before any lobby-full fix is added.

## Task 3: Add a Focused Loaded-Run Capacity Fix

**Files:**
- Modify: `src/Network/LobbyManagerModule.cs`
- Modify: `src/Network/SteamLobbyHelper.cs`
- Test: `tests/RemoveMultiplayerPlayerLimit.Tests.csproj`
- Test: `tests/LobbyCapacitySyncTests.cs`

- [ ] **Step 1: Write the failing test**

Add a helper test that models this behavior:

```csharp
private static void LoadedRunHostUpdatesSteamLimitWhenCurrentLimitIsVanilla()
{
    var steam = new FakeSteamLobby(currentLimit: 4);
    var changed = LobbyCapacitySync.TrySyncSteamLimit(steam.GetLimit, steam.SetLimit, 16);

    AssertEx.Equal(true, changed);
    AssertEx.Equal(16, steam.CurrentLimit);
    AssertEx.Equal(1, steam.SetCalls);
}
```

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project tests/RemoveMultiplayerPlayerLimit.Tests.csproj -c Release
```

Expected before implementation: fails because `LobbyCapacitySync.TrySyncSteamLimit` does not exist.

- [ ] **Step 2: Implement the minimal helper**

Add a small helper that is testable without Steam:

```csharp
internal static class LobbyCapacitySync
{
    internal static bool TrySyncSteamLimit(Func<int> getCurrentLimit, Func<int, bool> setLimit, int targetLimit)
    {
        int currentLimit = getCurrentLimit();
        if (currentLimit == targetLimit)
        {
            return false;
        }
        return setLimit(targetLimit);
    }
}
```

- [ ] **Step 3: Wire loaded-run host state into the helper**

In `LobbyManagerModule.HandleLoadedRunLobby`, when `loadRunLobby.NetService.Type == NetGameType.Host`, call:

```csharp
RmpProtocol.Bind(loadRunLobby.NetService);
LobbyCapacitySync.TrySyncSteamLimit(
    () => SteamLobbyHelper.GetCurrentMemberLimit(loadRunLobby.NetService),
    limit => SteamLobbyHelper.TryUpdateMemberLimit(loadRunLobby.NetService, limit),
    ProtocolConfig.TargetPlayerLimit);
RmpProtocol.BroadcastConfig(ProtocolConfig.TargetPlayerLimit);
```

Expected effect: a loaded-run lobby created through vanilla or another mod is repaired after creation instead of remaining at Steam's vanilla member limit.

- [ ] **Step 4: Verify tests and build**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project tests/RemoveMultiplayerPlayerLimit.Tests.csproj -c Release
DOTNET_ROLL_FORWARD=Major dotnet build sts2-RMP.sln -c Release
```

Expected: tests pass and build succeeds.

## Task 4: Smoke Test the Real Bug

**Files:**
- No source edits
- Use local STS2 install and logs

- [ ] **Step 1: Package a local test build**

Run:

```bash
scripts/release-local.sh --package-only v0.1.8
```

Expected: `build/sts2-RMP-0.1.8.zip` exists with exactly:

```text
RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.json
RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.dll
RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.pck
```

- [ ] **Step 2: Reproduce the user flow**

Manual smoke:

```text
1. Install the local RMP package on host and all clients.
2. Install Multiplayer Save Slots on the host if testing issue #24.
3. Host a 5-player run.
4. During the run, create or switch to a different multiplayer run.
5. Have the fifth player join the loaded/switched lobby.
```

Expected after fix: fifth player joins. Logs should show the loaded-run lobby member limit moving from `4` or unavailable to `16`.

## Task 5: Release Hygiene

**Files:**
- Modify: `tools/build_release.sh`
- Create: `scripts/release-local.sh`
- Create: `.github/workflows/publish-nexus.yml`

- [ ] **Step 1: Use Release configuration for packaging**

`tools/build_release.sh` should build:

```bash
DOTNET_ROLL_FORWARD=Major dotnet build "$ROOT_DIR/RemoveMultiplayerPlayerLimit.csproj" -c Release
```

and copy:

```text
.godot/mono/temp/bin/Release/RemoveMultiplayerPlayerLimit.dll
```

- [ ] **Step 2: Verify shell syntax**

Run:

```bash
bash -n tools/build_release.sh
bash -n scripts/release-local.sh
```

Expected: no output and exit code `0`.

- [ ] **Step 3: Configure Nexus publishing**

In GitHub repository settings, set:

```text
Secret: NEXUSMODS_API_KEY
Variable: NEXUSMODS_FILE_GROUP_ID
```

Expected: publishing a GitHub Release with asset `sts2-RMP-X.Y.Z.zip` uploads that asset as the Nexus main file and archives the previous main file.
