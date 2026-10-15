using System.Collections.Generic;
using System.Reflection;
using Game;
using Game.UI.InGame;

namespace Communes
{

public partial class CommuneSectionRegistrar : GameSystemBase
    {
        private static readonly FieldInfo kMiddleSectionsField =
            typeof(SelectedInfoUISystem).GetField("m_MiddleSections", BindingFlags.Instance | BindingFlags.NonPublic);

        private bool m_LoggedOnce;

        protected override void OnUpdate()
        {
            SelectedInfoUISystem ui = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            if (ui == null)
            {
                return;
            }

            CommuneResidentsSection residents = World.GetOrCreateSystemManaged<CommuneResidentsSection>();
            CommuneHappinessSection happiness = World.GetOrCreateSystemManaged<CommuneHappinessSection>();
            CommunePanelSection panel = World.GetOrCreateSystemManaged<CommunePanelSection>();
            DistrictAreaSection districtArea = World.GetOrCreateSystemManaged<DistrictAreaSection>();

            bool addedMiddle = EnsurePresent(ui, kMiddleSectionsField, happiness);
            addedMiddle |= EnsurePresent(ui, kMiddleSectionsField, panel);
            addedMiddle |= EnsurePresent(ui, kMiddleSectionsField, residents);
            addedMiddle |= EnsurePresent(ui, kMiddleSectionsField, districtArea);

            if (addedMiddle && !m_LoggedOnce)
            {
                m_LoggedOnce = true;
                Mod.log.Info("[Communes] Sections statistiques communales réenregistrées dans SelectedInfoUISystem.");
            }
        }

private static bool EnsurePresent(SelectedInfoUISystem ui, FieldInfo field, ISectionSource section)
        {
            if (field == null)
            {
                return false;
            }

            if (!(field.GetValue(ui) is List<ISectionSource> list))
            {
                return false;
            }

for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], section))
                {
                    return false;
                }
            }

            list.Add(section);
            return true;
        }
    }
}
