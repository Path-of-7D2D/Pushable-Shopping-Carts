namespace PushableShoppingCarts
{
    internal static class ShoppingCartWheelActions
    {
        internal static bool CanRemoveWheel(EntityVehicle vehicle, EntityAlive player)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                return false;
            }

            ShoppingCartState state = ShoppingCartState.Get(vehicle);
            return state.MissingWheels < ShoppingCartState.MaxMissingWheels &&
                ShoppingCartInventory.IsHoldingWrenchTool(player);
        }

        internal static bool CanInstallWheel(EntityVehicle vehicle, EntityAlive player)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                return false;
            }

            ShoppingCartState state = ShoppingCartState.Get(vehicle);
            return state.MissingWheels > 0 &&
                ShoppingCartInventory.CountItem(player, ShoppingCartInventory.WheelItemName) > 0;
        }

        internal static bool TryRemoveWheel(EntityVehicle vehicle, EntityAlive player, out string message)
        {
            message = null;
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                message = "That is not a shopping cart.";
                return false;
            }

            if (!ShoppingCartInventory.IsHoldingWrenchTool(player))
            {
                message = "Hold a wrench, ratchet, or impact driver to remove a shopping cart wheel.";
                return false;
            }

            ShoppingCartState state = ShoppingCartState.Get(vehicle);
            if (state.MissingWheels >= ShoppingCartState.MaxMissingWheels)
            {
                message = "This shopping cart has no wheels left to remove.";
                return false;
            }

            if (!ShoppingCartInventory.GiveItem(player, ShoppingCartInventory.WheelItemName, 1, out message))
            {
                return false;
            }

            state = state.WithMissingWheels(state.MissingWheels + 1);
            state.ApplyTo(vehicle);
            SaveVehiclesSoon();
            message = "Removed a shopping cart wheel. Cart state: " + state.DisplaySummary() + ".";
            return true;
        }

        internal static bool TryInstallWheel(EntityVehicle vehicle, EntityAlive player, out string message)
        {
            message = null;
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                message = "That is not a shopping cart.";
                return false;
            }

            ShoppingCartState state = ShoppingCartState.Get(vehicle);
            if (state.MissingWheels <= 0)
            {
                message = "This shopping cart already has all four wheels.";
                return false;
            }

            if (!ShoppingCartInventory.ConsumeOne(player, ShoppingCartInventory.WheelItemName))
            {
                message = "You need a Shopping Cart Wheel.";
                return false;
            }

            state = state.WithMissingWheels(state.MissingWheels - 1);
            state.ApplyTo(vehicle);
            SaveVehiclesSoon();
            message = "Installed a shopping cart wheel. Cart state: " + state.DisplaySummary() + ".";
            return true;
        }

        private static void SaveVehiclesSoon()
        {
            VehicleManager manager = VehicleManager.Instance;
            if (manager != null)
            {
                manager.TriggerSave();
            }
        }
    }
}
