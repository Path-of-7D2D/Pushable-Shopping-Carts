using System;
using HarmonyLib;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartBlockCommands
    {
        internal const string PushCommand = "shoppingcartBlockPush";
        private const string InspectPromptKey = "shoppingcartBlockTooltipInspect";

        internal static BlockActivationCommand CreatePushCommand()
        {
            return new BlockActivationCommand(PushCommand, "ui_game_symbol_shopping_cart", _enabled: true);
        }

        internal static BlockActivationCommand[] AddPushCommand(BlockActivationCommand[] commands)
        {
            if (commands == null || commands == BlockActivationCommand.Empty)
            {
                return new[] { CreatePushCommand() };
            }

            for (int i = 0; i < commands.Length; i++)
            {
                if (IsPushCommand(commands[i].text))
                {
                    commands[i].enabled = true;
                    return commands;
                }
            }

            var result = new BlockActivationCommand[commands.Length + 1];
            Array.Copy(commands, result, commands.Length);
            result[result.Length - 1] = CreatePushCommand();
            return result;
        }

        internal static bool IsPushCommand(string commandName)
        {
            return PushCommand.Equals(commandName, StringComparison.Ordinal);
        }

        internal static string GetInspectPrompt(EntityAlive entityFocusing)
        {
            EntityPlayerLocal player = entityFocusing as EntityPlayerLocal ?? GameManager.Instance?.World?.GetPrimaryPlayer();
            string binding = "E";
            if (player != null)
            {
                string inputBinding = player.playerInput.Activate.GetBindingXuiMarkupString() +
                    player.playerInput.PermanentActions.Activate.GetBindingXuiMarkupString();
                if (!string.IsNullOrEmpty(inputBinding))
                {
                    binding = inputBinding;
                }
            }

            string template = Localization.Get(InspectPromptKey);
            if (string.IsNullOrEmpty(template) || template == InspectPromptKey)
            {
                template = "Press {0} to Inspect";
            }

            return string.Format(template, binding);
        }

        internal static string AppendInspectPrompt(string current, EntityAlive entityFocusing)
        {
            string prompt = GetInspectPrompt(entityFocusing);
            if (string.IsNullOrEmpty(current))
            {
                return prompt;
            }

            if (current.IndexOf(prompt, StringComparison.Ordinal) >= 0)
            {
                return current;
            }

            return current + "\n" + prompt;
        }

        internal static bool TryHandleActivation(
            string commandName,
            WorldBase world,
            Vector3i blockPos,
            BlockValue blockValue,
            EntityPlayerLocal player,
            out bool result)
        {
            result = false;
            if (!IsPushCommand(commandName))
            {
                return false;
            }

            if (!ShoppingCartVisuals.IsWorldShoppingCartBlock(blockValue.Block))
            {
                return false;
            }

            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                ShoppingCartSpawnService.SendServerCommand("sc convert " + blockPos.x + " " + blockPos.y + " " + blockPos.z);
                ShowTooltip(player, "Requested shopping cart conversion.");
                result = true;
                return true;
            }

            if (ShoppingCartSpawnService.TryConvertWorldBlock(blockPos, player, out string message))
            {
                ShowTooltip(player, message);
            }
            else
            {
                ShowTooltip(player, message);
            }

            result = true;
            return true;
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

            __result = ShoppingCartBlockCommands.AddPushCommand(__result);
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch(nameof(Block.GetActivationText), new Type[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) })]
    internal static class ShoppingCartBlockActivationTextPatch
    {
        [Preserve]
        private static void Postfix(Block __instance, EntityAlive _entityFocusing, ref string __result)
        {
            if (ShoppingCartVisuals.IsWorldShoppingCartBlock(__instance))
            {
                __result = ShoppingCartBlockCommands.AppendInspectPrompt(__result, _entityFocusing);
            }
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.HasBlockActivationCommands))]
    internal static class ShoppingCartCompositeBlockHasActivationPatch
    {
        [Preserve]
        private static void Postfix(BlockCompositeTileEntity __instance, ref bool __result)
        {
            if (ShoppingCartVisuals.IsWorldShoppingCartBlock(__instance))
            {
                __result = true;
            }
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.GetBlockActivationCommands))]
    internal static class ShoppingCartCompositeBlockGetActivationCommandsPatch
    {
        [Preserve]
        private static void Postfix(BlockCompositeTileEntity __instance, ref BlockActivationCommand[] __result)
        {
            if (ShoppingCartVisuals.IsWorldShoppingCartBlock(__instance))
            {
                __result = ShoppingCartBlockCommands.AddPushCommand(__result);
            }
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(BlockCompositeTileEntity))]
    [HarmonyPatch(nameof(BlockCompositeTileEntity.GetActivationText), new Type[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) })]
    internal static class ShoppingCartCompositeBlockActivationTextPatch
    {
        [Preserve]
        private static void Postfix(BlockCompositeTileEntity __instance, EntityAlive _entityFocusing, ref string __result)
        {
            if (ShoppingCartVisuals.IsWorldShoppingCartBlock(__instance))
            {
                __result = ShoppingCartBlockCommands.AppendInspectPrompt(__result, _entityFocusing);
            }
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
            if (ShoppingCartBlockCommands.TryHandleActivation(_commandName, _world, _blockPos, _blockValue, _player, out bool result))
            {
                __result = result;
                return false;
            }

            return true;
        }
    }

    [Preserve]
    [HarmonyPatch(typeof(BlockCompositeTileEntity))]
    [HarmonyPatch(nameof(BlockCompositeTileEntity.OnBlockActivated), new Type[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
    internal static class ShoppingCartCompositeBlockActivatedPatch
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
            if (ShoppingCartBlockCommands.TryHandleActivation(_commandName, _world, _blockPos, _blockValue, _player, out bool result))
            {
                __result = result;
                return false;
            }

            return true;
        }
    }
}
