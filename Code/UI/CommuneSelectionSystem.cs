using System.Reflection;
using Game;
using Game.Areas;
using Game.Common;
using Game.Input;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;

namespace Communes
{

    public partial class CommuneSelectionSystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private ToolRaycastSystem m_RaycastSystem;
        private DefaultToolSystem m_DefaultTool;
        private SelectedInfoUISystem m_SelectedInfo;
        private PropertyInfo m_ApplyAction;
        private EntityQuery m_CommuneQuery;
        private bool m_LoggedInputWarning;
        private Entity m_HoveredCommune = Entity.Null;
        private Entity m_SelectedCommune = Entity.Null;
        private bool m_PanelWasOpened;

        public Entity HoveredCommune => m_HoveredCommune;
        public Entity SelectedCommune => m_SelectedCommune;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_RaycastSystem = World.GetOrCreateSystemManaged<ToolRaycastSystem>();
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_SelectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_ApplyAction = typeof(ToolBaseSystem).GetProperty("applyAction",
                BindingFlags.Instance | BindingFlags.NonPublic);
            m_CommuneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Commune>(), ComponentType.ReadOnly<Area>(),
                    ComponentType.ReadOnly<Geometry>(), ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Triangle>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Hidden>() }
            });
        }

        protected override void OnUpdate()
        {
            if (!(m_ToolSystem.activeTool is DefaultToolSystem) || !InputManager.instance.mouseOnScreen
                || m_CommuneQuery.IsEmptyIgnoreFilter)
            {
                ClearHover();
                MaintainSelectedCommune(false);
                return;
            }

Entity hovered = Entity.Null;
            if (m_RaycastSystem.GetRaycastResult(out RaycastResult hoverResult)
                && hoverResult.m_Owner != Entity.Null
                && EntityManager.HasComponent<Commune>(hoverResult.m_Owner))
                hovered = hoverResult.m_Owner;
            UpdateHover(hovered);

            IProxyAction apply = m_ApplyAction?.GetValue(m_DefaultTool, null) as IProxyAction;
            if (apply == null)
            {
                if (!m_LoggedInputWarning)
                {
                    m_LoggedInputWarning = true;
                    Mod.log.Warn("[Communes][Selection] Action Apply du DefaultToolSystem introuvable.");
                }
                return;
            }

            if (apply.WasPressedThisFrame())
            {
                Entity clicked = hovered;
                if (clicked == Entity.Null && InputManager.instance.controlOverWorld
                    && m_RaycastSystem.GetRaycastResult(out RaycastResult result))
                {
                    if (result.m_Owner == Entity.Null)
                    {

clicked = FindCommuneAt(result.m_Hit.m_HitPosition);
                    }
                    else if (EntityManager.HasComponent<District>(result.m_Owner))
                    {

                        m_SelectedCommune = Entity.Null;
                        m_PanelWasOpened = false;
                        m_ToolSystem.selected = result.m_Owner;
                        m_ToolSystem.selectedIndex = -1;
                    }
                }

                if (clicked != Entity.Null)
                {
                    m_SelectedCommune = clicked;
                    m_PanelWasOpened = false;
                    Mod.log.Info($"[Communes][Selection] Commune {clicked} sélectionnée.");
                }
                else if (InputManager.instance.controlOverWorld)
                {
                    m_SelectedCommune = Entity.Null;
                    m_PanelWasOpened = false;
                }
            }

MaintainSelectedCommune(true);
        }

        private void MaintainSelectedCommune(bool reassert)
        {
            if (m_SelectedCommune == Entity.Null) return;
            Entity panelEntity = m_SelectedInfo.selectedEntity;
            if (panelEntity == m_SelectedCommune)
                m_PanelWasOpened = true;

bool userChangedSelection = m_PanelWasOpened && panelEntity != m_SelectedCommune;
            if (!EntityManager.Exists(m_SelectedCommune) || userChangedSelection)
            {
                if (m_ToolSystem.selected == m_SelectedCommune) m_ToolSystem.selected = Entity.Null;
                m_SelectedCommune = Entity.Null;
                m_PanelWasOpened = false;
            }
            else if (reassert && !m_PanelWasOpened)
            {
                m_ToolSystem.selected = m_SelectedCommune;
                m_ToolSystem.selectedIndex = -1;
            }
        }

        private Entity FindCommuneAt(Unity.Mathematics.float3 position)
        {
            Entity selected = Entity.Null;
            float bestArea = float.MaxValue;
            NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < communes.Length; i++)
                {
                    Entity commune = communes[i];
                    if (EntityManager.HasComponent<Temp>(commune)
                        && EntityManager.GetComponentData<Temp>(commune).m_Original != Entity.Null)
                        continue;

                    Geometry geometry = EntityManager.GetComponentData<Geometry>(commune);
                    DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(commune, true);
                    DynamicBuffer<Triangle> triangles = EntityManager.GetBuffer<Triangle>(commune, true);
                    if (!CommuneGeometry.Contains(geometry.m_Bounds, nodes, triangles, position))
                        continue;

                    float area = geometry.m_SurfaceArea;
                    if (selected == Entity.Null || area < bestArea
                        || (area == bestArea && commune.Index < selected.Index))
                    {
                        selected = commune;
                        bestArea = area;
                    }
                }
            }
            finally
            {
                communes.Dispose();
            }

            return selected;
        }

        private void UpdateHover(Entity commune)
        {
            if (commune == m_HoveredCommune)
                return;

            ClearHover();
            m_HoveredCommune = commune;
        }

        private void ClearHover()
        {
            m_HoveredCommune = Entity.Null;
        }

        protected override void OnDestroy()
        {
            ClearHover();
            base.OnDestroy();
        }
    }
}
