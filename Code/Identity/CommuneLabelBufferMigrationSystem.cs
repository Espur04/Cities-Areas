using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Common;
using Game.Policies;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;

namespace Communes
{

public partial class CommuneLabelBufferMigrationSystem : GameSystemBase
    {
        private EntityQuery m_CommuneQuery;
        private EntityQuery m_AreaCandidatesQuery;
        private readonly HashSet<Entity> m_InitializedCommunes = new HashSet<Entity>();
        private bool m_LoadingComplete;
        private bool m_MigrationComplete;

        public bool CurrentLoadMigrationComplete => m_MigrationComplete;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CommuneQuery = GetEntityQuery(ComponentType.ReadOnly<Commune>(),
                ComponentType.ReadOnly<Area>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            m_AreaCandidatesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Area>(), ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Geometry>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_LoadingComplete = false;
            m_MigrationComplete = false;
            m_InitializedCommunes.Clear();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            m_LoadingComplete = false;
            m_MigrationComplete = false;
            m_InitializedCommunes.Clear();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_LoadingComplete = mode == GameMode.Game || mode == GameMode.Editor;
            m_MigrationComplete = !m_LoadingComplete;
        }

        protected override void OnUpdate()
        {
            if (!m_MigrationComplete)
            {
                if (!m_LoadingComplete)
                    return;

CommuneToolPrefabSystem toolPrefabSystem = World.GetOrCreateSystemManaged<CommuneToolPrefabSystem>();
                Entity communePrefab = toolPrefabSystem.ToolPrefabEntity;
                if (communePrefab == Entity.Null || !EntityManager.Exists(communePrefab))
                    return;

                MigrateLegacyAreas(communePrefab);
                m_MigrationComplete = true;
                Mod.log.Info("[Communes][Migration] Reprise des aires de commune du chargement terminée.");
            }

            RepairCommuneBuffersAndRendering();
        }

        private void MigrateLegacyAreas(Entity communePrefab)
        {
            NativeArray<Entity> candidates = m_AreaCandidatesQuery.ToEntityArray(Allocator.Temp);
            int migrated = 0;
            try
            {
                NameSystem nameSystem = World.GetOrCreateSystemManaged<NameSystem>();
                for (int i = 0; i < candidates.Length; i++)
                {
                    Entity entity = candidates[i];
                    if (EntityManager.HasComponent<Commune>(entity))
                        continue;

                    bool identityPersisted = EntityManager.HasComponent<CommuneIdentity>(entity);
                    bool prefabMatches = EntityManager.TryGetComponent(entity, out PrefabRef prefabRef)
                        && prefabRef.m_Prefab == communePrefab;
                    bool hasCommuneName = nameSystem.TryGetCustomName(entity, out string customName)
                        && !string.IsNullOrEmpty(customName)
                        && customName.StartsWith("Commune ", System.StringComparison.OrdinalIgnoreCase);

if (!identityPersisted && !prefabMatches && !hasCommuneName)
                        continue;

                    EntityManager.AddComponent<Commune>(entity);
                    if (!EntityManager.HasComponent<CommuneIdentity>(entity))
                        EntityManager.AddComponentData(entity, new CommuneIdentity { m_Id = 0 });
                    if (!EntityManager.HasBuffer<CommuneBuildingMember>(entity))
                        EntityManager.AddBuffer<CommuneBuildingMember>(entity);

RemoveIfPresent<District>(entity);
                    RemoveIfPresent<DistrictData>(entity);
                    if (EntityManager.HasBuffer<DistrictModifier>(entity))
                        EntityManager.RemoveComponent<DistrictModifier>(entity);
                    if (EntityManager.HasBuffer<Policy>(entity))
                        EntityManager.RemoveComponent<Policy>(entity);

                    if (EntityManager.HasComponent<Hidden>(entity))
                        EntityManager.RemoveComponent<Hidden>(entity);
                    MarkUpdated(entity);
                    migrated++;
                }
            }
            finally { candidates.Dispose(); }

            if (migrated > 0)
                Mod.log.Warn($"[Communes][Migration] {migrated} ancienne(s) aire(s) restaurée(s) comme commune(s)." );
        }

        private void RepairCommuneBuffersAndRendering()
        {
            NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
            int repaired = 0;
            try
            {
                for (int i = 0; i < communes.Length; i++)
                {
                    Entity commune = communes[i];
                    bool firstSeen = m_InitializedCommunes.Add(commune);

if (!firstSeen)
                        continue;

                    bool changed = false;
                    if (EntityManager.HasComponent<Hidden>(commune))
                    {
                        EntityManager.RemoveComponent<Hidden>(commune);
                        changed = true;
                    }
                    if (!EntityManager.HasBuffer<LabelExtents>(commune))
                    {
                        EntityManager.AddBuffer<LabelExtents>(commune);
                        changed = true;
                    }
                    if (!EntityManager.HasBuffer<LabelVertex>(commune))
                    {
                        EntityManager.AddBuffer<LabelVertex>(commune);
                        changed = true;
                    }
                    if (!EntityManager.HasBuffer<CommuneBuildingMember>(commune))
                    {
                        EntityManager.AddBuffer<CommuneBuildingMember>(commune);
                        changed = true;
                    }

if (EntityManager.TryGetComponent(commune, out PrefabRef prefabRef)
                        && EntityManager.Exists(prefabRef.m_Prefab)
                        && EntityManager.HasComponent<AreaColorData>(prefabRef.m_Prefab))
                    {
                        EntityManager.SetComponentData(prefabRef.m_Prefab, CommuneAreaPrefab.GetCommuneColorData());
                    }

                    if (changed || firstSeen)
                    {
                        MarkUpdated(commune);
                        repaired++;
                    }
                }
            }
            finally { communes.Dispose(); }

            if (repaired > 0)
                Mod.log.Info($"[Communes][Migration] Buffers/rendu vérifiés sur {repaired} commune(s).");
        }

        private void MarkUpdated(Entity entity)
        {
            if (EntityManager.HasComponent<Updated>(entity))
                EntityManager.RemoveComponent<Updated>(entity);
            EntityManager.AddComponent<Updated>(entity);
        }

        private void RemoveIfPresent<T>(Entity entity) where T : unmanaged, IComponentData
        {
            if (EntityManager.HasComponent<T>(entity))
                EntityManager.RemoveComponent<T>(entity);
        }
    }
}
