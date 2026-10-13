# Aucy 💰

A real-time auction house backend for online games.

Lots go in. Bids go in. Background workers match them. Clients get the result immediately.

Built as a concurrent order book rather than a CRUD API with a `Bids` table.

## 🛠️ Tech Stack

[![.NET](https://img.shields.io/badge/10-512BD4?style=plastic&logo=dotnet&logoColor=white)](https://github.com/dotnet/runtime)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=plastic&logo=dotnet&logoColor=white)](https://github.com/dotnet/aspnetcore)
[![MagicOnion](https://img.shields.io/badge/MagicOnion-6F42C1?style=plastic&logo=github&logoColor=white)](https://github.com/Cysharp/MagicOnion)
[![gRPC](https://img.shields.io/badge/gRPC-2DA6B0?style=plastic&logo=google&logoColor=white)](https://github.com/grpc/grpc-dotnet)
[![MessagePack](https://img.shields.io/badge/MessagePack-111111?style=plastic&logo=github&logoColor=white)](https://github.com/MessagePack-CSharp/MessagePack-CSharp)

[![EF Core](https://img.shields.io/badge/EF_Core-512BD4?style=plastic&logo=dotnet&logoColor=white)](https://github.com/dotnet/efcore)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=plastic&logo=postgresql&logoColor=white)](https://github.com/postgres/postgres)
[![StackExchange.Redis](https://img.shields.io/badge/StackExchange.Redis-DC382D?style=plastic&logo=redis&logoColor=white)](https://github.com/StackExchange/StackExchange.Redis)
[![DistributedLock](https://img.shields.io/badge/DistributedLock-B7472A?style=plastic&logo=github&logoColor=white)](https://github.com/madelson/DistributedLock)

[![xUnit](https://img.shields.io/badge/xUnit-5E5E5E?style=plastic&logo=github&logoColor=white)](https://github.com/xunit/xunit)
[![Testcontainers](https://img.shields.io/badge/Testcontainers-2496ED?style=plastic&logo=docker&logoColor=white)](https://github.com/testcontainers/testcontainers-dotnet)
[![Serilog](https://img.shields.io/badge/Serilog-704880?style=plastic&logo=github&logoColor=white)](https://github.com/serilog/serilog)
[![Polly](https://img.shields.io/badge/Polly-2C3E50?style=plastic&logo=github&logoColor=white)](https://github.com/App-vNext/Polly)

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
