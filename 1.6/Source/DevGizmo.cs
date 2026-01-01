using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;

namespace GestaltEngineUnlimited
{
    // 添加开发者模式的即时冷却按钮
    [HarmonyPatch]
    public class Patch_CompUpgradeable_DevGizmo
    {
        // 准备目标方法
        static bool Prepare()
        {
            return true;
        }

        // 目标方法选择器
        static MethodBase TargetMethod()
        {
            Type compType = AccessTools.TypeByName("GestaltEngine.CompUpgradeable");
            if (compType == null)
            {
                Log.Warning("[格式塔枢纽无限升级] 未找到 GestaltEngine.CompUpgradeable 类");
                return null;
            }

            // 查找 CompGetGizmosExtra 方法
            MethodInfo method = AccessTools.Method(compType, "CompGetGizmosExtra");
            if (method != null)
            {
                Log.Message("[格式塔枢纽无限升级] 成功找到 CompGetGizmosExtra 方法");
                return method;
            }

            Log.Warning("[格式塔枢纽无限升级] 无法找到 CompGetGizmosExtra 方法");
            return null;
        }

        // Postfix：在原方法返回的 Gizmo 列表后添加开发者按钮
        static void Postfix(object __instance, ref IEnumerable<Gizmo> __result)
        {
            // 只在开发者模式显示
            if (!Prefs.DevMode) return;

            try
            {
                // 获取cooldownPeriod字段
                FieldInfo cooldownField = AccessTools.Field(__instance.GetType(), "cooldownPeriod");
                if (cooldownField == null) return;

                int currentCooldown = (int)cooldownField.GetValue(__instance);
                int currentTick = Find.TickManager.TicksGame;

                // 只有在冷却中才显示按钮
                if (currentCooldown <= currentTick) return;

                // 创建dev按钮
                Command_Action devButton = new Command_Action
                {
                    defaultLabel = "GEU_Dev_InstantCooldown".Translate(),
                    defaultDesc = "GEU_Dev_InstantCooldownDesc".Translate(),
                    icon = TexCommand.GatherSpotActive, // 使用一个内置图标
                    action = delegate
                    {
                        // 将cooldownPeriod设为当前时间，结束冷却
                        cooldownField.SetValue(__instance, currentTick);
                        Log.Message("[格式塔枢纽无限升级] Dev：已清零冷却时间");
                    }
                };

                // 将按钮添加到结果列表
                __result = __result.Concat(new Gizmo[] { devButton });
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("[格式塔枢纽无限升级] Dev按钮添加错误: {0}", ex.Message));
            }
        }
    }
}
