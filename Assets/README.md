# Camp, Guard Duty, and Social Systems

These scripts are intentionally independent of a networking package. They run on the local player object and expose public methods and UnityEvents, so they can be connected to Netcode for GameObjects, Photon Fusion, or another multiplayer layer without coupling gameplay code to one vendor.

For a networked build, only the owning client should read movement and shortcut input. Send `SetGuardDuty`, `Laugh`, `Speak`, and `WaveBye` through the network authority layer, validate camp membership on the server, and replicate the resulting state/animation to observers. Do not trust a client-provided recovery amount or guard state.

## 1. Player prefab

1. Create a player prefab with a root `CharacterController`, `NetworkObject`, `PlayerController`, `NetworkTransform`, `PlayerSurvival`, `PlayerCampGuard`, `PlayerSocialInteractions`, `NetworkPlayer`, and `NetworkPlayerCombat`. `PlayerController`, `PlayerSurvival`, and `PlayerSocialInteractions` automatically require the NetworkObject; `PlayerController` also requires NetworkTransform.
2. Assign the player Animator to `PlayerCampGuard` and `PlayerSocialInteractions`.
3. Add Animator parameters named `IsOnGuardDuty` (Bool), `Laugh` (Trigger), `Speak` (Trigger), and `Wave` (Trigger). Add transitions from the locomotion state to the corresponding action states and back using exit time.
4. Create a child object such as `GuardIndicator` with a visible mesh, light, or world-space Canvas icon. Assign it to `PlayerCampGuard.guardIndicator` and disable the object initially.
5. If the player uses the common Joystick Pack asset, assign its `VariableJoystick` to `PlayerController.mobileJoystick`. If another joystick package is used, adapt that one field to the package's joystick type.

## 2. Camp scene setup

1. Create an empty object named `CampArea_Main` and add a Box Collider, Sphere Collider, or custom trigger volume. Enable `Is Trigger`.
2. Add `CampArea` to the same object. Its `Reset` method makes the collider a trigger; verify this manually in the Inspector.
3. Create the `CampArea` tag in **Edit > Project Settings > Tags and Layers**, then assign that tag to every camp trigger object. The component is the gameplay authority; the tag makes the area easy to identify in tools, minimaps, and scene queries.
4. Set `healthRecoveryPerSecond` and `energyRecoveryPerSecond`. Recovery runs only while the player is inside the trigger and not on guard duty.
5. Ensure the player has a non-trigger collider or CharacterController. At least one object in the trigger interaction must have a Rigidbody or CharacterController for `OnTriggerEnter` and `OnTriggerExit` to fire.

## 3. Health and energy UI

1. In a Canvas, create two Slider objects named `HealthSlider` and `EnergySlider`.
2. Set each Slider's Min Value to `0`, Max Value to `1`, and disable whole-number rounding. Set Fill Rect references and choose different colors for health and energy.
3. Assign the sliders to `PlayerSurvival.healthSlider` and `PlayerSurvival.energySlider` on the player prefab. The script writes normalized values (`0` to `1`).
4. For mobile, anchor the bars to the top safe area and leave space for the joystick and action controls. A world-space guard icon is useful for nearby teammates; the assigned `GuardIndicator` provides that state locally.

## 4. Guard duty controls

1. Create a mobile Button labelled `Guard` and add the player `PlayerCampGuard` object to its On Click event.
2. Select `PlayerCampGuard.ToggleGuardDuty` as the callback. The method refuses to activate outside a camp.
3. Use `guardDutyStarted` and `guardDutyStopped` to enable/disable a status label, play an alert sound, or update a team HUD.
4. On PC, the default shortcut is `G`. Change `guardShortcut` in the Inspector if it conflicts with another action. Guard duty disables local movement and keeps the guard indicator/Animator state active.

## 5. Social controls

1. Add three mobile Buttons to the action HUD: Laugh, Speak, and Bye.
2. Bind their On Click events to `PlayerSocialInteractions.Laugh`, `.Speak`, and `.WaveBye`.
3. The default PC shortcuts are `1`, `2`, and `3`. Change the serialized keys if needed.
4. Connect `voiceLinePerformed` to an AudioSource or voice-line manager. `PlayerSocialInteractions` replicates the selected emote ID through a `ServerRpc` and `ClientRpc`, so every client plays the matching local animation/audio. Only the owning client reads social keyboard input.

## 6. Cross-platform input

