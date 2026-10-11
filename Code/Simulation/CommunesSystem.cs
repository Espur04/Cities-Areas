
using Game;
using Game.Tools;

namespace Communes
{
    public partial class CommunesSystem : GameSystemBase
    {
        private ToolSystem m_ToolSystem;
        private bool m_Logged;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            Mod.log.Info("[Communes] Diagnostic des outils initialisé.");
        }

        protected override void OnUpdate()
        {
            if (m_Logged || m_ToolSystem == null)
                return;

            foreach (ToolBaseSystem tool in m_ToolSystem.tools)
            {
                if (tool is CommunesToolSystem)
                {
                    Mod.log.Info("[Communes] Notre outil est présent dans ToolSystem.tools.");
                    m_Logged = true;
                    break;
                }
            }
        }
    }
}
