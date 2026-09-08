# 🌳 Yggdrasilnet

**Authoritative multiplayer server for an ARPG** (Diablo / Path of Exile-like), built in C# on top of [LiteNetLib](https://github.com/RevenantX/LiteNetLib).

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![LiteNetLib](https://img.shields.io/badge/networking-LiteNetLib-blue)](https://github.com/RevenantX/LiteNetLib)
[![Serilog](https://img.shields.io/badge/logging-Serilog-9d6bff)](https://serilog.net/)

---

## ✨ Features

- **Authoritative simulation** - the server owns game state; clients cannot cheat by manipulating local state.
- **ECS-style world** - entities, components and systems drive gameplay logic in a data-oriented way.
- **Fixed tick-rate game loop** - deterministic, predictable simulation steps.
- **Lightweight UDP networking** via LiteNetLib, with a shared binary packet protocol between client and server.

## 📁 Project structure

```
src/
  Yggdrasilnet.Server/         Server app - network loop, tick loop, ECS simulation, packet handlers
  Yggdrasilnet.Shared/         Code shared between server and clients — packet protocol, network types
  Yggdrasilnet.FakeClient/     Minimal client used to connect to and exercise the server manually
  Yggdrasilnet.Server.Tests/   Automated tests for the server/simulation logic
```

## 🏗️ Architecture

```
                          Client
                            │  network packets (LiteNetLib)
                            ▼
                       NetServer
                            │
                            ▼
                       GameLoop (tick)
                            │
                            ▼
                       Simulation
                     ┌──────┴───────┐
                     ▼              ▼
                 Handlers         World (ECS)
              (process incoming ┌─────────────┐
               packets)         │ Entities     │
                                │ Components   │
                                │ Systems      │
                                └─────────────┘
                            │
                            ▼
                   Snapshot sent back to clients
```

- **`Yggdrasilnet.Shared`** — packet protocol shared between client and server.
- **`Yggdrasilnet.Server`** — network loop, tick loop, ECS-driven simulation.

## 🚀 Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Run the server

```powershell
dotnet run --project src/Yggdrasilnet.Server
```

The server listens on UDP port **`9050`** by default.

### Run tests then start the server (recommended)

A helper script runs the test suite first and only starts the server if it passes:

```powershell
./run-server.ps1
```

### Run tests only

```powershell
dotnet test src/Yggdrasilnet.Server.Tests/Yggdrasilnet.Server.Tests.csproj -c Release
```

## 🧰 Tech stack

| Component  | Purpose                          |
|------------|-----------------------------------|
| .NET 9     | Runtime / language (C#)           |
| LiteNetLib | Reliable UDP networking            |
| Serilog    | Structured console logging         |
