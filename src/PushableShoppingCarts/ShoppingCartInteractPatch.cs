using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartVehicleCommands
    {
        internal const string RemoveWheel = "shoppingcartRemoveWheel";
        internal const string InstallWheel = "shoppingcartInstallWheel";
        internal const string RemoveWheelIcon = "shopping_cart_wheel_remove";
        internal const string InstallWheelIcon = "shopping_cart_wheel_install";

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
            _addCallback(new EntityActivationCommand(
                ShoppingCartTagging.ToggleCommand,
                ShoppingCartTagging.GetCommandIcon(__instance),
                null,
                ShoppingCartTagging.GetCommandTextKey(__instance)));
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.ReorderActivationCommands))]
    internal static class ShoppingCartVehicleCommandOrderPatch
    {
        [Preserve]
        private static void Postfix(EntityVehicle __instance, List<EntityActivationCommand> _commands)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(__instance) || _commands == null)
            {
                return;
            }

            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                if (ShoppingCartTagging.IsSecurityCommand(_commands[i].commandId))
                {
                    _commands.RemoveAt(i);
                }
            }
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
            if (ShoppingCartTagging.IsSecurityCommand(commandId))
            {
                __instance.SetLocked(false);
                return false;
            }

            __instance.SetLocked(false);

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

            if (ShoppingCartTagging.IsToggleCommand(commandId))
            {
                HandleTagCommand(__instance, _playerFocusing);
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

        private static void HandleTagCommand(EntityVehicle vehicle, EntityPlayerLocal player)
        {
            bool tagged = !ShoppingCartTagging.IsTagged(vehicle);
            ShoppingCartTagging.SetTagged(vehicle, tagged, out string message);
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                ShoppingCartSpawnService.SendServerCommand("sc tag " + vehicle.entityId + " " + (tagged ? "on" : "off"));
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

    [Preserve]
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.IsLockedForLocalPlayer))]
    internal static class ShoppingCartVehicleLockStatusPatch
    {
        [Preserve]
        private static bool Prefix(EntityVehicle __instance, ref bool __result)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.HandleNavObject))]
    internal static class ShoppingCartVehicleNavObjectPatch
    {
        [Preserve]
        private static bool Prefix(EntityVehicle __instance)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(__instance))
            {
                return true;
            }

            ShoppingCartTagging.RefreshNavObject(__instance);
            return false;
        }
    }
}