`PlayerController` uses Unity's legacy `Horizontal` and `Vertical` axes for WASD/arrow keys and overrides them with the assigned virtual joystick when its values are outside the dead zone. Sprint uses Left Shift on PC. Replace the input reads with the Unity Input System action callbacks if the project has migrated to the new system; keep the public gameplay methods unchanged so the UI wiring remains the same.

## Included scripts

- `PlayerController.cs`: movement, gravity, joystick fallback, and movement lock.
- `PlayerSurvival.cs`: health, energy drain, recovery, damage, and normalized sliders.
- `CampArea.cs`: trigger zone and configurable recovery rates.
- `PlayerCampGuard.cs`: camp membership, rest/guard state, indicator, and events.
- `PlayerSocialInteractions.cs`: keyboard and UI-callable emotes.
- `PlayerSocialInteractions.cs`: owner-only input with networked laugh, speak, and wave emotes.

## 7. NGO package and NetworkManager

This integration uses **Netcode for GameObjects (NGO)** with Unity Transport.

1. Install `com.unity.netcode.gameobjects` and ensure the compatible Unity Transport package is installed through Package Manager.
2. Create a persistent `NetworkManager` object and add `NetworkManager` plus `UnityTransport`.
3. Create a player prefab from the existing player object. Add `NetworkObject`, `NetworkTransform`, `NetworkPlayer`, and `NetworkPlayerCombat` to the root. Keep the existing `CharacterController`, `PlayerController`, `PlayerSurvival`, and `PlayerCampGuard` on that same root. The required components are added automatically by the scripts, but verify there is only one NetworkObject and one NetworkTransform.
4. Register the prefab in **NetworkManager > Network Prefabs**, and assign it as **Player Prefab**. The prefab must have exactly one root `NetworkObject`.
5. Configure `NetworkTransform` to use client authority/owner authority for movement if that option is available in your NGO version. If your version is server-authoritative only, use NGO's supported `ClientNetworkTransform` sample implementation or move input simulation to server RPCs. Do not let both client and server independently write the transform.
6. Add `NetworkMatchManager` and `NetworkObject` to a scene object. Register that object as a scene NetworkObject and ensure it exists in the online scene before players connect.

`PlayerController` only reads movement input for the owning client and uses `NetworkTransform` to replicate its transform. `PlayerSurvival.NetworkHealth` and `PlayerSurvival.NetworkEnergy` are server-written NetworkVariables. `NetworkPlayer.NetworkOnGuardDuty`, `TeamId`, and `DisplayName` provide the remaining network state. The server is the only authority allowed to accept damage, drain energy, recover players, or enable guard duty.

## 8. Team camp spawning

1. On the scene object containing `NetworkMatchManager`, expand **Team Camps** and create one entry per team.
2. Give each entry a stable `teamId` and assign its child spawn-point Transforms. Place spawn points inside that team's camp and rotate them toward the play area.
3. Set `minimumPlayers`, `waitingDuration`, `battleDuration`, and `safeZoneDuration`. The manager enters `BattleStarted` when the minimum is reached or the waiting timer expires, then enters `SafeZoneShrinking`, and finally `MatchEnded`.
4. At registration and again when battle starts, each player is assigned to a camp using round-robin team assignment. Replace `RegisterPlayer` assignment with authenticated party/team data if matchmaking already knows a player's team.
5. The `SafeZoneRadius` NetworkVariable is replicated for a zone visual or damage system. `NetworkMatchManager` does not apply zone damage itself; have a server-only zone component consume that value and damage players outside the radius.

## 9. Networked guard, shooting, and HUD buttons

1. Bind the mobile Guard button to `NetworkPlayer.ToggleGuardDutyNetworked`, not the local `PlayerCampGuard.ToggleGuardDuty`. The server validates that the player is inside a camp.
2. Add a mobile Fire button and bind it to `NetworkPlayerCombat.Fire`. Assign the owner camera, muzzle, hit layers, damage, and range. The server performs the raycast and applies damage; clients only request a shot and receive the fire animation.
3. Subscribe a match HUD to `NetworkMatchManager.CurrentState`, `StateTimeRemaining`, and `SafeZoneRadius` to show Waiting, Battle Started, Safe Zone Shrinking, and Match Ended. NetworkVariables invoke their change callbacks on clients, so the HUD should not run its own match timer.
4. For teammate labels, read `NetworkPlayer.TeamId` and `DisplayName` from each spawned player. Set `DisplayName.Value` on the server from authenticated player data rather than trusting a client string.

