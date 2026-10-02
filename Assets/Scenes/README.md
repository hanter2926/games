# Scenes

Create these scenes in the Unity Editor and add both to Build Settings:

## MainMenuScene.unity

1. Add a persistent `NetworkManager` with `UnityTransport`.
2. Enable NGO Scene Management.
3. Add `NetworkMatchManager` and `NetworkObject` to a scene object. Configure minimum players, match timers, and team camps. Register the scene object as a NetworkObject.
4. Add a Canvas with `MainMenuManager`.
5. Create MenuPanel controls for address, port, Host Game, Join Game, and Exit.
6. Create a disabled LobbyPanel with a connection status Text. Assign all references to MainMenuManager.
7. Add the PlayerPrefab to NetworkManager > Player Prefab and Network Prefabs.

## BattleScene.unity

1. Create the terrain, safe-zone center, cover, and lighting.
2. Add one or more camp trigger objects with a Collider set to Is Trigger and the `CampArea` component. Set recovery rates and the `CampArea` tag.
3. Create team spawn-point Transforms and assign them to the matching Team Camps on NetworkMatchManager in the bootstrap scene.
4. Add `SafeZoneManager` and `NetworkObject` at the safe-zone center. Assign a ring/cylinder visual, configure damage per second, and ensure it exists as a networked scene object.
5. Create Hospital trigger volumes on the `Hospital` layer and place safe respawn markers with the `HospitalSpawn` tag.
6. Add a Screen Space - Overlay Canvas with `GameHUD`.
7. Create HealthSlider, EnergySlider, StaminaSlider, HungerSlider, TeamCampStatusText, GuardStatusText, MatchStatusText, MoneyText, MedicalKitsText, and DeliveryStatusText. Assign them to GameHUD.
8. Add Eat, Guard, Laugh, Speak, Bye, Fire, Cook Meal, Order Meal, Order Medical Kit, Hospital, Medical Kit, Accept Delivery, and Complete Delivery Buttons. Assign them to GameHUD.
9. Assign `NetworkPlayerCombat.muzzleFlash` and an optional crosshair `hitMarker` on PlayerPrefab. Assign `PlayerCampGuard.guardAlertRaised` and `guardAlertCleared` to the alert UI/audio callbacks.
10. Keep the gameplay scene in the NGO Scene Management list. The host loads it when NetworkMatchManager enters BattleStarted; clients follow automatically.

Do not place a second NetworkManager or a second persistent NetworkMatchManager in BattleScene. The bootstrap manager persists across the NGO scene transition.
