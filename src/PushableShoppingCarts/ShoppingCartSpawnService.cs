using System;
using Platform;
using UnityEngine;

namespace PushableShoppingCarts
{
    internal static class ShoppingCartSpawnService
    {
        internal const string DefaultWorldBlockName = "cntShoppingCartEmptyWhite";

        private const float DefaultWorldBlockDistance = 3.0f;
        private const int PlacementSearchRadius = 3;

        internal static bool TrySpawnFixedInFront(EntityPlayer player, float distance, out string message)
        {
            message = null;
            if (player == null)
            {
                message = "No player entity is available.";
                return false;
            }

            Vector3 spawnPosition = GetSpawnPosition(player, distance);
            Vector3 spawnRotation = new Vector3(0f, player.rotation.y + 90f, 0f);
            return TrySpawnVehicle(spawnPosition, spawnRotation, player, ShoppingCartState.Fixed(), out _, out message);
        }

        internal static bool TrySpawnFixedAt(Vector3 spawnPosition, float yaw, EntityPlayer owner, out string message)
        {
            return TrySpawnVehicle(spawnPosition, new Vector3(0f, yaw, 0f), owner, ShoppingCartState.Fixed(), out _, out message);
        }

        internal static bool TrySpawnWorldBlockInFront(EntityPlayer player, string blockName, float distance, out string message)
        {
            message = null;
            if (player == null)
            {
                message = "No player entity is available.";
                return false;
            }

            blockName = string.IsNullOrEmpty(blockName) ? DefaultWorldBlockName : blockName;
            if (!TryGetWorldCartBlock(blockName, out Block block, out message))
            {
                return false;
            }

            if (!TryFindPlacementInFront(player, block, distance <= 0f ? DefaultWorldBlockDistance : distance, out Vector3i blockPos, out byte rotation, out message))
            {
                return false;
            }

            return TrySpawnWorldBlockAt(blockName, blockPos, rotation, out message);
        }

        internal static bool TrySpawnWorldBlockAt(string blockName, Vector3i blockPos, byte rotation, out string message)
        {
            message = null;
            if (!TryGetWorldCartBlock(blockName, out Block block, out message))
            {
                return false;
            }

            World world = GameManager.Instance?.World;
            if (world == null)
            {
                message = "No world is loaded.";
                return false;
            }

            BlockValue blockValue = block.ToBlockValue();
            blockValue.rotation = block.SupportsRotation(rotation) ? rotation : (byte)0;
            if (!block.CanPlaceBlockAt(world, blockPos, blockValue))
            {
                message = "Cannot place " + blockName + " at " + Format(blockPos) + ".";
                return false;
            }

            var result = new BlockPlacement.Result(
                BlockPlacement.EnumPlacement.Voxel,
                blockPos + Vector3.one * 0.5f,
                blockPos,
                BlockFace.None,
                blockValue,
                default(PropTransform));

            block.PlaceBlock(world, result, null);
            message = "Spawned world shopping cart block " + blockName + " at " + Format(blockPos) + ".";
            return true;
        }

        internal static bool TryConvertWorldBlock(Vector3i blockPos, EntityPlayer owner, out string message)
        {
            message = null;
            World world = GameManager.Instance?.World;
            if (world == null)
            {
                message = "No world is loaded.";
                return false;
            }

            BlockValue blockValue = world.GetBlock(blockPos);
            if (blockValue.ischild)
            {
                blockPos += blockValue.parent;
                blockValue = world.GetBlock(blockPos);
            }

            Block block = blockValue.Block;
            if (!ShoppingCartVisuals.IsWorldShoppingCartBlock(block))
            {
                message = "No shopping cart block found at " + Format(blockPos) + ".";
                return false;
            }

            string blockName = block.GetBlockName();
            ShoppingCartState state = ShoppingCartState.CreateWorldState(blockName, blockPos);
            Vector3 spawnPosition = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.08f, blockPos.z + 0.5f);
            Vector3 spawnRotation = new Vector3(0f, RotationToYaw(blockValue.rotation) + 90f, 0f);

            world.SetBlockRPC(blockPos, BlockValue.Air);
            if (!TrySpawnVehicle(spawnPosition, spawnRotation, owner, state, out _, out message))
            {
                return false;
            }

            message = "Converted " + blockName + " to a pushable shopping cart: " + state.DisplaySummary() + ".";
            return true;
        }

        internal static bool TrySpawnVehicle(Vector3 spawnPosition, Vector3 spawnRotation, EntityPlayer owner, ShoppingCartState state, out EntityVehicle vehicle, out string message)
        {
            vehicle = null;
            message = null;

            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                message = "Vehicle spawning must run on the server.";
                return false;
            }

            World world = GameManager.Instance?.World;
            if (world == null)
            {
                message = "No world is loaded.";
                return false;
            }

            int entityId = EntityClass.GetId(ShoppingCartVisuals.EntityName);
            if (entityId <= 0)
            {
                message = "Could not find entity class '" + ShoppingCartVisuals.EntityName + "'.";
                return false;
            }

