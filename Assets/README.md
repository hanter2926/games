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
3. The default PC shortcuts are `L` for Laugh, `V` for Speak, and `B` for Wave Bye. Change the serialized keys if needed.
4. Connect `voiceLinePerformed` to an AudioSource or voice-line manager. `PlayerSocialInteractions` replicates the selected emote ID through a `ServerRpc` and `ClientRpc`, so every client plays the matching local animation/audio. Only the owning client reads social keyboard input.
5. Bind `chatPopupPerformed` to a speech-bubble or status-text method that accepts one `string` argument. The event receives the configured laugh, speak, or wave message on every client.

## 6. Cross-platform input

`PlayerController` uses Unity's legacy `Horizontal` and `Vertical` axes for WASD/arrow keys and overrides them with the assigned virtual joystick when its values are outside the dead zone. Sprint uses Left Shift on PC. Replace the input reads with the Unity Input System action callbacks if the project has migrated to the new system; keep the public gameplay methods unchanged so the UI wiring remains the same.

`PlayerController` exposes `maxStamina`, `sprintStaminaDrainPerSecond`, `staminaRecoveryPerSecond`, and `exhaustionRecoveryThreshold`. Assign a normalized Slider (`Min Value = 0`, `Max Value = 1`) to `staminaSlider`. Stamina drains only while sprinting with movement input, recovers while walking or standing still, and blocks sprinting until the recovery threshold is reached.

## Included scripts

- `PlayerProgression.cs`: server-authoritative XP, levels, ranks, kills, deliveries, matches, and survival time.
- `KillFeedUI.cs`: timed elimination feed driven by the replicated match message.
- `MainMenuUIController.cs`: profile dashboard, mode/map controls, settings, store/inventory events, and Play/Host/Join UI.
- `NetworkVehicle.cs`: server-authoritative standard and Delivery Boy vehicle driving.
- `PlayerInventory.cs`: synchronized stack-based food, ingredients, medical, weapon, ammunition, and supply slots.
- `DynamicWeatherManager.cs`: synchronized rain/fog/storm effects and survival exposure.
- `MatchResultsUI.cs`: replicated winner, MVP, kills, survival, and delivery scoreboard.

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
3. Set `minimumPlayers` and `waitingDuration`. Match timing is enforced by `NetworkMatchManager`: 720 seconds of BattleStarted followed by 600 seconds of SafeZoneShrinking, for exactly 1,320 seconds (22 minutes) total. The manager then enters `MatchEnded`.
4. At registration and again when battle starts, each player is assigned to a camp using round-robin team assignment. Replace `RegisterPlayer` assignment with authenticated party/team data if matchmaking already knows a player's team.
5. The `SafeZoneRadius` NetworkVariable is replicated for a zone visual or damage system. `NetworkMatchManager` does not apply zone damage itself; have a server-only zone component consume that value and damage players outside the radius.

## 9. Networked guard, shooting, and HUD buttons

1. Bind the mobile Guard button to `NetworkPlayer.ToggleGuardDutyNetworked`, not the local `PlayerCampGuard.ToggleGuardDuty`. The server validates that the player is inside a camp.
2. Add a mobile Fire button and bind it to `NetworkPlayerCombat.Fire`. Assign the owner camera, muzzle, hit layers, damage, and range. The server performs the raycast and applies damage; clients only request a shot and receive the fire animation.
3. Subscribe a match HUD to `NetworkMatchManager.CurrentState`, `StateTimeRemaining`, and `SafeZoneRadius` to show Waiting, Battle Started, Safe Zone Shrinking, and Match Ended. NetworkVariables invoke their change callbacks on clients, so the HUD should not run its own match timer.
4. For teammate labels, read `NetworkPlayer.TeamId` and `DisplayName` from each spawned player. Set `DisplayName.Value` on the server from authenticated player data rather than trusting a client string.

## 9a. Safe zone, hit feedback, and guard alerts

1. Add `SafeZoneManager` and `NetworkObject` to a scene object that persists in the battle scene. Assign the persistent `NetworkMatchManager` and an optional flat cylinder or ring Transform to `zoneVisual`.
2. Set `zoneDamagePerSecond`, `damageTickInterval`, and `visualBaseDiameter`. The visual should have a 1-unit diameter when `visualBaseDiameter` is `1`; the script scales its X/Z size to the replicated `SafeZoneRadius`.
3. Place the SafeZoneManager object's position at the safe-zone center. The server damages every spawned `NetworkPlayer` outside the horizontal radius during `BattleStarted` and `SafeZoneShrinking`.
4. On `NetworkPlayerCombat`, assign the weapon muzzle, hit layers, `muzzleFlash` ParticleSystem, and optional `hitMarker` UI object. `hitConfirmed` can play a sound or animate a crosshair. Hit effects are sent only to the shooter after the server confirms a target hit.
5. On `PlayerCampGuard`, set `guardAlertRadius` and `guardAlertLayer`. Connect `guardAlertRaised` to a warning label, siren, or team HUD and `guardAlertCleared` to hide/stop it. The server compares nearby players' `TeamId` values, and `NetworkAlertActive` replicates the alert state.

