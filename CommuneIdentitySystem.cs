using System.Collections.Generic;
using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;

namespace Communes
{

public partial class CommuneIdentitySystem : GameSystemBase
    {
        private static readonly FieldInfo kNamesField = typeof(NameSystem).GetField(
            "m_Names", BindingFlags.Instance | BindingFlags.NonPublic);
        private EntityQuery m_CommuneQuery;
        private NameSystem m_NameSystem;
        private Dictionary<Entity, string> m_Names;
        private CommuneLabelBufferMigrationSystem m_MigrationSystem;
        private int m_NextId = 1;
        private bool m_Seeded;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_Names = kNamesField?.GetValue(m_NameSystem) as Dictionary<Entity, string>;
            m_MigrationSystem = World.GetOrCreateSystemManaged<CommuneLabelBufferMigrationSystem>();
            m_CommuneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Commune>(), ComponentType.ReadOnly<Area>(),
                    ComponentType.ReadOnly<Geometry>(), ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() }
            });
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            ResetForLoad();
            RefreshNameSystemReferences();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            ResetForLoad();
            RefreshNameSystemReferences();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode != GameMode.Game && mode != GameMode.Editor)
                ResetForLoad();
        }

        private void ResetForLoad()
        {
            m_NextId = 1;
            m_Seeded = false;
        }

        protected override void OnUpdate()
        {

if (!m_MigrationSystem.CurrentLoadMigrationComplete)
                return;

RefreshNameSystemReferences();

            NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                if (!m_Seeded)
                {
                    SeedAndRepairIdentities(communes);
                    m_Seeded = true;
                }

                for (int i = 0; i < communes.Length; i++)
                {
                    Entity entity = communes[i];
                    if (!EntityManager.HasComponent<CommuneIdentity>(entity))
                        EntityManager.AddComponentData(entity, new CommuneIdentity { m_Id = m_NextId++ });

                    CommuneIdentity identity = EntityManager.GetComponentData<CommuneIdentity>(entity);
                    if (identity.m_Id <= 0)
                    {
                        identity.m_Id = m_NextId++;
                        EntityManager.SetComponentData(entity, identity);
                    }

                    string desiredName = GeneratedName(identity.m_Id, Mod.EffectiveLanguage);
                    if (!m_NameSystem.TryGetCustomName(entity, out string currentName)
                        || (IsGeneratedName(currentName, identity.m_Id) && currentName != desiredName))
                    {
                        SetCustomNameWithoutBarrier(entity, desiredName);
                        MarkLabelsUpdated(entity);
                        Mod.log.Info($"[Communes][Identity] Commune #{identity.m_Id} nommée \"{desiredName}\" via NameSystem.");
                    }
                }
            }
            finally { communes.Dispose(); }
        }

        private void SeedAndRepairIdentities(NativeArray<Entity> communes)
        {
            int maxId = 0;
            for (int i = 0; i < communes.Length; i++)
            {
                Entity entity = communes[i];
                if (EntityManager.HasComponent<CommuneIdentity>(entity))
                    maxId = Unity.Mathematics.math.max(maxId,
                        EntityManager.GetComponentData<CommuneIdentity>(entity).m_Id);
            }

            HashSet<int> seenIds = new HashSet<int>();
            m_NextId = maxId + 1;
            int repaired = 0;
            for (int i = 0; i < communes.Length; i++)
            {
                Entity entity = communes[i];
                if (!EntityManager.HasComponent<CommuneIdentity>(entity))
                {
                    EntityManager.AddComponentData(entity, new CommuneIdentity { m_Id = m_NextId++ });
                    MarkLabelsUpdated(entity);
                    repaired++;
                    continue;
                }

                CommuneIdentity identity = EntityManager.GetComponentData<CommuneIdentity>(entity);
                int oldId = identity.m_Id;
                if (oldId <= 0 || !seenIds.Add(oldId))
                {
                    identity.m_Id = m_NextId++;
                    EntityManager.SetComponentData(entity, identity);

if (m_NameSystem.TryGetCustomName(entity, out string customName)
                        && IsGeneratedName(customName, oldId))
                    {
                        SetCustomNameWithoutBarrier(entity, GeneratedName(identity.m_Id, Mod.EffectiveLanguage));
                    }
                    MarkLabelsUpdated(entity);
                    repaired++;
                }
            }

            if (repaired > 0)
                Mod.log.Warn($"[Communes][Identity] {repaired} identité(s) absente(s) ou dupliquée(s) réparée(s); prochain numéro #{m_NextId}.");
            else
                Mod.log.Info($"[Communes][Identity] Identifiants existants restaurés; prochain numéro #{m_NextId}.");
        }

        private void SetCustomNameWithoutBarrier(Entity entity, string name)
        {
            RefreshNameSystemReferences();
            if (m_Names == null)
                return;

m_Names[entity] = name;
            if (!EntityManager.HasComponent<CustomName>(entity))
                EntityManager.AddComponent<CustomName>(entity);
            if (!EntityManager.HasComponent<BatchesUpdated>(entity))
                EntityManager.AddComponent<BatchesUpdated>(entity);
        }

        private void RefreshNameSystemReferences()
        {
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_Names = kNamesField?.GetValue(m_NameSystem) as Dictionary<Entity, string>;
        }

        private static string GeneratedName(int id, InterfaceLanguage language)
            => $"{LocaleText.CommunePrefix(language)} {id}";

        private static bool IsGeneratedName(string name, int id)
        {
            foreach (InterfaceLanguage language in System.Enum.GetValues(typeof(InterfaceLanguage)))
                if (name == GeneratedName(id, language)) return true;

            return name == $"Commune {id}";
        }

        private void MarkLabelsUpdated(Entity entity)
        {
            if (EntityManager.HasComponent<Updated>(entity))
                EntityManager.RemoveComponent<Updated>(entity);
            EntityManager.AddComponent<Updated>(entity);
        }
    }
}
