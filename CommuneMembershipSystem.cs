using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Communes
{

public partial class CommuneMembershipSystem : GameSystemBase
    {
        private EntityQuery m_CommuneQuery;
        private EntityQuery m_BuildingQuery;
        private EntityQuery m_ChangedBuildingQuery;
        private NativeParallelHashMap<Entity, uint> m_CommuneGeometryHashes;
        private NativeParallelHashMap<Entity, float3> m_BuildingPositions;
        private int m_CleanupTick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CommuneQuery = GetEntityQuery(
                ComponentType.ReadOnly<Commune>(), ComponentType.ReadOnly<Geometry>(),
                ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_BuildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Transform>(), ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());
            m_ChangedBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Updated>(), ComponentType.ReadOnly<Building>(),
                    ComponentType.ReadOnly<Transform>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            m_CommuneGeometryHashes = new NativeParallelHashMap<Entity, uint>(16, Allocator.Persistent);
            m_BuildingPositions = new NativeParallelHashMap<Entity, float3>(256, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (m_CommuneGeometryHashes.IsCreated)
                m_CommuneGeometryHashes.Dispose();
            if (m_BuildingPositions.IsCreated)
                m_BuildingPositions.Dispose();
            base.OnDestroy();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

m_CommuneGeometryHashes.Clear();
            m_BuildingPositions.Clear();
            m_CleanupTick = 0;
        }

        protected override void OnUpdate()
        {
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
                }

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
                    for (int c = 0; c < communes.Length; c++)
                    {
                        Entity commune = communes[c];
                        Geometry geometry = EntityManager.GetComponentData<Geometry>(commune);
                        DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                        DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
                        bool inside = CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, position);
                        SetMembership(commune, building, inside);
                    }
                    m_BuildingPositions[building] = position;
                }

if (m_CleanupTick++ % 64 == 0)
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

        private void SetMembership(Entity commune, Entity building, bool inside)
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

            if (inside && found < 0)
                members.Add(new CommuneBuildingMember { m_Building = building });
            else if (!inside && found >= 0)
                members.RemoveAtSwapBack(found);
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
