using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.SceneFlow;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Communes
{

public partial class CommuneToolPrefabSystem : GameSystemBase
    {

private int m_OnPreloadCount;
        private int m_OnLoadingCompleteCount;
        private int m_OnLoadedCount;
        private int m_OnCreateCount;

private ulong m_RegisteredWorldSignature = ulong.MaxValue;

public const string GroupName = "Zones Toolbar Group";

public const string ToolIconName = "Media/Game/Icons/Districts.svg";

public const string ToolPrefabName = "CommuneTool";

public const int ToolPriority = 100;

        private PrefabSystem m_PrefabSystem;
        private ILog m_Log;

        private CommuneAreaPrefab m_ToolPrefab;
        private UIObject m_ToolUIObject;

private Entity m_ToolPrefabEntity = Entity.Null;

        public CommuneAreaPrefab ToolPrefab => m_ToolPrefab;
        public Entity ToolPrefabEntity => m_ToolPrefabEntity;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OnCreateCount++;
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Log = Mod.log;

m_Log.Info($"[Communes][Diag] OnCreate #{m_OnCreateCount} : world=\"{World.Name}\" " +
                       $"seqNo={World.SequenceNumber} valid={World.IsCreated == true} " +
                       $"GameManager.instance={(GameManager.instance == null ? "null" : "ok")}");
        }

        protected override void OnDestroy()
        {

m_Log.Info($"[Communes][Diag] OnDestroy : world=\"{World.Name}\" " +
                       $"preload={m_OnPreloadCount} loadingComplete={m_OnLoadingCompleteCount} " +
                       $"loaded={m_OnLoadedCount} create={m_OnCreateCount}");
            base.OnDestroy();
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_OnPreloadCount++;

m_Log.Info($"[Communes][Diag] OnGamePreload #{m_OnPreloadCount} : purpose={purpose} " +
                       $"mode={mode} world=\"{World.Name}\" seqNo={World.SequenceNumber}");

            CreateToolPrefab("OnGamePreload");
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

m_OnLoadedCount++;
            m_Log.Info($"[Communes][Diag] OnGameLoaded #{m_OnLoadedCount} : world=\"{World.Name}\" " +
                       $"seqNo={World.SequenceNumber} prefabEntity={m_ToolPrefabEntity} " +
                       $"entityAlive={EntityManager.Exists(m_ToolPrefabEntity)}");
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_OnLoadingCompleteCount++;

            m_Log.Info($"[Communes][Diag] OnGameLoadingComplete #{m_OnLoadingCompleteCount} : purpose={purpose} " +
                       $"mode={mode} world=\"{World.Name}\" seqNo={World.SequenceNumber}");

            if (mode != GameMode.Game && mode != GameMode.Editor)
            {
                return;
            }

            CreateToolPrefab("OnGameLoadingComplete");

DiagnosePrefabState("OnGameLoadingComplete");

DiagnoseGroups();
        }

        protected override void OnUpdate()
        {

            Enabled = false;
        }

private void CreateToolPrefab(string caller)
        {

            bool hasManagedPrefab = m_ToolPrefab != null;
            bool entityAlive = false;
            string observedPrefabName = "<n/a>";

            if (hasManagedPrefab)
            {
                observedPrefabName = m_ToolPrefab.name;
                entityAlive = EntityManager.Exists(m_ToolPrefabEntity);
            }

            m_Log.Info($"[Communes][Diag] CreateToolPrefab ({caller}) : managedPrefab={hasManagedPrefab} " +
                       $"({observedPrefabName}) storedEntity={m_ToolPrefabEntity} " +
                       $"entityAlive={entityAlive} world=\"{World.Name}\" seqNo={World.SequenceNumber}");

            bool needsCreation = !hasManagedPrefab || !entityAlive;
            if (!needsCreation)
            {

                return;
            }

            if (hasManagedPrefab)
            {
                m_Log.Warn($"[Communes] Le prefab d'outil doit être (re)créé : " +
                           $"managedPrefab={hasManagedPrefab}, entityAlive={entityAlive}.");
            }

var group = ResolveGroup(GroupName);
            if (group == null)
            {
                m_Log.Warn($"[Communes] Groupe natif \"{GroupName}\" non résolu pour l'instant ; " +
                           "nouvelle tentative au prochain chargement.");
                return;
            }

m_ToolPrefab = ScriptableObject.CreateInstance<CommuneAreaPrefab>();
            m_ToolPrefab.name = ToolPrefabName;
            m_ToolPrefab.active = true;

m_ToolUIObject = ScriptableObject.CreateInstance<UIObject>();
            m_ToolUIObject.name = ToolPrefabName;
            m_ToolUIObject.active = true;
            m_ToolUIObject.m_IsDebugObject = false;
            m_ToolUIObject.m_Priority = ToolPriority;
            m_ToolUIObject.m_Group = group;

m_ToolUIObject.m_Icon = ToolIconName;

            m_ToolPrefab.AddComponentFrom(m_ToolUIObject);

bool added = m_PrefabSystem.AddPrefab(m_ToolPrefab, null, null, null);

if (added && m_PrefabSystem.TryGetEntity(m_ToolPrefab, out Entity toolEntity))
            {
                m_ToolPrefabEntity = toolEntity;
                m_RegisteredWorldSignature = World.SequenceNumber;
            }
            else
            {
                m_ToolPrefabEntity = Entity.Null;
            }

            m_Log.Info($"[Communes] Prefab d'outil \"{ToolPrefabName}\" enregistré depuis {caller} " +
                       $"(groupe=\"{GroupName}\", priority={ToolPriority}, icon=\"{ToolIconName}\", " +
                       $"AddPrefab={added}, entity={m_ToolPrefabEntity}, world=\"{World.Name}\", " +
                       $"seqNo={World.SequenceNumber}).");
        }

private void DiagnosePrefabState(string caller)
        {
            var em = EntityManager;

            bool managed = m_ToolPrefab != null;
            bool entityExists = em.Exists(m_ToolPrefabEntity);
            string name = managed ? m_ToolPrefab.name : "<null>";

            bool foundByPrefabId = m_PrefabSystem.TryGetPrefab(
                new PrefabID(nameof(CommuneAreaPrefab), name, default), out PrefabBase found);

Entity probe = m_ToolPrefabEntity;
            if (m_ToolPrefab != null && m_PrefabSystem.TryGetEntity(m_ToolPrefab, out Entity live))
            {
                probe = live;
            }

            bool hasUiObjectData = em.Exists(probe) && em.HasComponent<UIObjectData>(probe);
            int uiPriority = hasUiObjectData ? em.GetComponentData<UIObjectData>(probe).m_Priority : -1;

bool inNativeGroup = false;
            if (em.Exists(probe))
            {
                var query = GetEntityQuery(ComponentType.ReadOnly<UIToolbarGroupData>());
                var groupEntities = query.ToEntityArray(Allocator.Temp);
                foreach (var g in groupEntities)
                {
                    if (!em.HasBuffer<UIGroupElement>(g))
                    {
                        continue;
                    }

                    var buffer = em.GetBuffer<UIGroupElement>(g, true);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        if (buffer[i].m_Prefab == probe)
                        {
                            inNativeGroup = true;
                            break;
                        }
                    }

                    if (inNativeGroup)
                    {
                        break;
                    }
                }

                groupEntities.Dispose();
            }

            m_Log.Info($"[Communes][Diag] ETAT ({caller}) : managedPrefab={managed} name=\"{name}\" " +
                       $"storedEntity={m_ToolPrefabEntity} entityExists={entityExists} " +
                       $"foundByPrefabId={foundByPrefabId} (type={found?.GetType().Name ?? "n/a"}) " +
                       $"probe={probe} hasUIObjectData={hasUiObjectData} uiPriority={uiPriority} " +
                       $"inNativeGroupBuffer={inNativeGroup} registeredSeqNo={m_RegisteredWorldSignature} " +
                       $"currentSeqNo={World.SequenceNumber}");
        }

