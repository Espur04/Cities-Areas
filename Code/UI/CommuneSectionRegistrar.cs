using Game;
using Game.UI.InGame;

namespace Communes
{

public partial class CommuneSectionRegistrar : GameSystemBase
    {
        private SelectedInfoUISystem m_RegisteredSystem;

        protected override void OnUpdate()
        {
            SelectedInfoUISystem ui = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            if (ui == null)
            {
                return;
            }

            if (ReferenceEquals(ui, m_RegisteredSystem))
            {
                return;
            }

            CommuneResidentsSection residents = World.GetOrCreateSystemManaged<CommuneResidentsSection>();
            CommuneHappinessSection happiness = World.GetOrCreateSystemManaged<CommuneHappinessSection>();
            CommunePanelSection panel = World.GetOrCreateSystemManaged<CommunePanelSection>();
            DistrictAreaSection districtArea = World.GetOrCreateSystemManaged<DistrictAreaSection>();

            ui.AddMiddleSection(happiness);
            ui.AddMiddleSection(panel);
            ui.AddMiddleSection(residents);
            ui.AddMiddleSection(districtArea);
            ui.SetDirty();
            ui.RequestUpdate();
            m_RegisteredSystem = ui;
            Mod.log.Info("[Communes] Sections communales ajoutées au panneau d'informations.");
        }
    }
}
