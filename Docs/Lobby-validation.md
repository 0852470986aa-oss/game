# Lobby phase 1

Implemented in `Assets/Scripts/Managers/LobbyManager.cs`. The room browser and waiting-room UI are built at runtime, using the existing LobbyScene manager and sprites. No scene regeneration is required. Inventory remains a separate later phase.

## Implemented behavior

- Room browser: create, quick match, join by code, incremental room-list cache, room population and map, full-room disabled buttons, empty and error messages.
- Waiting room: two player cards, host/self labels, ship and skill previews, readiness, copy code, ping, and leave.
- Three map cards with preview, cover/hazard descriptions and selected state. Only the host can change the room map.
- Readiness is tied to both map index and selection revision, so selecting A → B → A does not restore stale readiness.
- Only the host starts a battle, after checking two active, loaded, ready players. Room start is acknowledged by the server before loading SampleScene; readiness is checked again on acknowledgement.
- Host transfer refreshes roles and clears local readiness. Inactive players cannot satisfy the start conditions.
- Profile loading gates matchmaking and inventory. Retry ignores callbacks from earlier load attempts.
- Unexpected lobby disconnect: retain the actor slot for 60 seconds; attempt reconnect/rejoin for up to 50 seconds. Explicit Leave uses `LeaveRoom(false)` to release the slot immediately. Expiry/failure returns to the menu with a reconnect action.
- Room browser and waiting-room surfaces fit landscape/tablet aspect ratios inside Screen.safeArea.

Room recovery is for the waiting room, not recovery of an ongoing gameplay match. Map descriptions correspond to the current fixed obstacle generation and MapHazardManager.

## Local verification

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`
- Unity menu: **Battlefield → Lobby → Validate and Render Preview**
- This command opens an isolated preview scene, checks required controls and sprites, runs seven readiness regression checks, and renders both panels at 1280×720, 1920×1080, 2340×1080, and 1024×768.
- Output: `Library/LobbyValidation/report.txt` and preview PNGs in the same folder. It does not save over the current scene.
- Preview pilot names/readiness are sample data for visual review, not evidence of a networked session.

## Two-client acceptance test (not yet executed)

Use two clients built from the same version, with different player identities.

1. Host creates a room; guest joins using the copied code. Both see the same room, player ships/skills, and selected map.
2. Guest cannot change the map or start. One ready player is insufficient; two ready players enable the host's Start.
3. Change maps while ready, including A → B → A. Both must confirm readiness again.
4. Try rapid Ready, Join and Create clicks. No duplicate room transition or stale selection should occur.
5. Add/remove another room while browsing. Existing unrelated entries remain.
6. Host leaves before starting. Guest becomes host, the old slot clears, and a new guest can join.
7. Disable a waiting client's network and restore it within the recovery window. The slot shows reconnecting; Start is disabled; the same client returns and confirms Ready again.
8. Let recovery expire. Show failure and allow reconnect/new matchmaking. Explicit Leave must not reserve a slot for 60 seconds.
9. Start a battle on each of the three maps. Both clients load SampleScene with only the selected layout and its hazards.
10. On a phone, check notch/safe-area padding, keyboard entry, code copy/paste, scroll list and button touch targets.

The local compilation, preview renders and readiness checks do not replace these network/device acceptance tests.
