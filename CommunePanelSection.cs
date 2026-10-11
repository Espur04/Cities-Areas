using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.UI.Binding;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Communes
{

    public partial class CommunePanelSection : InfoSectionBase, IJsonWritable
    {
        public const string TypeName = "Communes.CommunePanelSection";
        private const float kMinDistrictArea = 5000f;
        private EntityQuery m_DistrictQuery;
        private float m_Area;
        private float m_UnassignedArea;
        private int m_UnassignedResidents;
        private int m_UnassignedDensity;
        private int m_CachedTicks;
        private Entity m_CachedCommune = Entity.Null;
        private uint m_CachedGeometryHash;
        private List<Row> m_Districts = new List<Row>();

        protected override string group => "CommunePanelSection";

        private struct Row
        {
            public Entity Entity;
            public float Area;
            public int Residents;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_DistrictQuery = GetEntityQuery(ComponentType.ReadOnly<District>(), ComponentType.ReadOnly<Geometry>(),
                ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(), ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());
        }

        protected override void Reset()
        {

}

        protected override void OnUpdate()
        {
            visible = false;
            Entity commune = selectedEntity;
            if (!EntityManager.HasComponent<Commune>(commune)
                || !EntityManager.HasComponent<Geometry>(commune)
                || !EntityManager.HasBuffer<Node>(commune)
                || !EntityManager.HasBuffer<Triangle>(commune))
            {
                return;
            }

            visible = true;
            uint hash = GeometryHash(commune);
            if (commune != m_CachedCommune || hash != m_CachedGeometryHash || m_CachedTicks-- <= 0)
            {
                m_CachedCommune = commune;
                m_CachedGeometryHash = hash;
                m_CachedTicks = 60;
                Rebuild(commune);
            }
        }

        private uint GeometryHash(Entity area)
        {
            DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(area, true);
            DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(area, true);
            uint hash = math.hash(new uint4((uint)nodes.Length, (uint)triangles.Length, 0, 0));
            for (int i = 0; i < nodes.Length; i++) hash = math.hash(new uint4(hash, math.hash(nodes[i].m_Position), (uint)i, 0));
            return hash;
        }

        private void Rebuild(Entity commune)
        {
            m_Districts.Clear();
            Geometry communeGeometry = EntityManager.GetComponentData<Geometry>(commune);
            DynamicBuffer<Node> communeNodes = EntityManager.GetBuffer<Node>(commune, true);
            DynamicBuffer<Triangle> communeTriangles = EntityManager.GetBuffer<Triangle>(commune, true);
            m_Area = SurfaceArea(communeNodes, communeTriangles);

            NativeArray<Entity> districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            Dictionary<Entity, int> residentsByDistrict = new Dictionary<Entity, int>();
            HashSet<Entity> districtEntities = new HashSet<Entity>();
            List<Entity> candidateDistricts = new List<Entity>();
            for (int i = 0; i < districts.Length; i++)
            {
                Entity district = districts[i];
                districtEntities.Add(district);
                Geometry districtGeometry = EntityManager.GetComponentData<Geometry>(district);
                if (BoundsOverlap(communeGeometry.m_Bounds, districtGeometry.m_Bounds))
                    candidateDistricts.Add(district);
            }
            int totalResidents = m_StatsPopulation();
            int residentsCountedFromMembers = 0;
            if (EntityManager.HasBuffer<CommuneBuildingMember>(commune))
            {
                DynamicBuffer<CommuneBuildingMember> members = EntityManager.GetBuffer<CommuneBuildingMember>(commune, true);
                for (int i = 0; i < members.Length; i++)
                {
                    Entity building = members[i].m_Building;
                    if (!EntityManager.Exists(building) || EntityManager.HasComponent<Deleted>(building)
                        || !EntityManager.HasComponent<Building>(building)
                        || !TryGetNativeRenters(building, out DynamicBuffer<Renter> renters)) continue;
                    Entity district = FindDistrictForBuilding(building, districtEntities, candidateDistricts);
                    for (int r = 0; r < renters.Length; r++)
                    {
                        Entity household = renters[r].m_Renter;
                        if (!EntityManager.HasComponent<Household>(household)
                            || !EntityManager.TryGetBuffer(household, true, out DynamicBuffer<HouseholdCitizen> citizens)) continue;
                        for (int c = 0; c < citizens.Length; c++)
                        {
                            Entity citizenEntity = citizens[c].m_Citizen;
                            if (!EntityManager.TryGetComponent(citizenEntity, out Citizen citizen)
                                || CitizenUtils.IsCorpsePickedByHearse(EntityManager, citizenEntity)) continue;
                            residentsCountedFromMembers++;
                            if (district != Entity.Null)
                            {
                                residentsByDistrict.TryGetValue(district, out int current);
                                residentsByDistrict[district] = current + 1;
                            }
                        }
                    }
                }
            }

            try
            {
                float coveredArea = 0f;
                int residentsInDisplayedDistricts = 0;
                if (totalResidents <= 0)
                    totalResidents = residentsCountedFromMembers;
                for (int i = 0; i < districts.Length; i++)
                {
                    Entity district = districts[i];
                    Geometry districtGeometry = EntityManager.GetComponentData<Geometry>(district);
                    if (!BoundsOverlap(communeGeometry.m_Bounds, districtGeometry.m_Bounds)) continue;
                    DynamicBuffer<Node> districtNodes = EntityManager.GetBuffer<Node>(district, true);
                    DynamicBuffer<Triangle> districtTriangles = EntityManager.GetBuffer<Triangle>(district, true);
                    float fragmentArea = IntersectionArea(communeNodes, communeTriangles, districtNodes, districtTriangles);
                    residentsByDistrict.TryGetValue(district, out int residents);
                    if (fragmentArea < kMinDistrictArea) continue;
                    coveredArea += fragmentArea;
                    residentsInDisplayedDistricts += residents;
                    m_Districts.Add(new Row { Entity = district, Area = fragmentArea, Residents = residents });
                }
                m_UnassignedArea = math.max(0f, m_Area - coveredArea);
                m_UnassignedResidents = math.max(0, totalResidents - residentsInDisplayedDistricts);

                m_UnassignedDensity = m_UnassignedArea > 0f ? (int)(m_UnassignedResidents * 1000000f / m_UnassignedArea) : 0;
            }
            finally { districts.Dispose(); }

        }

        private Entity FindDistrictForBuilding(Entity building, HashSet<Entity> districts, List<Entity> candidateDistricts)
        {

if (EntityManager.TryGetComponent(building, out CurrentDistrict currentDistrict)
                && districts.Contains(currentDistrict.m_District)
                && ContainsBuilding(currentDistrict.m_District, building))
                return currentDistrict.m_District;

            if (!EntityManager.TryGetComponent(building, out Transform transform))
                return Entity.Null;

            for (int i = 0; i < candidateDistricts.Count; i++)
            {
                Entity district = candidateDistricts[i];
                if (ContainsBuilding(district, transform.m_Position))
                    return district;
            }
            return Entity.Null;
        }

        private bool ContainsBuilding(Entity district, Entity building)
        {
            return EntityManager.TryGetComponent(building, out Transform transform)
                && ContainsBuilding(district, transform.m_Position);
        }

        private bool ContainsBuilding(Entity district, float3 position)
        {
            Geometry geometry = EntityManager.GetComponentData<Geometry>(district);
            DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(district, true);
            DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(district, true);
            return CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, position);
        }

        private bool TryGetNativeRenters(Entity building, out DynamicBuffer<Renter> renters)
        {
            bool abandoned = EntityManager.HasComponent<Abandoned>(building);
            bool park = EntityManager.HasComponent<Game.Buildings.Park>(building);
            bool eligiblePrefab = EntityManager.TryGetComponent(building, out PrefabRef prefabRef)
                && EntityManager.TryGetComponent(prefabRef.m_Prefab, out BuildingPropertyData propertyData)
                && propertyData.m_ResidentialProperties > 0;
            bool hasRenters = EntityManager.TryGetBuffer(building, true, out renters) && renters.Length > 0;
            return hasRenters && ((eligiblePrefab && !abandoned) || park || abandoned);
        }

        private static bool BoundsOverlap(Bounds3 a, Bounds3 b) => a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.z <= b.max.z && a.max.z >= b.min.z;

        private static float SurfaceArea(DynamicBuffer<Node> nodes, DynamicBuffer<Triangle> triangles)
        {
            float area = 0f;
            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle2 triangle = AreaUtils.GetTriangle3(nodes, triangles[i]).xz;
                area += math.abs(Cross(triangle.b - triangle.a, triangle.c - triangle.a)) * 0.5f;
            }
            return area;
        }

