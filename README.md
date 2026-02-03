# Project: Distributed Cache like Redis
Purpose of this project is to learn how distributed cache server works. How they handle persistance and (for) crash recovery, traffic and replication.
Project has 2 parts a Client and a Server, this project supports simple cache commands, but explores in depth of what goes behind managing things at scale.

## TL;DR

- **Client**: Supports simple GET and SET commands in normal operation, and bulk operations handling 1000s of keys at once. Uses the RESP protocol for communication and TCP sockets to transfer messages to the server.
- **Server**: Performs GET and SET operations in-memory, persists write operations using RDB Snapshots and Append-Only Files (AOF), and restores data based on the preferred format. Handles bulk GET and SET operations to simulate real-world scale scenarios. Replication (master-slave architecture) is currently work in progress.

## Quick Start

set key with value, expiry time is optinal

```
CACHE Client:
+-------------------------------------------+
get [key] | set [key] [value] [seconds*] | batch-set [count] | batch-get [count] | status | exit
+-------------------------------------------+
>>  set abc 123 10
set abc 123 10
Connected to 127.0.0.1:9500
Response Received
OK
```


get key
```
>>  get abc
get abc
Response Received
123

// after 10 seconds

>>  get abc
get abc
Response Received
null
>>
```

batch-set command sets 1 to N keys in batch in parellel
```
>>  batch-set 10
batch-set 10
OK
OK
OK
OK
OK
OK
OK
OK
OK
OK
>>
```





## Archietecture
```
 Client                    Server
    │                         │
    │──── SYN ───────────────►│
    │◄─── SYN-ACK ────────────│
    │──── ACK ───────────────►│   ← TCP Handshake (Connected)
    │                         │
    │──── RESP Command ──────►│   ← Send (WriteAsync)
    │◄─── RESP Response ──────│   ← Receive (ReadAsync)
    │                         │
    │──── FIN ───────────────►│   ← Client Disconnects
    │◄─── FIN-ACK ────────────│   ← Server detects (bytesRead == 0)
    │                         │
```

### Understanding Connection Re-use to Save Time and Resources
Socket connections are established on the first request and persist for 5 seconds before timing out. If a subsequent request arrives within this time frame, the same connection is reused instead of creating a new one.

When the server disconnects, it sends a FIN packet to gracefully close the connection. The client checks whether the connection has been terminated before attempting to send data, and automatically reconnects if needed.

For bulk transfers, the same connection is reused across multiple batches. All commands are sent as an array and processed together on the server side. Since bulk transfer batch processing typically completes in milliseconds, requests almost always fall within the timeout window, allowing the connection to be reused efficiently across sequential batches.

```

Client                              Server                                    Storage
   ┌──────────────┐                  ┌──────────────────┐                   ┌─────────────────────┐
   │              │                  │                  │                   │                     │
   │   App/UI     │                  │  TCP Listener    │                   │   In-Memory Cache   │
   │              │                  │  (AcceptClient)  │                   │  ┌─────────────┐    │
   │              │                  │                  │                   │  │  Key-Value  │    │
   │  ┌────────┐  │   TCP Connect    │  ┌────────────┐  │                   │  │   Store     │    │
   │  │ TcpClient├─┼─────────────────►  │ TcpClient  │  │                   │  └──────┬──────┘    │
   │  └───┬────┘  │◄────────────────────┤ (per conn) │  │                   │         │           │
   │      │       │   TCP Connected  │  └─────┬──────┘  │                   │         │           │
   │      │       │                  │        │         │                   │         │           │
   │      ▼       │                  │        ▼         │                   │         ▼           │
   │  ┌────────┐  │                  │  ┌────────────┐  │                   │  ┌─────────────┐    │
   │  │ Stream │  │                  │  │   Stream   │  │                   │  │  Persistence│    │
   │  └───┬────┘  │                  │  └─────┬──────┘  │                   │  │   Manager   │    │
   │      │       │                  │        │         │                   │  └──────┬──────┘    │
   │      ▼       │                  │        ▼         │                   │         │           │
   │  ┌────────┐  │  RESP Serialize  │  ┌────────────┐  │                   │    ┌────┴────┐      │
   │  │ RESP   ├──┼─────────────────►   │ RESP       │  │                   │    │         │      │
   │  │        │  │  (Command)       │  │ Deserialize│  │                   │    ▼         ▼      │
   │  └────────┘  │                  │  └─────┬──────┘  │                   │ ┌───────┐ ┌──────┐  │
   │              │                  │        │         │                   │ │  RDB  │ │ AOF  │  │
   │              │                  │        ▼         │                   │ │       │ │      │  │
   │              │                  │  ┌────────────┐  │                   │ │Snapshot││Append│  │
   │              │                  │  │  Handler   ├──┼──────────────────►  │       │ │      │  │
   │              │                  │  │ (GET/SET)  │  │   Read/Write      │ │  .rdb │ │ .aof │  │
   │              │                  │  └─────┬──────┘  │◄──────────────────  └───────┘ └──────┘  │
   │              │                  │        │         │                   │                     │
   │  ┌────────┐  │  RESP Serialize  │        ▼         │                   │                     │
   │  │ RESP   │◄─┼───────────────────  ┌────────────┐  │                   │                     │
   │  │ Deser. │  │  (Response)      │  │   RESP     │  │                   │                     │
   │  └───┬────┘  │                  │  │ Serialize  │  │                   │                     │
   │      │       │                  │  └────────────┘  │                   │                     │
   │      ▼       │                  │                  │                   │                     │
   │  ┌────────┐  │                  │                  │                   │                     │
   │  │ Result │  │                  │                  │                   │                     │
   │  └────────┘  │                  │                  │                   │                     │
   └──────────────┘                  └──────────────────┘                   └─────────────────────┘
```

