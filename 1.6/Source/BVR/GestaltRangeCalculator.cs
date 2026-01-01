
using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using GestaltEngine;

namespace GestaltEngineUnlimited
{
    public static class GestaltRangeCalculator
    {
        public static float GetMaxRange(CompGestaltEngine hub)
        {
            int relayCount = GetRelayCount(hub);
            
            if (relayCount == 0) return 0f;
            
            if (relayCount >= 12) return float.PositiveInfinity;
            
            float baseRange = GestaltEngineUnlimitedMod.settings.bvrBaseRange;
            float growthFactor = GestaltEngineUnlimitedMod.settings.bvrGrowthFactor;
            
            return baseRange * (float)Math.Pow(growthFactor, relayCount - 1);
        }

        public static int GetRelayCount(CompGestaltEngine hub)
        {
            if (hub == null || hub.parent == null) return 0;
            
            CompFacility facilityComp = hub.parent.GetComp<CompFacility>();
            
            CompAffectedByFacilities affectedComp = hub.parent.GetComp<CompAffectedByFacilities>();
            if (affectedComp == null) 
            {
                return 0; 
            }
            
            int count = 0;
            List<Thing> facilities = affectedComp.LinkedFacilitiesListForReading;
            if (facilities != null)
            {
                for (int i = 0; i < facilities.Count; i++)
                {
                    if (facilities[i].def.defName == "GEU_SignalExpansionTower")
                    {
                        // 升级：仅计算已通电的中继塔
                        ThingWithComps tower = facilities[i] as ThingWithComps;
                        if (tower != null)
                        {
                            CompPowerTrader power = tower.GetComp<CompPowerTrader>();
                            if (power == null || power.PowerOn)
                            {
                                count++;
                            }
                        }
                    }
                }
            }
            return count;
        }
        
        public static bool IsInRange(CompGestaltEngine hub, Pawn mech)
        {
            // 修正：使用 MapHeld 以支持位于容器（如穿梭机、休眠舱）内的机械体
            // MapHeld 会递归查找持有者，直到找到所在的地图
            Map mechMap = mech.MapHeld;
            if (mechMap != null && hub.parent.Map == mechMap) return true; 
            
            float maxRange = GetMaxRange(hub);
            if (maxRange <= 0) return false; 
            
            if (float.IsPositiveInfinity(maxRange)) return true;
            
            int tileA = hub.parent.Map.Tile;
            int tileB = -1;
            
            // 优先使用 MapHeld.Tile
            if (mechMap != null)
            {
                tileB = mechMap.Tile;
            }
            else
            {
                // 如果没有 MapHeld（例如在飞行中的穿梭机或远行队），尝试获取世界 Tile
                // Pawn.Tile 属性会自动处理远行队 (Caravan) 和部分世界对象的情况
                int pTile = mech.Tile;
                if (pTile >= 0)
                {
                    tileB = pTile;
                }
            }
            
            if (tileB == -1) 
            {
                // 如果无法确定位置（例如处于空投舱发射瞬间、穿梭机降落过程中的临时状态），
                // 默认维持连接，防止因瞬间状态转换导致误断连。
                // 仅在确诊位置且超出范围时才断开。
                return true; 
            }
            
            int dist = Find.WorldGrid.TraversalDistanceBetween(tileA, tileB);
            
            if (dist == int.MaxValue) return false;
            
            return dist <= maxRange;
        }
    }
}