## 10. Connection and platform testing

1. Start a host and one or more clients with `NetworkManager.StartHost` and `NetworkManager.StartClient` from your connection UI. Use Unity Transport Relay for internet play; use direct address/port only for controlled tests.
2. Test one Windows build and two device builds with the same protocol/version. Confirm that every client sees remote movement, guard indicator changes, fire animation, health changes, team IDs, and camp spawn positions.
3. Test disconnects and late joins. The manager's current NetworkVariables replicate to late joiners; production matchmaking should reject joins after `BattleStarted` or implement a spectator flow.

## Additional network scripts

- `NetworkPlayer.cs`: NGO player initialization, owner input routing, replicated survival/guard state, and team spawn placement.
- `NetworkMatchManager.cs`: server match phases, timers, safe-zone radius, team assignment, and camp spawning.
- `NetworkPlayerCombat.cs`: owner fire request, server raycast/damage validation, and client fire animation.

## 11. Game HUD setup

1. In the gameplay scene, create a `Screen Space - Overlay` Canvas with a `GameHUD` component. Add a top status row containing `HealthSlider`, `EnergySlider`, `TeamCampStatusText`, and `GuardStatusText`.
2. Set both sliders to Min `0`, Max `1`, disable whole-number mode, and assign their Fill Rects. `GameHUD` reads the normalized values from the local `PlayerSurvival` NetworkVariables.
3. Add a `MatchStatusText` near the top center. It displays Waiting for players, Battle started, Safe zone shrinking, or Match ended from `NetworkMatchManager.CurrentState`.
4. Add a bottom action row with Buttons named `EatButton`, `GuardButton`, `LaughButton`, `SpeakButton`, and `ByeButton`. Assign them to the matching `GameHUD` fields. The script registers their click handlers automatically; do not also add duplicate OnClick entries in the Inspector.
5. Set the Eat button's `foodAmount` to the desired gameplay value. The call is routed through `PlayerSurvival`'s server RPC, while Guard calls `NetworkPlayer.ToggleGuardDutyNetworked` for server validation.
6. Anchor the status row to the top safe area and the action row to the bottom safe area. Keep the joystick on the lower-left and action buttons on the lower-right, with at least 44 points of touch target size for iOS and Android.
7. The HUD binds to `NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()` after the local player is spawned, so it can exist before connection. Keep it in the gameplay scene or in a persistent UI scene loaded for every client.

## 12. Main menu and lobby setup

1. Create a Bootstrap/Menu scene with a `NetworkManager` and `UnityTransport`. Enable **Scene Management** on the NetworkManager and add both the menu scene and the gameplay scene to **File > Build Settings**.
2. Add a `MainMenuManager` to a menu root. Assign the NetworkManager, UnityTransport, gameplay scene name (for example `Gameplay`), and a menu Canvas.
3. Create `MenuPanel` with address and port `InputField`s plus Host Game, Join Game, and Exit Buttons. Assign these references to `MainMenuManager`. The default address is `127.0.0.1` and default port is `7777`.
4. Create `LobbyPanel` with a status `Text`, initially disabled. When hosting or joining, the manager hides the menu and shows the lobby. The host loads the gameplay scene through NGO SceneManager when `NetworkMatchManager` reaches `BattleStarted`; clients follow the network scene transition.
5. Put `NetworkMatchManager` and `NetworkObject` in the Bootstrap/Menu scene and register it as a scene NetworkObject. The manager calls `DontDestroyOnLoad`, so it continues publishing match state after the gameplay scene loads.
6. For a direct LAN/PC test, enter the host machine's LAN IPv4 address on clients and use the same port. Do not use `127.0.0.1` on a second device because it points to that device itself.
7. For internet play, configure Unity Relay before calling `HostGame` or `JoinGame`, using the Relay package's allocation data to call `UnityTransport.SetRelayServerData`. Keep `MainMenuManager`'s `StartHost`/`StartClient` flow unchanged after the transport has been configured.

## 13. UI and connection notes

- `GameHUD.cs` is local presentation only. It never writes health, energy, or guard NetworkVariables directly.
- `MainMenuManager.cs` uses `InputField` and `Text` from `UnityEngine.UI`; replace them with TMP components if the project uses TextMeshPro, changing only the field types.
- The host owns match state and scene loading. Clients should not call `NetworkSceneManager.LoadScene` directly.
- Test host plus client on Windows first, then test Android and iOS with the same NetworkManager prefab, transport settings, and gameplay scene list.