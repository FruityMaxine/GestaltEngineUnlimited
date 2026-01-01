using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace GestaltEngineUnlimited
{
    // Harmony补丁 - 动态扩展Props.upgrades列表
    // 策略：在每次访问前检查并确保upgrades列表足够长
    
    [HarmonyPatch]
    public static class Patch_CompUpgradeable_Props
    {
        private static MethodBase targetMethod;

        static bool Prepare()
        {
            Type compType = AccessTools.TypeByName("GestaltEngine.CompUpgradeable");
            if (compType == null)
            {
                Log.Warning("[格式塔枢纽无限升级] 未找到GestaltEngine.CompUpgradeable类");
                return false;
            }

            // 查找Props属性的getter
            targetMethod = FindTargetMethod(compType);
            
            if (targetMethod == null)
            {
                Log.Warning("[格式塔枢纽无限升级] 无法找到Props属性");
                return false;
            }

            Log.Message("[格式塔枢纽无限升级] 成功找到Props方法");
            return true;
        }

        static MethodBase TargetMethod()
        {
            return targetMethod;
        }


        static MethodBase FindTargetMethod(Type compType)
        {
            PropertyInfo propsProp = AccessTools.Property(compType, "Props");
            if (propsProp != null)
            {
                return propsProp.GetGetMethod(true); // true = 包括私有方法
            }
            return null;
        }

        // 缓存上次的设置hash，用于检测设置变化
        private static int lastSettingsHash = 0;
        
        // 生成设置的简单hash（用于检测变化）
        private static int GetSettingsHash()
        {
            var s = GestaltEngineUnlimitedMod.settings;
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + s.curveMode.GetHashCode();
                hash = hash * 31 + s.linearPowerSlope.GetHashCode();
                hash = hash * 31 + s.linearBandwidthSlope.GetHashCode();
                hash = hash * 31 + s.linearControlGroupSlope.GetHashCode();
                hash = hash * 31 + s.linearResearchSlope.GetHashCode();
                hash = hash * 31 + s.linearHeatSlope.GetHashCode();
                hash = hash * 31 + s.linearUpgradeCooldown.GetHashCode();
                hash = hash * 31 + s.linearUpgradeDuration.GetHashCode();
                hash = hash * 31 + s.logPowerBase.GetHashCode();
                hash = hash * 31 + s.logBandwidthBase.GetHashCode();
                hash = hash * 31 + s.logHeatBase.GetHashCode();
                hash = hash * 31 + s.logMinBandwidthPerLevel.GetHashCode();
                hash = hash * 31 + s.logMaxUpgradeCooldown.GetHashCode();
                hash = hash * 31 + s.logMaxUpgradeDuration.GetHashCode();
                hash = hash * 31 + s.logCooldownIncrement.GetHashCode();
                hash = hash * 31 + s.logDurationIncrement.GetHashCode();
                return hash;
            }
        }

        // 在返回Props前，确保upgrades列表足够�?
        static void Postfix(object __instance, ref object __result)
        {
            try
            {
                if (__result == null) return;
                
                // 获取当前level
                FieldInfo levelField = AccessTools.Field(__instance.GetType(), "level");
                if (levelField == null) return;
                
                int currentLevel = (int)levelField.GetValue(__instance);
                
                // 获取upgrades列表
                FieldInfo upgradesField = AccessTools.Field(__result.GetType(), "upgrades");
                if (upgradesField == null) return;
                
                System.Collections.IList upgradesList = upgradesField.GetValue(__result) as System.Collections.IList;
                if (upgradesList == null) return;
                
                // 检测设置是否变化
                int currentHash = GetSettingsHash();
                if (currentHash != lastSettingsHash)
                {
                    // 设置已变化，清除5级以上的缓存数据
                    Log.Message("[格式塔枢纽无限升级] 检测到设置变化，清除缓存的升级数据");
                    
                    // 移除所有5级及以上的数据（索引5开始）
                    while (upgradesList.Count > 5)
                    {
                        upgradesList.RemoveAt(upgradesList.Count - 1);
                    }
                    
                    lastSettingsHash = currentHash;
                }
                
                // [FIX-V3] 使用明确的 Mod 设置控制远征能力
                // 此时无需再尝试与 Level 4 同步，而是听从用户在 GEU 设置中的设定
                bool allowCaravans = GestaltEngineUnlimitedMod.settings.allowInfiniteCaravans;
                
                // 确保列表至少有level+2个元素，且至少有10个元素（0-9级）
                int requiredCount = Math.Max(currentLevel + 2, 10);
                while (upgradesList.Count < requiredCount)
                {
                    int newLevel = upgradesList.Count;
                    object newUpgrade = GenerateExtendedUpgrade(newLevel);
                    if (newUpgrade != null)
                    {
                        upgradesList.Add(newUpgrade);
                    }
                    else
                    {
                        break;
                    }
                }

                // 强制同步所有 5级+ 的升级数据到设置状态
                if (upgradesList.Count > 5)
                {
                    FieldInfo allowCaravansField = AccessTools.Field(AccessTools.TypeByName("GestaltEngine.Upgrade"), "allowCaravans");
                    if (allowCaravansField != null)
                    {
                        for (int i = 5; i < upgradesList.Count; i++)
                        {
                            object upgrade = upgradesList[i];
                            // 读取当前值，如果不同则修改（减少不必要的写入）
                            bool currentVal = (bool)allowCaravansField.GetValue(upgrade);
                            if (currentVal != allowCaravans)
                            {
                                allowCaravansField.SetValue(upgrade, allowCaravans);
                            }
                        }
                    }
                }

                // 尝试获取 4级升级数据用于后续的修复逻辑
                object level4Upgrade = null;
                if (upgradesList.Count >= 5)
                {
                    level4Upgrade = upgradesList[4];
                }
                    if (level4Upgrade != null)
                    {
                        Type upgradeType = level4Upgrade.GetType();
                        
                        // 检查并修补upgradeCooldownTicks
                        FieldInfo upgradeCooldownField = AccessTools.Field(upgradeType, "upgradeCooldownTicks");
                        if (upgradeCooldownField != null)
                        {
                            int currentValue = (int)upgradeCooldownField.GetValue(level4Upgrade);
                            if (currentValue == 0)
                            {
                                upgradeCooldownField.SetValue(level4Upgrade, 180000);
                            }
                        }
                        
                        // 检查并修补upgradeDurationTicks
                        FieldInfo upgradeDurationField = AccessTools.Field(upgradeType, "upgradeDurationTicks");
                        if (upgradeDurationField != null)
                        {
                            int currentValue = (int)upgradeDurationField.GetValue(level4Upgrade);
                            if (currentValue == 0)
                            {
                                upgradeDurationField.SetValue(level4Upgrade, 60000);
                            }
                        }
                    }
                }
            catch (Exception ex)
            {
                Log.Error(string.Format("[格式塔枢纽无限升级] Props Postfix错误: {0}", ex.Message));
            }
        }

        static object GenerateExtendedUpgrade(int level)
        {
            UpgradeData data = UpgradeCurveCalculator.GenerateUpgradeData(level, GestaltEngineUnlimitedMod.settings.curveMode);
            if (data == null) return null;

            Type upgradeType = AccessTools.TypeByName("GestaltEngine.Upgrade");
            if (upgradeType == null) return null;

            object upgrade = Activator.CreateInstance(upgradeType);

            SetFieldIfExists(upgradeType, upgrade, "overlayGraphic", data.overlayGraphic);
            SetFieldIfExists(upgradeType, upgrade, "upgradeCooldownTicks", data.upgradeCooldownTicks);
            SetFieldIfExists(upgradeType, upgrade, "downgradeCooldownTicks", data.downgradeCooldownTicks);
            SetFieldIfExists(upgradeType, upgrade, "upgradeDurationTicks", data.upgradeDurationTicks);
            SetFieldIfExists(upgradeType, upgrade, "downgradeDurationTicks", data.downgradeDurationTicks);
            SetFieldIfExists(upgradeType, upgrade, "powerConsumption", data.powerConsumption);
            SetFieldIfExists(upgradeType, upgrade, "heatPerSecond", data.heatPerSecond);
            SetFieldIfExists(upgradeType, upgrade, "researchPointsPerSecond", data.researchPointsPerSecond);
            SetFieldIfExists(upgradeType, upgrade, "totalMechBandwidth", data.totalMechBandwidth);
            SetFieldIfExists(upgradeType, upgrade, "totalControlGroups", data.totalControlGroups);
            SetFieldIfExists(upgradeType, upgrade, "unlockedRecipes", data.unlockedRecipes);
            SetFieldIfExists(upgradeType, upgrade, "allowCaravans", data.allowCaravans);

            // 验证关键字段是否成功设置
            FieldInfo durationField = AccessTools.Field(upgradeType, "upgradeDurationTicks");
            if (durationField != null)
            {
                int actualValue = (int)durationField.GetValue(upgrade);
                if (actualValue <= 0)
                {
                    Log.Error(string.Format("[格式塔枢纽无限升级] 等级{0}的upgradeDurationTicks设置失败，值为{1}。预期值：{2}", 
                        level, actualValue, data.upgradeDurationTicks));
                    return null;
                }
                else
                {
                    Log.Message(string.Format("[格式塔枢纽无限升级] 等级{0}的upgradeDurationTicks设置成功：{1}", level, actualValue));
                }
            }

            return upgrade;
        }

        static void SetFieldIfExists(Type type, object instance, string fieldName, object value)
        {
            // 跳过unlockedRecipes字段，因为类型不匹配（需要List<RecipeDef>而不是List<string>�?
            if (fieldName == "unlockedRecipes")
                return;
                
            FieldInfo field = AccessTools.Field(type, fieldName);
            if (field != null)
            {
                field.SetValue(instance, value);
            }
        }
    }
}

