
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using GestaltEngine;
using UnityEngine;
using Verse.AI;

namespace GestaltEngineUnlimited
{
    public static class HarmonyPatches_BVR
    {
        // 缓存超出范围的机械体 ID，用于在 Draftable 补丁中快速检查
        public static HashSet<int> outOfRangePawns = new HashSet<int>();

        [HarmonyPatch(typeof(CompGestaltEngine), "connectMechanoidTargetParameters")]
        public static class Patch_ConnectTargetParameters
        {
            static void Postfix(CompGestaltEngine __instance, ref TargetingParameters __result)
            {
                // [SAFETY] 如果 BVR 禁用，不修改验证器
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return;

                Predicate<TargetInfo> originalValidator = __result.validator;
                
                __result.validator = (TargetInfo t) => 
                {
                    Pawn p = t.Thing as Pawn;
                    if (p == null) return false;
                    
                    if (originalValidator != null && originalValidator(t)) return true;
                    
                    if (p.Map != __instance.parent.Map)
                    {
                        if (GestaltRangeCalculator.IsInRange(__instance, p))
                        {
                            bool canConnect = (bool)AccessTools.Method(typeof(CompGestaltEngine), "canConnect")
                                .Invoke(__instance, new object[] { new LocalTargetInfo(p) });
                                
                            return canConnect;
                        }
                    }
                    
                    return false;
                };
            }
        }
        
        [HarmonyPatch(typeof(CompGestaltEngine), "connectNonColonyMechanoidTargetParameters")]
        public static class Patch_ConnectNonColonyTargetParameters
        {
            static void Postfix(CompGestaltEngine __instance, ref TargetingParameters __result)
            {
                Predicate<TargetInfo> originalValidator = __result.validator;
                __result.validator = (TargetInfo t) => 
                {
                    if (originalValidator != null && originalValidator(t)) return true;
                    Pawn p = t.Thing as Pawn;
                    if (p != null && p.Map != __instance.parent.Map)
                    {
                        if (GestaltRangeCalculator.IsInRange(__instance, p))
                        {
                            bool canConnect = (bool)AccessTools.Method(typeof(CompGestaltEngine), "canConnectNonColonyMech")
                                .Invoke(__instance, new object[] { new LocalTargetInfo(p) });
                            return canConnect;
                        }
                    }
                    return false;
                };
            }
        }

        [HarmonyPatch(typeof(CompGestaltEngine), "connectEffects")]
        public static class Patch_ConnectEffects
        {
            static bool Prefix(CompGestaltEngine __instance, Pawn mech)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return true;

                if (__instance.parent.Map != mech.Map)
                {
                    return false; 
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(CompGestaltEngine), "CompTick")]
        public static class Patch_CompTick_Heartbeat
        {
            static void Postfix(CompGestaltEngine __instance)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return;

                if (BasicTickCheck(__instance))
                {
                    try
                    {
                        CheckConnections(__instance);
                    }
                    catch
                    {
                        // 捕获潜在的空引用异常，防止错误日志泛滥
                    }
                }
            }

            static bool BasicTickCheck(CompGestaltEngine comp)
            {
                return comp.parent.Map != null && Find.TickManager.TicksGame % 250 == 0;
            }

