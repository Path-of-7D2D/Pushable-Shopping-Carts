using System;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartInventory
    {
        internal const string CartItemName = "vehicleShoppingCartPlaceable";
        internal const string WheelItemName = "vehicleShoppingCartWheel";

        internal static bool TryGetItemValue(string itemName, out ItemValue itemValue)
        {
            itemValue = ItemClass.GetItem(itemName, _caseInsensitive: true);
            return itemValue != null && itemValue.type != 0;
        }

        internal static bool GiveItem(EntityAlive player, string itemName, int count, out string message)
        {
            message = null;
            if (player == null)
            {
                message = "No player is available.";
                return false;
            }

            if (!TryGetItemValue(itemName, out ItemValue itemValue))
            {
                message = "Unknown item: " + itemName;
                return false;
            }

            var stack = new ItemStack(itemValue.Clone(), Math.Max(1, count));
            if (player.inventory != null && player.inventory.AddItem(stack))
            {
                return true;
            }

            if (player.bag != null && player.bag.AddItem(stack))
            {
                return true;
            }

            message = "Inventory is full.";
            return false;
        }

        internal static int CountItem(EntityAlive player, string itemName)
        {
            if (player == null || !TryGetItemValue(itemName, out ItemValue itemValue))
            {
                return 0;
            }

            int count = 0;
            if (player.inventory != null)
            {
                count += player.inventory.GetItemCount(itemValue, false, -1, -1, false);
            }

            if (player.bag != null)
            {
                count += player.bag.GetItemCount(itemValue, -1, -1, false);
            }

            return count;
        }

        internal static bool ConsumeOne(EntityAlive player, string itemName)
        {
            if (player == null || !TryGetItemValue(itemName, out ItemValue itemValue))
            {
                return false;
            }

            int before = CountItem(player, itemName);
            if (before < 1)
            {
                return false;
            }

            if (player.inventory != null)
            {
                player.inventory.DecItem(itemValue, 1, false, null);
                if (CountItem(player, itemName) < before)
                {
                    return true;
                }
            }

            if (player.bag != null)
            {
                player.bag.DecItem(itemValue, 1, false, null);
            }

            return CountItem(player, itemName) < before;
        }

        internal static bool IsHoldingWrenchTool(EntityAlive player)
        {
            if (player == null || player.inventory == null)
            {
                return false;
            }

            ItemValue held = player.inventory.holdingItemItemValue;
            ItemClass itemClass = held != null ? held.ItemClass : null;
            string itemName = itemClass != null ? itemClass.Name : string.Empty;
            return itemName.IndexOf("Wrench", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("Ratchet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemName.IndexOf("ImpactDriver", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