## 10. Connection and platform testing

1. Start a host and one or more clients with `NetworkManager.StartHost` and `NetworkManager.StartClient` from your connection UI. Use Unity Transport Relay for internet play; use direct address/port only for controlled tests.
2. Test one Windows build and two device builds with the same protocol/version. Confirm that every client sees remote movement, guard indicator changes, fire animation, health changes, team IDs, and camp spawn positions.
3. Test disconnects and late joins. The manager's current NetworkVariables replicate to late joiners; production matchmaking should reject joins after `BattleStarted` or implement a spectator flow.

## Additional network scripts

- `NetworkPlayer.cs`: NGO player initialization, owner input routing, replicated survival/guard state, and team spawn placement.
- `NetworkMatchManager.cs`: server match phases, timers, safe-zone radius, team assignment, and camp spawning.
- `NetworkPlayerCombat.cs`: owner fire request, server raycast/damage validation, and client fire animation.

## 11. Game HUD setup

1. In the gameplay scene, create a `Screen Space - Overlay` Canvas with a `GameHUD` component. Add a top status row containing `HealthSlider`, `EnergySlider`, `StaminaSlider`, `TeamCampStatusText`, and `GuardStatusText`.
2. Set all three sliders to Min `0`, Max `1`, disable whole-number mode, and assign their Fill Rects. `GameHUD` reads health and energy from the local `PlayerSurvival` and stamina from the local `PlayerController`.
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

## 14. Daily-life economy and hospital setup

1. Add `DeliveryAndHospitalSystem` to PlayerPrefab. `NetworkPlayer` requires it automatically, but verify the component exists on the prefab.
2. Set cooked meal, medical kit, and general supply prices. Set meal hunger/energy restoration and delivery reward values on the server prefab.
3. Give each player a starting money balance through the system defaults. Never change `NetworkMoney` from a client UI script; use the exposed server RPC actions.
4. Create a `Hospital` layer and a trigger volume around each clinic. Add the layer to the hospital collider and assign it to `hospitalZoneLayer` on the player prefab. Create a `HospitalSpawn` tag and tag a safe respawn Transform if a dedicated hospital respawn flow is added.
5. Use `RequestHospitalTreatment` only inside a hospital zone. `UseMedicalKit` works anywhere and consumes one synchronized medical kit before healing/reviving.
6. For cooking, create an ingredients pickup or gather interaction that calls `GatherIngredients`, then bind a Cook button to `GameHUD.CookMeal`.
7. Bind `OrderMeal` and `OrderMedicalKit` to shop/order buttons. Orders charge the recipient when accepted by the server, then wait for a Delivery Boy.
8. Assign `IsDeliveryBoy` only from server-side role or match logic. A courier accepts an active order, travels to the recipient, and completes it within `deliveryDistance`. The recipient must be a connected player.
9. Add delivery status text to the HUD and assign `GameHUD.deliveryStatusText`. The status is replicated through `DeliveryStatus`.
10. NPC delivery AI can call `CompleteNpcDeliveryServer(orderId, npcTransform)` on the server. Give a visible NPC delivery agent a NetworkObject/NetworkTransform if clients must see its route, but keep order completion server-only.

## 16. Delivery immunity rule

1. Add `DeliveryImmunitySystem` to PlayerPrefab. `NetworkPlayer` requires it automatically.
2. A player orders food or supplies normally. The protection window does not start when the order is placed.
3. When a server-authorized Delivery Boy accepts the order, `DeliveryImmunitySystem.ActivateForDeliveryServer` records the courier, recipient, recipient `TeamId`, and `NetworkManager.ServerTime` expiry at exactly 60 seconds.
4. `NetworkPlayerCombat` calls `TryApplyDamageFromPlayerServer` before applying player damage. The courier rejects damage from the ordering client and any attacker with the protected team ID. The shot can still play its normal fire animation, but it is not treated as a successful hit and does not produce a hit marker.
5. Safe-zone, starvation, and other non-player damage continue to work because they use the environment-only `ApplyDamageServer` path.
6. Immunity ends on successful delivery, expiry, courier/recipient disconnect, or server cleanup. `NetworkRemainingSeconds` and `NetworkIsImmune` can drive a HUD countdown or courier icon.

## 17. Day/night and wild animal setup

