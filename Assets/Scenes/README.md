# Scenes

Create these scenes in the Unity Editor and add both to Build Settings:

## MainMenuScene.unity

1. Add a persistent `NetworkManager` with `UnityTransport`.
2. Enable NGO Scene Management.
3. Add `NetworkMatchManager` and `NetworkObject` to a scene object. Configure minimum players, match timers, and team camps. Register the scene object as a NetworkObject.
4. Add a Canvas with `MainMenuManager`.
5. Create MenuPanel controls for address, port, player name, `Join previous team` Toggle, Host Game, Join Game, and Exit. Assign the player-name InputField and Toggle to MainMenuManager.
6. Create a disabled LobbyPanel with a connection status Text. Assign all references to MainMenuManager.
7. Add the PlayerPrefab to NetworkManager > Player Prefab and Network Prefabs. It must include PlayerSessionManager.

## BattleScene.unity

1. Create the terrain, safe-zone center, cover, and lighting.
2. Add one or more camp trigger objects with a Collider set to Is Trigger and the `CampArea` component. Set recovery rates and the `CampArea` tag.
3. Create team spawn-point Transforms and assign them to the matching Team Camps on NetworkMatchManager in the bootstrap scene.
4. Add `SafeZoneManager` and `NetworkObject` at the safe-zone center. Assign a ring/cylinder visual, configure damage per second, and ensure it exists as a networked scene object.
5. Add `DayNightCycleManager` and `NetworkObject` to a persistent battle-scene object. Assign the directional Light, optional zone lighting values, the WildAnimalPrefab, map spawn points, and camp-near spawn points. The cycle starts at Day and runs 9 minutes Day followed by 3 minutes Night. Match timing is enforced at 12 minutes before safe-zone shrink plus 10 minutes of shrinking, exactly 22 minutes total.
6. Add `NetworkObject`, `NetworkTransform`, and `NavMeshAgent` to the WildAnimalPrefab. Bake the NavMesh and register the prefab in NetworkManager > Network Prefabs. Add the animal prefab to DayNightCycleManager.wildAnimalPrefab.
7. Create Hospital trigger volumes on the `Hospital` layer and place safe respawn markers with the `HospitalSpawn` tag.
8. Add a Screen Space - Overlay Canvas with `GameHUD`.
9. Create HealthSlider, EnergySlider, StaminaSlider, HungerSlider, TeamCampStatusText, GuardStatusText, and MatchStatusText. Assign them to GameHUD.
10. Add Eat, Guard, Laugh, Speak, Bye, Fire, Cook Vegetarian, Cook Non-Vegetarian, Order Meal, Order Medical Kit, Hospital, Medical Kit, Accept Delivery, and Complete Delivery Buttons. Assign them to GameHUD.
11. Add `CookingStatusText` to show `Cooking`, `Complete`, `NoIngredients`, or `Ready` plus prepared meal count. Build a teammate list that calls `GameHUD.SharePreparedMeal(clientId)` for a selected same-team player.
12. The portable cooking kit is enabled by default on each PlayerPrefab. Ingredient gathering calls `DeliveryAndHospitalSystem.GatherIngredients`; recipe buttons call `CampCookingSystem.CookVegetarian` or `CookNonVegetarian`. Cooking is server-authoritative and can be used with the portable kit outside or at a camp/tent.
13. Assign `NetworkPlayerCombat.muzzleFlash` and an optional crosshair `hitMarker` on PlayerPrefab. Assign `PlayerCampGuard.guardAlertRaised` and `guardAlertCleared` to the alert UI/audio callbacks.
14. Keep the gameplay scene in the NGO Scene Management list. The host loads it when NetworkMatchManager enters BattleStarted; clients follow automatically.
Do not place a second NetworkManager or a second persistent NetworkMatchManager in BattleScene. The bootstrap manager persists across the NGO scene transition.