            if (!ShoppingCartInventory.TryGetItemValue(ShoppingCartInventory.CartItemName, out ItemValue itemValue))
            {
                message = "Could not find item '" + ShoppingCartInventory.CartItemName + "'.";
                return false;
            }

            if (!VehicleManager.CanAddMoreVehicles())
            {
                message = "The world is at the vehicle limit.";
                return false;
            }

            if (state != null && !state.IsFixed)
            {
                itemValue.SetMetadata(ShoppingCartState.MetadataKey, SerializeForSpawn(state));
            }

            Entity entity = EntityFactory.CreateEntity(entityId, spawnPosition, spawnRotation);
            if (entity == null)
            {
                message = "EntityFactory returned null for '" + ShoppingCartVisuals.EntityName + "'.";
                return false;
            }

            entity.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
            vehicle = entity as EntityVehicle;
            if (vehicle != null)
            {
                Vehicle vehicleData = vehicle.GetVehicle();
                if (vehicleData != null)
                {
                    vehicleData.SetItemValue(itemValue.Clone());
                }

                if (owner != null)
                {
                    vehicle.SetOwner(GetOwner(owner));
                }
            }

            world.SpawnEntityInWorld(entity);
            if (vehicle != null)
            {
                (state ?? ShoppingCartState.Fixed()).ApplyTo(vehicle);
                ShoppingCartVisuals.RepairVehicle(vehicle, true);
            }

            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                NetPackageManager.GetPackage<NetPackageVehicleCount>().Setup());

            message = "Spawned shopping cart at " + Format(spawnPosition) + ".";
            return true;
        }

        internal static void SendServerCommand(string command)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackageConsoleCmdServer>().Setup(command));
        }

        internal static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string SerializeForSpawn(ShoppingCartState state)
        {
            return "1|" + state.MissingWheels + "|" + (state.RottedFrame ? "1" : "0") + "|" + (state.WorldCart ? "1" : "0");
        }

        private static bool TryGetWorldCartBlock(string blockName, out Block block, out string message)
        {
            block = Block.GetBlockByName(blockName, _caseInsensitive: true);
            message = null;
            if (block == null)
            {
                message = "Unknown block: " + blockName;
                return false;
            }

            if (!ShoppingCartVisuals.IsWorldShoppingCartBlock(block))
            {
                message = block.GetBlockName() + " is not a vanilla shopping cart block.";
                return false;
            }

            return true;
        }

        private static bool TryFindPlacementInFront(EntityPlayer player, Block block, float distance, out Vector3i blockPos, out byte rotation, out string message)
        {
            blockPos = Vector3i.zero;
            rotation = 0;
            message = null;

            Vector3 forward = Quaternion.Euler(0f, player.rotation.y, 0f) * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            Vector3 target = player.position + forward.normalized * distance;
            Vector3i basePos = World.worldToBlockPos(target);
            rotation = (byte)(Mathf.RoundToInt(player.rotation.y / 90f) & 3);

            BlockValue blockValue = block.ToBlockValue();
            if (!block.SupportsRotation(rotation))
            {
                rotation = 0;
            }

            blockValue.rotation = rotation;
            World world = GameManager.Instance.World;
            int[] yOffsets = { 0, 1, -1, 2, 3 };

            for (int radius = 0; radius <= PlacementSearchRadius; radius++)
            {
                for (int yIndex = 0; yIndex < yOffsets.Length; yIndex++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dz = -radius; dz <= radius; dz++)
                        {
                            if (radius > 0 && Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                            {
                                continue;
                            }

                            var candidate = new Vector3i(basePos.x + dx, basePos.y + yOffsets[yIndex], basePos.z + dz);
                            if (candidate.y < 1 || candidate.y > 253)
                            {
                                continue;
                            }

                            if (block.CanPlaceBlockAt(world, candidate, blockValue))
                            {
                                blockPos = candidate;
                                return true;
                            }
                        }
                    }
                }
            }

            message = "Could not find a clear placement spot in front of the player.";
            return false;
        }

        private static Vector3 GetSpawnPosition(EntityPlayer player, float distance)
        {
            Vector3 forward = Quaternion.Euler(0f, player.rotation.y, 0f) * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            Vector3 position = player.position + forward.normalized * Mathf.Clamp(distance, 2.5f, 12f);
            position.y = player.position.y;
            SnapToGround(ref position);
            return position;
        }

        private static void SnapToGround(ref Vector3 position)
        {
            Vector3 rayOrigin = position - Origin.position + Vector3.up * 6f;
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 18f, ~0, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + Origin.position.y + 0.08f;
            }
        }

        private static PlatformUserIdentifierAbs GetOwner(EntityPlayer player)
        {
            ClientInfo clientInfo = SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(player.entityId);
            return clientInfo != null ? clientInfo.InternalId : PlatformManager.InternalLocalUserIdentifier;
        }

        private static float RotationToYaw(byte rotation)
        {
            return (rotation & 3) * 90f;
        }

        private static string Format(Vector3 position)
        {
            return position.x.ToString("0.0") + ", " + position.y.ToString("0.0") + ", " + position.z.ToString("0.0");
        }

        private static string Format(Vector3i position)
        {
            return position.x + ", " + position.y + ", " + position.z;
        }
    }
}
