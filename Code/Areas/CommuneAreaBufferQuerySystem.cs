using System;
using System.Reflection;
using Game;
using Game.Areas;
using Game.Common;
using Game.Rendering;
using Game.SceneFlow;
using Game.Tools;
using Colossal.Serialization.Entities;
using Unity.Collections;
using Unity.Entities;

namespace Communes
{

public partial class CommuneAreaBufferQuerySystem : GameSystemBase
    {
        private AreaBufferSystem m_AreaBufferSystem;
        private CommuneAreaMaterialSystem m_CommuneMaterialSystem;
        private ToolSystem m_ToolSystem;
        private Array m_Slots;
        private object m_SpaceSlot;
        private FieldInfo m_AreaQueryField;
        private FieldInfo m_UpdatedQueryField;
        private EntityQuery m_OriginalAreaQuery;
        private EntityQuery m_OriginalUpdatedQuery;
        private EntityQuery m_MergedAreaQuery;
        private EntityQuery m_MergedUpdatedQuery;
        private EntityQuery m_CommuneRefreshQuery;
        private EntityQuery m_CommuneEntityQuery;
        private FieldInfo m_BufferDataDirtyField;
        private bool m_Patched;
        private bool m_CommuneToolWasActive;
        private bool m_RefreshAfterLoad;
        private bool m_LoadingComplete;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_AreaBufferSystem = World.GetOrCreateSystemManaged<AreaBufferSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_CommuneMaterialSystem = World.GetOrCreateSystemManaged<CommuneAreaMaterialSystem>();
            m_CommuneRefreshQuery = GetRefreshQuery(ComponentType.ReadOnly<Commune>());
            m_CommuneEntityQuery = GetEntityQuery(ComponentType.ReadOnly<Commune>(), ComponentType.ReadOnly<Area>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            PatchSpaceQueries();
        }

        private void PatchSpaceQueries()
        {
            FieldInfo slotsField = typeof(AreaBufferSystem).GetField("m_AreaTypeData", BindingFlags.Instance | BindingFlags.NonPublic);
            m_Slots = slotsField?.GetValue(m_AreaBufferSystem) as Array;
            int spaceIndex = (int)AreaType.Space;
            if (m_Slots == null || m_Slots.Length <= spaceIndex)
            {
                Mod.log.Error("[Communes][Rendu] Slot Space introuvable dans AreaBufferSystem.");
                return;
            }

            m_SpaceSlot = m_Slots.GetValue(spaceIndex);
            Type slotType = m_SpaceSlot.GetType();
            m_BufferDataDirtyField = slotType.GetField("m_BufferDataDirty", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m_AreaQueryField = slotType.GetField("m_AreaQuery", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m_UpdatedQueryField = slotType.GetField("m_UpdatedQuery", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m_AreaQueryField == null || m_UpdatedQueryField == null)
            {
                Mod.log.Error("[Communes][Rendu] Champs de requête AreaBufferSystem introuvables.");
                return;
            }

            m_OriginalAreaQuery = (EntityQuery)m_AreaQueryField.GetValue(m_SpaceSlot);
            m_OriginalUpdatedQuery = (EntityQuery)m_UpdatedQueryField.GetValue(m_SpaceSlot);

            m_MergedAreaQuery = GetEntityQuery(
                AreaQuery(ComponentType.ReadOnly<Game.Areas.Space>()),
                AreaQuery(ComponentType.ReadOnly<Commune>()));
            m_MergedUpdatedQuery = GetEntityQuery(
                UpdatedAreaQuery(ComponentType.ReadOnly<Game.Areas.Space>()),
                UpdatedAreaQuery(ComponentType.ReadOnly<Commune>()));

            m_AreaQueryField.SetValue(m_SpaceSlot, m_MergedAreaQuery);
            m_UpdatedQueryField.SetValue(m_SpaceSlot, m_MergedUpdatedQuery);
            m_Slots.SetValue(m_SpaceSlot, spaceIndex);
            m_Patched = true;
            Mod.log.Info("[Communes][Rendu] Requêtes AreaBuffer Space étendues à Commune; districts natifs isolés.");
        }

        private static EntityQueryDesc AreaQuery(ComponentType areaKind)
        {
            return new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Area>(), areaKind,
                    ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(),
                    ComponentType.ReadOnly<LabelExtents>(), ComponentType.ReadOnly<LabelVertex>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() }
            };
        }

