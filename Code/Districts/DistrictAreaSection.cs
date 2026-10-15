using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.UI.Binding;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst.Intrinsics;

namespace Communes
{

    public partial class DistrictAreaSection : InfoSectionBase, IJsonWritable
    {
        public const string TypeName = "Communes.DistrictAreaSection";
        private EntityQuery m_BuildingQuery;
        private NativeArray<int> m_Results;
        private float m_Area;
        private int m_Residents;
        private Entity m_CachedDistrict = Entity.Null;
        private int m_CachedTicks;

        protected override string group => "DistrictAreaSection";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_BuildingQuery = GetEntityQuery(ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<CurrentDistrict>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Renter>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_Results = new NativeArray<int>(1, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (m_Results.IsCreated)
                m_Results.Dispose();
            base.OnDestroy();
        }

        protected override void Reset()
        {

}

        protected override void OnUpdate()
        {
            visible = false;
            Entity district = selectedEntity;
            if (!EntityManager.HasComponent<District>(district)
                || !EntityManager.HasComponent<Geometry>(district)
                || !EntityManager.HasBuffer<Node>(district)
                || !EntityManager.HasBuffer<Triangle>(district))
                return;

            visible = true;
            if (district == m_CachedDistrict && m_CachedTicks-- > 0)
                return;

            m_CachedDistrict = district;
            m_CachedTicks = 60;
            DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(district, true);
            DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(district, true);
            m_Area = 0f;
            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle2 triangle = AreaUtils.GetTriangle3(nodes, triangles[i]).xz;
                m_Area += math.abs(Cross(triangle.b - triangle.a, triangle.c - triangle.a)) * 0.5f;
            }

            m_Residents = CountResidents(district);
        }

        private int CountResidents(Entity district)
        {
            m_Results[0] = 0;
            JobChunkExtensions.Schedule(new CountDistrictResidentsJob
            {
                m_SelectedDistrict = district,
                m_EntityHandle = GetEntityTypeHandle(),
                m_CurrentDistrictHandle = GetComponentTypeHandle<CurrentDistrict>(true),
                m_PrefabRefHandle = GetComponentTypeHandle<PrefabRef>(true),
                m_ParkLookup = GetComponentLookup<Game.Buildings.Park>(true),
                m_AbandonedLookup = GetComponentLookup<Abandoned>(true),
                m_PropertyDataLookup = GetComponentLookup<BuildingPropertyData>(true),
                m_HouseholdLookup = GetComponentLookup<Household>(true),
                m_HouseholdCitizenLookup = GetBufferLookup<HouseholdCitizen>(true),
                m_RenterLookup = GetBufferLookup<Renter>(true),
                m_HealthProblemLookup = GetComponentLookup<HealthProblem>(true),
                m_TravelPurposeLookup = GetComponentLookup<TravelPurpose>(true),
                m_Results = m_Results,
            }, m_BuildingQuery, Dependency).Complete();
            return m_Results[0];
        }

        private struct CountDistrictResidentsJob : IJobChunk
        {
            [ReadOnly] public Entity m_SelectedDistrict;
            [ReadOnly] public EntityTypeHandle m_EntityHandle;
            [ReadOnly] public ComponentTypeHandle<CurrentDistrict> m_CurrentDistrictHandle;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefHandle;
            [ReadOnly] public ComponentLookup<Game.Buildings.Park> m_ParkLookup;
            [ReadOnly] public ComponentLookup<Abandoned> m_AbandonedLookup;
            [ReadOnly] public ComponentLookup<BuildingPropertyData> m_PropertyDataLookup;
            [ReadOnly] public ComponentLookup<Household> m_HouseholdLookup;
            [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizenLookup;
            [ReadOnly] public BufferLookup<Renter> m_RenterLookup;
            [ReadOnly] public ComponentLookup<HealthProblem> m_HealthProblemLookup;
            [ReadOnly] public ComponentLookup<TravelPurpose> m_TravelPurposeLookup;
            public NativeArray<int> m_Results;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityHandle);
                NativeArray<CurrentDistrict> districts = chunk.GetNativeArray(ref m_CurrentDistrictHandle);
                NativeArray<PrefabRef> prefabs = chunk.GetNativeArray(ref m_PrefabRefHandle);
                int residents = 0;
                for (int i = 0; i < entities.Length; i++)
                {
                    if (districts[i].m_District != m_SelectedDistrict)
                        continue;

                    Entity building = entities[i];
                    bool abandoned = m_AbandonedLookup.HasComponent(building);
                    bool park = m_ParkLookup.HasComponent(building);
                    bool residential = m_PropertyDataLookup.TryGetComponent(prefabs[i].m_Prefab, out BuildingPropertyData property)
                        && property.m_ResidentialProperties > 0 && !abandoned;
                    if (!(residential || park || abandoned)
                        || !m_RenterLookup.TryGetBuffer(building, out DynamicBuffer<Renter> renters)
                        || renters.Length == 0)
                        continue;

                    for (int r = 0; r < renters.Length; r++)
                    {
                        Entity household = renters[r].m_Renter;
                        if (!m_HouseholdLookup.HasComponent(household)
                            || !m_HouseholdCitizenLookup.TryGetBuffer(household, out DynamicBuffer<HouseholdCitizen> citizens))
                            continue;
                        for (int c = 0; c < citizens.Length; c++)
                        {
                            if (!CitizenUtils.IsCorpsePickedByHearse(citizens[c].m_Citizen,
                                ref m_HealthProblemLookup, ref m_TravelPurposeLookup))
                                residents++;
                        }
                    }
                }
                m_Results[0] += residents;
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask) => Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        protected override void OnProcess() { }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("language"); writer.Write((int)Mod.EffectiveLanguage);
            writer.PropertyName("units"); writer.Write((int)Mod.EffectiveUnits);
            writer.PropertyName("area"); writer.Write(m_Area);
            writer.PropertyName("density"); writer.Write(m_Area > 0f ? (int)(m_Residents * 1000000f / m_Area) : 0);
        }

        public new void Write(IJsonWriter writer)
        {
            if (!visible) { writer.WriteNull(); return; }
            writer.TypeBegin(TypeName);
            writer.PropertyName("group"); writer.Write(group);
            writer.PropertyName("tooltipKeys"); writer.ArrayBegin(0u); writer.ArrayEnd();
            writer.PropertyName("tooltipTags"); writer.ArrayBegin(0u); writer.ArrayEnd();
            OnWriteProperties(writer);
            writer.TypeEnd();
        }
    }
}
