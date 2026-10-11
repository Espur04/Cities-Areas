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

namespace Communes
{

    public partial class DistrictAreaSection : InfoSectionBase, IJsonWritable
    {
        public const string TypeName = "Communes.DistrictAreaSection";
        private EntityQuery m_BuildingQuery;
        private float m_Area;
        private int m_Residents;
        private Entity m_CachedDistrict = Entity.Null;
        private int m_CachedTicks;

        protected override string group => "DistrictAreaSection";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_BuildingQuery = GetEntityQuery(ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<CurrentDistrict>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
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
            NativeArray<Entity> buildings = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            int total = 0;
            try
            {
                for (int i = 0; i < buildings.Length; i++)
                {
                    Entity building = buildings[i];
                    if (EntityManager.GetComponentData<CurrentDistrict>(building).m_District != district)
                        continue;

                    bool abandoned = EntityManager.HasComponent<Abandoned>(building);
                    bool park = EntityManager.HasComponent<Game.Buildings.Park>(building);
                    PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(building);
                    bool residential = EntityManager.TryGetComponent(prefabRef.m_Prefab, out BuildingPropertyData property)
                        && property.m_ResidentialProperties > 0 && !abandoned;
                    if (!EntityManager.TryGetBuffer(building, true, out DynamicBuffer<Renter> renters)
                        || renters.Length == 0 || !(residential || park || abandoned))
                        continue;

                    for (int r = 0; r < renters.Length; r++)
                    {
                        Entity household = renters[r].m_Renter;
                        if (!EntityManager.HasComponent<Household>(household)
                            || !EntityManager.TryGetBuffer(household, true, out DynamicBuffer<HouseholdCitizen> citizens))
                            continue;
                        for (int c = 0; c < citizens.Length; c++)
                        {
                            Entity citizen = citizens[c].m_Citizen;
                            if (EntityManager.HasComponent<Citizen>(citizen)
                                && !CitizenUtils.IsCorpsePickedByHearse(EntityManager, citizen))
                                total++;
                        }
                    }
                }
            }
            finally { buildings.Dispose(); }
            return total;
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
