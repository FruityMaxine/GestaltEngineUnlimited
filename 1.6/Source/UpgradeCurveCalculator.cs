using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace GestaltEngineUnlimited
{
    // 升级数据结构（模拟原mod的结构）
    public class UpgradeData
    {
        public GraphicData overlayGraphic;
        public int upgradeCooldownTicks;
        public int downgradeCooldownTicks;
        public int upgradeDurationTicks;
        public int downgradeDurationTicks;
        public float powerConsumption;
        public float heatPerSecond;
        public float researchPointsPerSecond;
        public int totalMechBandwidth;
        public int totalControlGroups;
        public List<string> unlockedRecipes;
        public bool allowCaravans;

        public UpgradeData()
        {
            unlockedRecipes = new List<string>();
        }
    }

    // 升级曲线计算器
    public static class UpgradeCurveCalculator
    {
        // 生成动态升级数据
        public static UpgradeData GenerateUpgradeData(int level, CurveMode mode)
        {
            if (level <= 4)
            {
                // 0-4级使用原始XML配置，不应该调用此函数
                Log.Warning(string.Format("[格式塔枢纽无限升级] 试图为{0}级生成动态数据，但该等级应使用原始配置", level));
                return null;
            }

            UpgradeData data = new UpgradeData();
            int levelDiff = level - 4; // 相对于4级的差值
            var settings = GestaltEngineUnlimitedMod.settings;

            if (mode == CurveMode.Linear)
            {
                // 线性模式 - 使用用户配置的斜率
                data.powerConsumption = 6000f + settings.linearPowerSlope * levelDiff;
                data.heatPerSecond = 50f + settings.linearHeatSlope * levelDiff;
                data.researchPointsPerSecond = 0.25f + settings.linearResearchSlope * levelDiff;
                data.totalMechBandwidth = 24 + settings.linearBandwidthSlope * levelDiff;
                data.totalControlGroups = 4 + settings.linearControlGroupSlope * levelDiff;
                data.upgradeCooldownTicks = settings.linearUpgradeCooldown;
                data.upgradeDurationTicks = settings.linearUpgradeDuration;
            }
            else // Logarithmic
            {
                // 对数模式 - 使用用户配置的底数和参数
                data.powerConsumption = 6000f + 2000f * (float)Math.Log(levelDiff + 1, settings.logPowerBase);
                data.heatPerSecond = 50f + 20f * (float)Math.Log(levelDiff + 1, settings.logHeatBase);
                data.researchPointsPerSecond = 0.25f + 0.05f * (float)Math.Sqrt(levelDiff);
                
                // 修改带宽计算，使用用户配置的底数和最小增长
                int baseBandwidth = 24 + (int)(6f * Math.Log(levelDiff + 2, settings.logBandwidthBase));
                data.totalMechBandwidth = Math.Max(24 + levelDiff * settings.logMinBandwidthPerLevel, baseBandwidth);
                
                data.totalControlGroups = 4 + (int)Math.Ceiling(Math.Sqrt(levelDiff + 1));
                
                // 升级时间使用用户配置的增量和上限
                data.upgradeCooldownTicks = Math.Min(180000 + settings.logCooldownIncrement * levelDiff, settings.logMaxUpgradeCooldown);
                data.upgradeDurationTicks = Math.Min(60000 + settings.logDurationIncrement * levelDiff, settings.logMaxUpgradeDuration);
            }

            // 通用设置
            data.downgradeCooldownTicks = 180000;
            data.downgradeDurationTicks = 60000;
            data.unlockedRecipes.Add("RM_HackBiocodedThings");
            // [FIX] 初始值跟随设置
            data.allowCaravans = GestaltEngineUnlimitedMod.settings.allowInfiniteCaravans;
            
            // 创建虚拟贴图数据（使用4级贴图）
            data.overlayGraphic = new GraphicData
            {
                texPath = "Things/Building/Biotech/Tier4_GestaltEngine",
                graphicClass = typeof(Graphic_Single),
                drawSize = new Vector2(11f, 11f)
            };

            return data;
        }
    }
}
