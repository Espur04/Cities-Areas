using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI.InGame;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Communes
{
    internal struct CommuneBuildingStats
    {
        public int BuildingCount;
        public int HouseholdCount;
        public int MaxHouseholds;
        public int ResidentCount;
        public int PetCount;
        public int HappinessSum;
        public int HappinessCitizenCount;
        public AgeData Age;
        public EducationData Education;
    }

    internal struct CommuneBuildingStatsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CommuneBuildingMember> Members;
        [ReadOnly] public ComponentLookup<Building> Buildings;
        [ReadOnly] public ComponentLookup<ResidentialProperty> ResidentialProperties;
        [ReadOnly] public ComponentLookup<Deleted> Deleted;
        [ReadOnly] public ComponentLookup<Temp> Temp;
        [ReadOnly] public ComponentLookup<PrefabRef> PrefabRefs;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> PropertyData;
        [ReadOnly] public ComponentLookup<Game.Buildings.Park> Parks;
        [ReadOnly] public ComponentLookup<Abandoned> Abandoned;
        [ReadOnly] public ComponentLookup<Household> Households;
        [ReadOnly] public BufferLookup<Renter> Renters;
        [ReadOnly] public BufferLookup<HouseholdCitizen> HouseholdCitizens;
        [ReadOnly] public BufferLookup<HouseholdAnimal> HouseholdAnimals;
        [ReadOnly] public ComponentLookup<Citizen> Citizens;
        [ReadOnly] public ComponentLookup<HealthProblem> HealthProblems;
        [ReadOnly] public ComponentLookup<TravelPurpose> TravelPurposes;
        [ReadOnly] public bool HappinessParametersAvailable;
        [WriteOnly] public NativeArray<CommuneBuildingStats> Results;

        public void Execute(int index)
        {
            Results[index] = default;
            ComponentLookup<HealthProblem> healthProblems = HealthProblems;
            ComponentLookup<TravelPurpose> travelPurposes = TravelPurposes;
            Entity building = Members[index].m_Building;
            if (!Buildings.HasComponent(building) || !ResidentialProperties.HasComponent(building)
                || Deleted.HasComponent(building) || Temp.HasComponent(building)
                || !PrefabRefs.TryGetComponent(building, out PrefabRef prefabRef)
                || !Renters.TryGetBuffer(building, out DynamicBuffer<Renter> renters)
                || renters.Length == 0)
                return;

            bool abandoned = Abandoned.HasComponent(building);
            bool park = Parks.HasComponent(building);
            bool residentialPrefab = PropertyData.TryGetComponent(prefabRef.m_Prefab,
                out BuildingPropertyData propertyData) && propertyData.m_ResidentialProperties > 0;
            if (!((residentialPrefab && !abandoned) || park || abandoned))
                return;

            CommuneBuildingStats result = new CommuneBuildingStats
            {
                BuildingCount = 1,
                MaxHouseholds = math.max(propertyData.m_ResidentialProperties, 0),
            };

            for (int r = 0; r < renters.Length; r++)
            {
                Entity household = renters[r].m_Renter;
                if (!Households.HasComponent(household)
                    || !HouseholdCitizens.TryGetBuffer(household, out DynamicBuffer<HouseholdCitizen> citizens))
                    continue;

                result.HouseholdCount++;
                for (int c = 0; c < citizens.Length; c++)
                {
                    Entity citizenEntity = citizens[c].m_Citizen;
                    if (CitizenUtils.IsCorpsePickedByHearse(citizenEntity,
                        ref healthProblems, ref travelPurposes))
                        continue;

                    result.ResidentCount++;
                    if (!Citizens.TryGetComponent(citizenEntity, out Citizen citizen))
                        continue;

                    switch (citizen.GetAge())
                    {
                        case CitizenAge.Child: result.Age += new AgeData(1, 0, 0, 0); break;
                        case CitizenAge.Teen: result.Age += new AgeData(0, 1, 0, 0); break;
                        case CitizenAge.Adult: result.Age += new AgeData(0, 0, 1, 0); break;
                        case CitizenAge.Elderly: result.Age += new AgeData(0, 0, 0, 1); break;
                    }
                    result.Education += EducationFor(citizen);
                    if (HappinessParametersAvailable && !CitizenUtils.IsDead(citizenEntity, ref healthProblems))
                    {
                        result.HappinessSum += citizen.Happiness;
                        result.HappinessCitizenCount++;
                    }
                }

                if (HouseholdAnimals.TryGetBuffer(household, out DynamicBuffer<HouseholdAnimal> animals))
                    result.PetCount += animals.Length;
            }

            Results[index] = result;
        }

        private static EducationData EducationFor(Citizen citizen)
        {
            switch (citizen.GetEducationLevel())
            {
                case 0: return new EducationData(1, 0, 0, 0, 0);
                case 1: return new EducationData(0, 1, 0, 0, 0);
                case 2: return new EducationData(0, 0, 1, 0, 0);
                case 3: return new EducationData(0, 0, 0, 1, 0);
                case 4: return new EducationData(0, 0, 0, 0, 1);
                default: return default;
            }
        }
    }
}
