using System;
using System.Collections.Generic;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace PushableShoppingCarts.Commands
{
    [Preserve]
    public class ConsoleCmdShoppingCart : ConsoleCmdAbstract
    {
        private const float DefaultDistance = 4.5f;

        public override bool IsExecuteOnClient => true;

        public override DeviceFlag AllowedDeviceTypesClient =>
            DeviceFlag.StandaloneWindows | DeviceFlag.StandaloneLinux | DeviceFlag.StandaloneOSX;

        public override string[] getCommands()
        {
            return new[] { "shoppingcart", "spawnshoppingcart", "sc" };
        }

        public override string getDescription()
        {
            return "Spawns, converts, and debugs pushable shopping carts.";
        }

        public override string getHelp()
        {
            return "Usage:\n" +
                "  sc fixed [distance]              spawn a fully fixed pushable cart\n" +
                "  sc item [count]                  give fixed shopping cart item(s)\n" +
                "  sc wheel [count]                 give shopping cart wheel item(s)\n" +
                "  sc world [distance] [blockName]  spawn a vanilla world shopping cart block\n" +
                "  sc push [offset] [lift] [tilt]   push nearest active cart\n" +
                "  sc tag [entityId] [on|off|toggle] tag or untag a cart icon\n" +
                "  sc hands x y z                   tune hand rotation while pushing\n" +
                "  sc handpos x y z                 tune grip-local hand offset while pushing\n" +
                "  sc drop                          release the pushed cart\n" +
                "  sc debug                         log active cart state\n" +
                "  sc cleanup                       remove active and unloaded cart vehicles\n" +
                "\n" +
                "Server/internal:\n" +
                "  sc fixedat x y z yaw\n" +
                "  sc worldat blockName x y z [rotation]\n" +
                "  sc convert x y z\n" +
                "  sc removewheel entityId\n" +
                "  sc installwheel entityId\n" +
                "  sc tag entityId on|off|toggle";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count == 0)
            {
                SpawnFixed(_params, _senderInfo);
                return;
            }

            string sub = _params[0];
            if (IsSubcommand(sub, "help"))
            {
                Output(getHelp());
                return;
            }

            if (IsSubcommand(sub, "fixed") || IsSubcommand(sub, "spawn"))
            {
                SpawnFixed(_params, _senderInfo);
                return;
            }

            if (IsSubcommand(sub, "item") || IsSubcommand(sub, "givefixed"))
            {
                GiveItem(_params, _senderInfo, ShoppingCartInventory.CartItemName);
                return;
            }

            if (IsSubcommand(sub, "wheel") || IsSubcommand(sub, "givewheel"))
            {
                GiveItem(_params, _senderInfo, ShoppingCartInventory.WheelItemName);
                return;
            }

            if (IsSubcommand(sub, "world") || IsSubcommand(sub, "worldcart"))
            {
                SpawnWorldBlock(_params, _senderInfo);
                return;
            }

            if (IsSubcommand(sub, "push") || IsSubcommand(sub, "grab"))
            {
                PushNearest(_senderInfo, _params);
                return;
            }

            if (IsSubcommand(sub, "drop") || IsSubcommand(sub, "release") || IsSubcommand(sub, "park"))
            {
                DropCurrent();
                return;
            }

            if (IsSubcommand(sub, "hands") || IsSubcommand(sub, "handpos"))
            {
                TuneHands(_params);
                return;
            }

            if (IsCleanupSubcommand(sub))
            {
                CleanupShoppingCarts();
                return;
            }

            if (IsSubcommand(sub, "debug"))
            {
                DebugShoppingCarts();
                return;
            }

            if (IsSubcommand(sub, "fixedat"))
            {
                SpawnFixedAt(_params, _senderInfo);
                return;
            }

            if (IsSubcommand(sub, "worldat"))
            {
                SpawnWorldBlockAt(_params);
                return;
            }

            if (IsSubcommand(sub, "convert"))
            {
                ConvertWorldBlock(_params, _senderInfo);
                return;
            }

            if (IsSubcommand(sub, "removewheel"))
            {
                WheelCommand(_params, _senderInfo, remove: true);
                return;
            }

            if (IsSubcommand(sub, "installwheel"))
            {
                WheelCommand(_params, _senderInfo, remove: false);
                return;
            }

            if (IsSubcommand(sub, "tag") || IsSubcommand(sub, "untag"))
            {
                TagCommand(_params, _senderInfo, IsSubcommand(sub, "untag") ? "off" : null);
                return;
            }

            Output("Unknown subcommand: " + sub);
            Output(getHelp());
        }

        private static void SpawnFixed(List<string> parameters, CommandSenderInfo senderInfo)
        {
            EntityPlayer player = GetSenderPlayer(senderInfo);
            if (player == null)
            {
                Output("No player entity is available. Run this from an in-game client console.");
                return;
            }

            float distance = DefaultDistance;
            if (parameters.Count > 1 && !float.TryParse(parameters[1], out distance))
            {
                Output("Invalid distance: " + parameters[1]);
                return;
            }

            distance = Mathf.Clamp(distance, 2.5f, 12f);
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Vector3 position = GetSpawnPosition(player, distance);
                ShoppingCartSpawnService.SendServerCommand("sc fixedat " +
                    position.x.ToString("0.###") + " " +
                    position.y.ToString("0.###") + " " +
                    position.z.ToString("0.###") + " " +
                    (player.rotation.y + 90f).ToString("0.###"));
                Output("Requested fixed shopping cart spawn at " + Format(position) + ".");
                return;
            }

            if (ShoppingCartSpawnService.TrySpawnFixedInFront(player, distance, out string message))
            {
                Output(message);
            }
            else
            {
                Output(message);
            }
        }

        private static void GiveItem(List<string> parameters, CommandSenderInfo senderInfo, string itemName)
        {
            EntityAlive player = GetSenderPlayer(senderInfo);
            if (player == null)
            {
                Output("No player entity is available.");
                return;
            }

            int count = 1;
            if (parameters.Count > 1 && !int.TryParse(parameters[1], out count))
            {
                Output("Invalid count: " + parameters[1]);
                return;
            }

            if (ShoppingCartInventory.GiveItem(player, itemName, Mathf.Clamp(count, 1, 100), out string message))
            {
                Output("Gave " + count + " " + itemName + ".");
            }
            else
            {
                Output(message);
            }
        }

        private static void SpawnWorldBlock(List<string> parameters, CommandSenderInfo senderInfo)
        {
            EntityPlayer player = GetSenderPlayer(senderInfo);
            if (player == null)
            {
                Output("No player entity is available. Run this from an in-game client console.");
                return;
            }

            float distance = 3f;
            if (parameters.Count > 1 && !float.TryParse(parameters[1], out distance))
            {
                Output("Invalid distance: " + parameters[1]);
                return;
            }

            string blockName = parameters.Count > 2 ? parameters[2] : ShoppingCartSpawnService.DefaultWorldBlockName;
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Vector3 forward = Quaternion.Euler(0f, player.rotation.y, 0f) * Vector3.forward;
                Vector3i blockPos = World.worldToBlockPos(player.position + forward.normalized * distance);
                byte rotation = (byte)(Mathf.RoundToInt(player.rotation.y / 90f) & 3);
                ShoppingCartSpawnService.SendServerCommand("sc worldat " + ShoppingCartSpawnService.Quote(blockName) + " " +
                    blockPos.x + " " + blockPos.y + " " + blockPos.z + " " + rotation);
                Output("Requested world shopping cart block spawn.");
                return;
            }

            if (ShoppingCartSpawnService.TrySpawnWorldBlockInFront(player, blockName, distance, out string message))
            {
                Output(message);
            }
            else
            {
                Output(message);
            }
        }

        private static void SpawnFixedAt(List<string> parameters, CommandSenderInfo senderInfo)
        {
            if (parameters.Count < 5)
            {
                Output("Usage: sc fixedat x y z yaw");
                return;
            }

            if (!TryParseVector3(parameters, 1, out Vector3 position) ||
                !float.TryParse(parameters[4], out float yaw))
            {
                Output("Invalid position or yaw. Usage: sc fixedat x y z yaw");
                return;
            }

            EntityPlayer owner = GetSenderPlayer(senderInfo);
            if (ShoppingCartSpawnService.TrySpawnFixedAt(position, yaw, owner, out string message))
            {
                Output(message);
            }
            else
            {
                Output(message);
            }
        }

        private static void SpawnWorldBlockAt(List<string> parameters)
        {
            if (parameters.Count < 5)
            {
                Output("Usage: sc worldat blockName x y z [rotation]");
                return;
            }

            string blockName = parameters[1];
            if (!TryParseVector3i(parameters, 2, out Vector3i blockPos))
            {
                Output("Invalid position. Usage: sc worldat blockName x y z [rotation]");
                return;
            }

            byte rotation = 0;
            if (parameters.Count > 5 && !byte.TryParse(parameters[5], out rotation))
            {
                Output("Invalid rotation. Use a value from 0 to 27.");
                return;
            }

            if (ShoppingCartSpawnService.TrySpawnWorldBlockAt(blockName, blockPos, rotation, out string message))
            {
                Output(message);
            }
            else
            {
                Output(message);
            }
        }

        private static void ConvertWorldBlock(List<string> parameters, CommandSenderInfo senderInfo)
        {
            if (parameters.Count < 4 || !TryParseVector3i(parameters, 1, out Vector3i blockPos))
            {
                Output("Usage: sc convert x y z");
                return;
            }

            EntityPlayer owner = GetSenderPlayer(senderInfo);
            if (ShoppingCartSpawnService.TryConvertWorldBlock(blockPos, owner, out string message))
            {
                Output(message);
            }
            else
            {
                Output(message);
            }
        }

        private static void WheelCommand(List<string> parameters, CommandSenderInfo senderInfo, bool remove)
        {
            if (parameters.Count < 2 || !int.TryParse(parameters[1], out int entityId))
            {
                Output(remove ? "Usage: sc removewheel entityId" : "Usage: sc installwheel entityId");
                return;
            }

            EntityVehicle vehicle = GameManager.Instance?.World?.GetEntity(entityId) as EntityVehicle;
            EntityAlive player = GetSenderPlayer(senderInfo);
            bool ok = remove
                ? ShoppingCartWheelActions.TryRemoveWheel(vehicle, player, out string message)
                : ShoppingCartWheelActions.TryInstallWheel(vehicle, player, out message);
            Output(message ?? (ok ? "Updated shopping cart." : "Could not update shopping cart."));
        }

        private static void TagCommand(List<string> parameters, CommandSenderInfo senderInfo, string forcedMode)
        {
            EntityPlayer player = GetSenderPlayer(senderInfo);
            EntityVehicle vehicle = null;
            string mode = forcedMode ?? "toggle";

            if (parameters.Count > 1 && int.TryParse(parameters[1], out int entityId))
            {
                vehicle = GameManager.Instance?.World?.GetEntity(entityId) as EntityVehicle;
                if (parameters.Count > 2)
                {
                    mode = parameters[2];
                }
            }
            else
            {
                if (parameters.Count > 1 && forcedMode == null)
                {
                    mode = parameters[1];
                }

                vehicle = FindNearestShoppingCart(player);
            }

            if (!ShoppingCartVisuals.IsShoppingCart(vehicle))
            {
                Output("No shopping cart found.");
                return;
            }

            if (!TryNormalizeTagMode(mode, out string normalizedMode))
            {
                Output("Invalid tag mode. Use on, off, or toggle.");
                return;
            }

            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                ApplyTagMode(vehicle, normalizedMode, out string localMessage);
                ShoppingCartSpawnService.SendServerCommand("sc tag " + vehicle.entityId + " " + normalizedMode);
                Output(localMessage ?? "Requested shopping cart tag update.");
                return;
            }

            ApplyTagMode(vehicle, normalizedMode, out string message);
            Output(message ?? "Updated shopping cart tag.");
        }

        private static void DropCurrent()
        {
            if (PushableShoppingCartsPush.IsActive)
            {
                PushableShoppingCartsPush.Release();
                Output("Dropped the shopping cart.");
            }
            else
            {
                Output("No shopping cart is being pushed.");
            }
        }

        private static void TuneHands(List<string> parameters)
        {
            bool position = IsSubcommand(parameters[0], "handpos");
            if (parameters.Count >= 4 &&
                float.TryParse(parameters[1], out float x) &&
                float.TryParse(parameters[2], out float y) &&
                float.TryParse(parameters[3], out float z))
            {
                if (position)
                {
                    PushableShoppingCartsPush.HandOffset = new Vector3(x, y, z);
                }
                else
                {
                    PushableShoppingCartsPush.HandEuler = new Vector3(x, y, z);
                }

                PushableShoppingCartsPush.RefreshHandIK();
            }

            Output("Hand rot=" + Format(PushableShoppingCartsPush.HandEuler) +
                " offset=" + Format(PushableShoppingCartsPush.HandOffset) +
                (PushableShoppingCartsPush.IsActive ? "" : " (push a cart to see it update)"));
        }

        private static void CleanupShoppingCarts()
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Output("Cleanup must run on the server. In single-player, run it from the in-game console after loading the world.");
                return;
            }

            World world = GameManager.Instance?.World;
            VehicleManager manager = VehicleManager.Instance;
            if (world == null || manager == null)
            {
                Output("World or VehicleManager is not available yet.");
                return;
            }

            List<int> activeEntityIds = new List<int>();
            for (int i = 0; i < manager.vehiclesActive.Count; i++)
            {
                EntityVehicle vehicle = manager.vehiclesActive[i];
                if (ShoppingCartVisuals.IsShoppingCart(vehicle))
                {
                    activeEntityIds.Add(vehicle.entityId);
                }
            }

            int removedActive = 0;
            for (int i = 0; i < activeEntityIds.Count; i++)
            {
                Entity entity = world.GetEntity(activeEntityIds[i]);
                if (entity != null)
                {
                    world.RemoveEntity(entity.entityId, EnumRemoveEntityReason.Killed);
                    removedActive++;
                }
            }

            int removedUnloaded = 0;
            for (int i = manager.vehiclesUnloaded.Count - 1; i >= 0; i--)
            {
                EntityCreationData vehicleData = manager.vehiclesUnloaded[i];
                if (vehicleData != null && ShoppingCartVisuals.IsShoppingCartEntityClass(vehicleData.entityClass))
                {
                    manager.vehiclesUnloaded.RemoveAt(i);
                    removedUnloaded++;
                }
            }

            if (removedActive > 0 || removedUnloaded > 0)
            {
                manager.TriggerSave();
                manager.UpdateVehicleWaypoints();
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                    NetPackageManager.GetPackage<NetPackageVehicleCount>().Setup());
                manager.Save();
                manager.WaitOnSave();
            }

            Output("Removed " + removedActive + " active and " + removedUnloaded + " unloaded shopping cart record(s).");
        }

        private static void DebugShoppingCarts()
        {
            List<EntityVehicle> shoppingCarts = ShoppingCartVisuals.GetActiveShoppingCarts();
            if (shoppingCarts.Count == 0)
            {
                Output("No active shopping carts found.");
                return;
            }

            int repaired = 0;
            for (int i = 0; i < shoppingCarts.Count; i++)
            {
                EntityVehicle vehicle = shoppingCarts[i];
                if (ShoppingCartVisuals.RepairVehicle(vehicle, true))
                {
                    repaired++;
                }

                ShoppingCartState state = ShoppingCartState.Get(vehicle);
                Output("Cart " + vehicle.entityId + ": " + state.DisplaySummary() +
                    ", cargoPenalty=" + PushableShoppingCartsPush.CurrentCargoPenalty.ToString("0.00") +
                    ", conditionPenalty=" + state.ConditionMovePenalty.ToString("0.00") +
                    ", physics=" + GetPhysicsSummary(vehicle));
            }

            Output("Debugged " + shoppingCarts.Count + " active shopping cart(s); renderers found on " + repaired + ".");
        }

        private static string GetPhysicsSummary(EntityVehicle vehicle)
        {
            Rigidbody rb = vehicle != null ? vehicle.vehicleRB : null;
            if (vehicle == null || rb == null)
            {
                return "<none>";
            }

            return "active=" + vehicle.RBActive +
                ", kinematic=" + rb.isKinematic +
                ", sleeping=" + rb.IsSleeping() +
                ", vel=" + rb.velocity.magnitude.ToString("0.000") +
                ", angVel=" + rb.angularVelocity.magnitude.ToString("0.000");
        }

        private static void PushNearest(CommandSenderInfo senderInfo, List<string> parameters)
        {
            EntityPlayer player = GetSenderPlayer(senderInfo);
            if (player == null)
            {
                Output("No player entity is available. Run this from an in-game client console.");
                return;
            }

            float offset = PushableShoppingCartsPush.DefaultFrontOffset;
            if (parameters.Count > 1 && !float.TryParse(parameters[1], out offset))
            {
                Output("Invalid offset: " + parameters[1]);
                return;
            }

            float lift = PushableShoppingCartsPush.DefaultHeightLift;
            if (parameters.Count > 2 && !float.TryParse(parameters[2], out lift))
            {
                Output("Invalid lift: " + parameters[2]);
                return;
            }

            float tilt = PushableShoppingCartsPush.DefaultTiltDegrees;
            if (parameters.Count > 3 && !float.TryParse(parameters[3], out tilt))
            {
                Output("Invalid tilt: " + parameters[3]);
                return;
            }

            List<EntityVehicle> shoppingCarts = ShoppingCartVisuals.GetActiveShoppingCarts();
            EntityVehicle nearest = FindNearestShoppingCart(player, shoppingCarts);

            if (nearest == null)
            {
                Output("No shopping cart found nearby. Spawn one with 'sc fixed' first.");
                return;
            }

            PushableShoppingCartsPush.Begin(
                nearest,
                Mathf.Clamp(offset, 0.6f, 3f),
                Mathf.Clamp(lift, -0.2f, 1.2f),
                Mathf.Clamp(tilt, 0f, 45f));
            Output("Now pushing shopping cart " + nearest.entityId + ". Use 'sc drop' to release.");
        }

        private static EntityVehicle FindNearestShoppingCart(EntityPlayer player)
        {
            return FindNearestShoppingCart(player, ShoppingCartVisuals.GetActiveShoppingCarts());
        }

        private static EntityVehicle FindNearestShoppingCart(EntityPlayer player, List<EntityVehicle> shoppingCarts)
        {
            if (player == null || shoppingCarts == null)
            {
                return null;
            }

            EntityVehicle nearest = null;
            float nearestSq = float.MaxValue;
            for (int i = 0; i < shoppingCarts.Count; i++)
            {
                EntityVehicle candidate = shoppingCarts[i];
                float distSq = (candidate.position - player.position).sqrMagnitude;
                if (distSq < nearestSq)
                {
                    nearestSq = distSq;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static bool TryNormalizeTagMode(string mode, out string normalizedMode)
        {
            normalizedMode = "toggle";
            if (string.IsNullOrEmpty(mode) ||
                mode.Equals("toggle", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (mode.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("tag", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("tagged", StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = "on";
                return true;
            }

            if (mode.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("untag", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("untagged", StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = "off";
                return true;
            }

            return false;
        }

        private static bool ApplyTagMode(EntityVehicle vehicle, string normalizedMode, out string message)
        {
            if (normalizedMode == "on")
            {
                return ShoppingCartTagging.SetTagged(vehicle, true, out message);
            }

            if (normalizedMode == "off")
            {
                return ShoppingCartTagging.SetTagged(vehicle, false, out message);
            }

            return ShoppingCartTagging.Toggle(vehicle, out message);
        }

        private static EntityPlayer GetSenderPlayer(CommandSenderInfo senderInfo)
        {
            World world = GameManager.Instance?.World;
            if (world == null)
            {
                return null;
            }

            if (senderInfo.RemoteClientInfo != null)
            {
                return world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
            }

            if (!GameManager.IsDedicatedServer)
            {
                return world.GetPrimaryPlayer();
            }

            return null;
        }

        private static Vector3 GetSpawnPosition(EntityPlayer player, float distance)
        {
            Vector3 forward = Quaternion.Euler(0f, player.rotation.y, 0f) * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            return player.position + forward.normalized * distance;
        }

        private static bool TryParseVector3(List<string> parameters, int startIndex, out Vector3 position)
        {
            position = Vector3.zero;
            if (!float.TryParse(parameters[startIndex], out float x) ||
                !float.TryParse(parameters[startIndex + 1], out float y) ||
                !float.TryParse(parameters[startIndex + 2], out float z))
            {
                return false;
            }

            position = new Vector3(x, y, z);
            return true;
        }

        private static bool TryParseVector3i(List<string> parameters, int startIndex, out Vector3i blockPos)
        {
            blockPos = Vector3i.zero;
            if (!int.TryParse(parameters[startIndex], out int x) ||
                !int.TryParse(parameters[startIndex + 1], out int y) ||
                !int.TryParse(parameters[startIndex + 2], out int z))
            {
                return false;
            }

            blockPos = new Vector3i(x, y, z);
            return true;
        }

        private static bool IsSubcommand(string value, string expected)
        {
            return value.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCleanupSubcommand(string value)
        {
            return IsSubcommand(value, "cleanup") ||
                IsSubcommand(value, "clean") ||
                IsSubcommand(value, "clear") ||
                IsSubcommand(value, "remove");
        }

        private static string Format(Vector3 position)
        {
            return position.x.ToString("0.0") + ", " + position.y.ToString("0.0") + ", " + position.z.ToString("0.0");
        }

        private static void Output(string message)
        {
            SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[ShoppingCart] " + message);
        }
    }
}
