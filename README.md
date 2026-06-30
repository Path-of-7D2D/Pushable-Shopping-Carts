# Pushable Shopping Carts

A 7 Days To Die V3.0 mod that adds craftable and world-converted pushable shopping carts.

## Features

- Craftable fixed shopping cart placeable.
- Existing `cntShoppingCart*` world blocks gain a Push command that converts them into pushable carts.
- World carts spawn with 1-4 missing wheels, heavily weighted toward 4 missing wheels.
- World carts may also have a rotted frame.
- Missing wheels apply 25% movement penalty per missing wheel.
- Rotted frames apply an additional movement penalty.
- Dirt-like terrain applies 50% movement penalty while pushing.
- Sand/desert terrain applies 75% movement penalty while pushing.
- Wheels can be removed from shopping carts with a wrench, ratchet, or impact driver.
- Missing wheels can be replaced with Shopping Cart Wheel items.
- Shopping Cart Wheel items are not craftable; they come from removing wheels or test commands.

## Crafting

Crafted carts are always fully fixed.

Shopping Cart:

- 1 Shopping Cart Hull
- 1 Shopping Cart Frame
- 4 Shopping Cart Wheels

Shopping Cart Frame includes the handles.

## Test Commands

Use the in-game console after loading a world:

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

`sc fixed` spawns a fully fixed pushable cart in front of you.
`sc item` gives a fixed cart placeable item.
`sc world` spawns a vanilla world shopping cart block; look at it and use Push to convert it into a damaged pushable cart.

## Notes

This uses the existing push logic from the wheelbarrow mod. The vehicle physics scaffold comes from that proven setup, while the visible cart attempts to use the vanilla grocery cart prefab at runtime.
