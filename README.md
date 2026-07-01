# Pushable Shopping Carts

Pushable Shopping Carts adds craftable and world-converted shopping carts to 7 Days To Die V3.0. They are small storage carts you walk behind and push instead of driving like a vehicle.

## Requirements

- 7 Days To Die V3.0.
- EAC disabled. This mod includes a DLL and is marked `SkipWithAntiCheat`.
- For multiplayer, install the mod on the server and on every client that connects.

## Installation

1. Download the latest release zip.
2. Extract the zip.
3. Copy the `1A-PushableShoppingCarts` folder into your game `Mods` folder.
4. Restart the game.

For a default Steam install, the final folder should look like this:

```text
C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\1A-PushableShoppingCarts
```

## What It Adds

- A pushable shopping cart with 20 storage slots.
- A craftable fixed shopping cart item.
- New Shopping Cart Frame, Shopping Cart Hull, and Shopping Cart Wheel items.
- Interactable shopping carts already found in the world.
- Damaged world carts with missing wheels and possible rotted frames.
- Wheel removal and repair actions on the cart interaction wheel.
- Custom item and action-wheel icons.
- Terrain and cargo movement penalties while pushing.

## Using Shopping Carts

Look at a shopping cart and use the normal interaction prompt to push it. Press the interaction key again while pushing to let go.

Shopping carts keep physics when released, so they can settle, tip, or fall naturally after you stop pushing them.

## Storage

Shopping carts have 20 storage slots arranged as 2 rows of 10.

Carrying cargo slows the player while pushing. The cart has a small baseline slowdown, and each filled slot increases the cargo penalty. The total slowdown from cargo, damage, and terrain is capped.

## Crafting

Crafted shopping carts are always fully fixed.

Shopping Cart Frame:

- 4 Short Iron Pipes
- 20 Scrap Iron
- 10 Wood

Shopping Cart Hull:

- 100 Scrap Iron

Shopping Cart:

- 1 Shopping Cart Hull
- 1 Shopping Cart Frame
- 4 Shopping Cart Wheels

Shopping Cart Wheels cannot be crafted. They must be salvaged from shopping carts or spawned with admin commands.

## World Shopping Carts

Existing `cntShoppingCart*` world blocks gain a Push action. Using Push converts the world prop into a pushable shopping cart.

World carts are usually damaged:

- 1-4 missing wheels, heavily weighted toward 4 missing wheels.
- Each missing wheel adds a 25% movement penalty.
- Some world carts have a rotted frame, adding another 15% movement penalty.

## Wheels And Repairs

Hold a wrench, ratchet, or impact driver and use Remove Wheel on a shopping cart to salvage a Shopping Cart Wheel.

Use Add Wheel on a cart with missing wheels to install one Shopping Cart Wheel from your inventory.

## Terrain Penalties

Shopping carts are harder to push off-road:

- Dirt-like terrain applies a 50% movement penalty.
- Sand and desert ground apply a 75% movement penalty.

These penalties apply while walking and sprinting.

## Admin And Testing Commands

Open the in-game console after loading a world:

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

Useful commands:

- `sc fixed` spawns a fully fixed pushable cart in front of you.
- `sc item` gives a fixed placeable cart item.
- `sc wheel 4` gives four Shopping Cart Wheel items.
- `sc world` spawns a vanilla world shopping cart block. Look at it and use Push to convert it.
- `sc push` grabs the nearest active cart for push-position testing.
- `sc drop` releases the currently pushed cart.
- `sc debug` logs active cart state.
- `sc cleanup` removes active and unloaded shopping-cart vehicles.

## Compatibility Notes

This mod adds a new vehicle entity, item definitions, recipes, localization, icons, and Harmony patches for cart interaction. Mods that heavily replace vehicle interaction, shopping cart blocks, or vehicle storage may need compatibility testing.

To uninstall, remove `1A-PushableShoppingCarts` from your `Mods` folder and restart the game. Remove or empty any shopping carts in active saves first if you want to avoid missing modded entities or items.

## For Contributors

Developer setup, build instructions, implementation notes, and release workflow details are in [CONTRIBUTORS.md](CONTRIBUTORS.md).