            static void CheckConnections(CompGestaltEngine comp)
            {
                Pawn overseer = comp.dummyPawn;
                if (overseer == null || overseer.relations == null) return;
                
                if (overseer.mechanitor == null) return;
                
                List<Pawn> overseen = overseer.mechanitor.OverseenPawns;
                if (overseen.NullOrEmpty()) return;

                for (int i = overseen.Count - 1; i >= 0; i--)
                {
                    Pawn p = overseen[i];
                    
                    // 仅检查跨地图或明显异常的
                    bool inRange = GestaltRangeCalculator.IsInRange(comp, p);
                    
                    if (!inRange)
                    {
                        // 如果尚未标记（首次检测到超出范围），则发送警告
                        if (!outOfRangePawns.Contains(p.thingIDNumber))
                        {
                            outOfRangePawns.Add(p.thingIDNumber);
                            
                            // 计算距离用于显示
                            int dist = -1;
                            int tileA = comp.parent.Map.Tile;
                            int tileB = -1;
                            if (p.MapHeld != null) tileB = p.MapHeld.Tile;
                            else if (p.Tile >= 0) tileB = p.Tile;
                            
                            if (tileB != -1)
                            {
                                dist = Find.WorldGrid.TraversalDistanceBetween(tileA, tileB);
                            }
                            
                            // 发送详细警告：谁 + 距离 + 范围
                            Messages.Message("GEU_Message_SignalLost".Translate(p.LabelShort, dist, GestaltRangeCalculator.GetMaxRange(comp)), 
                                p, MessageTypeDefOf.NegativeEvent);
                                
                            // 强制取消征召
                            if (p.drafter != null) p.drafter.Drafted = false;

                            // 强制中断当前工作并清理队列 (模拟失控状态)
                            if (p.jobs != null)
                            {
                                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                                p.jobs.ClearQueuedJobs();
                            }
                        }
                    }
                    else
                    {
                        // 在范围内：移除标记
                        if (outOfRangePawns.Contains(p.thingIDNumber))
                        {
                            outOfRangePawns.Remove(p.thingIDNumber);
                            
                            // 发送重获连接的消息
                            Messages.Message("GEU_Message_SignalRestored".Translate(p.LabelShort), 
                                p, MessageTypeDefOf.PositiveEvent);
                        }
                    }
                }
            }

            static void Disconnect(Pawn overseer, Pawn mech)
            {
                overseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
            }

            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    // 抑制错误以防止Tick崩溃
                    return null;
                }
                return null;
            }
        }

        [HarmonyPatch(typeof(CompGestaltEngine), "CompTick")]
        public static class Patch_CompTick_RemoteHacking
        {
            static void Postfix(CompGestaltEngine __instance)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return;

                var dict = Patch_CompGetGizmosExtra.pendingConnections;
                if (dict.ContainsKey(__instance))
                {
                    var ctx = dict[__instance];
                    ctx.ticksLeft--;
                    
                    if (ctx.effecter != null)
                    {
                        ctx.effecter.EffectTick(ctx.target, ctx.target);
                    }

                    if (ctx.ticksLeft <= 0)
                    {
                        // 完成连接
                        try
                        {
                            ctx.onComplete();
                        }
                        catch (Exception ex) 
                        {
                            Log.Error("[BVR] 远程连接回调失败: " + ex);
                        }

                        // 清理
                        if (ctx.effecter != null) ctx.effecter.Cleanup();
                        
                        // 恢复按钮状态 (connectingTo = null)
                        FieldInfo connectingToField = AccessTools.Field(typeof(CompGestaltEngine), "connectingTo");
                        if (connectingToField != null)
                        {
                            connectingToField.SetValue(__instance, null);
                        }
                        
                        dict.Remove(__instance);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CompGestaltEngine), "reset")]
        public static class Patch_Reset
        {
            static bool Prefix(CompGestaltEngine __instance)
            {
                // 如果能确定是跨地图，也许跳过reset？
                // 但reset没有参数。我们可以检查连接的棋子？
                // 最安全的方法是通过 Finalizer 进行 try-catch。
                return true;
            }

            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    return null; // 抑制 reset 中的 NRE
                }
                return null;
            }
        }

        [HarmonyPatch(typeof(CompGestaltEngine), "connect")]
        public static class Patch_Connect
        {
            static bool Prefix(CompGestaltEngine __instance, LocalTargetInfo target)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return true;

                // 防止无限循环连接：如果目标超出范围，直接禁止连接尝试
                Pawn p = target.Thing as Pawn;
                if (p != null && p.Map != __instance.parent.Map)
                {
                    if (!GestaltRangeCalculator.IsInRange(__instance, p))
                    {
                        return false; // 拦截连接，打破循环
                    }
                }
                return true;
            }

            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                     return null; // 抑制 connect 中的 NRE
                }
                return null;
            }
        }