### Flow

```
  1. Client ──── TCP Connect ────────► Server (Listener accepts)
  2. Client ──── RESP Command ────────► Server (Deserialize → Handler)
  3. Handler ──── Read/Write ─────────► In-Memory Cache (Key-Value Store)
  4. Persistence Manager ─────────────► RDB Snapshot  (periodic full dump)
  5. Persistence Manager ─────────────► AOF Append    (every write logged)
  6. Server ──── RESP Response ───────► Client (Deserialize → Result)
  7. Repeat steps 2-6 until disconnected
```

#### Components
- RESP Library: Serializes and deserializes string commands to/from RESP protocol format for both requests and responses.
- Commands Manager: Parses string commands and converts them into executable operations.
- Storage Class: Handles in-memory key-value operations (GET, SET, etc.).
- Persistance Manager:  Writes data to the chosen persistence format (RDB or AOF) and restores server state on restart.


## Perisitence

```
  ┌─────────────────────────────────────────────────────────────┐
  │                    In-Memory Cache                          │
  │              { key1: val1, key2: val2, ... }                │
  └───────────────────┬─────────────────────┬───────────────────┘
                      │                     │
                      ▼                     ▼
  ┌───────────────────────┐   ┌─────────────────────────────┐
  │        RDB            │   │           AOF               │
  │   (Snapshot)          │   │     (Append Only File)      │
  │                       │   │                             │
  │  • Full dump of data  │   │  • Logs every write op      │
  │  • Runs periodically  │   │  • Appends on every SET     │
  │  • History .rdb file  │   │  • Single .aof file         │
  │  • Fast recovery      │   │  • Minimal data loss        │
  │  • Smaller file size  │   │  • Larger file over time    │
  │                       │   │                             │
  │  save 900 1           │   │  appendonly yes             │
  │  save 300 10          │   │                             │
  │  save 60 10000        │   │                             │
  └───────────────────────┘   └─────────────────────────────┘
```


### Snapshot
The current state of memory is preserved by a background process running at configurable intervals (default: 5 minutes). A copy of the current memory state is created for snapshot generation, ensuring the process doesn't interfere with ongoing operations.

Each snapshot contains Key, Value, and Expiry data serialized in binary format, along with a checksum (for simplicity, the record count) to validate file integrity. The checksum is verified every time a file is read to ensure the contents haven't been corrupted.

The number of snapshots retained is configurable, allowing you to keep the last N snapshots in history. When recovery mode is set to RDB Snapshot, the latest snapshot is used to restore memory state on restart.
Snapshots are automatically deleted once the retention threshold is exceeded, following FIFO order (oldest first).

**_One Important Note_**: Redis forks its process to create snapshot copies an operation not supported on Windows, which is why Redis doesn't run natively on Windows.
To work around this limitation, this project clones the memory state instead and performs snapshot operations on the copy, achieving similar non-blocking behavior without requiring process forking.


### Append Only File

Each data-manipulating operation is appended to the end of a file via a file stream. This mode is recommended when data loss is unacceptable, as it eliminates the risk of losing data between snapshot intervals. Both AOF and RDB modes can run concurrently for maximum durability.

The AOF file grows continuously until the stream is closed or the program terminates.

When recovery mode is set to AOF, the file is read sequentially and each RESP command is executed to restore memory state. Since AOF is a human-readable log file, restoration time increases with file size if the file is very large, it can take considerable time for the server to fully restore state and begin listening for connections.


## Configuration

file: `appsettings.json` 

`DefaultExpiryTime` : 60 //Default expiry time of each key in seconds

`PersistanceMode`: // NONE, RDB, AOF, RDB_AOF 

`RDBSnapshotInterval `: 5 // in minutes

`RDBSnapshotVersionCount` 5 // version of RDB history

`RDBSnapshotDirectory`: // Directory to save RDB snapshot

`AOFSnapshotDirectory`: // directory to save AOF 

`RecoveryMode `: // NONE, AOF, RDB

## Project Status

Features:
1. Master-Replica architecture
2. RESP protocol communication
3. Crash Recovery by AOf or RDB Files
4. Bulk Query
5. Persistent connection for frequent Queries
6. Data Persistence in both modes just like Redis: append-only file and RDB file
7. Client interface to interact with Redis
8. Replication to secondary node (WIP)


## Future WIP

### Replication Achiectecture

Replication flow is currently under development. The architecture works as follows: two server processes start one configured as master and the other as replica via their respective config files.

When both instances start, if the master is configured to accept replicas, it listens on a specified port. The replica initiates a connection to the master upon startup.

The handshake involves a password exchange where the master validates the replica's credentials. Once authorized, the replica is registered as a replication instance.

After the handshake, the replica syncs its data from the master's latest RDB snapshot. The master maintains a replication buffer to track commands sent to the replica, while the replica keeps an acknowledgment buffer to confirm receipt.

The replica can request either a full sync (complete state transfer) or partial sync (incremental updates) based on its needs. During synchronization, an array of commands is streamed from master to replica to achieve state parity.

The master also forwards all SET-related commands to the replica through the replication buffer in real-time. A configurable heartbeat mechanism monitors replica health — if the replica doesn't respond within the timeout window, it's marked as disconnected.

If the master goes down, the replica can promote itself to master (failover).



   


