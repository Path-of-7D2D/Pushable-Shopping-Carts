# Contributors

This document is for anyone modifying `PushableShoppingCarts`. The consumer-facing install and usage instructions live in `README.md`.

## Repository Layout

- `1A-PushableShoppingCarts/` - deployable mod folder.
- `1A-PushableShoppingCarts/Config/` - XML appends and localization.
- `1A-PushableShoppingCarts/Resources/shoppingcart.unity3d` - packaged vehicle scaffold bundle.
- `1A-PushableShoppingCarts/PushableShoppingCarts.dll` - built gameplay and command DLL.
- `src/PushableShoppingCarts/` - C# source for Harmony patches, commands, visual repair, wheel state, and push behavior.

## Build Workflow

Run from the repository root:

```powershell
dotnet build src\PushableShoppingCarts\PushableShoppingCarts.csproj -v:minimal
```

The C# project copies `PushableShoppingCarts.dll` into `1A-PushableShoppingCarts`. If a local 7D2D install exists at the default Steam path, it also mirrors the deployable folder to `Mods\1A-PushableShoppingCarts`.

The current visual approach uses the packaged vehicle scaffold for 7D2D vehicle transforms and attaches the vanilla grocery-cart prefab at runtime. There is no Unity source project in this repo yet; if the scaffold or wheel pivots need dedicated art work later, add the Unity project and builder in a separate, explicit asset-pipeline change.

## In-Game Test Commands

```text
sc fixed
sc item
sc wheel 4
sc world
sc push
sc drop
sc debug
sc cleanup
```

Useful notes:

- `sc fixed` spawns a fully repaired pushable shopping cart.
- `sc item` gives the fixed placeable cart item.
- `sc wheel 4` gives test wheel items.
- `sc world` spawns a vanilla shopping-cart block; look at it and use Push to convert it.
- `sc cleanup` removes active and unloaded shopping-cart vehicles.
- Restart 7D2D after DLL or localization changes.

## Implementation Notes

- The shopping-cart entity is `vehicleShoppingCart`.
- The placeable item is `vehicleShoppingCartPlaceable`.
- The wheel item is `vehicleShoppingCartWheel`; it intentionally has no recipe.
- `ShoppingCartPushController.cs` owns the walk-behind push state and terrain penalties.
- `ShoppingCartState.cs` persists missing-wheel and rotted-frame state on the vehicle item metadata.
- `ShoppingCartBlockInteractionPatch.cs` adds Push to existing vanilla `cntShoppingCart*` blocks.
- `ShoppingCartInteractPatch.cs` replaces ride/drive activation with push behavior and adds wheel actions.
- `ShoppingCartVisuals.cs` attaches the vanilla cart visual and hides wheels according to state.

## Git Hygiene

Do not commit local generated state:

- `src/**/bin/`
- `src/**/obj/`
- editor folders such as `.vs/`, `.vscode/`, `.idea/`

Do commit intentional deployable outputs when they change:

- `1A-PushableShoppingCarts/PushableShoppingCarts.dll`
- `1A-PushableShoppingCarts/Resources/shoppingcart.unity3d`

Before publishing, run:

```powershell
dotnet build src\PushableShoppingCarts\PushableShoppingCarts.csproj -v:minimal
```
