using System;
using Verse;
using GestaltEngine;

namespace GestaltEngineUnlimited
{
    // [NEW] 存档保护组件
    // 用途：单独存储真实的超高等级数据，而让原版CompUpgradeable只存储安全等级（4级）
    // 这样当Mod移除时，原版游戏读取到的只是Level 4，不会因为数组越界而崩溃
    public class CompSaveProtection : ThingComp
    {
        public int realLevel = 0; // 真实的等级（比如 10, 20, 100...）
        
        // 当组件生成时（包括加载存档后），尝试恢复真实等级到原版组件
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            // 确保在加载完成后同步数据
            if (respawningAfterLoad)
            {
                RestoreLevelToOrigin();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            // 存储真实的等级
            Scribe_Values.Look(ref realLevel, "GEU_RealLevel", 0);
        }

        // 将真实等级同步回原版组件 (Loading -> Memory)
        public void RestoreLevelToOrigin()
        {
            CompUpgradeable comp = parent.GetComp<CompUpgradeable>();
            if (comp != null)
            {
                // 如果存储的真实等级 > 当前组件等级（通常是加载出来的安全等级4）
                // 则恢复它
                if (realLevel > comp.level)
                {
                    comp.level = realLevel;
                    // 可能需要触发一些刷新逻辑，但Level属性本身只是个int
                    // 具体的属性通常是动态获取的(CurrentUpgrade)，所以改level没问题
                    
                     // 再次调用 SetLevel 以确保刷新状态（如冷却时间重置等副作用，视情况而定）
                     // 但 SetLevel 会触发消息和效果，可能不适合在加载时调用。
                     // 仅修改 level 值通常足够，因为 CurrentUpgrade 是个 Property。
                }
                else if (comp.level > realLevel)
                {
                    // 反向同步：如果是在游戏中升级了，更新 realLevel
                    realLevel = comp.level;
                }
            }
        }

        // 从原版组件同步真实等级 (Memory -> Safe Storage)
        // 在保存前调用
        public void SyncFromOrigin()
        {
            CompUpgradeable comp = parent.GetComp<CompUpgradeable>();
            if (comp != null)
            {
                realLevel = comp.level;
            }
        }
    }
    
    // 对应的 CompProperties，需要在代码动态注入
    public class CompProperties_SaveProtection : CompProperties
    {
        public CompProperties_SaveProtection()
        {
            compClass = typeof(CompSaveProtection);
        }
    }
}
