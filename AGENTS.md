# Project guidance

- This is a Unity **6000.3.8f1** / FishNet multiplayer capstone.
- Inspect current code and serialized prefab/scene references before making assumptions.
- Preserve working multiplayer architecture; avoid unrelated refactors.
- Movement uses client-authoritative NetworkTransform. Do not change authority or introduce prediction without an explicit task.
- Preserve server authority for combat, health, currency, boosts, respawning, scores, and match lifecycle.
- Gameplay validation must account for server-approved boosts and lifecycle transitions. Never use client-provided values as authoritative limits.
- Avoid modifying FishNet or other vendor code unless explicitly required.
- Preserve Unity asset GUIDs, required `.meta` files, and prefab inheritance.
- Exclude development-only cheat/test controls from release builds.
- Preserve unrelated work; do not discard untracked files.
- Report checks actually run separately from pending Unity import, FishNet processing, build, and multiplayer runtime tests.
- For anti-cheat tasks, consult `Docs/AntiCheatHandoff.md` and `Docs/MovementObserver.md`, then verify relevant details against current code.