// ... (Patch_CurrentUpgrade omitted, keep as is) ...

        [HarmonyPatch(typeof(CompGestaltEngine), "PostSpawnSetup")]
        public static class Patch_PostSpawnSetup
        {
            static void Postfix(CompGestaltEngine __instance)
            {
                // 修复穿梭机无法装载的问题：
                // 游戏逻辑直接读取 props.upgrades 列表中的数据，而不是调用 patched CurrentUpgrade 属性。
                // 因此我们必须修改内存中的 Upgrade 对象，强制开启 allowCaravans。
                if (__instance.props != null)
                {
                    CompProperties_Upgradeable upgProps = __instance.props as CompProperties_Upgradeable;
                    
                    if (upgProps != null && upgProps.upgrades != null)
                    {
                        FieldInfo allowCaravansField = AccessTools.Field(typeof(Upgrade), "allowCaravans");
                        foreach (var upg in upgProps.upgrades)
                        {
                            if (!upg.allowCaravans)
                            {
                                if (allowCaravansField != null)
                                {
                                    allowCaravansField.SetValue(upg, true);
                                }
                            }
                        }
                    }
                }

                // 强制同步 dummyPawn 的名称为建筑名称，解决语言不一致问题
                if (__instance.dummyPawn != null && __instance.parent != null)
                {
                    NameTriple name = __instance.dummyPawn.Name as NameTriple;
                    if (name != null && name.Nick != __instance.parent.LabelCap)
                    {
                        __instance.dummyPawn.Name = new NameTriple(name.First, __instance.parent.LabelCap, name.Last);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
        public static class Patch_Pawn_SpawnSetup
        {
            static void Postfix(Pawn __instance, bool respawningAfterLoad)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return;

                if (respawningAfterLoad) return;
                if (__instance.Map == null) return;
                
                // 检查是否为格式塔控制的机械体
                Pawn overseer = __instance.GetOverseer();
                if (overseer == null) return;
                
                CompGestaltEngine hub = overseer.TryGetComp<CompGestaltEngine>();
                if (hub == null) return;
                
                // 如果机械体进入了新地图（例如空投落地），检查距离
                if (__instance.Map != hub.parent.Map)
                {
                    if (!GestaltRangeCalculator.IsInRange(hub, __instance))
                    {
                        // 超出范围，发送从上方弹出的警告消息
                        Messages.Message("GEU_SignalStatus_ArrivalLost".Translate(__instance.LabelShort), 
                            __instance, MessageTypeDefOf.NegativeEvent);
                        
                        // 可选：立即断开连接？暂时只发警告，让 Heartbeat Patch 去处理断开
                    }
                }
            }
        }

        // 新增：拦截征召功能，使超出范围的机械体无法被征召（表现为"不受控"）
        // 新增：拦截征召功能，使超出范围的机械体无法被征召（表现为"不受控"）
        // 改为拦截 GetGizmos，这比 Patch 属性Getter 更稳定，且均能达到隐藏征召按钮的效果
        // 改为拦截 GetGizmos，这比 Patch 属性Getter 更稳定，且均能达到隐藏征召按钮的效果
        [HarmonyPatch(typeof(Pawn_DraftController), "GetGizmos")]
        public static class Patch_DraftController_GetGizmos
        {
            static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_DraftController __instance)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR)
                {
                    foreach (var g in __result) yield return g;
                    yield break;
                }

                // 如果机械体在超出范围名单中
                bool isOutOfRange = __instance.pawn != null && outOfRangePawns.Contains(__instance.pawn.thingIDNumber);
                
                foreach (var g in __result)
                {
                    if (isOutOfRange)
                    {
                        Command command = g as Command;
                        // 识别征召按钮：通过快捷键识别比反射私有字段更稳定
                        if (command != null && command.hotKey == KeyBindingDefOf.Command_ColonistDraft)
                        {
                             command.Disable("GEU_DraftDisabledReason".Translate());
                        }
                    }
                    yield return g;
                }
            }
        }

        // 修复：Mote_MechUncontrolled 在绘制失控图标时可能因空引用崩溃
        // 这通常发生在机械体处于特殊状态（如在穿梭机中但被判定为失控）
        [HarmonyPatch(typeof(Graphic_PawnBodySilhouette), "DrawWorker")]
        public static class Patch_MoteCrashFix
        {
            static Exception Finalizer(Exception __exception)
            {
                if (__exception is NullReferenceException)
                {
                    // 抑制空引用异常，防止游戏因图标绘制失败而报错/卡顿
                    return null;
                }
                return __exception;
            }
        }

        // 新增：禁止失控机械体工作
        // 模拟原版低宽带/失控状态：机械体不应进行任何工作
        [HarmonyPatch(typeof(Pawn), "WorkTypeIsDisabled")]
        public static class Patch_WorkTypeIsDisabled
        {
            static void Postfix(Pawn __instance, WorkTypeDef w, ref bool __result)
            {
                if (!GestaltEngineUnlimitedMod.settings.enableBVR) return;
                
                if (__result) return; // 如果已经禁用了，无需处理

                // 如果是失控的格式塔机械体
                if (outOfRangePawns.Contains(__instance.thingIDNumber))
                {
                    __result = true; // 强制禁用所有工作
                }
            }
        }

        [HarmonyPatch(typeof(CompGestaltEngine), "CompGetGizmosExtra")]
        public static class Patch_CompGetGizmosExtra
        {
            static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, CompGestaltEngine __instance)
            {
                // 仅在 BVR 开启时显示信号指示器
                if (GestaltEngineUnlimitedMod.settings.enableBVR)
                {
                    yield return new Gizmo_SignalRelayStatus
                    {
                        hub = __instance
                    };
                }

                // DEV: 重置骇入冷却
                if (DebugSettings.godMode)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "GEU_Command_DevResetHackCooldown".Translate(),
                        defaultDesc = "GEU_Command_DevResetHackCooldownDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct", true), // 临时借用图标，或者 null
                        action = delegate
                        {
                            FieldInfo ticksField = AccessTools.Field(typeof(CompGestaltEngine), "hackCooldownTicks");
                            if (ticksField != null)
                            {
                                ticksField.SetValue(__instance, 0);
                                Messages.Message("GEU_Command_DevResetHackCooldown".Translate() + ": Success", MessageTypeDefOf.PositiveEvent, false);
                            }
                        }
                    };
                }

                foreach (var gizmo in __result)
                {
                    Command_Action command = gizmo as Command_Action;
                    if (command != null && GestaltEngineUnlimitedMod.settings.enableBVR) // BVR 关闭时，不替换原版按钮功能
                    {
                        if (command.defaultLabel == "RM.ConnectMechanoid".Translate() || command.defaultLabel == "远程控制")
                        {
                            command.action = () =>
                            {
                                ShowMapSelectionMenu(__instance, (map) => StartTargeting(__instance, map));
                            };
                        }
                        else if (command.defaultLabel == "RM.HackMechanoid".Translate() || command.defaultLabel == "骇入单位")
                        {
                            // 新增：骇入单位也支持跨地图选择
                            command.action = () =>
                            {
                                ShowMapSelectionMenu(__instance, (map) => StartHacking(__instance, map));
                            };
                        }
                    }
                    yield return gizmo;
                }
            }

            // 提取通用的地图选择菜单逻辑
            static void ShowMapSelectionMenu(CompGestaltEngine instance, Action<Map> onMapSelected)
            {
                List<FloatMenuOption> mapOptions = new List<FloatMenuOption>();
                List<Map> validMaps = new List<Map>();
                
                foreach (Map map in Find.Maps)  
                {
                    if (map == instance.parent.Map) continue; 
                    validMaps.Add(map);
                }

                if (validMaps.Count == 0)
                {
                    onMapSelected(instance.parent.Map);
                }
                else
                {
                    mapOptions.Add(new FloatMenuOption("GEU_MapSelect_Current".Translate(), () => 
                    {
                        onMapSelected(instance.parent.Map);
                    }));
                    
                    foreach (Map map in validMaps)
                    {
                        mapOptions.Add(new FloatMenuOption("GEU_MapSelect_SwitchTo".Translate(map.Parent.Label), () => 
                        {
                            CameraJumper.TryJump(map.Center, map);
                            LongEventHandler.QueueLongEvent(() => onMapSelected(map), "GEU_Message_RedirectingSignal".Translate(), false, null);
                        }));
                    }
                    
                    Find.WindowStack.Add(new FloatMenu(mapOptions));
                }
            }

            // 字典追踪正在进行的远程连接：枢纽 -> 上下文
            public static Dictionary<CompGestaltEngine, PendingConnectionContext> pendingConnections = new Dictionary<CompGestaltEngine, PendingConnectionContext>();

            public class PendingConnectionContext
            {
                public int ticksLeft;
                public Pawn target;
                public Effecter effecter;
                public Action onComplete;

                public PendingConnectionContext(Pawn t, int ticks, Effecter eff, Action complete)
                {
                    target = t;
                    ticksLeft = ticks;
                    effecter = eff;
                    onComplete = complete;
                }
            }

            static void StartTargeting(CompGestaltEngine instance, Map targetMap)
            {
                var connectParamsMethod = AccessTools.Method(typeof(CompGestaltEngine), "connectMechanoidTargetParameters");
                // 恢复 startConnect 方法用于本地连接
                var startConnectMethod = AccessTools.Method(typeof(CompGestaltEngine), "startConnect", new Type[] { typeof(LocalTargetInfo) });
                // 远程连接使用的底层 connect 方法
                var connectMethod = AccessTools.Method(typeof(CompGestaltEngine), "connect", new Type[] { typeof(LocalTargetInfo), typeof(Pawn) });
                var highlightMethod = AccessTools.Method(typeof(CompGestaltEngine), "highlight", new Type[] { typeof(LocalTargetInfo) });
                var canConnectMethod = AccessTools.Method(typeof(CompGestaltEngine), "canConnect", new Type[] { typeof(LocalTargetInfo) });

                TargetingParameters parms = (TargetingParameters)connectParamsMethod.Invoke(instance, null);
                
                Action<LocalTargetInfo> onSelect = (t) => 
                {
                    try
                    {
                        Pawn targetPawn = t.Thing as Pawn;
                        
                        // 1. 距离检查
                        if (targetPawn != null && !GestaltRangeCalculator.IsInRange(instance, targetPawn))
                        {
                            Messages.Message("GEU_SignalStatus_OutOfRange".Translate(), targetPawn, MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        if (targetPawn != null)
                        {
                            // 2. 分情况处理
                            if (targetPawn.Map == instance.parent.Map)
                            {
                                // === 本地地图：使用原版逻辑 (Job系统) ===
                                if (startConnectMethod != null)
                                {
                                    startConnectMethod.Invoke(instance, new object[] { t });
                                }
                            }
                            else
                            {
                                // === 跨地图：使用拟真模拟逻辑 ===
                                Effecter eff = null;
                                EffecterDef hackingEffect = DefDatabase<EffecterDef>.GetNamed("RM_Hacking", false);
                                if (hackingEffect != null)
                                {
                                    eff = hackingEffect.Spawn(targetPawn, targetPawn.Map);
                                }

                                FieldInfo connectingToField = AccessTools.Field(typeof(CompGestaltEngine), "connectingTo");
                                if (connectingToField != null)
                                {
                                    connectingToField.SetValue(instance, targetPawn);
                                }

                                if (targetPawn.pather != null) targetPawn.pather.StopDead();
                                if (targetPawn.jobs != null) targetPawn.jobs.StopAll(); 
                                targetPawn.stances.SetStance(new Stance_Cooldown(180, null, null)); 

                                if (pendingConnections.ContainsKey(instance)) pendingConnections.Remove(instance);
                                
                                pendingConnections.Add(instance, new PendingConnectionContext(
                                    targetPawn, 
                                    180, 
                                    eff, 
                                    () => 
                                    {
                                        if (connectMethod != null)
                                        {
                                            connectMethod.Invoke(instance, new object[] { t, instance.dummyPawn });
                                        }
                                    }
                                ));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[BVR] 远程连接失败: " + ex.ToString());
                        Messages.Message("连接失败，请查看日志", MessageTypeDefOf.RejectInput, false);
                    }
                };
                
                Action<LocalTargetInfo> onHighlight = (t) =>
                {
                    try
                    {
                        highlightMethod.Invoke(instance, new object[] { t });
                    }
                    catch {}
                };
                
                Func<LocalTargetInfo, bool> validator = (t) =>
                {
                    try
                    {
                        return (bool)canConnectMethod.Invoke(instance, new object[] { t });
                    }
                    catch
                    {
                        return false;
                    }
                };

                Find.Targeter.BeginTargeting(parms, onSelect, onHighlight, validator, null, null, null, true);
            }

            // 新增：远程骇入的 Targeting 方法
            // 新增：远程骇入的 Targeting 方法
            static void StartHacking(CompGestaltEngine instance, Map targetMap)
            {
                // 根据反编译代码 (GestaltEngine.cs) 修正反射名称
                var hackParamsMethod = AccessTools.Method(typeof(CompGestaltEngine), "connectNonColonyMechanoidTargetParameters");
                var startHackMethod = AccessTools.Method(typeof(CompGestaltEngine), "startConnectNonColonyMech", new Type[] { typeof(LocalTargetInfo) }); 
                var hackMethod = AccessTools.Method(typeof(CompGestaltEngine), "connect", new Type[] { typeof(LocalTargetInfo), typeof(Pawn) }); 
                var highlightHackingMethod = AccessTools.Method(typeof(CompGestaltEngine), "highlight", new Type[] { typeof(LocalTargetInfo) });
                var canHackMethod = AccessTools.Method(typeof(CompGestaltEngine), "canConnectNonColonyMech", new Type[] { typeof(LocalTargetInfo) });

                TargetingParameters parms = null;
                if (hackParamsMethod != null)
                {
                    parms = (TargetingParameters)hackParamsMethod.Invoke(instance, null);
                }
                else
                {
                    // Fallback: 依然保留回退机制防止版本差异
                    parms = new TargetingParameters
                    {
                        canTargetPawns = true,
                        canTargetBuildings = false,
                        canTargetLocations = false,
                        validator = (TargetInfo t) => 
                        {
                            if (canHackMethod != null)
                            {
                                // Fix: TargetInfo implicit conversion to LocalTargetInfo works, or valid ctor
                                return (bool)canHackMethod.Invoke(instance, new object[] { new LocalTargetInfo(t.Thing) });
                            }
                            Pawn p = t.Thing as Pawn;
                            return p != null && p.RaceProps.IsMechanoid; 
                        }
                    };
                }
                
                Action<LocalTargetInfo> onSelect = (t) => 
                {
                    try
                    {
                        Pawn targetPawn = t.Thing as Pawn;
                        
                        // 1. 距离检查
                        if (targetPawn != null && !GestaltRangeCalculator.IsInRange(instance, targetPawn))
                        {
                            Messages.Message("GEU_SignalStatus_OutOfRange".Translate(), targetPawn, MessageTypeDefOf.RejectInput, false);
                            return;
                        }

                        if (targetPawn != null)
                        {
                            if (targetPawn.Map == instance.parent.Map)
                            {
                                // 本地地图
                                if (startHackMethod != null)
                                {
                                    startHackMethod.Invoke(instance, new object[] { t });
                                }
                                else
                                {
                                    Messages.Message("GEU_Error_StartHackMethodNotFound".Translate(), MessageTypeDefOf.RejectInput, false);
                                }
                            }
                            else
                            {
                                // === 跨地图：使用拟真模拟逻辑 ===
                                Effecter eff = null;
                                EffecterDef hackingEffect = DefDatabase<EffecterDef>.GetNamed("RM_Hacking", false);
                                if (hackingEffect != null)
                                {
                                    eff = hackingEffect.Spawn(targetPawn, targetPawn.Map);
                                }

                                FieldInfo connectingToField = AccessTools.Field(typeof(CompGestaltEngine), "connectingTo");
                                if (connectingToField != null)
                                {
                                    connectingToField.SetValue(instance, targetPawn);
                                }

                                if (targetPawn.pather != null) targetPawn.pather.StopDead();
                                if (targetPawn.jobs != null) targetPawn.jobs.StopAll(); 
                                targetPawn.stances.SetStance(new Stance_Cooldown(180, null, null)); 

                                if (pendingConnections.ContainsKey(instance)) pendingConnections.Remove(instance);
                                
                                pendingConnections.Add(instance, new PendingConnectionContext(
                                    targetPawn, 
                                    180, 
                                    eff, 
                                    () => 
                                    {
                                        // 完成回调：执行骇入 (connect 方法包含骇入逻辑)
                                        if (hackMethod != null)
                                        {
                                            hackMethod.Invoke(instance, new object[] { t, instance.dummyPawn });
                                        }
                                    }
                                ));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[BVR] 远程骇入失败: " + ex.ToString());
                        Messages.Message("GEU_Error_HackFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                    }
                };
                
                Action<LocalTargetInfo> onHighlight = (t) =>
                {
                    try
                    {
                        if (highlightHackingMethod != null)
                            highlightHackingMethod.Invoke(instance, new object[] { t });
                    }
                    catch {}
                };
                
                Func<LocalTargetInfo, bool> validator = (t) =>
                {
                    try
                    {
                        if (canHackMethod != null)
                        {
                             // 修复：TargetInfo 隐式转换为 LocalTargetInfo 有效，或使用有效构造函数
                             return (bool)canHackMethod.Invoke(instance, new object[] { new LocalTargetInfo(t.Thing) });
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                };

                Find.Targeter.BeginTargeting(parms, onSelect, onHighlight, validator, null, null, null, true);
            }
        }
        
        // 新增：拦截 RM2 独角仙 (Caretaker) 自爆逻辑
        // 目标：ReinforcedMechanoids.CompExplodeIfNoOtherFactionPawns.CompTick
        [HarmonyPatch]
        public static class Patch_RMSelfDestruct
        {
            private static MethodBase cachedMethod;

            static bool Prepare()
            {
                Type compType = AccessTools.TypeByName("ReinforcedMechanoids.CompExplodeIfNoOtherFactionPawns");
                if (compType == null)
                {
                     compType = AccessTools.TypeByName("ReinforcedMechanoids.CompExplodeIfNoOtherFactionPawns_Fix"); 
                }

                if (compType != null)
                {
                    cachedMethod = AccessTools.Method(compType, "CompTick");
                    return cachedMethod != null;
                }
                return false;
            }

            static MethodBase TargetMethod()
            {
                // 如果 Prepare 返回 false，这个方法应该不会被调用
                // 但为了安全起见，我们还是返回 cachedMethod
                // 如果 cachedMethod 也是 null，此时 Harmony 可能会报错，但逻辑上 Prepare 会阻止这种情况
                if (cachedMethod == null)
                {
                    // 尝试再次查找，以防万一
                    Prepare();
                }
                return cachedMethod;
            }

            static bool Prefix(ThingComp __instance)
            {
                // 如果设置未开启，运行原版逻辑
                if (!GestaltEngineUnlimitedMod.settings.smartCaretakerCheck) return true;

                Pawn pawn = __instance.parent as Pawn;
                if (pawn == null || pawn.Map == null || pawn.Faction == null) return true;
                Map map = pawn.Map;

                // 1. 原版逻辑：检查已生成的 Pawn
                List<Pawn> spawnedPawns = map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
                bool hasSpawnedAlly = false;
                for (int i = 0; i < spawnedPawns.Count; i++)
                {
                    if (spawnedPawns[i].kindDef != pawn.kindDef)
                    {
                        hasSpawnedAlly = true;
                        break;
                    }
                }
                if (hasSpawnedAlly) return true; // 原版逻辑判为安全，无需干预

                // 2. 智能逻辑：检查隐匿的 Pawn (穿梭机、载具、被携带等)
                // 之前的补丁失败是因为 Shuttles 属于 Transporter 组，不一定都在 ThingHolder 组
                
                // 2.1 检查自己是否携带着队友 (搬运伤员时)
                if (pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
                {
                   Pawn carried = pawn.carryTracker.CarriedThing as Pawn;
                   if (carried != null && carried.Faction == pawn.Faction) return false; // 抱着队友 -> 不炸
                }

                // 2.2 遍历地图上的容器
                // 重点检查: ThingHolder (棺材/休眠舱) 和 Transporter (空投舱/穿梭机)
                List<ThingRequestGroup> groupsToCheck = new List<ThingRequestGroup> 
                { 
                    ThingRequestGroup.ThingHolder, 
                    ThingRequestGroup.Transporter 
                };

                foreach (ThingRequestGroup group in groupsToCheck)
                {
                    List<Thing> things = map.listerThings.ThingsInGroup(group);
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing t = things[i];
                        if (t.Destroyed) continue;

                        IThingHolder holder = t as IThingHolder;
                        // 对于 CompTransporter，容器在 Comp 里
                        if (holder == null)
                        {
                            CompTransporter trans = t.TryGetComp<CompTransporter>();
                            if (trans != null) holder = trans;
                        }

                        if (holder != null)
                        {
                            // 递归查找容器内的东西
                            bool foundAlly = false;
                            // 使用简单的 GetDirectlyHeldThings 可能会漏掉嵌套，但 GetAllThingsRecursively 比较慢
                            // 在这里为了保险起见，我们只检查一层 (穿梭机直接装着人)
                            // 修正：穿梭机可能也是一层套一层？使用递归更安全，且 60 tick 一次还行
                            
                            List<Thing> content = new List<Thing>();
                            ThingOwnerUtility.GetAllThingsRecursively(holder, content, true, null);
                            
                            for (int j = 0; j < content.Count; j++)
                            {
                                Pawn p = content[j] as Pawn;
                                if (p != null && p.Faction == pawn.Faction && !p.Dead && p != pawn)
                                {
                                    // 还要排除其他 Caretaker 吗？原版逻辑排除了
                                    if (p.kindDef != pawn.kindDef)
                                    {
                                        foundAlly = true;
                                        break;
                                    }
                                }
                            }
                            if (foundAlly) return false; // 找到藏着的队友 -> 拦截自爆
                        }
                    }
                }

                // 3. 如果都没找到，运行原版逻辑 (返回 true，让它 tick -> explode)
                return true; 
            }

        }
        
        // 新增：当枢纽被拆除/销毁时，强制断开所有连接
        [HarmonyPatch(typeof(CompGestaltEngine), "PostDeSpawn")]
        public static class Patch_PostDeSpawn_Disconnect
        {
            static void Prefix(CompGestaltEngine __instance, Map map)
            {
                try
                {
                    Pawn overseer = __instance.dummyPawn;
                    if (overseer == null || overseer.relations == null || overseer.mechanitor == null) return;

                    List<Pawn> overseeing = overseer.mechanitor.OverseenPawns;
                    // 必须倒序遍历，因为移除关系会修改列表
                    for (int i = overseeing.Count - 1; i >= 0; i--)
                    {
                        Pawn mech = overseeing[i];
                        overseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[BVR] Error during hub destruction cleanup: " + ex);
                }
            }
        }

        // 专门修复 "Inspect string contains empty lines" 报错
        // 增加对 InspectPaneFiller 的拦截，作为最后的防线
        [HarmonyPatch(typeof(InspectPaneFiller), "DrawInspectStringFor")]
        public static class Patch_InspectPaneFiller_Fix
        {
            static void Prefix(ISelectable sel, ref Rect rect)
            {
                // 该方法不直接修改字符串，这里只是占位，实际修复仍在 Thing.GetInspectString
                // 但为了保险，我们可以修改 Thing.GetInspectString 的逻辑更暴力一点
            }
        }

        // 拦截并屏蔽特定的报错日志
        [HarmonyPatch(typeof(Log), "ErrorOnce")]
        public static class Patch_Log_SuppressInspectError
        {
            static bool Prefix(string text, int key)
            {
                // 如果是关于 GEU 信号塔的空行报错，直接拦截
                if (text.Contains("Inspect string for GEU_SignalExpansionTower") && text.Contains("contains empty lines"))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Thing), "GetInspectString")]
        public static class Patch_SignalTower_InspectString
        {
            [HarmonyPriority(Priority.Last)] // 确保最后执行，清理所有其他 Mod 留下的杂乱数据
            static void Postfix(Thing __instance, ref string __result)
            {
                if (__instance.def.defName == "GEU_SignalExpansionTower")
                {
                    if (string.IsNullOrEmpty(__result)) return;

                    // 1. 强制清理所有非标准换行符
                    string str = __result.Replace("\r\n", "\n").Replace("\r", "\n");
                    
                    // 2. 使用更严谨的分割与重组
                    // Trim() 是关键步骤
                    string[] lines = str.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (lines.Length > 0)
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i].Trim();
                            if (!string.IsNullOrEmpty(line))
                            {
                                if (sb.Length > 0) sb.Append("\n");
                                sb.Append(line);
                            }
                        }
                        __result = sb.ToString(); 
                    }
                    else
                    {
                        __result = "";
                    }
                }
            }
        }

        // 修复：拦截 GestaltEngine 原版 Mod 中的 NullReferenceException
        // 目标：GestaltEngine.Pawn_MechanitorTracker_TotalBandwidth.Postfix
        // 该方法在某些复杂地图状态下（如穿梭机、远征队）可能会崩溃，导致 Gizmo 绘制失败。
        [HarmonyPatch]
        public static class Patch_Fix_Pawn_MechanitorTracker_TotalBandwidth
        {
            static MethodBase TargetMethod()
            {
                Type patchType = AccessTools.TypeByName("GestaltEngine.Pawn_MechanitorTracker_TotalBandwidth");
                if (patchType != null)
                {
                    return AccessTools.Method(patchType, "Postfix");
                }
                return null;
            }

            // Finalizer: 捕获并抑制原有 Postfix 抛出的异常
            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    // 仅在 Debug 模式下打印，或者是为了防止刷屏可以完全抑制
                    // Log.Warning("[BVR] 拦截到原版带宽计算崩溃 (已抑制): " + __exception.Message);
                    return null; // 返回 null 表示异常已处理，不会向上抛出
                }
                return null;
            }
        }
    }
}
