using Game;
using Game.Tools;

namespace Communes
{

    public partial class CommuneShortcutSystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private CommuneToolPrefabSystem m_PrefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<CommuneToolPrefabSystem>();
        }

        protected override void OnUpdate()
        {
            if (Mod.m_ToggleToolAction == null || !Mod.m_ToggleToolAction.WasPerformedThisFrame())
                return;

            if (m_ToolSystem.activeTool is CommuneAreaToolSystem)
            {
                m_ToolSystem.ActivatePrefabTool(null);
                return;
            }

            if (m_PrefabSystem.ToolPrefab != null)
                m_ToolSystem.ActivatePrefabTool(m_PrefabSystem.ToolPrefab);
        }
    }
}