private static float IntersectionArea(DynamicBuffer<Node> aNodes, DynamicBuffer<Triangle> aTriangles,
            DynamicBuffer<Node> bNodes, DynamicBuffer<Triangle> bTriangles)
        {
            float total = 0f;
            float2[] first = new float2[8];
            float2[] second = new float2[8];
            for (int i = 0; i < aTriangles.Length; i++)
            {
                Triangle2 a = AreaUtils.GetTriangle3(aNodes, aTriangles[i]).xz;
                for (int j = 0; j < bTriangles.Length; j++)
                {
                    Triangle2 b = AreaUtils.GetTriangle3(bNodes, bTriangles[j]).xz;
                    first[0] = a.a; first[1] = a.b; first[2] = a.c;
                    float2[] polygon = first;
                    float2[] output = second;
                    int count = 3;
                    float orientation = Cross(b.b - b.a, b.c - b.a) >= 0f ? 1f : -1f;
                    count = Clip(polygon, count, output, b.a, b.b, orientation); Swap(ref polygon, ref output);
                    count = Clip(polygon, count, output, b.b, b.c, orientation); Swap(ref polygon, ref output);
                    count = Clip(polygon, count, output, b.c, b.a, orientation); Swap(ref polygon, ref output);
                    total += PolygonArea(polygon, count);
                }
            }
            return total;
        }

        private static int Clip(float2[] input, int count, float2[] output, float2 edgeA, float2 edgeB, float orientation)
        {
            if (count == 0) return 0;
            int outputCount = 0;
            float2 previous = input[count - 1];
            float previousSide = orientation * Cross(edgeB - edgeA, previous - edgeA);
            for (int i = 0; i < count; i++)
            {
                float2 current = input[i];
                float currentSide = orientation * Cross(edgeB - edgeA, current - edgeA);
                bool previousInside = previousSide >= -0.001f;
                bool currentInside = currentSide >= -0.001f;
                if (previousInside != currentInside)
                {
                    float t = previousSide / (previousSide - currentSide);
                    if (outputCount < output.Length) output[outputCount++] = math.lerp(previous, current, t);
                }
                if (currentInside && outputCount < output.Length) output[outputCount++] = current;
                previous = current;
                previousSide = currentSide;
            }
            return outputCount;
        }

        private static void Swap(ref float2[] a, ref float2[] b) { float2[] temp = a; a = b; b = temp; }
        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
        private static float PolygonArea(float2[] p, int count)
        {
            if (count < 3) return 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += Cross(p[i], p[(i + 1) % count]);
            return math.abs(sum) * 0.5f;
        }

        protected override void OnProcess() { }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("language"); writer.Write((int)Mod.EffectiveLanguage);
            writer.PropertyName("units"); writer.Write((int)Mod.EffectiveUnits);
            writer.PropertyName("area"); writer.Write(m_Area);
            writer.PropertyName("residents"); writer.Write(m_StatsPopulation());
            writer.PropertyName("density"); writer.Write(m_Area > 0f ? (int)(m_StatsPopulation() * 1000000f / m_Area) : 0);
            writer.PropertyName("unassignedArea"); writer.Write(m_UnassignedArea);
            writer.PropertyName("unassignedResidents"); writer.Write(m_UnassignedResidents);
            writer.PropertyName("unassignedDensity"); writer.Write(m_UnassignedDensity);
            writer.PropertyName("districts"); WriteRows(writer, m_Districts);
        }

        private int m_StatsPopulation()
        {
            var statsSystem = World.GetOrCreateSystemManaged<CommuneStatsSystem>();
            return statsSystem.Results.TryGetValue(selectedEntity, out CommuneStats stats) ? stats.ResidentCount : 0;
        }

        private void WriteRows(IJsonWriter writer, List<Row> rows)
        {
            writer.ArrayBegin((uint)rows.Count);
            foreach (Row row in rows)
            {
                writer.TypeBegin("selectedInfo.District");
                writer.PropertyName("entity"); writer.Write(row.Entity);
                writer.PropertyName("name"); m_NameSystem.BindName(writer, row.Entity);
                writer.PropertyName("area"); writer.Write(row.Area);
                writer.PropertyName("residents"); writer.Write(row.Residents);
                writer.PropertyName("density"); writer.Write(row.Area > 0f ? (int)(row.Residents * 1000000f / row.Area) : 0);
                writer.TypeEnd();
            }
            writer.ArrayEnd();
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
