using Game;
using Game.Areas;
using Game.Prefabs;
using Unity.Entities;

namespace Communes
{

public partial class CommuneAreaGeometrySystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private CommuneToolPrefabSystem m_ToolPrefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolPrefabSystem = World.GetOrCreateSystemManaged<CommuneToolPrefabSystem>();
        }

        protected override void OnUpdate()
        {
            CommuneAreaPrefab prefab = m_ToolPrefabSystem.ToolPrefab;
            if (prefab == null || !m_PrefabSystem.TryGetEntity(prefab, out Entity entity)
                || !EntityManager.HasComponent<AreaGeometryData>(entity))
                return;

            AreaGeometryData geometry = EntityManager.GetComponentData<AreaGeometryData>(entity);
            float districtSnap = AreaUtils.GetMinNodeDistance(AreaType.District);
            if (geometry.m_Type == AreaType.None
                && geometry.m_SnapDistance == districtSnap
                && (geometry.m_Flags & GeometryFlags.ClearArea) == 0)
                return;

            geometry.m_Type = AreaType.None;

geometry.m_Flags &= ~GeometryFlags.ClearArea;
            geometry.m_SnapDistance = districtSnap;
            geometry.m_MaxHeight = 0f;
            geometry.m_LodBias = 0f;
            EntityManager.SetComponentData(entity, geometry);
        }
    }
}
