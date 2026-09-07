# Yggdrasilnet

Authoritative multiplayer server for an ARPG (Diablo/Path of Exile-like), written in C# with [LiteNetLib](https://github.com/RevenantX/LiteNetLib).

V3 of the network engine — simpler and leaner than previous iterations.

## Structure

```
src/
  Yggdrasilnet.Server/   Server app (network loop, tick loop)
  Yggdrasilnet.Shared/   Shared server/client code (packet protocol, types)
```

## Run

```powershell
dotnet run --project src/Yggdrasilnet.Server
```

Server listens on port `9050` by default.
