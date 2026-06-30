using System;
using UnityEngine;

namespace PushableShoppingCarts
{
    internal sealed class ShoppingCartState
    {
        internal const string MetadataKey = "p7d2d.shoppingcart.v1";
        internal const int MaxMissingWheels = 4;
        internal const float MissingWheelPenalty = 0.25f;
        internal const float RottedFramePenalty = 0.15f;

        internal ShoppingCartState(int missingWheels, bool rottedFrame, bool worldCart)
        {
            MissingWheels = Mathf.Clamp(missingWheels, 0, MaxMissingWheels);
            RottedFrame = rottedFrame;
            WorldCart = worldCart;
        }

        internal int MissingWheels { get; private set; }

        internal bool RottedFrame { get; private set; }

        internal bool WorldCart { get; private set; }

        internal bool IsFixed => MissingWheels == 0 && !RottedFrame;

        internal float ConditionMovePenalty =>
            Mathf.Clamp01(MissingWheels * MissingWheelPenalty + (RottedFrame ? RottedFramePenalty : 0f));

        internal static ShoppingCartState Fixed()
        {
            return new ShoppingCartState(0, false, false);
        }

        internal static ShoppingCartState CreateWorldState(string blockName, Vector3i blockPos)
        {
            int seed = blockPos.x * 73856093 ^ blockPos.y * 19349663 ^ blockPos.z * 83492791;
            if (!string.IsNullOrEmpty(blockName))
            {
                seed ^= blockName.GetHashCode();
            }

            var random = new System.Random(seed & 0x7fffffff);
            int roll = random.Next(100);
            int missingWheels;
            if (roll < 62)
            {
                missingWheels = 4;
            }
            else if (roll < 82)
            {
                missingWheels = 3;
            }
            else if (roll < 94)
            {
                missingWheels = 2;
            }
            else
            {
                missingWheels = 1;
            }

            bool tipped = !string.IsNullOrEmpty(blockName) &&
                blockName.IndexOf("Tipped", StringComparison.OrdinalIgnoreCase) >= 0;
            int rottedChance = tipped ? 70 : 40;
            bool rotted = random.Next(100) < rottedChance;

            return new ShoppingCartState(missingWheels, rotted, true);
        }

        internal static ShoppingCartState Get(EntityVehicle vehicle)
        {
            if (TryRead(GetItemValue(vehicle), out ShoppingCartState state))
            {
                return state;
            }

            return Fixed();
        }

        internal void ApplyTo(EntityVehicle vehicle)
        {
            ItemValue itemValue = GetItemValue(vehicle);
            if (itemValue != null)
            {
                itemValue.SetMetadata(MetadataKey, Serialize());
            }

            ShoppingCartVisuals.ApplyState(vehicle, this);
        }

        internal ShoppingCartState WithMissingWheels(int missingWheels)
        {
            return new ShoppingCartState(missingWheels, RottedFrame, WorldCart);
        }

        internal string DisplaySummary()
        {
            if (IsFixed)
            {
                return "fixed";
            }

            string text = MissingWheels + " missing wheel" + (MissingWheels == 1 ? "" : "s");
            if (RottedFrame)
            {
                text += ", rotted frame";
            }

            return text;
        }

        private static ItemValue GetItemValue(EntityVehicle vehicle)
        {
            if (vehicle == null)
            {
                return null;
            }

            Vehicle vehicleData = vehicle.GetVehicle();
            return vehicleData != null ? vehicleData.itemValue : null;
        }

        private string Serialize()
        {
            return "1|" + MissingWheels + "|" + (RottedFrame ? "1" : "0") + "|" + (WorldCart ? "1" : "0");
        }

        private static bool TryRead(ItemValue itemValue, out ShoppingCartState state)
        {
            state = null;
            if (itemValue == null || !itemValue.TryGetMetadata(MetadataKey, out string raw) || string.IsNullOrEmpty(raw))
            {
                return false;
            }

            string[] parts = raw.Split('|');
            if (parts.Length != 4 || parts[0] != "1")
            {
                return false;
            }

            if (!int.TryParse(parts[1], out int missingWheels))
            {
                return false;
            }

            state = new ShoppingCartState(
                missingWheels,
                parts[2] == "1",
                parts[3] == "1");
            return true;
        }
    }
}
