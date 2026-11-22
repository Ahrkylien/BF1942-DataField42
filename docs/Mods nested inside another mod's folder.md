## Mods nested inside another mod's folder

### What a mod can declare
A mod's `init.con` lists the folders the game reads, in priority order, with `game.addModPath`.
Usually each path is a top-level mod folder:

```text
game.setCustomGameName XWWII
game.addModPath Mods/XWWII/XWWIIBlood/
game.addModPath Mods/XWWII/
game.addModPath Mods/BF1942/
```

The first path, `Mods/XWWII/XWWIIBlood/`, is a folder **inside** the `XWWII` mod, not a mod folder of
its own. It layers an optional add-on over the mod.

### What goes wrong
- **The server does not see `XWWIIBlood` as a valid mod.** The mod scan (`ModListJob`) lists the
  directories directly under `mods/` and records each mod's `game.addModPath` entries as its
  dependencies. A dependency is resolved to a mod by the last segment of its path, here
  `XWWIIBlood`, and there is no such folder under `mods/`, so it matches nothing. It is also never
  scanned for maps, since it is not a top-level folder.
- **The DataField42 client does not allow this location for a mod.** It expects every mod to be a
  top-level folder under `Mods/`.

### What needs deciding
- How a nested folder is identified: by its full path (`XWWII/XWWIIBlood`) rather than the last
  segment.
- Whether it is a mod in its own right, with its own maps and archives, or part of the mod that
  contains it.
- What the client has to accept to download, store and list such a folder.
