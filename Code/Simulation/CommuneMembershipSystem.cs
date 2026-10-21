using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Companies;
using Game.Common;
using Game.Objects;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Communes
{

public partial class CommuneMembershipSystem : GameSystemBase
    {
        private const int kInitialBuildingsPerUpdate = 512;
        private EntityQuery m_CommuneQuery;
        private EntityQuery m_BuildingQuery;
        private EntityQuery m_ChangedBuildingQuery;
        private EntityQuery m_ChangedHouseholdQuery;
        private NativeParallelHashMap<Entity, uint> m_CommuneGeometryHashes;
        private NativeParallelHashMap<Entity, float3> m_BuildingPositions;
        private NativeArray<Entity> m_InitialCommunes;
        private NativeArray<Entity> m_InitialBuildings;
        private int m_InitialBuildingIndex;
        private bool m_InitialSyncComplete;
        private CommuneStatsSystem m_StatsSystem;
        private int m_UpdateTick;
        private int m_CleanupTick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CommuneQuery = GetEntityQuery(
                ComponentType.ReadOnly<Commune>(), ComponentType.ReadOnly<Geometry>(),
                ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_BuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Transform>() },
                Any = new[] { ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<Renter>(),
                    ComponentType.ReadOnly<WorkProvider>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            m_ChangedBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Building>(),
                    ComponentType.ReadOnly<Transform>() },
                Any = new[] { ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<Renter>(),
                    ComponentType.ReadOnly<WorkProvider>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            m_ChangedHouseholdQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Household>(), ComponentType.ReadOnly<HouseholdCitizen>(),
                    ComponentType.ReadOnly<Game.Buildings.PropertyRenter>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            m_ChangedHouseholdQuery.SetChangedVersionFilter(ComponentType.ReadOnly<HouseholdCitizen>());
            m_CommuneGeometryHashes = new NativeParallelHashMap<Entity, uint>(16, Allocator.Persistent);
            m_BuildingPositions = new NativeParallelHashMap<Entity, float3>(256, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (m_CommuneGeometryHashes.IsCreated)
                m_CommuneGeometryHashes.Dispose();
            if (m_BuildingPositions.IsCreated)
                m_BuildingPositions.Dispose();
            DisposeInitialSyncArrays();
            base.OnDestroy();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

m_CommuneGeometryHashes.Clear();
            m_BuildingPositions.Clear();
            DisposeInitialSyncArrays();
            m_InitialBuildingIndex = 0;
            m_InitialSyncComplete = false;
            m_UpdateTick = 0;
            m_CleanupTick = 0;
        }

        protected override void OnUpdate()
        {
            if (!m_InitialSyncComplete)
            {
                ProcessInitialSyncSlice();
                return;
            }

            ProcessChangedHouseholds();
            if (m_UpdateTick++ % 8 != 0)
                return;

            if (m_CommuneQuery.IsEmptyIgnoreFilter)
            {
                m_CommuneGeometryHashes.Clear();
                m_BuildingPositions.Clear();
                return;
            }

            NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
            NativeArray<byte> rebuildFlags = new NativeArray<byte>(communes.Length, Allocator.Temp);
            NativeArray<uint> geometryHashes = new NativeArray<uint>(communes.Length, Allocator.Temp);
            NativeArray<Entity> allBuildings = default;
            NativeArray<Entity> changedBuildings = default;
            try
            {
                if (m_CommuneGeometryHashes.Capacity < communes.Length * 2)
                    m_CommuneGeometryHashes.Capacity = math.max(16, communes.Length * 2);

                bool anyRebuild = false;

for (int c = 0; c < communes.Length; c++)
                {
                    Entity commune = communes[c];
                    if (!EntityManager.HasBuffer<CommuneBuildingMember>(commune))
                        EntityManager.AddBuffer<CommuneBuildingMember>(commune);

                    uint geometryHash = GetGeometryHash(commune);
                    bool rebuild = !m_CommuneGeometryHashes.TryGetValue(commune, out uint previousHash)
                        || previousHash != geometryHash;
                    rebuildFlags[c] = rebuild ? (byte)1 : (byte)0;
                    geometryHashes[c] = geometryHash;
                    anyRebuild |= rebuild;
                }

if (anyRebuild)
                {
                    allBuildings = m_BuildingQuery.ToEntityArray(Allocator.Temp);
                    if (m_BuildingPositions.Capacity < allBuildings.Length * 2)
                        m_BuildingPositions.Capacity = math.max(256, allBuildings.Length * 2);
                }

                for (int c = 0; c < communes.Length; c++)
                {
                    if (rebuildFlags[c] == 0)
                        continue;

                    Entity commune = communes[c];
                    DynamicBuffer<CommuneBuildingMember> members = EntityManager.GetBuffer<CommuneBuildingMember>(commune);
                    members.Clear();
                    Geometry geometry = EntityManager.GetComponentData<Geometry>(commune);
                    DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                    DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
                    for (int b = 0; b < allBuildings.Length; b++)
                    {
                        Entity building = allBuildings[b];
                        if (!EntityManager.TryGetComponent(building, out Transform transform))
                            continue;
                        if (CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, transform.m_Position))
                            members.Add(new CommuneBuildingMember { m_Building = building });
                        m_BuildingPositions[building] = transform.m_Position;
                    }

                    m_CommuneGeometryHashes[commune] = geometryHashes[c];
                    m_StatsSystem?.MarkDirty(commune);
                }

                bool reconcileAllBuildings = m_CleanupTick % 8 == 0;
                m_CleanupTick++;
                if (reconcileAllBuildings && !allBuildings.IsCreated)
                    allBuildings = m_BuildingQuery.ToEntityArray(Allocator.Temp);

                if (!m_ChangedBuildingQuery.IsEmptyIgnoreFilter)
                    changedBuildings = m_ChangedBuildingQuery.ToEntityArray(Allocator.Temp);
                int requiredBuildingCapacity = m_BuildingPositions.Count() + changedBuildings.Length * 2;
                if (m_BuildingPositions.Capacity < requiredBuildingCapacity)
                    m_BuildingPositions.Capacity = math.max(256, requiredBuildingCapacity);
                for (int b = 0; b < changedBuildings.Length; b++)
                {
                    Entity building = changedBuildings[b];
                    if (!EntityManager.TryGetComponent(building, out Transform transform))
                        continue;
                    float3 position = transform.m_Position;
                    if (!m_BuildingPositions.TryGetValue(building, out float3 previousPosition)
                        || math.distancesq(position, previousPosition) > 0.01f)
                        UpdateBuildingMembership(communes, building, position);
                }

                if (reconcileAllBuildings)
                {
                    if (m_BuildingPositions.Capacity < allBuildings.Length * 2)
                        m_BuildingPositions.Capacity = math.max(256, allBuildings.Length * 2);
                    for (int b = 0; b < allBuildings.Length; b++)
                    {
                        Entity building = allBuildings[b];
                        if (!EntityManager.TryGetComponent(building, out Transform transform))
                            continue;
                        float3 position = transform.m_Position;
                        if (!m_BuildingPositions.TryGetValue(building, out float3 previousPosition)
                            || math.distancesq(position, previousPosition) > 0.01f)
                            UpdateBuildingMembership(communes, building, position);
                    }
                }

if (reconcileAllBuildings)
                {
                    RemoveStaleBuildings(communes);
                    RemoveStaleCommunes();
                }
            }
            finally
            {
                if (allBuildings.IsCreated)
                    allBuildings.Dispose();
                if (changedBuildings.IsCreated)
                    changedBuildings.Dispose();
                rebuildFlags.Dispose();
                geometryHashes.Dispose();
                communes.Dispose();
            }
        }

        private void ProcessInitialSyncSlice()
        {
            if (!m_InitialCommunes.IsCreated)
            {
                if (m_CommuneQuery.IsEmptyIgnoreFilter)
                {
                    m_CommuneGeometryHashes.Clear();
                    m_BuildingPositions.Clear();
                    m_InitialSyncComplete = true;
                    return;
                }

                m_InitialCommunes = m_CommuneQuery.ToEntityArray(Allocator.Persistent);
                m_InitialBuildings = m_BuildingQuery.ToEntityArray(Allocator.Persistent);
                m_InitialBuildingIndex = 0;
                if (m_CommuneGeometryHashes.Capacity < m_InitialCommunes.Length * 2)
                    m_CommuneGeometryHashes.Capacity = math.max(16, m_InitialCommunes.Length * 2);
                if (m_BuildingPositions.Capacity < m_InitialBuildings.Length * 2)
                    m_BuildingPositions.Capacity = math.max(256, m_InitialBuildings.Length * 2);

                for (int c = 0; c < m_InitialCommunes.Length; c++)
                {
                    Entity commune = m_InitialCommunes[c];
                    if (!EntityManager.HasBuffer<CommuneBuildingMember>(commune))
                        EntityManager.AddBuffer<CommuneBuildingMember>(commune);
                    EntityManager.GetBuffer<CommuneBuildingMember>(commune).Clear();
                    m_CommuneGeometryHashes[commune] = GetGeometryHash(commune);
                }
            }

            int end = math.min(m_InitialBuildingIndex + kInitialBuildingsPerUpdate, m_InitialBuildings.Length);
            for (int b = m_InitialBuildingIndex; b < end; b++)
            {
                Entity building = m_InitialBuildings[b];
                if (!EntityManager.TryGetComponent(building, out Transform transform))
                    continue;

                float3 position = transform.m_Position;
                for (int c = 0; c < m_InitialCommunes.Length; c++)
                {
                    Entity commune = m_InitialCommunes[c];
                    Geometry geometry = EntityManager.GetComponentData<Geometry>(commune);
                    DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                    DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
                    if (CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, position))
                        EntityManager.GetBuffer<CommuneBuildingMember>(commune).Add(new CommuneBuildingMember { m_Building = building });
                }
                m_BuildingPositions[building] = position;
            }

            m_InitialBuildingIndex = end;
            if (m_InitialBuildingIndex < m_InitialBuildings.Length)
                return;

            for (int c = 0; c < m_InitialCommunes.Length; c++)
                m_StatsSystem?.MarkDirty(m_InitialCommunes[c]);
            DisposeInitialSyncArrays();
            m_InitialSyncComplete = true;
        }

        private void DisposeInitialSyncArrays()
        {
            if (m_InitialCommunes.IsCreated)
                m_InitialCommunes.Dispose();
            if (m_InitialBuildings.IsCreated)
                m_InitialBuildings.Dispose();
            m_InitialCommunes = default;
            m_InitialBuildings = default;
        }

        private void ProcessChangedHouseholds()
        {
            if (m_CommuneQuery.IsEmptyIgnoreFilter || m_ChangedHouseholdQuery.IsEmptyIgnoreFilter)
                return;

            NativeArray<Entity> households = m_ChangedHouseholdQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int h = 0; h < households.Length; h++)
                {
                    Entity household = households[h];
                    if (!EntityManager.TryGetComponent(household, out Game.Buildings.PropertyRenter propertyRenter))
                        continue;
                    Entity building = propertyRenter.m_Property;
                    if (!EntityManager.TryGetComponent(building, out Transform transform))
                        continue;

                    float3 position = transform.m_Position;
                    if (!m_BuildingPositions.TryGetValue(building, out float3 previousPosition)
                        || math.distancesq(position, previousPosition) > 0.01f)
                    {
                        UpdateBuildingMembership(communes, building, position, true);
                        continue;
                    }

                    for (int c = 0; c < communes.Length; c++)
                    {
                        Entity commune = communes[c];
                        Geometry geometry = EntityManager.GetComponentData<Geometry>(commune);
                        DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                        DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
                        if (CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, position))
                        {
                            if (m_StatsSystem == null)
                                m_StatsSystem = World.GetOrCreateSystemManaged<CommuneStatsSystem>();
                            m_StatsSystem.MarkDirty(commune);
                        }
                    }
                }
            }
            finally
            {
                households.Dispose();
                communes.Dispose();
            }
        }

        private void SetMembership(Entity commune, Entity building, bool inside, bool statsChanged)
        {
            DynamicBuffer<CommuneBuildingMember> members = EntityManager.GetBuffer<CommuneBuildingMember>(commune);
            int found = -1;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].m_Building == building)
                {
                    found = i;
                    break;
                }
            }

            bool membershipChanged = false;
            if (inside && found < 0)
            {
                members.Add(new CommuneBuildingMember { m_Building = building });
                membershipChanged = true;
            }
            else if (!inside && found >= 0)
            {
                members.RemoveAtSwapBack(found);
                membershipChanged = true;
            }

            if (membershipChanged || (statsChanged && inside))
            {
                if (m_StatsSystem == null)
                    m_StatsSystem = World.GetOrCreateSystemManaged<CommuneStatsSystem>();
                m_StatsSystem.MarkDirty(commune);
            }
        }

        private void UpdateBuildingMembership(NativeArray<Entity> communes, Entity building, float3 position, bool statsChanged = false)
        {
            for (int c = 0; c < communes.Length; c++)
            {
                Entity commune = communes[c];
                Geometry geometry = EntityManager.GetComponentData<Geometry>(commune);
                DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
                SetMembership(commune, building,
                    CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, position), statsChanged);
            }
            m_BuildingPositions[building] = position;
        }

        private void RemoveStaleBuildings(NativeArray<Entity> communes)
        {
            NativeArray<Entity> previous = m_BuildingPositions.GetKeyArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < previous.Length; i++)
                {
                    Entity building = previous[i];
                    if (EntityManager.Exists(building) && !EntityManager.HasComponent<Deleted>(building)
                        && !EntityManager.HasComponent<Temp>(building)
                        && EntityManager.HasComponent<Building>(building)
                        && EntityManager.HasComponent<Transform>(building))
                        continue;

                    m_BuildingPositions.Remove(building);
                    for (int c = 0; c < communes.Length; c++)
                    {
                        DynamicBuffer<CommuneBuildingMember> members = EntityManager.GetBuffer<CommuneBuildingMember>(communes[c]);
                        for (int j = members.Length - 1; j >= 0; j--)
                        {
                            if (members[j].m_Building == building)
                                members.RemoveAtSwapBack(j);
                        }
                    }
                }
            }
            finally
            {
                previous.Dispose();
            }

        }

        private void RemoveStaleCommunes()
        {
            NativeArray<Entity> previous = m_CommuneGeometryHashes.GetKeyArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < previous.Length; i++)
                {
                    Entity commune = previous[i];
                    if (EntityManager.Exists(commune) && EntityManager.HasComponent<Commune>(commune)
                        && !EntityManager.HasComponent<Deleted>(commune))
                        continue;
                    m_CommuneGeometryHashes.Remove(commune);
                }
            }
            finally
            {
                previous.Dispose();
            }
        }

        private uint GetGeometryHash(Entity commune)
        {
            DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
            DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
            uint hash = math.hash(new uint4((uint)nodes.Length, (uint)triangles.Length, 0u, 0u));
            for (int i = 0; i < nodes.Length; i++)
                hash = math.hash(new uint4(hash, math.hash(nodes[i].m_Position), (uint)i, 1u));
            for (int i = 0; i < triangles.Length; i++)
                hash = math.hash(new uint4(hash, math.hash(triangles[i].m_Indices), (uint)i, 2u));
            return hash;
        }
    }

[InternalBufferCapacity(0)]
    public struct CommuneBuildingMember : IBufferElementData, ISerializable
    {
        public Entity m_Building;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter => writer.Write(m_Building);
        public void Deserialize<TReader>(TReader reader) where TReader : IReader => reader.Read(out m_Building);
    }

}
