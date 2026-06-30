using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

namespace PushableShoppingCarts
{
    [Preserve]
    public class PushableShoppingCartsModApi : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            new Harmony("com.pathof7d2d.pushableshoppingcarts").PatchAll(typeof(PushableShoppingCartsModApi).Assembly);
            DataLoader.PreloadBundle(ShoppingCartVisuals.VanillaVisualPrefab);

            if (GameObject.Find("ShoppingCartRuntime") == null)
            {
                GameObject repairObject = new GameObject("ShoppingCartRuntime");
                Object.DontDestroyOnLoad(repairObject);
                repairObject.AddComponent<ShoppingCartVisualRepairBehaviour>();
                repairObject.AddComponent<PushableShoppingCartsPushBehaviour>();
            }

            Log.Out("[PushableShoppingCarts] Loaded. Press the interact key on a shopping cart to push it.");
        }
    }
}
