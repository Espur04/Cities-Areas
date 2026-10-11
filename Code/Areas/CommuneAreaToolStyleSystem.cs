using Game;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using UnityEngine;

namespace Communes
{

public partial class CommuneAreaToolStyleSystem : GameSystemBase
    {
        private static readonly Color kCommunePreviewColor = new Color(0.9f, 0.035f, 0.055f, 0.95f);
        private ToolSystem m_ToolSystem;
        private CommuneSelectionSystem m_SelectionSystem;
        private EntityQuery m_SettingsQuery;
        private Color m_PreviousHoveredColor;
        private bool m_HasOverride;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_SelectionSystem = World.GetOrCreateSystemManaged<CommuneSelectionSystem>();
            m_SettingsQuery = GetEntityQuery(ComponentType.ReadWrite<RenderingSettingsData>());
        }

        protected override void OnUpdate()
        {
            if (m_SettingsQuery.IsEmptyIgnoreFilter)
                return;

            Entity settingsEntity = m_SettingsQuery.GetSingletonEntity();
            RenderingSettingsData settings = EntityManager.GetComponentData<RenderingSettingsData>(settingsEntity);
            bool active = m_ToolSystem.activeTool is AreaToolSystem
                && m_ToolSystem.activePrefab is CommuneAreaPrefab;
            bool hoveringCommune = m_SelectionSystem != null
                && m_SelectionSystem.HoveredCommune != Entity.Null;
            active |= hoveringCommune;

            if (active)
            {
                if (!m_HasOverride)
                {
                    m_PreviousHoveredColor = settings.m_HoveredColor;
                    m_HasOverride = true;
                }
                settings.m_HoveredColor = kCommunePreviewColor;
                EntityManager.SetComponentData(settingsEntity, settings);
            }
            else if (m_HasOverride)
            {
                settings.m_HoveredColor = m_PreviousHoveredColor;
                EntityManager.SetComponentData(settingsEntity, settings);
                m_HasOverride = false;
            }
        }

        protected override void OnDestroy()
        {
            if (m_HasOverride && !m_SettingsQuery.IsEmptyIgnoreFilter)
            {
                Entity settingsEntity = m_SettingsQuery.GetSingletonEntity();
                RenderingSettingsData settings = EntityManager.GetComponentData<RenderingSettingsData>(settingsEntity);
                settings.m_HoveredColor = m_PreviousHoveredColor;
                EntityManager.SetComponentData(settingsEntity, settings);
            }
            base.OnDestroy();
        }
    }
}
