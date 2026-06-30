using System;
using HarmonyLib;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartBlockCommands
    {
        internal const string PushCommand = "shoppingcartBlockPush";

        internal static BlockActivationCommand CreatePushCommand()
        {
            return new BlockActivationCommand(PushCommand, "ui_game_symbol_shopping_cart", _enabled: true);
        }

        internal static bool IsPushCommand(string commandName)
        {
            return PushCommand.Equals(commandName, StringComparison.Ordinal);
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(Block), nameof(Block.HasBlockActivationCommands))]
    internal static class ShoppingCartBlockHasActivationPatch
    {
        [Preserve]
        private static void Postfix(Block __instance, ref bool __result)
        {
            if (ShoppingCartVisuals.IsWorldShoppingCartBlock(__instance))
            {
                __result = true;
            }
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(Block), nameof(Block.GetBlockActivationCommands))]
    internal static class ShoppingCartBlockGetActivationCommandsPatch
    {
        [Preserve]
        private static void Postfix(Block __instance, ref BlockActivationCommand[] __result)
        {
            if (!ShoppingCartVisuals.IsWorldShoppingCartBlock(__instance))
            {
                return;
            }

            if (__result == null || __result == BlockActivationCommand.Empty)
            {
                __result = new[] { ShoppingCartBlockCommands.CreatePushCommand() };
                return;
            }

            for (int i = 0; i < __result.Length; i++)
            {
                if (ShoppingCartBlockCommands.IsPushCommand(__result[i].text))
                {
                    __result[i].enabled = true;
                    return;
                }
            }

            var commands = new BlockActivationCommand[__result.Length + 1];
            Array.Copy(__result, commands, __result.Length);
            commands[commands.Length - 1] = ShoppingCartBlockCommands.CreatePushCommand();
            __result = commands;
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch(nameof(Block.OnBlockActivated), new Type[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
    internal static class ShoppingCartBlockActivatedPatch
    {
        [Preserve]
        private static bool Prefix(
            string _commandName,
            WorldBase _world,
            Vector3i _blockPos,
            BlockValue _blockValue,
            EntityPlayerLocal _player,
            ref bool __result)
        {
            if (!ShoppingCartBlockCommands.IsPushCommand(_commandName))
            {
                return true;
            }

            if (!ShoppingCartVisuals.IsWorldShoppingCartBlock(_blockValue.Block))
            {
                return true;
            }

            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                ShoppingCartSpawnService.SendServerCommand("sc convert " + _blockPos.x + " " + _blockPos.y + " " + _blockPos.z);
                ShowTooltip(_player, "Requested shopping cart conversion.");
                __result = true;
                return false;
            }

            if (ShoppingCartSpawnService.TryConvertWorldBlock(_blockPos, _player, out string message))
            {
                ShowTooltip(_player, message);
            }
            else
            {
                ShowTooltip(_player, message);
            }

            __result = true;
            return false;
        }

        private static void ShowTooltip(EntityPlayerLocal player, string message)
        {
            if (player != null && !string.IsNullOrEmpty(message))
            {
                GameManager.ShowTooltip(player, "[ShoppingCart] " + message, false, false, 3f);
            }
        }
    }
}
