using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    /// <summary>
    /// Prototype of the "walk behind and push" mechanic.
    ///
    /// Instead of mounting the shopping cart like a vehicle, the player keeps their
    /// normal on-foot locomotion and the cart is rigidly glued a short distance in
    /// front of them, ground-snapped and facing the player's heading. The vehicle
    /// rigidbody is frozen (kinematic) so the engine's vehicle physics stop fighting
    /// us, the wheels are spun by distance travelled, and the
    /// player's hands are IK-pinned to the handle grips.
    ///
    /// Driven by <c>sc push [offset] [lift] [tilt]</c> / <c>sc drop</c> for now, so the
    /// stance can be dialled in before the mount interaction is replaced (Phase 2).
    /// </summary>
    internal static class PushableShoppingCartsPush
    {
        // Tunables - defaults are starting points; dial in-game via `sc push`.
        internal const float DefaultFrontOffset = 1.25f;   // cart root offset; tilted grip ends sit close enough for bent elbows
        internal const float DefaultHeightLift = -0.08f;   // lowered so the aligned cart visual rides near the ground
        internal const float DefaultTiltDegrees = 0f;      // shopping carts stay upright on four wheels
        internal const float GroundClearance = 0.06f;
        internal const int GroundRaycastMask = 1073807360;
        internal const float WheelRadius = 0.235f;         // artist wheel ~0.47m diameter
        internal const float YawOffset = 0f;               // cart-forward vs entity-forward (model faces +Z)
        internal const float TurnSmoothTime = 0.22f;       // higher = lazier turning
        internal const float TurnMaxRate = 120f;           // deg/sec cap on how fast the cart can swing
        internal const float MaxTurnOffset = 50f;          // nose may lag at most this far from your facing
        internal const float ToggleGuard = 0.25f;          // debounce so one keypress can't grab+drop
        internal const string BurdenBuffName = "buffPushableShoppingCartsBurden";
        internal const string BurdenPenaltyCVar = ".shoppingcartMovePenaltyDisplay";
        internal const int FreeFilledSlots = 5;
        internal const float BaseMovePenalty = 0.05f;
        internal const float PerFilledSlotPenalty = 0.01f;
        internal const float MaxMovePenalty = 0.75f;

        // Hand IK rotation/offset in grip-local space. The left hand is mirrored across the cart centreline.
        internal static Vector3 HandEuler = new Vector3(68f, 275f, 0f);
        internal static Vector3 HandOffset = new Vector3(0.03f, 0.01f, -0.02f);

        internal static EntityVehicle Current { get; private set; }
        internal static float FrontOffset = DefaultFrontOffset;
        internal static float HeightLift = DefaultHeightLift;
        internal static float TiltDegrees = DefaultTiltDegrees;
        internal static float CurrentMovePenalty { get; private set; }
        internal static int CurrentFilledSlots { get; private set; }
        internal static float CurrentCargoPenalty { get; private set; }
        internal static float CurrentConditionPenalty { get; private set; }
        internal static float CurrentTerrainPenalty { get; private set; }

        private static Vector3 lastCartPos;
        private static bool hasLastPos;
        private static Transform handTargetL;
        private static Transform handTargetR;
        private static bool ikApplied;
        private static bool pendingIKSetup;
        private static float smoothedYaw;
        private static float yawVelocity;
        private static bool hasYaw;
        private static float lastPushYaw;
        private static bool hasLastPushYaw;
        private static float lastBeginTime = -10f;
        private static float lastReleaseTime = -10f;

        internal static bool IsActive => Current != null;

        internal static bool IsPushing(EntityVehicle vehicle)
        {
            return Current != null && vehicle != null && Current.entityId == vehicle.entityId;
        }

        // True briefly after a drop, so the same keypress that dropped the cart can't
        // immediately re-grab it via the interact handler.
        internal static bool JustReleased => Time.unscaledTime - lastReleaseTime < ToggleGuard;

        internal static bool ShouldLockLocalPlayer(EntityPlayerLocal player)
        {
            return ValidateActiveState(player, true);
        }

        internal static void ApplyMovementPenalty(EntityPlayerLocal player)
        {
            if (player == null || player.movementInput == null)
            {
                return;
            }

            float scale = Mathf.Clamp01(1f - CurrentMovePenalty);
            player.movementInput.moveForward *= scale;
            player.movementInput.moveStrafe *= scale;
        }

        // Start pushing using the current (possibly console-tuned) stance values.
        internal static void Begin(EntityVehicle vehicle)
        {
            Begin(vehicle, FrontOffset, HeightLift, TiltDegrees);
        }

        internal static void Toggle(EntityVehicle vehicle)
        {
            if (IsActive && Current == vehicle)
            {
                Release();
            }
            else
            {
                Begin(vehicle);
            }
        }

        // While pushing, dropping is driven by the interact key directly (the cart sits
        // low/in front and is awkward to look-focus), not by entity activation.
        internal static void HandleReleaseInput()
        {
            if (!IsActive || Time.unscaledTime - lastBeginTime < ToggleGuard)
            {
                return;
            }

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
            if (!ValidateActiveState(player, true))
            {
                return;
            }

            PlayerActionsLocal input = player != null ? player.playerInput : null;
            if (input == null)
            {
                return;
            }

            bool pressed = (input.Activate != null && input.Activate.WasPressed) ||
                (input.PermanentActions != null && input.PermanentActions.Activate != null && input.PermanentActions.Activate.WasPressed);
            if (pressed)
            {
                Release();
            }
        }

        internal static void Begin(EntityVehicle vehicle, float frontOffset, float heightLift, float tiltDegrees)
        {
            if (vehicle == null)
            {
                return;
            }

            if (Current != null && Current != vehicle)
            {
                Release();
            }

            Current = vehicle;
            FrontOffset = frontOffset;
            HeightLift = heightLift;
            TiltDegrees = tiltDegrees;
            hasLastPos = false;
            hasYaw = false;
            lastBeginTime = Time.unscaledTime;

            FreezePhysics(vehicle, true);
            ShoppingCartVisuals.ConfigureStablePhysics(vehicle);
            UpdateBurden(GetPrimaryPlayer(), vehicle);

            // Defer hand IK to the first Tick, once the cart is glued in front of the
            // player, so we can read the grips' real world positions to pick sides.
            pendingIKSetup = true;
        }

        internal static void Release()
        {
            EntityVehicle vehicle = Current;
            float releaseYaw = hasLastPushYaw ? lastPushYaw : GetVehicleYaw(vehicle);
            ClearBurden(GetPrimaryPlayer());
            Current = null;
            hasLastPos = false;
            pendingIKSetup = false;
            hasLastPushYaw = false;
            lastReleaseTime = Time.unscaledTime;

            ClearHandIK();
            RestorePlayerHands();

            if (vehicle != null)
            {
                PrepareReleasedVehicle(vehicle, releaseYaw);
                FreezePhysics(vehicle, false);
                ReactivateReleasedPhysics(vehicle);
            }
        }

        /// <summary>Called every frame (LateUpdate) by the behaviour below.</summary>
        internal static void Tick()
        {
            EntityVehicle vehicle = Current;
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
            if (!ValidateActiveState(player, true))
            {
                return;
            }

            vehicle = Current;
            if (vehicle == null)
            {
                return;
            }

            UpdateBurden(player, vehicle);

            // Smoothly chase the player's heading with a capped turn rate, so the cart
            // swings/trails into corners instead of snapping rigidly to face them.
            float targetYaw = player.rotation.y;
            if (!hasYaw)
            {
                smoothedYaw = targetYaw;
                yawVelocity = 0f;
                hasYaw = true;
            }
            else
            {
                smoothedYaw = Mathf.SmoothDampAngle(smoothedYaw, targetYaw, ref yawVelocity, TurnSmoothTime, TurnMaxRate, Time.deltaTime);
            }

            // Hard cap how far the nose may lag from your facing, so it can't be whipped
            // around; it stays within MaxTurnOffset degrees and drags along past that.
            float offset = Mathf.DeltaAngle(targetYaw, smoothedYaw);
            if (Mathf.Abs(offset) > MaxTurnOffset)
            {
                smoothedYaw = targetYaw + Mathf.Sign(offset) * MaxTurnOffset;
                yawVelocity = 0f;
            }

            float yaw = smoothedYaw;
            lastPushYaw = yaw;
            hasLastPushYaw = true;

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 target = player.position + forward * FrontOffset;
            SnapToGround(ref target);
            target.y += HeightLift;

            // Yaw to face the player's heading; shopping carts stay upright on four wheels by default.
            Quaternion rotation = Quaternion.Euler(0f, yaw + YawOffset, 0f) * Quaternion.Euler(TiltDegrees, 0f, 0f);

            vehicle.SetPosition(target, false);
            vehicle.SetRotation(rotation.eulerAngles);

            // Pin physics + model transforms so nothing visually lags or drifts.
            Vector3 unityPos = target - Origin.position;
            Rigidbody rb = vehicle.vehicleRB;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = unityPos;
                rb.rotation = rotation;
            }

            Transform model = vehicle.ModelTransform;
            if (model != null)
            {
                model.SetPositionAndRotation(unityPos, rotation);
            }

            Transform physics = vehicle.PhysicsTransform;
            if (physics != null)
            {
                physics.SetPositionAndRotation(unityPos, rotation);
            }

            SpinWheel(vehicle, target, forward);

            if (pendingIKSetup)
            {
                SetupHandIK(vehicle, player);
                pendingIKSetup = false;
            }

            lastCartPos = target;
            hasLastPos = true;
        }

        private static void SetupHandIK(EntityVehicle vehicle, EntityPlayerLocal player)
        {
            ClearHandIK();

            Transform model = vehicle != null ? vehicle.ModelTransform : null;
            if (player == null || model == null)
            {
                return;
            }

            Transform gripA = ShoppingCartVisuals.GetOrCreateGrip(vehicle, true);
            Transform gripB = ShoppingCartVisuals.GetOrCreateGrip(vehicle, false);
            if (gripA == null || gripB == null)
            {
                Log.Warning("[PushableShoppingCarts] Could not find handle grips for hand IK; pushing without hands attached.");
                return;
            }

            // Pick sides from each grip's real position relative to the player's right
            // vector, so it stays correct even though the FBX export mirrors X (which
            // makes the "Left"/"Right" mesh names unreliable).
            Vector3 right = Quaternion.Euler(0f, player.rotation.y, 0f) * Vector3.right;
            float sideA = Vector3.Dot(gripA.position - player.position, right);
            float sideB = Vector3.Dot(gripB.position - player.position, right);
            Transform leftGrip = sideA <= sideB ? gripA : gripB;
            Transform rightGrip = sideA <= sideB ? gripB : gripA;

            handTargetL = MakeHandTarget("SC_HandTargetL", leftGrip, isLeft: true);
            handTargetR = MakeHandTarget("SC_HandTargetR", rightGrip, isLeft: false);

            List<IKController.Target> targets = new List<IKController.Target>
            {
                new IKController.Target { avatarGoal = AvatarIKGoal.LeftHand, transform = handTargetL },
                new IKController.Target { avatarGoal = AvatarIKGoal.RightHand, transform = handTargetR }
            };

            player.SetIKTargets(targets);
            ikApplied = true;
        }

        private static Transform MakeHandTarget(string name, Transform grip, bool isLeft)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(grip, worldPositionStays: false);

            Vector3 offset = HandOffset;
            Vector3 euler = HandEuler;
            if (isLeft)
            {
                offset.x = -offset.x; // mirror across the cart centreline
                euler = new Vector3(euler.x, -euler.y, -euler.z);
            }

            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go.transform;
        }

        // Re-pin the hands using the current HandEuler/HandOffset (for live console tuning).
        internal static void RefreshHandIK()
        {
            if (!IsActive)
            {
                return;
            }

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
            if (player != null)
            {
                SetupHandIK(Current, player);
            }
        }

        private static void ClearHandIK()
        {
            if (ikApplied)
            {
                World world = GameManager.Instance != null ? GameManager.Instance.World : null;
                EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
                if (player != null)
                {
                    player.RemoveIKTargets();
                }

                ikApplied = false;
            }

            if (handTargetL != null)
            {
                UnityEngine.Object.Destroy(handTargetL.gameObject);
                handTargetL = null;
            }

            if (handTargetR != null)
            {
                UnityEngine.Object.Destroy(handTargetR.gameObject);
                handTargetR = null;
            }
        }

        private static void SpinWheel(EntityVehicle vehicle, Vector3 cartPos, Vector3 forward)
        {
            if (!hasLastPos)
            {
                return;
            }

            List<Transform> wheels = ShoppingCartVisuals.GetSpinWheelTransforms(vehicle);
            if (wheels.Count == 0)
            {
                return;
            }

            Vector3 delta = cartPos - lastCartPos;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.0005f)
            {
                return;
            }

            float direction = Vector3.Dot(delta, forward) >= 0f ? 1f : -1f;
            float circumference = 2f * Mathf.PI * WheelRadius;
            float degrees = distance / circumference * 360f * direction;
            for (int i = 0; i < wheels.Count; i++)
            {
                wheels[i].Rotate(degrees, 0f, 0f, Space.Self);
            }
        }

        private static void FreezePhysics(EntityVehicle vehicle, bool frozen)
        {
            if (frozen)
            {
                vehicle.RBActive = false;
                vehicle.RBNoDriverGndTime = 0f;
                vehicle.RBNoDriverSleepTime = 0f;
                vehicle.isTryToFall = false;
            }

            Rigidbody rb = vehicle.vehicleRB;
            if (rb != null)
            {
                rb.isKinematic = frozen;
                if (frozen)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                else
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.WakeUp();
                }
            }

            // While pushing, drop the solid body collider so the cart can't shove or
            // trap the player; wheel colliders are inert on a kinematic body.
            Transform physics = vehicle.PhysicsTransform;
            if (physics != null)
            {
                BoxCollider box = physics.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.enabled = !frozen;
                }
            }
        }

        private static void PrepareReleasedVehicle(EntityVehicle vehicle, float yaw)
        {
            if (vehicle == null || vehicle.IsDead())
            {
                return;
            }

            Vector3 parkedPosition = vehicle.position;
            SnapToGround(ref parkedPosition);

            Quaternion parkedRotation = Quaternion.Euler(0f, yaw + YawOffset, 0f);
            Vector3 unityPosition = parkedPosition - Origin.position;

            vehicle.SetPosition(parkedPosition, false);
            vehicle.SetRotation(parkedRotation.eulerAngles);

            Rigidbody rb = vehicle.vehicleRB;
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = unityPosition;
                rb.rotation = parkedRotation;
            }

            ShoppingCartVisuals.ConfigureStablePhysics(vehicle);

            Transform model = vehicle.ModelTransform;
            if (model != null)
            {
                model.SetPositionAndRotation(unityPosition, parkedRotation);
            }

            Transform physics = vehicle.PhysicsTransform;
            if (physics != null)
            {
                physics.SetPositionAndRotation(unityPosition, parkedRotation);
            }
        }

        private static void ReactivateReleasedPhysics(EntityVehicle vehicle)
        {
            Rigidbody rb = vehicle != null ? vehicle.vehicleRB : null;
            if (rb == null || vehicle.isEntityRemote)
            {
                return;
            }

            vehicle.RBActive = true;
            vehicle.RBNoDriverGndTime = 0f;
            vehicle.RBNoDriverSleepTime = 0f;
            vehicle.isTryToFall = false;
            rb.isKinematic = false;
            rb.WakeUp();

            // Match the vanilla wake path used by VehicleManager.PhysicsWakeNear.
            vehicle.AddForce(Vector3.zero);
        }

        private static void RestorePlayerHands()
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
            if (player == null)
            {
                return;
            }

            player.HolsterWeapon(false);
            if (player.bFirstPersonView)
            {
                player.ShowHoldingItemLayer(true);
            }

            if (player.inventory != null)
            {
                player.inventory.ShowRightHand(true);
                player.inventory.SetIsFinishedSwitchingHeldItem();
            }
        }

        private static float GetVehicleYaw(EntityVehicle vehicle)
        {
            if (vehicle == null)
            {
                return 0f;
            }

            if (hasLastPushYaw)
            {
                return lastPushYaw;
            }

            return vehicle.rotation.y;
        }

        private static EntityPlayerLocal GetPrimaryPlayer()
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            return world != null ? world.GetPrimaryPlayer() : null;
        }

        private static void UpdateBurden(EntityPlayerLocal player, EntityVehicle vehicle)
        {
            if (player == null || vehicle == null)
            {
                ClearBurden(player);
                return;
            }

            CurrentFilledSlots = CountFilledStorageSlots(vehicle);
            CurrentCargoPenalty = CalculateCargoMovePenalty(CurrentFilledSlots);
            CurrentConditionPenalty = ShoppingCartState.Get(vehicle).ConditionMovePenalty;
            CurrentTerrainPenalty = CalculateTerrainMovePenalty(vehicle);
            CurrentMovePenalty = Mathf.Clamp01(CurrentCargoPenalty + CurrentConditionPenalty + CurrentTerrainPenalty);
            player.SetCVar(BurdenPenaltyCVar, Mathf.Round(CurrentMovePenalty * 100f));

            if (player.Buffs != null && !player.Buffs.HasBuff(BurdenBuffName))
            {
                player.Buffs.AddBuff(BurdenBuffName);
            }
        }

        private static void ClearBurden(EntityPlayerLocal player)
        {
            CurrentFilledSlots = 0;
            CurrentMovePenalty = 0f;
            CurrentCargoPenalty = 0f;
            CurrentConditionPenalty = 0f;
            CurrentTerrainPenalty = 0f;

            if (player == null)
            {
                return;
            }

            player.SetCVar(BurdenPenaltyCVar, 0f);
            if (player.Buffs != null && player.Buffs.HasBuff(BurdenBuffName))
            {
                player.Buffs.RemoveBuff(BurdenBuffName);
            }
        }

        private static float CalculateCargoMovePenalty(int filledSlots)
        {
            int loadedSlots = Mathf.Max(filledSlots, FreeFilledSlots);
            return Mathf.Clamp(loadedSlots * PerFilledSlotPenalty, BaseMovePenalty, MaxMovePenalty);
        }

        private static int CountFilledStorageSlots(EntityVehicle vehicle)
        {
            if (vehicle == null || vehicle.bag == null)
            {
                return 0;
            }

            return vehicle.bag.GetUsedSlotCount();
        }

        private static float CalculateTerrainMovePenalty(EntityVehicle vehicle)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null || vehicle == null)
            {
                return 0f;
            }

            Vector3i basePos = World.worldToBlockPos(vehicle.position);
            for (int dy = 0; dy <= 3; dy++)
            {
                Vector3i blockPos = new Vector3i(basePos.x, basePos.y - dy, basePos.z);
                BlockValue blockValue = world.GetBlock(blockPos);
                if (blockValue.isair || blockValue.Block == null)
                {
                    continue;
                }

                string blockName = blockValue.Block.GetBlockName();
                if (IsSandBlock(blockName))
                {
                    return 0.75f;
                }

                if (IsDirtBlock(blockName))
                {
                    return 0.50f;
                }

                return 0f;
            }

            return 0f;
        }

        private static bool IsSandBlock(string blockName)
        {
            return !string.IsNullOrEmpty(blockName) &&
                (blockName.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 blockName.IndexOf("DesertGround", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsDirtBlock(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
            {
                return false;
            }

            return blockName.IndexOf("Dirt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                blockName.IndexOf("ForestGround", StringComparison.OrdinalIgnoreCase) >= 0 ||
                blockName.IndexOf("TopSoil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                blockName.IndexOf("Clay", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SnapToGround(ref Vector3 position)
        {
            Vector3 rayOrigin = position - Origin.position + Vector3.up * 6f;
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 18f, GroundRaycastMask, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + Origin.position.y + GroundClearance;
            }
        }

        private static bool ValidateActiveState(EntityPlayerLocal player, bool releaseIfInvalid)
        {
            EntityVehicle vehicle = Current;
            if (vehicle == null)
            {
                return false;
            }

            string reason = GetInvalidActiveStateReason(vehicle, player);
            if (reason == null)
            {
                return true;
            }

            if (releaseIfInvalid)
            {
                Log.Out("[PushableShoppingCarts] Releasing stale push lock: " + reason);
                Release();
            }

            return false;
        }

        private static string GetInvalidActiveStateReason(EntityVehicle vehicle, EntityPlayerLocal player)
        {
            if (vehicle == null)
            {
                return "missing vehicle";
            }

            if (vehicle.IsDead())
            {
                return "vehicle is dead";
            }

            if (vehicle.RootTransform == null || vehicle.ModelTransform == null || vehicle.PhysicsTransform == null)
            {
                return "vehicle transforms are missing";
            }

            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
            {
                return "world is unavailable";
            }

            if (player == null)
            {
                return "local player is unavailable";
            }

            if (player.IsDead())
            {
                return "local player is dead";
            }

            EntityPlayerLocal primaryPlayer = world.GetPrimaryPlayer();
            if (primaryPlayer == null || primaryPlayer.entityId != player.entityId)
            {
                return "input player is not the primary local player";
            }

            VehicleManager manager = VehicleManager.Instance;
            if (manager == null)
            {
                return "vehicle manager is unavailable";
            }

            bool activeVehicleFound = false;
            for (int i = 0; i < manager.vehiclesActive.Count; i++)
            {
                EntityVehicle activeVehicle = manager.vehiclesActive[i];
                if (activeVehicle != null && activeVehicle.entityId == vehicle.entityId)
                {
                    activeVehicleFound = true;
                    break;
                }
            }

            if (!activeVehicleFound)
            {
                return "vehicle is no longer active";
            }

            float maxDistance = Mathf.Max(FrontOffset + 4f, 8f);
            Vector3 delta = vehicle.position - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > maxDistance * maxDistance)
            {
                return "player moved too far from vehicle";
            }

            return null;
        }
    }

    [Preserve]
    internal sealed class PushableShoppingCartsPushBehaviour : MonoBehaviour
    {
        private void Update()
        {
            // Interact key drops the cart while pushing.
            PushableShoppingCartsPush.HandleReleaseInput();
        }

        private void LateUpdate()
        {
            if (PushableShoppingCartsPush.IsActive)
            {
                PushableShoppingCartsPush.Tick();
            }
        }
    }
}
