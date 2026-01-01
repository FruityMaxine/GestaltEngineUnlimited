
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using GestaltEngine;

namespace GestaltEngineUnlimited
{
    // Mod主类
    public class GestaltEngineUnlimitedMod : Mod
    {
        public static GEU_Settings settings;

        public GestaltEngineUnlimitedMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<GEU_Settings>();
            Harmony harmony = new Harmony("gestalt.engine.unlimited");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            // 手动应用 NRE 修复补丁，确保它一定生效
            ApplyEmergencyPatches(harmony);

            Log.Message("[格式塔枢纽无限升级] Mod已加载，当前曲线模式: " + settings.curveMode);
            
            // 注册DefDatabase加载完成后的回调，预填充升级数据
            LongEventHandler.ExecuteWhenFinished(PrePopulateUpgradeData);
        }
        
        // 预填充5-10级的升级数据，避免运行时生成时序问题
        private void PrePopulateUpgradeData()
        {
            Log.Message("[格式塔枢纽无限升级] 开始预填充升级数据...");
            // 这个方法将在游戏加载Defs后执行，此时CompUpgradeable的Props已经初始化
            
            // 修复：全局强制开启所有等级（包括0-4级）的 allowCaravans
            // 这解决了在穿梭机装载时的 "Gestalt level too low" 错误
            ThingDef gestaltEngine = DefDatabase<ThingDef>.GetNamed("RM_GestaltEngine", false);
            if (gestaltEngine != null)
            {
                // 使用反射查找 CompUpgradeable 和 Props
                // 因为我们没有硬引用 ReinforceMechanoids.dll，只能通过 Def 查找
                // 注意：ThingDef.comps 是 List<CompProperties>
                
                var compProps = gestaltEngine.comps.FirstOrDefault(c => c.GetType().FullName == "GestaltEngine.CompProperties_Upgradeable");
                if (compProps != null)
                {
                }
            }
            
            // [NEW] 动态注入存档保护组件
            if (gestaltEngine != null && gestaltEngine.GetCompProperties<CompProperties_SaveProtection>() == null)
            {
                gestaltEngine.comps.Add(new CompProperties_SaveProtection());
                Log.Message("[格式塔枢纽无限升级] 已动态注入 CompSaveProtection 存档保护组件");
            }

            // [NEW_AUTO_FIX] 强制修改信号塔的类为原版 Building，确保移除 Mod 后不崩档
            ThingDef towerDef = DefDatabase<ThingDef>.GetNamed("GEU_SignalExpansionTower", false);
            if (towerDef != null)
            {
                // 无论 XML 里写什么，这里强制改为 Building
                // 这样新创建的建筑保存时会写 <thing Class="Building">
                // 而不是 <thing Class="GestaltEngineUnlimited.Building_SignalTower">
                towerDef.thingClass = typeof(Building);
            }
            
            // [NEW] 应用内容抑制逻辑 (根据设置隐藏/显示 BVR 内容)
            ApplyContentSuppression();
        }

        public static void ApplyContentSuppression()
        {
            // [CHANGED] 恢复研究项目的隐藏逻辑 (这部分很安全，不会导致 UI 崩溃)
            // 建筑依然保留显示，以避免 "NullReferenceException"
            
            ResearchProjectDef bvrResearch = DefDatabase<ResearchProjectDef>.GetNamed("GEU_GestaltBVRCommunication", false);
            var settings = GestaltEngineUnlimitedMod.settings;

            if (bvrResearch != null)
            {
                if (settings.enableBVR)
                {
                    // 启用：如果原本就有 tab，恢复它 (XML默认无tab，意味着在Main里)
                    // 这里直接设为 Main 即可
                    bvrResearch.tab = DefDatabase<ResearchTabDef>.GetNamed("Main", false);
                }
                else
                {
                    // 禁用：设为 null 以隐藏
                    bvrResearch.tab = null;
                }
            }
            
            Log.Message("[格式塔枢纽无限升级] BVR 设置已更新 (研究显隐已同步)。");
        }

