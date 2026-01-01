using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using GestaltEngine;

namespace GestaltEngineUnlimited
{
    // 自定义 Gizmo，用于显示 BVR 信号中继状态 (12格信号条)
    [StaticConstructorOnStartup]
    public class Gizmo_SignalRelayStatus : Gizmo
    {
        public CompGestaltEngine hub;
        
        private static readonly Texture2D SignalBarFillTex = SolidColorMaterials.NewSolidColorTexture(new Color(0f, 0.8f, 1f)); // 青色填充
        private static readonly Texture2D SignalBarEmptyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.2f)); // 深灰背景
        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("Things/Building/Gestalt/SignalExpansionTower", true); // 使用塔的贴图作为图标

        public Gizmo_SignalRelayStatus()
        {
            // this.order = -99f; // 放在前面
        }

        public override float GetWidth(float maxWidth)
        {
            return 140f; // 比普通 Gizmo 宽一些，以便放下12个格子
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            
            // 绘制背景
            Widgets.DrawWindowBackground(rect);

            // 绘制标题
            Text.Font = GameFont.Tiny;
            Rect labelRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 20f);
            Widgets.Label(labelRect, "GEU_SignalStatus".Translate());

            // 获取当前中继器数量
            int relayCount = GestaltRangeCalculator.GetRelayCount(hub);
            int maxRelays = 12; // 硬编码最大值为12，与XML中的setMaxSimultaneous一致

            // 绘制12个格子
            float barHeight = 20f;
            float barWidth = rect.width - 10f;
            float cellWidth = (barWidth - (maxRelays - 1) * 2f) / maxRelays;
            float startY = rect.y + 30f;

            for (int i = 0; i < maxRelays; i++)
            {
                Rect cellRect = new Rect(rect.x + 5f + i * (cellWidth + 2f), startY, cellWidth, barHeight);
                if (i < relayCount)
                {
                    GUI.DrawTexture(cellRect, SignalBarFillTex);
                }
                else
                {
                    GUI.DrawTexture(cellRect, SignalBarEmptyTex);
                }
            }

            // 绘制数值文本
            Text.Font = GameFont.Small;
            string countStr = relayCount + " / " + maxRelays;
            Vector2 textSize = Text.CalcSize(countStr);
            Rect countRect = new Rect(rect.x + (rect.width - textSize.x) / 2f, startY + barHeight + 2f, textSize.x, 20f);
            Widgets.Label(countRect, countStr);

            // 鼠标悬停提示
            TooltipHandler.TipRegion(rect, () => GetTooltip(), 895421);

            return new GizmoResult(GizmoState.Clear);
        }

        protected string GetTooltip()
        {
                string rangeText = "";
                float maxRange = GestaltRangeCalculator.GetMaxRange(hub);
                
                if (maxRange >= 200000 || maxRange < 0)
                {
                   rangeText = "GEU_Unlimited".Translate();
                }
                else
                {
                   rangeText = maxRange.ToString("F0");
                }

                return "GEU_SignalStatusDesc".Translate() + "\n\n" + 
                       "GEU_SignalStatusDetail".Translate(
                           GestaltRangeCalculator.GetRelayCount(hub), 
                           rangeText
                       );
        }
    }
}