1. Register `WildAnimalPrefab` in NetworkManager > Network Prefabs. It needs NetworkObject, NetworkTransform, NavMeshAgent, WildAnimalAI, a collider, visual mesh, and an Animator with an `Attack` Trigger.
2. Bake a NavMesh over the BattleScene. Animal spawn points must be on walkable NavMesh areas and should be distributed across the map and near camps.
3. Add `DayNightCycleManager` and NetworkObject to a persistent BattleScene object. Assign the directional Light, WildAnimalPrefab, map spawn points, camp spawn points, and `animalsPerNight`.
4. The server advances exactly 540 seconds of Day and 180 seconds of Night once the match reaches BattleStarted. This 12-minute cycle continues through the 22-minute match; during Night it darkens each client's lighting, spawns animals, and allows animals to target players. At the next Day transition or MatchEnded it despawns remaining animals.
5. Animals prefer nearby players outside camps, but can attack any living player within detection range. Their attacks use `ApplyDamageServer`, so delivery immunity correctly protects only against player-originated damage, not wildlife, starvation, or safe-zone damage.

## 18. Portable camp cooking setup

1. Add `CampCookingSystem` to PlayerPrefab. `NetworkPlayer` requires it automatically.
2. Leave `startsWithPortableCookingKit` enabled so every player carries a kit outside, at a tent house, or at a camp. The server controls the synchronized `HasPortableCookingKit` state.
3. Configure vegetarian and non-vegetarian ingredient costs and health/energy/hunger restoration values. Use `GatherIngredients` from a server-validated pickup or resource interaction.
4. Add Cook Vegetarian and Cook Non-Vegetarian buttons to the GameHUD and assign them to `GameHUD.cookVegetarianButton` and `cookNonVegetarianButton`. Add a text field to `GameHUD.cookingStatusText`.
5. Cooking consumes ingredients, takes the configured preparation time, and adds one synchronized prepared meal. It never edits another player's health from the client.
6. Build a teammate/partner selection UI from spawned NetworkPlayers. Pass the selected teammate's `OwnerClientId` to `GameHUD.SharePreparedMeal(clientId)`. The server validates connected status, same TeamId, and sharing distance before applying recipe benefits.
7. A shared meal restores the recipient's health, energy, and hunger through `PlayerSurvival`; it is not blocked by the delivery immunity rule because it is a beneficial teammate action.

## 19. Mid-match player session setup

1. Add `PlayerSessionManager` to PlayerPrefab. It stores the synchronized player name, join preference, returning status, and acceptance status.
2. In MainMenuScene, assign a player-name `InputField` and `Join previous team` `Toggle` to `MainMenuManager`. The menu submits them after the local NGO player spawns.
3. `NetworkMatchManager.MatchTimeRemaining` is server-authoritative. The match manager accepts a session during BattleStarted or SafeZoneShrinking only when the remaining time is at most 720 seconds and greater than zero.
4. A new/solo request receives a unique server-generated TeamId and a configured camp spawn. A returning-player request uses the in-session profile for the normalized name and returns to the previous configured team when available.
5. If the previous team no longer exists, the server safely falls back to solo. If the 12-minute condition is not met, the server rejects the session and disconnects the client.
6. The profile currently uses player name for local integration. Replace it with an authenticated account ID before production deployment so users cannot impersonate returning players by typing the same name.
7. Solo/Duo/Squad selection is sent in the session request and resolved on the server. Solo receives a unique TeamId; Duo/Squad use a configured team camp with least-populated balancing unless the returning-player previous-team option is selected.

## 20. 50-80 player optimization and setup

1. `NetworkMatchManager.MaximumPlayers` is fixed at 80. Set the NGO connection approval limit to 80 as well so excess clients are rejected before gameplay objects are created.
2. Configure enough Team Camps and spawn points for the intended population. The manager chooses the least-populated camp for new solo players and cycles spawn points within the selected camp; returning players use their previous configured team.
3. Keep team camp spawn arrays longer than the expected camp population. Spawn points may be reused after the array is exhausted, so place additional points to avoid overlapping players.
4. The manager exposes `ActivePlayers` and `ActivePlayerCount`. Safe-zone damage, guard alerts, and wildlife target selection use this capped registry instead of repeated global scene searches.
5. Keep server-only work in the server branches: health, economy, cooking, delivery immunity, hospital treatment, safe-zone damage, animal AI, team assignment, and mid-match admission. Clients should only submit input/request RPCs and render replicated state.
6. Profile the server with the Unity Profiler and Network Profiler using 50-80 simulated clients. Test join rejection above 80, join rejection with more than 12 minutes remaining, solo joins at 12 minutes, and returning-team joins during both active match phases.

## 15. Safe zone and guard alert setup

1. Add `SafeZoneManager` and `NetworkObject` to a persistent battle-scene object positioned at the zone center. Assign `NetworkMatchManager` and a circular ring/cylinder visual.
2. Set the visual's base diameter in `visualBaseDiameter`. The script scales only X/Z, preserving the visual's Y scale.
3. Set `zoneDamagePerSecond` and `damageTickInterval`. Damage is server-only and applies during `BattleStarted` and `SafeZoneShrinking`.
4. On PlayerPrefab, set `PlayerCampGuard.guardAlertRadius` and `guardAlertLayer` so enemy PlayerPrefab colliders are detected.
5. Connect `guardAlertRaised` to team warning UI/audio and `guardAlertCleared` to hide/stop it. The replicated `NetworkAlertActive` state updates every client.