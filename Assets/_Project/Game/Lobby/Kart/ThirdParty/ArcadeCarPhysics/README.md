# ArcadeCarPhysics (third-party)

- Source: https://github.com/SergeyMakeev/ArcadeCarPhysics
- Commit: d3e519eeeaa97cf36f8197d25d07ccebb73e4279
- File taken: `Assets/Scripts/ArcadeCar.cs` (the vehicle controller only; no demo scene, camera or assets)
- Licence: MIT, Copyright (c) 2018 Sergey Makeev (see `LICENSE.txt`)

Raycast suspension on a Rigidbody, speed-curve engine, normalised lateral tyre friction, slippery tyres
after the handbrake (drift), Ackermann steering, stabiliser bars, downforce, in-flight stabilisation.

## Changes for Hide and Seek (marked `[Hide and Seek]` in the code)

- Controls come from public fields (`inputVertical`, `inputHorizontal`, `inputHandBrake`) set by the game
  every frame instead of the legacy Input Manager (the project uses the Input System only).
- The built-in R reset is disabled (the game puts a kart back in its parking slot).
- Wheel raycasts take a layer mask (`raycastMask`) and ignore triggers.
- The debug text (`OnGUI`) is off unless `showDebugGui` is set; its style is created lazily.
- `Rigidbody.velocity` renamed to `Rigidbody.linearVelocity` (Unity 6).
- Drift model hooks (2026-11-26, used by `KartDrift.cs` through `KartPhysics.cs`): a per-axle lateral grip limit
  (`gripLimit` x suspension load x `gripScale`; 0 keeps the original unlimited friction), a `beforeForces` callback
  each physics step, `externalSteering` (the player's steering goes to `playerSteerAngle`, the game sets the front
  axle's angle: countersteer assist), `engineScale` / `brakeScale`, `handBrakeFront` (false = rear-only handbrake),
  and the after-flight slippery time scaled by the time in the air.

The tuning for the go-karts (mass, axles, curves) is applied from `KartTuning.cs`.
