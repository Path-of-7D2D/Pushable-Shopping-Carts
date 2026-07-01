# Contributors

This document is for developers modifying Pushable Shopping Carts. Player-facing install and usage instructions live in [README.md](README.md).

## Repository Layout

- `1A-PushableShoppingCarts/` - deployable mod folder shipped in releases.
- `1A-PushableShoppingCarts/Config/` - XML appends for entity classes, vehicles, items, recipes, loot storage, buffs, navigation, and localization.
- `1A-PushableShoppingCarts/ItemIcons/` - item icon PNGs.
- `1A-PushableShoppingCarts/UIAtlases/` - icon atlas copies used by item and action-wheel UI.
- `1A-PushableShoppingCarts/Resources/shoppingcart.unity3d` - packaged vehicle scaffold bundle.
- `1A-PushableShoppingCarts/PushableShoppingCarts.dll` - built gameplay, Harmony patch, and console-command DLL.
- `src/PushableShoppingCarts/` - C# source.
- `.github/workflows/release.yml` - manual GitHub release workflow.

## Build And Deploy

Run from the repository root:

```powershell
dotnet build src\PushableShoppingCarts\PushableShoppingCarts.csproj -v:minimal
```

The build:

- Compiles `PushableShoppingCarts.dll`.
- Copies the DLL into `1A-PushableShoppingCarts`.
- Reinstalls the full deployable mod folder into the default Steam game path when `Mods` exists.

Default live install target:

```text
C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\1A-PushableShoppingCarts
```

Useful build options:

- Set `GAME_7D2D` to point at a non-default 7D2D install.
- Pass `/p:InstallToGame=false` when you want to build without touching the live game folder.

Examples:

```powershell
$env:GAME_7D2D = "D:\SteamLibrary\steamapps\common\7 Days To Die"
dotnet build src\PushableShoppingCarts\PushableShoppingCarts.csproj -v:minimal

dotnet build src\PushableShoppingCarts\PushableShoppingCarts.csproj -v:minimal /p:InstallToGame=false
```

Restart 7D2D after DLL, XML, localization, or icon changes. The running client will not reload those files automatically.

## Mod Content Map

- `Config/entityclasses.xml` defines `vehicleShoppingCart` and points it at the shopping cart vehicle and loot list. Cart icon tagging is runtime-managed, so this entity class intentionally does not declare `MapIcon` or `NavObject`.
- `Config/vehicles.xml` defines the vehicle scaffold, storage module, and vehicle tuning.
- `Config/items.xml` defines `vehicleShoppingCartPlaceable`, `vehicleShoppingCartFrame`, `vehicleShoppingCartHull`, and `vehicleShoppingCartWheel`.
- `Config/recipes.xml` defines frame, hull, and fixed-cart recipes.
- `Config/loot.xml` defines the cart storage container as `size="10,2"` for 20 slots.
- `Config/nav_objects.xml` defines the `shoppingcart` nav object class used only when a cart is explicitly tagged.
- `Config/buffs.xml` defines the pushing burden buff and displayed penalty cvar.
- `Config/Localization.csv` defines item names, prompts, wheel commands, tag commands, and buff text.

## Source Map

- `ShoppingCartModApi.cs` applies Harmony patches and starts runtime repair/push behaviours.
- `ShoppingCartVisuals.cs` attaches the vanilla grocery cart visual, hides scaffold renderers, creates handle grips, manages wheel visibility, and stabilizes physics.
- `ShoppingCartPushController.cs` owns walk-behind pushing, hand IK, wheel spinning, terrain/cargo/damage penalties, release physics, and push-state validation.
- `ShoppingCartInputLockPatches.cs` blocks jump/attack while pushing and applies the movement penalty through `EntityPlayerLocal.GetSpeedModifier`.
- `ShoppingCartState.cs` persists missing-wheel, rotted-frame, and world-cart state in vehicle item metadata under `p7d2d.shoppingcart.v1`.
- `ShoppingCartTagging.cs` persists tag state under `p7d2d.shoppingcart.tag.v1` and registers or removes the cart nav object.
- `ShoppingCartInteractPatch.cs` replaces ride/drive activation with push behaviour, removes lock/unlock/keypad commands, and adds Remove Wheel/Add Wheel plus Tag/Untag commands.
- `ShoppingCartBlockInteractionPatch.cs` adds Push to vanilla `cntShoppingCart*` world blocks and converts those blocks into pushable vehicles.
- `ShoppingCartWheelActions.cs` validates and applies wheel removal or installation.
- `ShoppingCartSpawnService.cs` spawns fixed carts, world blocks, and converted carts.
- `ShoppingCartInventory.cs` handles item lookup, give, count, consume, and wrench-tool detection.
- `Commands/ConsoleCmdShoppingCart.cs` exposes the `sc`, `shoppingcart`, and `spawnshoppingcart` console commands.
- `ShoppingCartPlacementPreviewPatch.cs` fixes the placeable cart preview model.

## Key Tuning Values

These values are current implementation details, not public API:

