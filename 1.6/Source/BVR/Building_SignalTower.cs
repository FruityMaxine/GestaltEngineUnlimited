using System;
using System.Text;
using Verse;
using RimWorld;

namespace GestaltEngineUnlimited
{
    public class Building_SignalTower : Building
    {
        public override string GetInspectString()
        {
            string str = base.GetInspectString();
            if (string.IsNullOrEmpty(str)) return "";
            
            // 修复 InspectPaneFiller 报错 "Inspect string ... contains empty lines"
            // 这个问题通常是由于 \r\n 换行符处理不一致或多个Comp叠加导致的空行
            
            // 1. 统一换行符为 \n
            str = str.Replace("\r\n", "\n").Replace("\r", "\n");
            
            // 2. 分割并重建，彻底移除空行
            string[] lines = str.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            StringBuilder sb = new StringBuilder();
            bool first = true;
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    if (!first)
                    {
                        sb.Append("\n");
                    }
                    sb.Append(trimmed);
                    first = false;
                }
            }
            
            return sb.ToString();
        }
    }
}