private UIGroupPrefab ResolveGroup(string groupName)
        {
            if (m_PrefabSystem.TryGetPrefab(
                    new PrefabID(nameof(UIToolbarGroupPrefab), groupName, default),
                    out PrefabBase prefab) && prefab is UIGroupPrefab group)
            {
                return group;
            }

if (m_PrefabSystem.TryGetPrefab(
                    new PrefabID(nameof(UIGroupPrefab), groupName, default),
                    out prefab) && prefab is UIGroupPrefab group2)
            {
                return group2;
            }

            return null;
        }

private void DiagnoseGroups()
        {
            var em = EntityManager;
            var query = GetEntityQuery(ComponentType.ReadOnly<UIToolbarGroupData>());
            var groups = query.ToEntityArray(Allocator.Temp);

            foreach (var groupEntity in groups)
            {
                var data = em.GetComponentData<UIToolbarGroupData>(groupEntity);

var groupName = "<inconnu>";
                if (m_PrefabSystem.TryGetPrefab(groupEntity, out PrefabBase groupPrefab))
                {
                    groupName = groupPrefab.name;
                }
                else
                {
                    groupName = em.GetName(groupEntity);
                }

                var children = new List<string>();
                if (em.HasBuffer<UIGroupElement>(groupEntity))
                {
                    var buffer = em.GetBuffer<UIGroupElement>(groupEntity, true);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var childEntity = buffer[i].m_Prefab;
                        var childName = em.GetName(childEntity);
                        if (m_PrefabSystem.TryGetPrefab(childEntity, out PrefabBase childPrefab))
                        {
                            childName = childPrefab.name;
                        }

var prio = 0;
                        if (em.HasComponent<UIObjectData>(childEntity))
                        {
                            prio = em.GetComponentData<UIObjectData>(childEntity).m_Priority;
                        }

                        children.Add($"{childName}(prio={prio})");
                    }
                }

                m_Log.Info($"[Communes][Diag] GROUPE \"{groupName}\" prio={data.m_Priority} " +
                           $"({children.Count} enfants) : {string.Join(", ", children)}");
            }

            groups.Dispose();
        }
    }
}
