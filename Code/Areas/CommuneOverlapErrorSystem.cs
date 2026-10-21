using Game;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace Communes
{
    public partial class CommuneOverlapErrorSystem : GameSystemBase
    {
        private const ToolErrorFlags kDisabledFlags = ToolErrorFlags.DisableInGame | ToolErrorFlags.DisableInEditor;

        private ToolSystem m_ToolSystem;
        private EntityQuery m_ToolErrorQuery;
        private NativeHashMap<Entity, ToolErrorFlags> m_AddedFlags;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ToolErrorQuery = GetEntityQuery(
                ComponentType.ReadWrite<ToolErrorData>(),
                ComponentType.ReadOnly<NotificationIconData>());
            m_AddedFlags = new NativeHashMap<Entity, ToolErrorFlags>(4, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            RestoreErrorFlags();
            if (m_AddedFlags.IsCreated)
                m_AddedFlags.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (m_ToolSystem.activeTool is CommuneAreaToolSystem)
            {
                DisableOverlapError();
                return;
            }

            RestoreErrorFlags();
        }

        private void DisableOverlapError()
        {
            NativeArray<Entity> entities = m_ToolErrorQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    ToolErrorData error = EntityManager.GetComponentData<ToolErrorData>(entity);
                    if (error.m_Error != ErrorType.OverlapExisting)
                        continue;

                    ToolErrorFlags added = kDisabledFlags & ~error.m_Flags;
                    if (!m_AddedFlags.ContainsKey(entity))
                        m_AddedFlags.TryAdd(entity, added);

                    error.m_Flags |= kDisabledFlags;
                    EntityManager.SetComponentData(entity, error);
                }
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void RestoreErrorFlags()
        {
            if (!m_AddedFlags.IsCreated || m_AddedFlags.Count == 0)
                return;

            NativeArray<Entity> entities = m_AddedFlags.GetKeyArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<ToolErrorData>(entity))
                        continue;

                    ToolErrorData error = EntityManager.GetComponentData<ToolErrorData>(entity);
                    error.m_Flags &= ~m_AddedFlags[entity];
                    EntityManager.SetComponentData(entity, error);
                }
            }
            finally
            {
                entities.Dispose();
                m_AddedFlags.Clear();
            }
        }
    }
}
