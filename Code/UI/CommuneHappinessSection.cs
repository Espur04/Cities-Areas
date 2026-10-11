using Colossal.UI.Binding;
using Game.UI.InGame;
using Unity.Entities;

namespace Communes
{

public partial class CommuneHappinessSection : InfoSectionBase, IJsonWritable
    {
        private const string kNativeType = "Game.UI.InGame.AverageHappinessSection";

        private CommuneStatsSystem m_StatsSystem;

        private CitizenHappiness averageHappiness;

        protected override string group => "CommuneHappiness";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_StatsSystem = World.GetOrCreateSystemManaged<CommuneStatsSystem>();
        }

        protected override void Reset()
        {
            averageHappiness = CitizenUIUtils.GetCitizenHappiness(0);
        }

protected override void OnUpdate()
        {
            visible = false;

            Entity selected = selectedEntity;
            if (!EntityManager.HasComponent<Commune>(selected))
            {
                return;
            }

            if (!m_StatsSystem.Results.TryGetValue(selected, out CommuneStats stats)
                || stats.HappinessCitizenCount <= 0)
            {
                return;
            }

            visible = true;

            int average = stats.HappinessSum / stats.HappinessCitizenCount;
            averageHappiness = CitizenUIUtils.GetCitizenHappiness(average);
        }

protected override void OnProcess()
        {
        }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("averageHappiness");
            writer.Write(averageHappiness);

writer.PropertyName("happinessFactors");
            writer.ArrayBegin(0u);
            writer.ArrayEnd();
        }

public new void Write(IJsonWriter writer)
        {
            if (!visible)
            {
                writer.WriteNull();
                return;
            }

            writer.TypeBegin(kNativeType);
            writer.PropertyName("group");
            writer.Write(group);
            writer.PropertyName("tooltipKeys");
            writer.ArrayBegin(0u);
            writer.ArrayEnd();
            writer.PropertyName("tooltipTags");
            writer.ArrayBegin(0u);
            writer.ArrayEnd();
            OnWriteProperties(writer);
            writer.TypeEnd();
        }
    }
}
