using System;
using HarmonyLib;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartVehicleCommands
    {
        internal const string RemoveWheel = "shoppingcartRemoveWheel";
        internal const string InstallWheel = "shoppingcartInstallWheel";
        internal const string RemoveWheelIcon = "ui_game_symbol_shopping_cart_wheel_remove";
        internal const string InstallWheelIcon = "ui_game_symbol_shopping_cart_wheel_install";

        internal static bool IsRemoveWheel(string command)
        {
            return string.Equals(command, RemoveWheel, StringComparison.Ordinal);
        }

        internal static bool IsInstallWheel(string command)
        {
            return string.Equals(command, InstallWheel, StringComparison.Ordinal);
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.InitLocalActivationCommands))]
    internal static class ShoppingCartVehicleCommandListPatch
    {
        [Preserve]
        private static void Postfix(EntityVehicle __instance, Action<EntityActivationCommand> _addCallback)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(__instance))
            {
                return;
            }

            _addCallback(new EntityActivationCommand(ShoppingCartVehicleCommands.RemoveWheel, ShoppingCartVehicleCommands.RemoveWheelIcon));
            _addCallback(new EntityActivationCommand(ShoppingCartVehicleCommands.InstallWheel, ShoppingCartVehicleCommands.InstallWheelIcon));
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.OnEntityActivated))]
    internal static class ShoppingCartVehicleActivatePatch
    {
        [Preserve]
        private static bool Prefix(EntityVehicle __instance, EntityActivationCommand _command, EntityPlayerLocal _playerFocusing)
        {
            if (__instance == null || _playerFocusing == null || !ShoppingCartVisuals.IsShoppingCart(__instance))
            {
                return true;
            }

            string commandId = _command.commandId;
            if (ShoppingCartVehicleCommands.IsRemoveWheel(commandId))
            {
                HandleWheelCommand(__instance, _playerFocusing, remove: true);
                return false;
            }

            if (ShoppingCartVehicleCommands.IsInstallWheel(commandId))
            {
                HandleWheelCommand(__instance, _playerFocusing, remove: false);
                return false;
            }

            if (commandId != "ride" && commandId != "drive")
            {
                if (PushableShoppingCartsPush.IsPushing(__instance))
                {
                    PushableShoppingCartsPush.Release();
                }

                return true;
            }

            if (PushableShoppingCartsPush.IsPushing(__instance))
            {
                PushableShoppingCartsPush.Release();
                return false;
            }

            if (!PushableShoppingCartsPush.IsActive && !PushableShoppingCartsPush.JustReleased)
            {
                PushableShoppingCartsPush.Begin(__instance);
            }

            return false;
        }

        private static void HandleWheelCommand(EntityVehicle vehicle, EntityPlayerLocal player, bool remove)
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                string command = remove ? "sc removewheel " : "sc installwheel ";
                ShoppingCartSpawnService.SendServerCommand(command + vehicle.entityId);
                ShowTooltip(player, "Requested shopping cart wheel update.");
                return;
            }

            string message;
            if (remove)
            {
                ShoppingCartWheelActions.TryRemoveWheel(vehicle, player, out message);
            }
            else
            {
                ShoppingCartWheelActions.TryInstallWheel(vehicle, player, out message);
            }

            ShowTooltip(player, message);
        }

        private static void ShowTooltip(EntityPlayerLocal player, string message)
        {
            if (player != null && !string.IsNullOrEmpty(message))
            {
                GameManager.ShowTooltip(player, "[ShoppingCart] " + message, false, false, 3f);
            }
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.GetActivationText))]
    internal static class ShoppingCartVehicleActivationTextPatch
    {
        [Preserve]
        private static void Postfix(EntityVehicle __instance, ref string __result)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(__instance))
            {
                return;
            }

            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null)
            {
                return;
            }

            string binding = player.playerInput.Activate.GetBindingXuiMarkupString() +
                player.playerInput.PermanentActions.Activate.GetBindingXuiMarkupString();
            string template = Localization.Get("shoppingcartTooltipPush");
            if (string.IsNullOrEmpty(template) || template == "shoppingcartTooltipPush")
            {
                template = "{0} to Push {1}";
            }

            string text = string.Format(template, binding, GetShoppingCartDisplayName());
            ShoppingCartState state = ShoppingCartState.Get(__instance);
            if (!state.IsFixed)
            {
                text += "\n" + state.DisplaySummary();
            }

            if (__instance.IsLockedForLocalPlayer(player))
            {
                text = Localization.Get("ttLocked") + "\n" + text;
            }

            __result = text;
        }

        private static string GetShoppingCartDisplayName()
        {
            string entityName = Localization.Get(ShoppingCartVisuals.EntityName);
            if (string.IsNullOrEmpty(entityName) || entityName == ShoppingCartVisuals.EntityName)
            {
                return "Shopping Cart";
            }

            return entityName;
        }
    }
}
