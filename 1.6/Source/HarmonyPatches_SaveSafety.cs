using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using GestaltEngine;
using RimWorld;

namespace GestaltEngineUnlimited
{
    // [NEW] 存档安全补丁
    // 拦截 CompUpgradeable.PostExposeData
    [HarmonyPatch(typeof(CompUpgradeable), "PostExposeData")]
    public static class Patch_CompUpgradeable_SaveSafety
    {
        // 状态变量：保存之前的真实等级
        // 注意：Harmony的 __state 是线程/调用隔离的，非常适合做这种临时保存
        
        static void Prefix(CompUpgradeable __instance, out int __state)
        {
            __state = __instance.level;
            
            // 仅在由于"保存"而触发ExposeData时介入
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 1. 同步数据到保护组件
                var protection = __instance.parent.GetComp<CompSaveProtection>();
                if (protection != null)
                {
                    protection.realLevel = __instance.level;
                    // 不需要手动Scribe，因为protection.PostExposeData会被游戏自动调用
                }
                else
                {
                    // 如果因为某种原因没有保护组件（理论上不应发生），打印警告
                    // 但不阻止保存，否则玩家会丢存档
                     // Log.Warning("[格式塔枢纽无限升级] 警告：未找到保护组件，无法执行安全保存！");
                }

                // 2. 也是最重要的一步：强制将等级钳位到 0 (1级)
                // 这样写入XML的 <level> 节点就是 0，完全避免越界风险
                // 只要 Mod 还在，Postfix 会立即恢复真实等级
                if (__instance.level > 0)
                {
                    __instance.level = 0;
                }
            }
        }

        static void Postfix(CompUpgradeable __instance, int __state)
        {
            // 如果也是保存模式，说明 Scribe_Values.Look(ref level...) 已经执行完毕
            // 此时内存中的 level 已经被写入磁盘（作为4）
            
            // 3. 恢复内存中的真实等级，以免影响当前游戏进程
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (__instance.level != __state)
                {
                    __instance.level = __state;
                }
            }
            
            // 如果是加载模式 (LoadingVars)
            // level 会被读取为 4 (如果是安全保存的存档)
            // 真正的恢复逻辑在 CompSaveProtection.PostSpawnSetup 中执行
            // 因为 PostSpawnSetup 在所有 ExposeData 之后运行，更安全
        }
    }

    // [NEW] 地图加载后自动修复信号塔实例
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Patch_Map_FinalizeInit_SaveSafety
    {
        static void Postfix(Map __instance)
        {
            try
            {
                ThingDef towerDef = DefDatabase<ThingDef>.GetNamed("GEU_SignalExpansionTower", false);
                if (towerDef == null) return;

                // 查找所有仍是旧类型 (Building_SignalTower) 的实例
                // 注意：如果我们在 Main 中修改了 thingClass，新生成的已经是 Building 了
                // 但旧存档加载进来的可能还是 Building_SignalTower
                
                List<Thing> oldTowers = __instance.listerThings.ThingsOfDef(towerDef);
                List<Thing> toReplace = new List<Thing>();

                foreach (Thing t in oldTowers)
                {
                    // 如果它的类型不是 Building (说明是旧的自定义类)
                    if (t.GetType() != typeof(Building))
                    {
                        toReplace.Add(t);
                    }
                }

                if (toReplace.Count > 0)
                {
                    Log.Message("[格式塔枢纽无限升级] 发现 " + toReplace.Count + " 个旧版信号塔，正在执行自动格式转化以确保卸载安全...");
                    
                    foreach (Thing oldT in toReplace)
                    {
                        // 1. 记录数据
                        IntVec3 pos = oldT.Position;
                        Rot4 rot = oldT.Rotation;
                        Faction faction = oldT.Faction;
                        int hp = oldT.HitPoints;
                        Thing selected = Find.Selector.IsSelected(oldT) ? oldT : null;

                        // 2. 销毁旧的
                        oldT.Destroy(DestroyMode.Vanish);

                        // 3. 生成新的 (现在 Def.thingClass 是 Building，所以生成的是 Building)
                        Thing newT = ThingMaker.MakeThing(towerDef, null);
                        newT.SetFactionDirect(faction);
                        newT.HitPoints = hp;
                        
                        GenSpawn.Spawn(newT, pos, __instance, rot);
                        
                        // 恢复选中状态
                        if (selected != null)
                        {
                            Find.Selector.Select(newT, false, false);
                        }
                    }
                    Log.Message("[格式塔枢纽无限升级] 信号塔转化完成。现在的存档将是安全的。");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[格式塔枢纽无限升级] 自动修复信号塔时出错: " + ex);
            }
        }
    }

    // [NEW] 运行时异常拦截：防止因为等级过高(例如100级)导致原版代码访问数组越界崩溃
    // 拦截属性 GestaltEngine.CompUpgradeable.CurrentUpgrade
    [HarmonyPatch(typeof(CompUpgradeable), "get_CurrentUpgrade")]
    public static class Patch_CompUpgradeable_RuntimeSafety
    {
        // 使用 Finalizer 拦截异常
        static Exception Finalizer(Exception __exception, CompUpgradeable __instance, ref object __result)
        {
            if (__exception != null)
            {
                // 如果发生了异常 (通常是 ArgumentOutOfRangeException)
                if (__exception is ArgumentOutOfRangeException || __exception is IndexOutOfRangeException)
                {
                    // 尝试返回最后一个有效的升级（满级属性）
                    try
                    {
                        // 使用反射获取 Props (因为直接访问可能会报 CS1061)
                        PropertyInfo propsProp = AccessTools.Property(typeof(CompUpgradeable), "Props");
                        if (propsProp != null)
                        {
                            object props = propsProp.GetValue(__instance, null);
                            if (props != null)
                            {
                                // 获取 upgrades 列表
                                FieldInfo upgradesField = AccessTools.Field(props.GetType(), "upgrades");
                                if (upgradesField != null)
                                {
                                    System.Collections.IList list = upgradesField.GetValue(props) as System.Collections.IList;
                                    if (list != null && list.Count > 0)
                                    {
                                        __result = list[list.Count - 1];
                                        return null; // 抑制异常，让游戏继续运行
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 如果连这都失败了，就真的没办法了，但不应该崩溃
                    }
                }
            }
            return __exception;
        }
    }
}
