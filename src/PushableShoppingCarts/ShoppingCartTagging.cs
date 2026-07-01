using UnityEngine;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartTagging
    {
        internal const string ToggleCommand = "shoppingcartToggleTag";
        internal const string TagIcon = "shopping_cart_tag";
        internal const string UntagIcon = "shopping_cart_untag";
        internal const string TagTextKey = "shoppingcartTagCart";
        internal const string UntagTextKey = "shoppingcartUntagCart";

        private const string MetadataKey = "p7d2d.shoppingcart.tag.v1";
        private const string TaggedValue = "tagged";
        private const string UntaggedValue = "untagged";
        private const string NavObjectClassName = "shoppingcart";

        internal enum TagMode
        {
            Default,
            Tagged,
            Untagged
        }

        internal static bool IsSecurityCommand(string command)
        {
            return command == "lock" || command == "unlock" || command == "keypad";
        }

        internal static bool IsToggleCommand(string command)
        {
            return command == ToggleCommand;
        }

        internal static string GetCommandTextKey(EntityVehicle vehicle)
        {
            return IsTagged(vehicle) ? UntagTextKey : TagTextKey;
        }

        internal static string GetCommandIcon(EntityVehicle vehicle)
        {
            return IsTagged(vehicle) ? UntagIcon : TagIcon;
        }

        internal static bool IsTagged(EntityVehicle vehicle)
        {
            return GetMode(vehicle) == TagMode.Tagged;
        }

        internal static void AutoTagFixedCartOnPush(EntityVehicle vehicle)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle) || GetMode(vehicle) != TagMode.Default)
            {
                RefreshNavObject(vehicle);
                return;
            }

            if (!ShoppingCartState.Get(vehicle).IsFixed)
            {
                RefreshNavObject(vehicle);
                return;
            }

            SetMode(vehicle, TagMode.Tagged);
            RefreshNavObject(vehicle);
            SaveVehiclesSoon();

            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                ShoppingCartSpawnService.SendServerCommand("sc tag " + vehicle.entityId + " on");
            }
        }

        internal static bool Toggle(EntityVehicle vehicle, out string message)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                message = "That is not a shopping cart.";
                return false;
            }

            return SetTagged(vehicle, !IsTagged(vehicle), out message);
        }

        internal static bool SetTagged(EntityVehicle vehicle, bool tagged, out string message)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                message = "That is not a shopping cart.";
                return false;
            }

            SetMode(vehicle, tagged ? TagMode.Tagged : TagMode.Untagged);
            RefreshNavObject(vehicle);
            SaveVehiclesSoon();
            message = tagged ? "Tagged shopping cart." : "Untagged shopping cart.";
            return true;
        }

        internal static void RefreshNavObject(EntityVehicle vehicle)
        {
            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                return;
            }

            if (GameManager.IsDedicatedServer || GameManager.Instance?.World?.GetPrimaryPlayer() == null)
            {
                Unregister(vehicle);
                return;
            }

            if (!IsTagged(vehicle) || !vehicle.LocalPlayerIsOwner())
            {
                Unregister(vehicle);
                return;
            }

            if (vehicle.NavObject == null)
            {
                Transform navTransform = GetNavTransform(vehicle);
                if (navTransform != null)
                {
                    vehicle.NavObject = NavObjectManager.Instance.RegisterNavObject(NavObjectClassName, navTransform);
                }
            }
            else
            {
                vehicle.NavObject.IsActive = true;
            }
        }

        private static TagMode GetMode(EntityVehicle vehicle)
        {
            ItemValue itemValue = GetItemValue(vehicle);
            if (itemValue == null || !itemValue.TryGetMetadata(MetadataKey, out string raw))
            {
                return TagMode.Default;
            }

            if (raw == TaggedValue)
            {
                return TagMode.Tagged;
            }

            if (raw == UntaggedValue)
            {
                return TagMode.Untagged;
            }

            return TagMode.Default;
        }

        private static void SetMode(EntityVehicle vehicle, TagMode mode)
        {
            ItemValue itemValue = GetItemValue(vehicle);
            if (itemValue == null)
            {
                return;
            }

            string value = mode == TagMode.Tagged ? TaggedValue :
                mode == TagMode.Untagged ? UntaggedValue :
                string.Empty;
            itemValue.SetMetadata(MetadataKey, value);
            vehicle.activationCommands = null;
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

        private static Transform GetNavTransform(EntityVehicle vehicle)
        {
            Vehicle vehicleData = vehicle != null ? vehicle.GetVehicle() : null;
            Transform meshTransform = vehicleData != null ? vehicleData.GetMeshTransform() : null;
            if (meshTransform != null)
            {
                return meshTransform;
            }

            return vehicle != null ? vehicle.ModelTransform : null;
        }

        private static void Unregister(EntityVehicle vehicle)
        {
            if (vehicle != null && vehicle.NavObject != null)
            {
                NavObjectManager.Instance.UnRegisterNavObject(vehicle.NavObject);
                vehicle.NavObject = null;
            }
        }

        private static void SaveVehiclesSoon()
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                return;
            }

            VehicleManager manager = VehicleManager.Instance;
            if (manager != null)
            {
                manager.TriggerSave();
            }
        }
    }
}
