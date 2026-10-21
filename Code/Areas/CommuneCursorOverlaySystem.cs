using Colossal.Mathematics;
using Game;
using Game.Areas;
using Game.Input;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
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
        private EntityQuery m_CommuneQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_SelectionSystem = World.GetOrCreateSystemManaged<CommuneSelectionSystem>();
            m_SelectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_CommuneQuery = GetEntityQuery(
                ComponentType.ReadOnly<Commune>(),
                ComponentType.ReadOnly<Node>());
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

            bool drawCommuneBoundaries = tool != null;
            if (!drawCursor && !drawSelectedOutline && !drawCommuneBoundaries)
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

            if (drawCommuneBoundaries)
            {
                NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
                Color outline = new Color(1f, 0.88f, 0.05f, 0.98f);
                for (int communeIndex = 0; communeIndex < communes.Length; communeIndex++)
                {
                    Entity commune = communes[communeIndex];
                    DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                    float snapDistance = EntityManager.HasComponent<AreaGeometryData>(commune)
                        ? EntityManager.GetComponentData<AreaGeometryData>(commune).m_SnapDistance
                        : 8f;
                    float lineWidth = snapDistance * 0.72f;

                    for (int i = 0; i < nodes.Length; i++)
                    {
                        float3 a = nodes[i].m_Position;
                        float3 b = nodes[(i + 1) % nodes.Length].m_Position;
                        a.y += 0.55f;
                        b.y += 0.55f;
                        Line3.Segment line = new Line3.Segment(a, b);
                        buffer.DrawLine(outline, outline, 0f, default, line, lineWidth, 1f);
                    }
                }
                communes.Dispose();
            }

            if (drawSelectedOutline && EntityManager.HasBuffer<Node>(selected))
            {
                DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(selected, true);
                Color outline = new Color(1f, 0.88f, 0.05f, 0.98f);
                for (int i = 0; i < nodes.Length; i++)
                {
                    float3 a = nodes[i].m_Position;
                    float3 b = nodes[(i + 1) % nodes.Length].m_Position;
                    a.y += 0.8f;
                    b.y += 0.8f;
                    buffer.DrawLine(outline, new Line3.Segment(a, b), 8.32f);
                }
            }

            m_OverlayRenderSystem.AddBufferWriter(default);
        }
    }
}
