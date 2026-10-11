using Colossal.Mathematics;
using Game;
using Game.Areas;
using Game.Input;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Game.UI.InGame;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Communes
{

    public partial class CommuneCursorOverlaySystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private PrefabSystem m_PrefabSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private CommuneSelectionSystem m_SelectionSystem;
        private SelectedInfoUISystem m_SelectedInfo;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_SelectionSystem = World.GetOrCreateSystemManaged<CommuneSelectionSystem>();
            m_SelectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
        }

        protected override void OnUpdate()
        {
            CommuneAreaToolSystem tool = m_ToolSystem.activeTool as CommuneAreaToolSystem;
            CommuneAreaPrefab prefab = m_ToolSystem.activePrefab as CommuneAreaPrefab;
            float3 position = default;
            bool drawCursor = tool != null && prefab != null
                && InputManager.instance.controlOverWorld
                && tool.TryGetCursorPosition(out position);

            Entity selected = m_SelectionSystem?.SelectedCommune ?? Entity.Null;
            bool drawSelectedOutline = selected != Entity.Null
                && EntityManager.Exists(selected)
                && m_SelectedInfo != null
                && m_SelectedInfo.selectedEntity == selected;

            if (!drawCursor && !drawSelectedOutline)
                return;

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            if (drawCursor)
            {
                Entity prefabEntity = m_PrefabSystem.GetEntity(prefab);
                if (EntityManager.HasComponent<AreaGeometryData>(prefabEntity))
                {
                    AreaGeometryData geometry = EntityManager.GetComponentData<AreaGeometryData>(prefabEntity);

                    buffer.DrawCircle(Color.white, position, geometry.m_SnapDistance * 0.3f);
                }
            }

            if (drawSelectedOutline && EntityManager.HasBuffer<Node>(selected))
            {
                DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(selected, true);
                Color outline = new Color(1f, 0.035f, 0.055f, 0.98f);
                for (int i = 0; i < nodes.Length; i++)
                {
                    float3 a = nodes[i].m_Position;
                    float3 b = nodes[(i + 1) % nodes.Length].m_Position;
                    a.y += 0.35f;
                    b.y += 0.35f;
                    buffer.DrawLine(outline, new Line3.Segment(a, b), 1.6f);
                }
            }

            m_OverlayRenderSystem.AddBufferWriter(default);
        }
    }
}