- Cart storage: `Config/loot.xml`, `vehicleShoppingCart`, `size="10,2"`.
- Missing wheel penalty: `0.25` per missing wheel in `ShoppingCartState.cs`.
- Rotted frame penalty: `0.15` in `ShoppingCartState.cs`.
- World-cart missing wheel weighting: 62% four missing wheels, 20% three, 12% two, 6% one.
- Rotted frame chance: 40% normally, 70% for tipped cart block names.
- Dirt terrain penalty: `0.50` in `ShoppingCartPushController.cs`.
- Sand/desert terrain penalty: `0.75` in `ShoppingCartPushController.cs`.
- Cargo penalty: 5% baseline through five filled slots, then roughly 1% per filled slot total, capped with other penalties.
- Total push movement penalty cap: `0.75`.
- Push ground clearance: `0.02m`.
- Default push offset/lift/tilt: `1.25`, `-0.08`, `0`.
- Tag metadata: `p7d2d.shoppingcart.tag.v1`, with `tagged` and `untagged` explicit states.
- Tag default: fixed carts auto-tag on first push while still in default tag state; broken carts do not auto-tag; explicitly untagged carts stay untagged until explicitly tagged.

## In-Game Test Commands

Open the console after loading a world:

```text
sc fixed [distance]              spawn a fully fixed pushable cart
sc item [count]                  give fixed shopping cart item(s)
sc wheel [count]                 give shopping cart wheel item(s)
sc world [distance] [blockName]  spawn a vanilla world shopping cart block
sc push [offset] [lift] [tilt]   push nearest active cart
sc tag [entityId] [on|off|toggle] tag or untag a cart icon
sc hands x y z                   tune hand rotation while pushing
sc handpos x y z                 tune grip-local hand offset while pushing
sc drop                          release the pushed cart
sc debug                         log active cart state
sc cleanup                       remove active and unloaded cart vehicles
```

Server/internal command paths:

```text
sc fixedat x y z yaw
sc worldat blockName x y z [rotation]
sc convert x y z
sc removewheel entityId
sc installwheel entityId
sc tag entityId on|off|toggle
```

Useful aliases:

- `shoppingcart`
- `spawnshoppingcart`
- `sc`

## Manual Test Checklist

After a build and game restart, validate the paths touched by your change:

- `sc fixed` spawns a fixed cart with the red shopping cart visual.
- `sc item` gives the placeable item, and placement uses the cart preview.
- Cart storage opens with 20 slots.
- Push starts from behind the cart and the hands land on the handle.
- Lock, unlock, and keypad/code actions do not appear for shopping carts.
- Jump and attack are blocked while pushing.
- Sprint and walk both respect dirt and sand penalties.
- Releasing the cart re-enables physics and lets the cart settle naturally.
- `sc world` creates a world cart block; using Push converts it into a damaged pushable cart.
- Broken converted carts do not get compass/on-screen icons by default.
- A fixed cart creates its icon on first push, unless it was explicitly untagged.
- Tag Cart/Untag Cart toggles the icon, and an explicitly untagged cart remains untagged after release/reload.
- Remove Wheel works only while holding a wrench, ratchet, or impact driver.
- Add Wheel consumes one Shopping Cart Wheel and restores a missing wheel.
- Action-wheel labels localize as `Remove Wheel`, `Add Wheel`, `Tag Cart`, and `Untag Cart`.
- `sc cleanup` removes active and unloaded shopping cart vehicles.

## Visual And Physics Notes

The repo currently uses a packaged vehicle scaffold for the engine-facing vehicle hierarchy, then attaches the vanilla `@:Entities/LootContainers/groceryCartEmptyPrefab.prefab` visual at runtime.

There is no Unity source project in this repo yet. If the scaffold, wheel pivots, or custom mesh pipeline need dedicated asset work later, add the Unity project and builder as a separate asset-pipeline change.

The visible wheel transforms are discovered from the attached vanilla visual. The runtime also keeps the scaffold vehicle physics stable enough to behave like a four-wheel cart when released.

## Release Workflow

Releases are created with `.github/workflows/release.yml`.

The workflow is manual:

1. Run the `Release` workflow.
2. Enter a `version_tag`, for example `v0.1.0`.
3. The workflow validates the deployable folder, DLL, and resource bundle.
4. It zips `1A-PushableShoppingCarts`.
5. It generates changelog notes with `Path-of-7D2D/Changelog-Generator`.
6. It publishes a GitHub release with the zip attached.

Before publishing, ensure the deployable folder is current and committed.

## Git Hygiene

Do not commit local generated state:

- `src/**/bin/`
- `src/**/obj/`
- editor folders such as `.vs/`, `.vscode/`, `.idea/`

Do commit intentional deployable outputs when they change:

- `1A-PushableShoppingCarts/PushableShoppingCarts.dll`
- `1A-PushableShoppingCarts/Resources/shoppingcart.unity3d`
- `1A-PushableShoppingCarts/ItemIcons/*.png`
- `1A-PushableShoppingCarts/UIAtlases/**/*.png`
- `1A-PushableShoppingCarts/Config/*.xml`
- `1A-PushableShoppingCarts/Config/Localization.csv`

Before pushing a code or config change, run:

```powershell
dotnet build src\PushableShoppingCarts\PushableShoppingCarts.csproj -v:minimal
git diff --check
```
