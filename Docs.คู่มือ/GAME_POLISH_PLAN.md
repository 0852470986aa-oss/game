# Playtest polish backlog — 2026-09-24

## Current requests

- [ ] How to Play: runtime English copy updated to avoid missing Thai glyphs; verify layout on device. Thai localization still needs a suitable font and rendering check.
- [ ] Prism swamp: replace the low-resolution pool artwork with a new user-provided image; preserve visible-area trigger alignment.
- [ ] Currency: add a coin icon consistently to lobby and hangar once artwork is selected.
- [ ] Audio: increase perceived loudness of the generated music/effects with headroom; preserve volume sliders and mute. Check overlapping effects for clipping.
- [ ] In-match settings: move Exit inside the settings panel; require explicit confirmation. Opening settings must not pause the multiplayer match.
- [ ] Login: improve typography, spacing and button/frame styling using existing project artwork; preserve authentication behavior.
- [ ] Fire control: add a saved aiming/drag sensitivity setting shared by lobby settings and in-match settings; verify touch responsiveness and avoid changing projectile speed.
- [ ] Post-match: add an explicit return-to-the-same-room action, reset ready/start state, handle missing rooms or absent opponents gracefully.

## Next phase — planning only, do not implement yet

- Game profile.
- Selectable match duration.
- Match history UI.

## Verification

Compile each implementation batch. Device visual/audio testing and two-client room-flow testing remain required; compile success alone does not verify these.