        private void ApplyEmergencyPatches(Harmony harmony)
        {
            try
            {
                Log.Message("[格式塔枢纽无限升级] 正在应用紧急修复补丁...");
                
                // 目标：GestaltEngine.Pawn_MechanitorTracker_TotalBandwidth.Postfix
                Type patchType = AccessTools.TypeByName("GestaltEngine.Pawn_MechanitorTracker_TotalBandwidth");
                if (patchType == null)
                {
                    Log.Error("[格式塔枢纽无限升级] 找不到 GestaltEngine.Pawn_MechanitorTracker_TotalBandwidth 类，无法修复 NRE。");
                    return;
                }

                MethodInfo originalPostfix = AccessTools.Method(patchType, "Postfix");
                if (originalPostfix == null)
                {
                    Log.Error("[格式塔枢纽无限升级] 找不到 Postfix 方法，无法修复 NRE。");
                    return;
                }

                MethodInfo finalizer = AccessTools.Method(typeof(GestaltEngineUnlimitedMod), "BandwidthPatchFinalizer");
                
                harmony.Patch(originalPostfix, finalizer: new HarmonyMethod(finalizer));
                Log.Message("[格式塔枢纽无限升级] 成功应用 Bandwidth NRE 拦截补丁！");
            }
            catch (Exception ex)
            {
                Log.Error("[格式塔枢纽无限升级] 应用紧急补丁失败: " + ex);
            }
        }

        public static Exception BandwidthPatchFinalizer(Exception __exception)
        {
            if (__exception != null)
            {
                // 抑制异常
                return null;
            }
            return null;
        }

        public override string SettingsCategory()
        {
            return "GEU_ModName".Translate();
        }

