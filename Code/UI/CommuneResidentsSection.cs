using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.UI.Binding;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Communes
{

public partial class CommuneResidentsSection : InfoSectionBase, IJsonWritable
    {

        private const string kNativeType = "Game.UI.InGame.ResidentsSection";

        private CommuneStatsSystem m_StatsSystem;

private bool isHousehold;
        private bool isDistrict;
        private int householdCount;
        private int maxHouseholds;
        private int residentCount;
        private int petCount;
        private HouseholdWealthKey wealthKey;
        private CommuneWealthData wealthData;
        private AgeData ageData;
        private EducationData educationData;

        protected override string group => "CommuneResidents";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_StatsSystem = World.GetOrCreateSystemManaged<CommuneStatsSystem>();
        }

        protected override void Reset()
        {
            isHousehold = false;
            isDistrict = true;   
            householdCount = 0;
            maxHouseholds = 0;
            residentCount = 0;
            petCount = 0;
            wealthKey = default;
            wealthData = default;
            ageData = default;
            educationData = default;
        }

protected override void OnUpdate()
        {
            visible = false;

Entity selected = selectedEntity;
            if (!EntityManager.HasComponent<Commune>(selected)
                || !EntityManager.HasComponent<Game.Areas.Geometry>(selected))
            {
                return;
            }

visible = true;

            if (!m_StatsSystem.Results.TryGetValue(selected, out CommuneStats stats))
            {
                return;
            }

            residentCount = stats.ResidentCount;
            householdCount = stats.HouseholdCount;
            maxHouseholds = stats.MaxHouseholds;
            petCount = stats.PetCount;

            ageData = stats.Age;
            educationData = stats.Education;

if (stats.Households.IsCreated)
            {
                wealthKey = CitizenUIUtils.GetAverageHouseholdWealth(
                    EntityManager,
                    stats.Households,
                    m_StatsSystem.CityHappinessParameters);
            }

wealthData = default;
            for (int i = 0; stats.Households.IsCreated && i < stats.Households.Length; i++)
            {
                wealthData += GetHouseholdEconomyData(stats.Households[i]);
            }
        }

protected override void OnProcess()
        {
        }

private CommuneWealthData GetHouseholdEconomyData(Entity household)
        {
            int rent = 0;
            int fees = 0;

            if (EntityManager.TryGetComponent(household, out Household householdData))
            {
                int rentShareDivisor = 1;
                if (EntityManager.TryGetComponent(household, out Game.Buildings.PropertyRenter propertyRenter))
                {
                    rent = propertyRenter.m_Rent;

                    if (EntityManager.TryGetBuffer(propertyRenter.m_Property, true, out DynamicBuffer<Game.Buildings.Renter> renters))
                    {
                        rentShareDivisor = Unity.Mathematics.math.max(renters.Length, 1);
                    }

                    if (EntityManager.TryGetBuffer(m_StatsSystem.City, true, out DynamicBuffer<ServiceFee> serviceFees))
                    {
                        if (EntityManager.TryGetComponent(propertyRenter.m_Property, out ElectricityConsumer electricity))
                        {
                            fees += (int)((float)electricity.m_FulfilledConsumption
                                * ServiceFeeSystem.GetFee(PlayerResource.Electricity, serviceFees));
                        }

                        if (EntityManager.TryGetComponent(propertyRenter.m_Property, out WaterConsumer water))
                        {
                            fees += (int)((float)water.m_FulfilledFresh
                                * ServiceFeeSystem.GetFee(PlayerResource.Water, serviceFees));
                            fees += (int)((float)water.m_FulfilledSewage
                                * ServiceFeeSystem.GetFee(PlayerResource.Water, serviceFees));
                        }

                        if (EntityManager.TryGetComponent(propertyRenter.m_Property, out PrefabRef propertyPrefab)
                            && EntityManager.TryGetComponent(propertyPrefab.m_Prefab, out ConsumptionData consumptionData))
                        {
                            fees += (int)(consumptionData.m_GarbageAccumulation
                                * ServiceFeeSystem.GetFee(PlayerResource.Garbage, serviceFees));
                        }
                    }
                }

                fees /= rentShareDivisor;

                return new CommuneWealthData
                {
                    income = householdData.m_Income,
                    rent = rent,
                    upkeep = Unity.Mathematics.math.abs(householdData.m_MoneySpendOnBuildingLevelingLastDay),
                    resourceCost = householdData.m_ShoppedValuePerDay,
                    fees = fees,
                };
            }

            return default;
        }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("isHousehold");
            writer.Write(isHousehold);
            writer.PropertyName("isDistrict");
            writer.Write(isDistrict);
            writer.PropertyName("householdCount");
            writer.Write(householdCount);
            writer.PropertyName("maxHouseholds");
            writer.Write(maxHouseholds);
            writer.PropertyName("residentCount");
            writer.Write(residentCount);
            writer.PropertyName("petCount");
            writer.Write(petCount);
            writer.PropertyName("wealthKey");
            writer.Write(wealthKey.ToString());
            writer.PropertyName("wealthData");
            writer.Write(wealthData);
            writer.PropertyName("residence");
            writer.WriteNull();
            writer.PropertyName("residenceEntity");
            writer.WriteNull();
            writer.PropertyName("residenceKey");
            writer.Write(CitizenResidenceKey.Home.ToString());
            writer.PropertyName("ageData");
            writer.Write(ageData);
            writer.PropertyName("educationData");
            writer.Write(educationData);
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
