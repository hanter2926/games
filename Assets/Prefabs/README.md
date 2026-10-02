# Prefabs

## PlayerPrefab

Create `PlayerPrefab` from a root GameObject with these components:

- CharacterController
- NetworkObject
- NetworkTransform
- PlayerController
- PlayerSurvival
- PlayerCampGuard
- PlayerSocialInteractions
- NetworkPlayer
- NetworkPlayerCombat
- DeliveryAndHospitalSystem
- DeliveryImmunitySystem
- CampCookingSystem
- PlayerSessionManager
- PlayerProgression
- PlayerInventory

The scripts add required components automatically, but keep exactly one NetworkObject and one NetworkTransform on the root.

Create these child objects:

- `Visual`: mesh, model, Animator, and animation controller.
- `GuardIndicator`: disabled mesh, light, or world-space icon. Assign it to PlayerCampGuard.
- `Muzzle`: weapon muzzle Transform. Assign it to NetworkPlayerCombat.
- `OwnerCamera`: camera used only by the owning client. Assign it to NetworkPlayerCombat.ownerCamera.
- `MedicalKitSocket` (optional): visual location for a medical bag or kit pickup.

Assign the Animator to PlayerCampGuard and PlayerSocialInteractions. Add Animator parameters:

- `IsOnGuardDuty` Bool
- `Laugh` Trigger
- `Speak` Trigger
- `Wave` Trigger
- `Fire` Trigger
- `Drive` Bool or Trigger
- `Deliver` Trigger

Set NetworkTransform to owner/client authority when supported by the installed NGO version. Register the prefab in NetworkManager > Network Prefabs and set it as Player Prefab.

Assign `NetworkPlayerCombat.muzzleFlash` to a ParticleSystem under `Muzzle`, and assign a disabled crosshair UI object to `hitMarker` if hit feedback is desired. Assign `PlayerCampGuard.guardIndicator`, `guardAlertRaised`, and `guardAlertCleared` to the guard visual and alert HUD/audio callbacks.

Assign `DeliveryAndHospitalSystem.hospitalZoneLayer`, configure prices and healing values, and use a `Hospital` layer on clinic trigger colliders. Delivery roles must be assigned by server-side code through `AssignDeliveryBoyServer`.

`PlayerProgression` is server-authoritative. It tracks XP, level, rank, kills, matches, deliveries, and survival time. Do not edit its NetworkVariables from UI scripts.

`CampCookingSystem` gives each player a portable cooking kit by default. Configure vegetarian and non-vegetarian ingredient costs, health, energy, hunger values, and cooking duration. The component consumes `DeliveryAndHospitalSystem.NetworkIngredients`, creates synchronized prepared meals, and shares them only with a same-team player within the configured sharing distance.

Delivery immunity is activated only when a Delivery Boy accepts an order. The server protects the courier from the ordering player and the ordering player's `TeamId` for exactly 60 seconds. Register the prefab with `DeliveryImmunitySystem` and do not apply player damage by directly changing `PlayerSurvival.currentHealth`; use `NetworkPlayer.TryApplyDamageFromPlayerServer` or the existing combat path.

For a delivery vehicle prefab, add a NetworkObject, NetworkTransform, vehicle collider, driver anchor, and an Animator with `Drive` and `Deliver`. A delivery bag can be a child of the player or vehicle; play `Deliver` when `CompleteDelivery` succeeds.

Use `NetworkVehicle` on the vehicle root. Set its type to `DeliveryVan` for Delivery Boy vehicles. Add `DriverSeat` and `ExitPoint` child Transforms and register the vehicle prefab with NetworkManager > Network Prefabs.

For mobile movement, assign the virtual joystick component to PlayerController.mobileJoystick. The concrete joystick type must match the joystick package installed in the project.

## WildAnimalPrefab

Create a separate registered NGO prefab with:

- NetworkObject
- NetworkTransform
- NavMeshAgent
- WildAnimalAI
- Collider
- Visual mesh and Animator
- Optional AudioSource

Configure `WildAnimalAI` detection radius, attack range, attack cooldown, damage, and target-refresh interval. Add an Animator Trigger named `Attack`. Bake a NavMesh over the map and ensure the animal spawn points are on the baked walkable surface. Register this prefab under NetworkManager > Network Prefabs.