        private static EntityQueryDesc UpdatedAreaQuery(ComponentType areaKind)
        {
            return new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Area>(), areaKind,
                    ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(),
                    ComponentType.ReadOnly<LabelExtents>(), ComponentType.ReadOnly<LabelVertex>() },
                Any = new[] { ComponentType.ReadOnly<Updated>(),
                    ComponentType.ReadOnly<BatchesUpdated>(), ComponentType.ReadOnly<Deleted>() },
                None = Array.Empty<ComponentType>()
            };
        }

        protected override void OnUpdate()
        {
            if (m_RefreshAfterLoad && m_LoadingComplete
                && m_CommuneMaterialSystem.CommuneLabelsReady)
            {

if (!m_CommuneEntityQuery.IsEmptyIgnoreFilter)
                {
                    RefreshLoadedCommunes(forceUpdated: true);
                    RequestBufferRefresh(AreaType.Space);
                    m_RefreshAfterLoad = false;
                    Mod.log.Info("[Communes][Rendu] Labels et buffers des communes rafraîchis après chargement.");
                }
            }

            bool communeToolActive = m_ToolSystem.activeTool is CommuneAreaToolSystem;
            if (communeToolActive && !m_CommuneToolWasActive)
            {

if (!m_CommuneRefreshQuery.IsEmptyIgnoreFilter)
                {
                    RefreshLabels(m_CommuneRefreshQuery);
                    RequestBufferRefresh(AreaType.Space);
                }
            }
            m_CommuneToolWasActive = communeToolActive;
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            m_RefreshAfterLoad = true;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_LoadingComplete = false;
            m_RefreshAfterLoad = true;
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_LoadingComplete = mode == GameMode.Game || mode == GameMode.Editor;
            if (m_LoadingComplete)
                m_RefreshAfterLoad = true;
        }

        private void RefreshLoadedCommunes(bool forceUpdated)
        {
            NativeArray<Entity> entities = m_CommuneEntityQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity commune = entities[i];
                    if (EntityManager.HasComponent<Hidden>(commune))
                        EntityManager.RemoveComponent<Hidden>(commune);

if (forceUpdated && EntityManager.HasComponent<Updated>(commune))
                        EntityManager.RemoveComponent<Updated>(commune);
                    if (!EntityManager.HasComponent<Updated>(commune))
                        EntityManager.AddComponent<Updated>(commune);
                }
            }
            finally { entities.Dispose(); }
        }

        private EntityQuery GetRefreshQuery(ComponentType areaKind)
        {
            return GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Area>(), areaKind,
                    ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>(),
                    ComponentType.ReadOnly<LabelExtents>(), ComponentType.ReadOnly<LabelVertex>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }

        private void RefreshLabels(EntityQuery query)
        {
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.HasComponent<Updated>(entities[i]))
                        EntityManager.AddComponent<Updated>(entities[i]);
                }
            }
            finally { entities.Dispose(); }
        }

        private void RequestBufferRefresh(AreaType type)
        {
            if (m_Slots != null && (int)type < m_Slots.Length && m_BufferDataDirtyField != null)
            {
                object slot = m_Slots.GetValue((int)type);
                if (slot != null)
                {
                    CommuneSystemDependency.Complete(m_AreaBufferSystem);
                    m_BufferDataDirtyField.SetValue(slot, true);
                }
            }
        }

        protected override void OnDestroy()
        {
            if (m_Patched && m_SpaceSlot != null)
            {
                CommuneSystemDependency.Complete(m_AreaBufferSystem);
                m_AreaQueryField.SetValue(m_SpaceSlot, m_OriginalAreaQuery);
                m_UpdatedQueryField.SetValue(m_SpaceSlot, m_OriginalUpdatedQuery);
                m_Slots.SetValue(m_SpaceSlot, (int)AreaType.Space);
            }
            base.OnDestroy();
        }
    }
}
