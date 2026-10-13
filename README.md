# Aucy 💰

A real-time auction house backend for online games.

Lots go in. Bids go in. Background workers match them. Clients get the result immediately.

Built as a concurrent order book rather than a CRUD API with a `Bids` table.

## 🛠️ Tech Stack

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://github.com/dotnet/runtime)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://github.com/dotnet/aspnetcore)
[![MagicOnion](https://img.shields.io/badge/MagicOnion-512BD4?style=for-the-badge)](https://github.com/Cysharp/MagicOnion)
[![gRPC](https://img.shields.io/badge/gRPC-244C5A?style=for-the-badge&logo=google&logoColor=white)](https://github.com/grpc/grpc-dotnet)
[![MessagePack](https://img.shields.io/badge/MessagePack-000000?style=for-the-badge)](https://github.com/MessagePack-CSharp/MessagePack-CSharp)

[![Entity Framework Core](https://img.shields.io/badge/EF_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://github.com/dotnet/efcore)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://github.com/postgres/postgres)
[![StackExchange.Redis](https://img.shields.io/badge/StackExchange.Redis-DC382D?style=for-the-badge&logo=redis&logoColor=white)](https://github.com/StackExchange/StackExchange.Redis)
[![DistributedLock](https://img.shields.io/badge/DistributedLock-Redis-DC382D?style=for-the-badge&logo=redis&logoColor=white)](https://github.com/madelson/DistributedLock)

[![xUnit](https://img.shields.io/badge/xUnit-Tests-5E5E5E?style=for-the-badge)](https://github.com/xunit/xunit)
[![Testcontainers](https://img.shields.io/badge/Testcontainers-.NET-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://github.com/testcontainers/testcontainers-dotnet)
[![Serilog](https://img.shields.io/badge/Serilog-Logging-1A1A1A?style=for-the-badge)](https://github.com/serilog/serilog)
[![Polly](https://img.shields.io/badge/Polly-Resilience-2C3E50?style=for-the-badge)](https://github.com/App-vNext/Polly)

## ⚡ What's Inside

- **Buy Orders & Sell Lots** — persistent orders with partial fills and collection.
- **Background Matching** — a dedicated order book worker continuously processes each item market.
- **Real-Time Updates** — MagicOnion StreamingHub pushes order, inventory and trade changes directly to connected clients.
- **Live Market Data** — server streaming provides aggregated buy/sell volume across configurable price tiers.
- **Concurrent by Design** — Redis-backed reader/writer locks coordinate API requests and matching workers.
- **Distributed Connections** — Redis keeps track of active user connections and StreamingHub groups.
- **Transactional Trading** — PostgreSQL transactions keep money, inventory and orders consistent.
- **Game Inventory** — item stacks, unique items, weight/capacity limits and player balances are part of the trading flow.
- **Integration Tests** — real PostgreSQL and Redis instances through Testcontainers.

## 🧠 Matching

Each item has its own market worker.

```text
                         ┌──────────────┐
                         │    Client    │
                         └──────┬───────┘
                                │
                        MagicOnion / gRPC
                                │
                                ▼
                         ┌──────────────┐
                         │     API      │
                         │              │
                         │ Auctions     │
                         │ Inventory    │
                         │ Listings     │
                         │ OrderHub     │
                         └───┬──────┬───┘
                             │      │
                ┌────────────┘      └────────────┐
                ▼                                ▼
        ┌──────────────┐                  ┌──────────────┐
        │  PostgreSQL  │                  │    Redis     │
        │              │                  │              │
        │ Orders       │                  │ RW Locks     │
        │ Inventory    │                  │ Groups       │
        │ Items        │                  │ Connections  │
        └──────▲───────┘                  └──────▲───────┘
               │                                 │
               │                                 │
               └────────────┐       ┌────────────┘
                            │       │
                         ┌──┴───────┴──┐
                         │   Workers   │
                         │             │
                         │ Order Book  │
                         │ Matching    │
                         │ Events      │
                         └─────────────┘
```

The worker processes crossed buy and sell orders every second.

Orders are prioritized by price and creation time. Matching happens inside a PostgreSQL transaction while a Redis-backed distributed write lock prevents the same item market from being modified concurrently.

API operations use the same distributed locking infrastructure, so order creation, removal and background matching operate against a consistent market state.

Instead of loading the entire order book, the matching query selects only the part of the book required to satisfy the currently executable supply and demand.

## 📡 Real-Time API

Aucy uses **MagicOnion** on top of gRPC/HTTP2.

`OrderHub` provides a duplex connection between a player and the auction house:

```text
Create Lot ─────►
Create Bid ─────►
Remove Order ───►     OrderHub     ───► Order Created
Collect Order ──►                  ───► Item Sold
                                    ───► Item Bought
                                    ───► Inventory Changed
```

Redis-backed connection tracking allows user connections and multicast groups to be coordinated outside of a single process.

Market listings use server streaming separately, allowing clients to receive aggregated order-book volume without repeatedly requesting the whole market.

## 🧱 Project Structure

```text
Aucy/
├── API/            # Server entry point, auth and MagicOnion endpoints
├── Application/    # Auction, inventory and market logic
├── Contracts/      # Shared MagicOnion / gRPC contracts
├── Data/           # EF Core, PostgreSQL and persistence
├── Domain/         # Domain entities
├── Workers/        # Order book matching and background processing
└── Tests/          # Integration and trading simulation tests
```

## 🚀 Running

Start PostgreSQL and Redis:

```bash
docker compose up -d
```

Configure the database/Redis connections and JWT secret through environment variables or user secrets.

Then start the API:

```bash
dotnet run --project API/Aucy.API.csproj
```

And the matching workers:

```bash
dotnet run --project Workers/Aucy.Workers.csproj
```

Run tests with:

```bash
dotnet test
```
