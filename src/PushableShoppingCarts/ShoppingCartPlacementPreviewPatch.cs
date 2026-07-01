using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    [Preserve]
    [HarmonyPatch(typeof(ItemActionSpawnVehicle), "SetupPreview")]
    internal static class ShoppingCartPlacementPreviewPatch
    {
        [Preserve]
        private static void Postfix(ItemActionSpawnVehicle.ItemActionDataSpawnVehicle data)
        {
            if (data == null || data.VehiclePreviewT == null || data.invData == null || data.invData.item == null)
            {
                return;
            }

            if (!string.Equals(data.invData.item.Name, ShoppingCartInventory.CartItemName, StringComparison.Ordinal))
            {
                return;
            }

            DisablePreviewCollisions(data.VehiclePreviewT);
            data.PreviewRenderers = data.VehiclePreviewT.GetComponentsInChildren<Renderer>(true);
        }

        private static void DisablePreviewCollisions(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }

            SetLayerRecursively(root, 2);
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i), layer);
            }
        }
    }
}