        // 滚动视图位置
        private Vector2 scrollPosition;

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            // 计算所需总高度：动态计算或预估
            // 简化实现：预设较大的视图区域
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 1200f);
            
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect, true);
            
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(viewRect);
            
            // 曲线模式选择
            listingStandard.Label("GEU_UpgradeCurveMode".Translate() + ": " + settings.curveMode.ToString());
            
            if (listingStandard.RadioButton("GEU_LinearMode".Translate(), settings.curveMode == CurveMode.Linear, 0f, "GEU_LinearModeDesc".Translate()))
            {
                settings.curveMode = CurveMode.Linear;
            }
            
            if (listingStandard.RadioButton("GEU_LogarithmicMode".Translate(), settings.curveMode == CurveMode.Logarithmic, 0f, "GEU_LogarithmicModeDesc".Translate()))
            {
                settings.curveMode = CurveMode.Logarithmic;
            }
            
            listingStandard.Gap(12f);
            
            // 根据模式显示不同的配置选项
            if (settings.curveMode == CurveMode.Linear)
            {
                DrawLinearModeSettings(listingStandard);
            }
            else
            {
                DrawLogarithmicModeSettings(listingStandard);
            }
            
            listingStandard.Gap(12f);
            
            // BVR 设置
            DrawBVRSettings(listingStandard);
            
            listingStandard.Gap(12f);
            
            // 重置按钮
            if (listingStandard.ButtonText("GEU_ResetToDefaults".Translate()))
            {
                settings.ResetToDefaults();
            }
            
            listingStandard.End();
            Widgets.EndScrollView();
            
            base.DoSettingsWindowContents(inRect);
        }

        private void DrawLinearModeSettings(Listing_Standard listing)
        {
            listing.Label("GEU_LinearModeSettings".Translate());
            listing.Gap(6f);
            
            // 电力斜率滑块
            listing.Label("GEU_PowerSlope".Translate() + ": " + settings.linearPowerSlope.ToString("F0") + " W/" + "GEU_PerLevel".Translate());
            settings.linearPowerSlope = listing.Slider(settings.linearPowerSlope, 0f, 10000f);
            
            // 带宽斜率滑块
            listing.Label("GEU_BandwidthSlope".Translate() + ": " + settings.linearBandwidthSlope + " " + "GEU_PerLevel".Translate());
            settings.linearBandwidthSlope = (int)listing.Slider(settings.linearBandwidthSlope, 0, 30);
            
            // 控制组斜率滑块
            listing.Label("GEU_ControlGroupSlope".Translate() + ": " + settings.linearControlGroupSlope + " " + "GEU_PerLevel".Translate());
            settings.linearControlGroupSlope = (int)listing.Slider(settings.linearControlGroupSlope, 0, 5);
            
            // 研究速度斜率滑块
            listing.Label("GEU_ResearchSlope".Translate() + ": " + settings.linearResearchSlope.ToString("F3") + " " + "GEU_PerLevel".Translate());
            settings.linearResearchSlope = listing.Slider(settings.linearResearchSlope, 0f, 0.5f);
            
            // 热量斜率滑块
            listing.Label("GEU_HeatSlope".Translate() + ": " + settings.linearHeatSlope.ToString("F1") + " " + "GEU_PerLevel".Translate());
            settings.linearHeatSlope = listing.Slider(settings.linearHeatSlope, 0f, 100f);
            
            listing.Gap(6f);
            
            // 升级冷却时间滑块（转换为天数显示）
            float cooldownDays = settings.linearUpgradeCooldown / 60000f;
            listing.Label("GEU_UpgradeCooldown".Translate() + ": " + cooldownDays.ToString("F1") + " " + "GEU_Days".Translate());
            settings.linearUpgradeCooldown = (int)(listing.Slider(cooldownDays, 0.1f, 60f) * 60000f);
            
            // 升级持续时间滑块（转换为天数显示）
            float durationDays = settings.linearUpgradeDuration / 60000f;
            listing.Label("GEU_UpgradeDuration".Translate() + ": " + durationDays.ToString("F1") + " " + "GEU_Days".Translate());
            settings.linearUpgradeDuration = (int)(listing.Slider(durationDays, 0.1f, 30f) * 60000f);
        }

        private void DrawLogarithmicModeSettings(Listing_Standard listing)
        {
            listing.Label("GEU_LogarithmicModeSettings".Translate());
            listing.Gap(6f);
            
            // 电力增长底数
            listing.Label("GEU_PowerLogBase".Translate() + ": " + settings.logPowerBase.ToString("F2"));
            settings.logPowerBase = listing.Slider(settings.logPowerBase, 1.1f, 5.0f);
            
            // 带宽增长底数
            listing.Label("GEU_BandwidthLogBase".Translate() + ": " + settings.logBandwidthBase.ToString("F2"));
            settings.logBandwidthBase = listing.Slider(settings.logBandwidthBase, 1.1f, 5.0f);
            
            // 热量增长底数
            listing.Label("GEU_HeatLogBase".Translate() + ": " + settings.logHeatBase.ToString("F2"));
            settings.logHeatBase = listing.Slider(settings.logHeatBase, 1.1f, 5.0f);
            
            // 每级最小带宽增加
            listing.Label("GEU_MinBandwidthPerLevel".Translate() + ": " + settings.logMinBandwidthPerLevel);
            settings.logMinBandwidthPerLevel = (int)listing.Slider(settings.logMinBandwidthPerLevel, 0, 10);
            
            listing.Gap(6f);
            
            // 冷却时间增量（转换为天数显示）
            float cooldownIncrementDays = settings.logCooldownIncrement / 60000f;
            listing.Label("GEU_CooldownIncrement".Translate() + ": " + cooldownIncrementDays.ToString("F1") + " " + "GEU_Days".Translate() + "/" + "GEU_PerLevel".Translate());
            settings.logCooldownIncrement = (int)(listing.Slider(cooldownIncrementDays, 0.01f, 10f) * 60000f);
            
            // 最大冷却时间（转换为天数显示）
            float maxCooldownDays = settings.logMaxUpgradeCooldown / 60000f;
            listing.Label("GEU_MaxUpgradeCooldown".Translate() + ": " + maxCooldownDays.ToString("F0") + " " + "GEU_Days".Translate());
            settings.logMaxUpgradeCooldown = (int)(listing.Slider(maxCooldownDays, 0.1f, 60f) * 60000f);
            
            // 持续时间增量（转换为天数显示）
            float durationIncrementDays = settings.logDurationIncrement / 60000f;
            listing.Label("GEU_DurationIncrement".Translate() + ": " + durationIncrementDays.ToString("F2") + " " + "GEU_Days".Translate() + "/" + "GEU_PerLevel".Translate());
            settings.logDurationIncrement = (int)(listing.Slider(durationIncrementDays, 0.01f, 2f) * 60000f);
            
            // 最大持续时间（转换为天数显示）
            float maxDurationDays = settings.logMaxUpgradeDuration / 60000f;
            listing.Label("GEU_MaxUpgradeDuration".Translate() + ": " + maxDurationDays.ToString("F0") + " " + "GEU_Days".Translate());
            settings.logMaxUpgradeDuration = (int)(listing.Slider(maxDurationDays, 0.1f, 30f) * 60000f);
        }

        private void DrawBVRSettings(Listing_Standard listing)
        {
            listing.GapLine();

            // [NEW] BVR 总开关
            bool previousBVR = settings.enableBVR;
            listing.CheckboxLabeled("启用广域格式塔信号模式 (BVR) [需重启]".Translate(), ref settings.enableBVR, "GEU_Setting_EnableBVRDesc".Translate());
            
            // [NEW] 远征能力独立设置
            // 逻辑：如果 BVR 开启，则必须开启远征（因为BVR核心功能就是远程控制）
            // 如果 BVR 关闭，用户可自由选择是否允许5级+远征
            bool forceCaravans = settings.enableBVR;
            if (forceCaravans)
            {
                bool tempTrue = true;
                listing.CheckboxLabeled("允许 5级+ 格式塔枢纽进行远征 (BVR模式强制开启)".Translate(), ref tempTrue, "当BVR开启时，此选项必须开启以支持远程连接。".Translate());
                settings.allowInfiniteCaravans = true; 
            }
            else
            {
                listing.CheckboxLabeled("允许 5级+ 格式塔枢纽进行远征".Translate(), ref settings.allowInfiniteCaravans, "决定5级及以上的格式塔枢纽是否具备控制机械体远征（搭乘穿梭机）的能力。".Translate());
            }

            if (previousBVR != settings.enableBVR)
            {
                GestaltEngineUnlimitedMod.ApplyContentSuppression();
            }

            if (!settings.enableBVR)
            {
                listing.Label("BVR模式已禁用。所有距离限制解除，无限升级依然有效。".Translate()); // 提示用户 BVR 已禁用，仅保留无限升级
                return; // 禁用时隐藏后续参数
            }
            
            listing.Label("GEU_Settings_BaseRange".Translate() + ": " + settings.bvrBaseRange.ToString("F1"));
            settings.bvrBaseRange = listing.Slider(settings.bvrBaseRange, 1f, 50f);
            
            listing.Label("GEU_Settings_GrowthFactor".Translate() + ": " + settings.bvrGrowthFactor.ToString("F2"));
            settings.bvrGrowthFactor = listing.Slider(settings.bvrGrowthFactor, 1.01f, 2.0f);

            listing.Gap(6f);
            
            // 智能独角仙检查
            listing.CheckboxLabeled("GEU_Setting_SmartCaretakerCheck".Translate(), ref settings.smartCaretakerCheck, "GEU_Setting_SmartCaretakerCheckDesc".Translate());

            listing.Gap(6f);

            // 信号塔电力消耗设置
            listing.Label("GEU_Setting_SignalTowerPower".Translate() + ": " + settings.signalTowerPower + " W");
            
            // 滑块
            int newValue = (int)listing.Slider(settings.signalTowerPower, 0f, 50000f);
            if (newValue != settings.signalTowerPower)
            {
                settings.signalTowerPower = newValue;
                ApplySettings();
            }

            // 数字输入框 (与滑块联动)
            string buffer = settings.signalTowerPower.ToString();
            listing.TextFieldNumeric(ref settings.signalTowerPower, ref buffer, 0f, 50000f);
            
            // 如果输入框修改了数值，也应用设置
            if (buffer != settings.signalTowerPower.ToString())
            {
                 ApplySettings();
            }
        }

        public static void ApplySettings()
        {
             // 动态修改 Def 中的电力消耗
             ThingDef towerDef = DefDatabase<ThingDef>.GetNamed("GEU_SignalExpansionTower", false);
             if (towerDef != null)
             {
                 CompProperties_Power powerProps = towerDef.GetCompProperties<CompProperties_Power>();
                 if (powerProps != null)
                 {
                     // 使用反射设置避免编译时字段名不匹配问题
                     FieldInfo basePowerField = AccessTools.Field(typeof(CompProperties_Power), "basePowerConsumption");
                     if (basePowerField != null)
                     {
                         basePowerField.SetValue(powerProps, (float)settings.signalTowerPower);
                     }
                 }
             }

             // 实时更新现有建筑（如果游戏正在运行）
             if (Current.ProgramState == ProgramState.Playing)
             {
                 foreach (Map map in Find.Maps)
                 {
                     if (towerDef != null)
                     {
                         List<Thing> towers = map.listerThings.ThingsOfDef(towerDef);
                         for (int i = 0; i < towers.Count; i++)
                         {
                             CompPowerTrader power = towers[i].TryGetComp<CompPowerTrader>();
                             if (power != null)
                             {
                                 power.PowerOutput = -(float)settings.signalTowerPower;
                             }
                         }
                     }
                 }
             }
        }
    }

    // 曲线模式枚举
    public enum CurveMode
    {
        Linear,       // 线性模式
        Logarithmic   // 对数模式
    }

    // Mod设置类
    public class GEU_Settings : ModSettings
    {
        // 基础模式选择
        public CurveMode curveMode = CurveMode.Logarithmic; // 默认使用对数模式

        // === 线性模式参数 ===
        public float linearPowerSlope = 2000f;           // 电力斜率 (W/级)
        public int linearBandwidthSlope = 6;             // 带宽斜率 (带宽/级)
        public int linearControlGroupSlope = 1;          // 控制组斜率 (组/级)
        public float linearResearchSlope = 0.05f;        // 研究速度斜率 (点/秒/级)
        public float linearHeatSlope = 10f;              // 热量斜率 (热/秒/级)
        public int linearUpgradeCooldown = 180000;       // 升级冷却时间 (ticks, 3天)
        public int linearUpgradeDuration = 60000;        // 升级持续时间 (ticks, 1天)

        // === 对数模式参数 ===
        public float logPowerBase = 1.5f;                // 电力增长底数
        public float logBandwidthBase = 1.5f;            // 带宽增长底数
        public float logHeatBase = 1.3f;                 // 热量增长底数
        public int logMinBandwidthPerLevel = 1;          // 每级最小带宽增加
        public int logMaxUpgradeCooldown = 1800000;      // 最大冷却时间 (ticks, 30天)
        public int logMaxUpgradeDuration = 300000;       // 最大持续时间 (ticks, 5天)
        public int logCooldownIncrement = 30000;         // 冷却时间增量 (ticks/级)
        public int logDurationIncrement = 10000;         // 持续时间增量 (ticks/级)

        // === BVR 模式参数 ===
        public bool enableBVR = false;                   // [NEW] BVR 开关 (默认关闭)
        public float bvrBaseRange = 8f;                  // 基础信号范围
        public float bvrGrowthFactor = 1.3f;             // 增长系数
        public bool smartCaretakerCheck = true;          // 智能独角仙检查 (默认开启)
        public int signalTowerPower = 1000;              // 信号塔耗电量
        // [NEW] 5级+ 远征能力开关
        public bool allowInfiniteCaravans = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref curveMode, "curveMode", CurveMode.Logarithmic);
            
            // 线性模式参数
            Scribe_Values.Look(ref linearPowerSlope, "linearPowerSlope", 2000f);
            Scribe_Values.Look(ref linearBandwidthSlope, "linearBandwidthSlope", 6);
            Scribe_Values.Look(ref linearControlGroupSlope, "linearControlGroupSlope", 1);
            Scribe_Values.Look(ref linearResearchSlope, "linearResearchSlope", 0.05f);
            Scribe_Values.Look(ref linearHeatSlope, "linearHeatSlope", 10f);
            Scribe_Values.Look(ref linearUpgradeCooldown, "linearUpgradeCooldown", 180000);
            Scribe_Values.Look(ref linearUpgradeDuration, "linearUpgradeDuration", 60000);
            
            // 对数模式参数
            Scribe_Values.Look(ref logPowerBase, "logPowerBase", 1.5f);
            Scribe_Values.Look(ref logBandwidthBase, "logBandwidthBase", 1.5f);
            Scribe_Values.Look(ref logHeatBase, "logHeatBase", 1.3f);
            Scribe_Values.Look(ref logMinBandwidthPerLevel, "logMinBandwidthPerLevel", 1);
            Scribe_Values.Look(ref logMaxUpgradeCooldown, "logMaxUpgradeCooldown", 1800000);
            Scribe_Values.Look(ref logMaxUpgradeDuration, "logMaxUpgradeDuration", 300000);
            Scribe_Values.Look(ref logCooldownIncrement, "logCooldownIncrement", 30000);
            Scribe_Values.Look(ref logDurationIncrement, "logDurationIncrement", 10000);

            // BVR 模式参数
            Scribe_Values.Look(ref enableBVR, "enableBVR", false);
            Scribe_Values.Look(ref bvrBaseRange, "bvrBaseRange", 8f);
            Scribe_Values.Look(ref bvrGrowthFactor, "bvrGrowthFactor", 1.3f);
            Scribe_Values.Look(ref smartCaretakerCheck, "smartCaretakerCheck", true);
            Scribe_Values.Look(ref signalTowerPower, "signalTowerPower", 1000);
            // [NEW] Scribe new setting
            Scribe_Values.Look(ref allowInfiniteCaravans, "allowInfiniteCaravans", true);
            
            base.ExposeData();
        }
        
        // 重置为默认值
        public void ResetToDefaults()
        {
            curveMode = CurveMode.Logarithmic;
            
            // 线性模式默认值
            linearPowerSlope = 2000f;
            linearBandwidthSlope = 6;
            linearControlGroupSlope = 1;
            linearResearchSlope = 0.05f;
            linearHeatSlope = 10f;
            linearUpgradeCooldown = 180000;
            linearUpgradeDuration = 60000;
            
            // 对数模式默认值
            logPowerBase = 1.5f;
            logBandwidthBase = 1.5f;
            logHeatBase = 1.3f;
            logMinBandwidthPerLevel = 1;
            logMaxUpgradeCooldown = 1800000;
            logMaxUpgradeDuration = 300000;
            logCooldownIncrement = 30000;
            logDurationIncrement = 10000;
            
            // BVR 模式默认值
            enableBVR = false;
            bvrBaseRange = 8f;
            bvrGrowthFactor = 1.3f;
            smartCaretakerCheck = true;
            signalTowerPower = 1000;
            allowInfiniteCaravans = true;

            GestaltEngineUnlimitedMod.ApplySettings();

            Log.Message("[格式塔枢纽无限升级] 参数已重置为默认值");
        }
    }
}
