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

The scripts add required components automatically, but keep exactly one NetworkObject and one NetworkTransform on the root.

Create these child objects:

- `Visual`: mesh, model, Animator, and animation controller.
- `GuardIndicator`: disabled mesh, light, or world-space icon. Assign it to PlayerCampGuard.
- `Muzzle`: weapon muzzle Transform. Assign it to NetworkPlayerCombat.
- `OwnerCamera`: camera used only by the owning client. Assign it to NetworkPlayerCombat.ownerCamera.

Assign the Animator to PlayerCampGuard and PlayerSocialInteractions. Add Animator parameters:

- `IsOnGuardDuty` Bool
- `Laugh` Trigger
- `Speak` Trigger
- `Wave` Trigger
- `Fire` Trigger

Set NetworkTransform to owner/client authority when supported by the installed NGO version. Register the prefab in NetworkManager > Network Prefabs and set it as Player Prefab.

For mobile movement, assign the virtual joystick component to PlayerController.mobileJoystick. The concrete joystick type must match the joystick package installed in the project.
