# Remaining issues in the DataField42 server

Line references are against [DataField42Server.py](DataField42%20Server/DataField42Server.py), in severity order. The last section lists things that look like bugs but cannot currently happen.

## 1. The auto-update path is unauthenticated remote code execution

[`update`](DataField42%20Server/DataField42Server.py:459) opens a plain TCP socket to `bf1942.eu:28901`, reads a Python script, and [`update_and_restart_script`](DataField42%20Server/DataField42Server.py:293) writes it straight over `sys.argv[0]` and executes it:

```python
with open(sys.argv[0], 'wb') as file:
    file.write(new_script_bytes)
```

No TLS, no signature, no checksum, no authentication of the peer. The heartbeat thread polls every 60 seconds and calls `update()` as soon as the master reports a higher version, so the window is permanent rather than operator-initiated. Anyone who can answer as that host — DNS spoofing, a hostile network between a server and the master, or compromise of the master itself — runs arbitrary code as the server user on every deployed server. The same code path already shells out to `sudo systemctl`.

## 2. No receive ever passes a timeout, so a silent client parks a thread forever

`receive_bytes` accepts a `timeout`, but none of the call sites supply one — lines 336, 456, 462, 463, 549 and 568 all take the `None` default. With `timeout=None` the wait is indefinite:

```python
ready, _, _ = select.select([self.socket], [], [])
```

`ThreadingTCPServer.daemon_threads` is `False`, so every client that connects and then stops talking costs one non-daemon thread for the life of the process. A dropped connection that never sends a FIN is enough; the client does not need to be malicious.

The error path at [line 552](DataField42%20Server/DataField42Server.py:552) inflicts this on the server deliberately:

```python
communication.send(f"no 0 0")
```

`send` defaults to `await_acknowledgement=True`, so after telling the client about a protocol error the server blocks forever waiting for that same client to acknowledge, and the `raise Exception` on the next line is never reached. Any client triggers it by replying to the file list with the wrong number of tokens.

## 3. Every file is read into memory before it is sent

[`download_files`](DataField42%20Server/DataField42Server.py:563):

```python
with open(file[2], "rb") as f:
    file_bytes = f.read()
```

The base archives run to hundreds of megabytes, and `ThreadingTCPServer` means concurrent clients multiply the resident size. [`send_file`](DataField42%20Server/DataField42Server.py:279) already streams in 8 KB chunks and is never called from anywhere.

## 4. An unguarded `smart_path_join` result crashes the request

[Line 476](DataField42%20Server/DataField42Server.py:476):

```python
all_relevant_mod_names = get_relevant_mod_names(smart_path_join(game_directory, f"mods/{mod_name}/init.con"))
```

`smart_path_join` returns `None` when the path does not exist, so a mod directory with no `init.con` reaches `open(None)` → `TypeError`. This is the only call site that does not check the result; every other one does. It aborts the one request rather than the server.

`mod_name` is client-supplied but is checked against `os.listdir` on the line above, so it cannot be used for path traversal.

## 5. The checksum lock is held across the hash

[`get_checksum`](DataField42%20Server/DataField42Server.py:188) takes `self.lock` for the repository lookup *and* the file read and hash. `download_files` calls it once per file at [line 539](DataField42%20Server/DataField42Server.py:539), so the first client to request a mod hashes every file in it while holding the lock, and concurrent clients queue behind that. The hashing is what holds the lock; `ChecksumRepository` already guards the records themselves with its own `RLock`.

Issue 6 makes this worse, because nothing warms the repository in the background.

## 6. The new-files watchdog never scans anything

[`new_files_watchdog`](DataField42%20Server/DataField42Server.py:206) walks `dataField42_server.game_directory`, which is `""` — the server is constructed as `DataField42Server("")` at line 573 and the attribute is never assigned anywhere else. `os.walk("")` yields nothing, so `all_files` is always empty and the thread is a do-nothing loop sleeping one second forever.

Every other path in the server goes through `smart_path_join`, which compensates with `"." if base_dir == ""` ([line 312](DataField42%20Server/DataField42Server.py:312)). `os.walk` has no such fallback, and this is the only place that passes `game_directory` to it raw.

So checksums are only ever computed on the request path, under the lock described in issue 5. If the walk did run, the loop would be a second problem: a full walk plus a linear `find_checksum` per file every second is O(files × records), and the repository is never pruned, so it grows with every file whose mtime changes.

## Minor

- `Version.__init__` unpacks `version_numbers[:-1]` into exactly three names ([line 19](DataField42%20Server/DataField42Server.py:19)), so any version string that is not 4-part raises `ValueError`. For a string from the master this is caught by the heartbeat's `except` and degrades to "updates silently stop". `major`, `minor` and `patch` are never read anywhere.
- `handle` does `arguments.pop(0)` without checking for an empty request ([line 337](DataField42%20Server/DataField42Server.py:337)); a zero-length or whitespace-only request raises `IndexError`.
- `total_size` is accumulated over all files at [line 534](DataField42%20Server/DataField42Server.py:534) and then unconditionally overwritten at [line 559](DataField42%20Server/DataField42Server.py:559) with the sum over `files_to_send`. The first accumulation is dead.
- `download_files`' `key_value_pair` is annotated `dict[str, str] | None` but [line 344](DataField42%20Server/DataField42Server.py:344) always passes a list, and the parameter is unused.

## Not currently reachable

`receive_bytes` reduces its own timeout without a floor ([line 241](DataField42%20Server/DataField42Server.py:241)):

```python
timeout -= elapsed_time
```

If one iteration's elapsed time exceeds what is left, the next `select.select(..., negative)` raises `ValueError: timeout must be non-negative` instead of the intended `TimeoutError`. This cannot happen today because no call site passes a timeout at all (issue 2), so the branch never executes. It becomes live as soon as timeouts are used.
