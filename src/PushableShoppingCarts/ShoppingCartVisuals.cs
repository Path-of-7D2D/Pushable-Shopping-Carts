using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartVisuals
    {
        internal const string EntityName = "vehicleShoppingCart";
        internal const string VanillaVisualPrefab = "@:Entities/LootContainers/groceryCartEmptyPrefab.prefab";

        private const string VisualRootName = "P7D2D_ShoppingCartVisual";
        private const string LeftGripName = "P7D2D_Grip_Left";
        private const string RightGripName = "P7D2D_Grip_Right";

        internal static int RepairActiveShoppingCarts(bool logDetails)
        {
            VehicleManager manager = VehicleManager.Instance;
            if (manager == null)
            {
                return 0;
            }

            int repaired = 0;
            for (int i = 0; i < manager.vehiclesActive.Count; i++)
            {
                EntityVehicle vehicle = manager.vehiclesActive[i];
                if (IsShoppingCart(vehicle) && RepairVehicle(vehicle, logDetails))
                {
                    repaired++;
                }
            }

            return repaired;
        }

        internal static bool RepairVehicle(EntityVehicle vehicle, bool logDetails)
        {
            if (!IsShoppingCart(vehicle))
            {
                return false;
            }

            Transform root = vehicle.RootTransform;
            Transform model = vehicle.ModelTransform;
            Transform physics = vehicle.PhysicsTransform;
            if (root == null || model == null)
            {
                if (logDetails)
                {
                    Log.Out("[PushableShoppingCarts] Visual repair skipped for id {0}: root={1}, model={2}, physics={3}",
                        vehicle.entityId,
                        FormatTransform(root),
                        FormatTransform(model),
                        FormatTransform(physics));
                }

                return false;
            }

            root.gameObject.SetActive(true);
            model.gameObject.SetActive(true);
            if (vehicle.emodel != null)
            {
                vehicle.emodel.SetVisible(true, true);
            }

            Transform visualRoot = EnsureVanillaVisual(model);
            if (visualRoot != null)
            {
                HideScaffoldRenderers(model);
                visualRoot.gameObject.SetActive(true);
            }
            else
            {
                ShowScaffoldRenderers(model);
            }

            ApplyState(vehicle, ShoppingCartState.Get(vehicle));
            EnsureGrip(model, true);
            EnsureGrip(model, false);

            int enabledRenderers = CountEnabledRenderers(model);
            if (logDetails)
            {
                Bounds? bounds = GetRendererBounds(model);
                string boundsText = bounds.HasValue
                    ? "center=" + Format(bounds.Value.center + Origin.position) + ", size=" + Format(bounds.Value.size)
                    : "<none>";

                Log.Out("[PushableShoppingCarts] Visual state id {0}: root={1}, model={2}, visual={3}, physics={4}, renderers={5}, state={6}, bounds={7}",
                    vehicle.entityId,
                    FormatTransform(root),
                    FormatTransform(model),
                    visualRoot != null ? GetPath(visualRoot, root) : "<fallback scaffold>",
                    FormatTransform(physics),
                    enabledRenderers,
                    ShoppingCartState.Get(vehicle).DisplaySummary(),
                    boundsText);
            }

            return enabledRenderers > 0;
        }

        internal static void ApplyState(EntityVehicle vehicle, ShoppingCartState state)
        {
            if (vehicle == null || state == null)
            {
                return;
            }

            Transform model = vehicle.ModelTransform;
            Transform visualRoot = model != null ? model.Find(VisualRootName) : null;
            if (visualRoot == null)
            {
                return;
            }

            List<Transform> wheelTransforms = FindWheelTransforms(visualRoot);
            for (int i = 0; i < wheelTransforms.Count; i++)
            {
                bool visible = i >= state.MissingWheels;
                SetRenderersEnabled(wheelTransforms[i], visible);
            }
        }

        internal static List<Transform> GetSpinWheelTransforms(EntityVehicle vehicle)
        {
            var results = new List<Transform>();
            Transform model = vehicle != null ? vehicle.ModelTransform : null;
            Transform visualRoot = model != null ? model.Find(VisualRootName) : null;
            if (visualRoot != null)
            {
                results.AddRange(FindWheelTransforms(visualRoot));
            }

            if (model != null)
            {
                Transform scaffoldWheel = model.Find("Mesh/M/Forks/Wheel0");
                if (scaffoldWheel != null)
                {
                    results.Add(scaffoldWheel);
                }
            }

            return results;
        }

        internal static Transform GetOrCreateGrip(EntityVehicle vehicle, bool left)
        {
            Transform model = vehicle != null ? vehicle.ModelTransform : null;
            if (model == null)
            {
                return null;
            }

            Transform existing = FindContains(model, left ? "Grip_Left" : "Grip_Right");
            if (existing != null)
            {
                return existing;
            }

            return EnsureGrip(model, left);
        }

        internal static List<EntityVehicle> GetActiveShoppingCarts()
        {
            List<EntityVehicle> results = new List<EntityVehicle>();
            VehicleManager manager = VehicleManager.Instance;
            if (manager == null)
            {
                return results;
            }

            for (int i = 0; i < manager.vehiclesActive.Count; i++)
            {
                EntityVehicle vehicle = manager.vehiclesActive[i];
                if (IsShoppingCart(vehicle))
                {
                    results.Add(vehicle);
                }
            }

            return results;
        }

        internal static bool IsShoppingCart(EntityVehicle vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            return IsShoppingCartEntityClass(vehicle.entityClass);
        }

        internal static bool IsShoppingCartEntityClass(int entityClassId)
        {
            int currentId = EntityClass.GetId(EntityName);
            if (entityClassId == currentId)
            {
                return true;
            }

            string entityClassName = EntityClass.GetEntityClassName(entityClassId);
            return EntityName.Equals(entityClassName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWorldShoppingCartBlock(Block block)
        {
            return block != null && IsWorldShoppingCartBlockName(block.GetBlockName());
        }

        internal static bool IsWorldShoppingCartBlockName(string blockName)
        {
            return !string.IsNullOrEmpty(blockName) &&
                blockName.StartsWith("cntShoppingCart", StringComparison.OrdinalIgnoreCase);
        }

        private static Transform EnsureVanillaVisual(Transform model)
        {
            Transform existing = model.Find(VisualRootName);
            if (existing != null)
            {
                return existing;
            }

            GameObject prefab = DataLoader.LoadAsset<GameObject>(VanillaVisualPrefab);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = VisualRootName;
            instance.transform.SetParent(model, worldPositionStays: false);
            instance.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            DisableColliders(instance.transform);
            return instance.transform;
        }

        private static Transform EnsureGrip(Transform model, bool left)
        {
            string name = left ? LeftGripName : RightGripName;
            Transform grip = model.Find(name);
            if (grip != null)
            {
                return grip;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(model, worldPositionStays: false);
            go.transform.localPosition = new Vector3(left ? -0.42f : 0.42f, 0.86f, -0.95f);
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static List<Transform> FindWheelTransforms(Transform root)
        {
            var results = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string name = all[i].name;
                if (name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("caster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("tire", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (all[i].GetComponentsInChildren<Renderer>(true).Length > 0)
                    {
                        results.Add(all[i]);
                    }
                }
            }

            return results;
        }

        private static void HideScaffoldRenderers(Transform model)
        {
            Transform mesh = model.Find("Mesh");
            if (mesh == null)
            {
                return;
            }

            Renderer[] renderers = mesh.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private static void ShowScaffoldRenderers(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = true;
            }
        }

        private static void DisableColliders(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static void SetRenderersEnabled(Transform root, bool enabled)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled;
            }
        }

        private static int CountEnabledRenderers(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static Bounds? GetRendererBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds? bounds = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                {
                    continue;
                }

                if (!bounds.HasValue)
                {
                    bounds = renderers[i].bounds;
                }
                else
                {
                    Bounds combined = bounds.Value;
                    combined.Encapsulate(renderers[i].bounds);
                    bounds = combined;
                }
            }

            return bounds;
        }

        private static Transform FindContains(Transform root, string namePart)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static string FormatTransform(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            return GetPath(transform, null) + " pos=" + Format(transform.position + Origin.position) +
                " local=" + Format(transform.localPosition) +
                " active=" + transform.gameObject.activeInHierarchy +
                " layer=" + transform.gameObject.layer;
        }

        private static string GetPath(Transform transform, Transform relativeRoot)
        {
            if (transform == null)
            {
                return "<null>";
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null && current != relativeRoot)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string Format(Vector3 position)
        {
            return position.x.ToString("0.00") + "," +
                position.y.ToString("0.00") + "," +
                position.z.ToString("0.00");
        }
    }

    [Preserve]
    internal sealed class ShoppingCartVisualRepairBehaviour : MonoBehaviour
    {
        private float nextRepairTime;

        private void Update()
        {
            if (Time.realtimeSinceStartup < nextRepairTime)
            {
                return;
            }

            nextRepairTime = Time.realtimeSinceStartup + 1.5f;
            World world = GameManager.Instance?.World;
            if (world == null)
            {
                return;
            }

            ShoppingCartVisuals.RepairActiveShoppingCarts(false);
        }
    }
}
