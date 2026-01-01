
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace GestaltEngineUnlimited
{
    public class PlaceWorker_RelayGap : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            CellRect rect = GenAdj.OccupiedRect(loc, rot, checkingDef.Size).ExpandedBy(1);
            
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map)) continue;
                
                List<Thing> thingList = c.GetThingList(map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing t = thingList[i];
                    if (t == thingToIgnore) continue;
                    
                    // 检查已建成的建筑
                    if (t.def == checkingDef)
                    {
                        return new AcceptanceReport("GEU_PlaceWorker_RelayGap".Translate());
                    }
                    
                    // 检查蓝图和框架 (Blueprints and Frames)
                    // 只要 def.entityDefToBuild 指向我们的塔，就说明它是塔的蓝图或框架
                    if (t.def.entityDefToBuild == checkingDef)
                    {
                        return new AcceptanceReport("GEU_PlaceWorker_RelayGap".Translate());
                    }
                }
            }
            
            return true;
        }

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            CellRect rect = GenAdj.OccupiedRect(center, rot, def.Size).ExpandedBy(1);
            GenDraw.DrawFieldEdges(rect.Cells.ToList(), Color.white);
            
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map)) continue;
                
                List<Thing> thingList = c.GetThingList(map);
                bool conflict = false;
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing t = thingList[i];
                    
                    if (t.def == def || t.def.entityDefToBuild == def)
                    {
                        conflict = true;
                        break;
                    }
                }
                
                if (conflict)
                {
                    GenDraw.DrawFieldEdges(new List<IntVec3> { c }, Color.red);
                }
            }
        }
    }
}
