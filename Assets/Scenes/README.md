# Scenes

Create these scenes in the Unity Editor and add both to Build Settings:

## MainMenuScene.unity

1. Add a persistent `NetworkManager` with `UnityTransport`.
2. Enable NGO Scene Management.
3. Add `NetworkMatchManager` and `NetworkObject` to a scene object. Configure minimum players, match timers, and team camps. Register the scene object as a NetworkObject.
4. Add a Canvas with `MainMenuManager`.
5. Create MenuPanel controls for address, port, player name, `Join previous team` Toggle, Solo/Duo/Squad Toggles, Host Game, Join Game, and Exit. Assign the player-name InputField and Toggle to MainMenuManager; assign the mode Toggles to MainMenuUIController.
6. Create a disabled LobbyPanel with a connection status Text. Assign all references to MainMenuManager.
7. Add the PlayerPrefab to NetworkManager > Player Prefab and Network Prefabs. It must include PlayerSessionManager.
8. Set NGO connection approval/max-player policy to 80. Configure NetworkMatchManager Team Camps with enough spawn points for the desired 50-80 player population.

## BattleScene.unity

1. Create the terrain, safe-zone center, cover, and lighting.
2. Add one or more camp trigger objects with a Collider set to Is Trigger and the `CampArea` component. Set recovery rates and the `CampArea` tag.
3. Create team spawn-point Transforms and assign them to the matching Team Camps on NetworkMatchManager in the bootstrap scene.
4. Add `SafeZoneManager` and `NetworkObject` at the safe-zone center. Assign a ring/cylinder visual, configure damage per second, and ensure it exists as a networked scene object.
5. Add `DayNightCycleManager` and `NetworkObject` to a persistent battle-scene object. Assign the directional Light, optional zone lighting values, the WildAnimalPrefab, map spawn points, and camp-near spawn points. The cycle starts at Day and runs 9 minutes Day followed by 3 minutes Night. Match timing is enforced at 12 minutes before safe-zone shrink plus 10 minutes of shrinking, exactly 22 minutes total.
6. Add `NetworkObject`, `NetworkTransform`, and `NavMeshAgent` to the WildAnimalPrefab. Bake the NavMesh and register the prefab in NetworkManager > Network Prefabs. Add the animal prefab to DayNightCycleManager.wildAnimalPrefab.
7. Create Hospital trigger volumes on the `Hospital` layer and place safe respawn markers with the `HospitalSpawn` tag.
8. Add a Screen Space - Overlay Canvas with `GameHUD` and a `KillFeedUI` component.
9. Create HealthSlider, EnergySlider, StaminaSlider, HungerSlider, TeamCampStatusText, GuardStatusText, MatchStatusText, MatchKillsText, MoneyText, MedicalKitsText, DeliveryStatusText, and CookingStatusText. Assign gameplay fields to GameHUD and a feed Text to KillFeedUI.
10. Add Eat, Guard, Laugh, Speak, Bye, Fire, Cook Vegetarian, Cook Non-Vegetarian, Order Meal, Order Medical Kit, Hospital, Medical Kit, Accept Delivery, and Complete Delivery Buttons. Assign them to GameHUD.
11. Add `CookingStatusText` to show `Cooking`, `Complete`, `NoIngredients`, or `Ready` plus prepared meal count. Build a teammate list that calls `GameHUD.SharePreparedMeal(clientId)` for a selected same-team player.
12. The portable cooking kit is enabled by default on each PlayerPrefab. Ingredient gathering calls `DeliveryAndHospitalSystem.GatherIngredients`; recipe buttons call `CampCookingSystem.CookVegetarian` or `CookNonVegetarian`. Cooking is server-authoritative and can be used with the portable kit outside or at a camp/tent.
13. Assign `NetworkPlayerCombat.muzzleFlash` and an optional crosshair `hitMarker` on PlayerPrefab. Assign `PlayerCampGuard.guardAlertRaised` and `guardAlertCleared` to the alert UI/audio callbacks.
14. Keep the gameplay scene in the NGO Scene Management list. The host loads it when NetworkMatchManager enters BattleStarted; clients follow automatically.
Do not place a second NetworkManager or a second persistent NetworkMatchManager in BattleScene. The bootstrap manager persists across the NGO scene transition.

## Final polish objects

1. Add `DynamicWeatherManager` and `NetworkObject` to BattleScene. Assign the DayNightCycleManager and a rain ParticleSystem; configure fog and survival exposure.
2. Add registered VehiclePrefab and DeliveryVanPrefab instances near roads and the Delivery Hub. Assign DriverSeat and ExitPoint; DeliveryVan requires the Delivery Boy role.
3. Add `PlayerInventory` to PlayerPrefab and register food, ingredient, medical kit, weapon, ammunition, and supply item interactions through server methods.
4. Add `KillFeedUI` to the gameplay Canvas with a feed Text.
5. Add `MatchResultsUI` with a ResultsPanel containing WinnerText, MvpText, and ScoreboardText.
6. Add MatchKillsText to GameHUD for the replicated match kill counter.

## Main menu dashboard

1. Add `MainMenuUIController` to the menu Canvas or dashboard root alongside `MainMenuManager`.
2. Create Play/Host/Join controls, Solo/Duo/Squad toggles, a map Dropdown, player-name InputField, and previous-team Toggle. Assign the controls to MainMenuUIController/MainMenuManager.
3. Create ProfilePanel with name, avatar Image, level, rank, kills, and matches Text fields. MainMenuUIController reads the local PlayerSessionManager, NetworkPlayer, and PlayerProgression after NGO spawn.
4. Create SettingsPanel with Master, SFX, Music, and Sensitivity Sliders. Values are persisted with PlayerPrefs; connect actual SFX/Music AudioMixer parameters when the project has its mixer assets.
5. Create StorePanel with Buy Food, Buy Supplies, and Customize buttons. Connect the UnityEvents to server-backed purchase/customization handlers; the controller intentionally does not mutate economy values locally.
