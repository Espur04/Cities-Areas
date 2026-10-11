using Colossal.Entities;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Colossal.Serialization.Entities;

namespace Communes
{

public partial class CommuneStatsSystem : GameSystemBase
    {
        private EntityQuery m_CommuneQuery;
        private EntityQuery m_ResidentialCountQuery;
        private EntityQuery m_HappinessParameterQuery;

private NativeParallelHashMap<Entity, CommuneStats> m_Results;

private int m_DiagnosticTick;
        private int m_UpdatesUntilRecalculation;

        public NativeParallelHashMap<Entity, CommuneStats> Results => m_Results;

public CitizenHappinessParameterData CityHappinessParameters
        {
            get
            {
                if (m_HappinessParameterQuery.IsEmptyIgnoreFilter)
                {
                    return default;
                }
                return m_HappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>();
            }
        }

        private CitySystem m_CitySystem;

public Entity City
        {
            get
            {
                if (m_CitySystem == null)
                {
                    m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
                }
                return m_CitySystem.City;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

m_CommuneQuery = GetEntityQuery(
                ComponentType.ReadOnly<Commune>(),
                ComponentType.ReadOnly<Geometry>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Triangle>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            m_ResidentialCountQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());

            m_HappinessParameterQuery = GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());

            m_Results = new NativeParallelHashMap<Entity, CommuneStats>(16, Allocator.Persistent);

            Mod.log.Info("[Communes] CommuneStatsSystem initialisé.");
        }

        protected override void OnDestroy()
        {
            foreach (var kv in m_Results)
            {
                kv.Value.Dispose();
            }
            m_Results.Dispose();
            base.OnDestroy();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            foreach (var entry in m_Results)
                entry.Value.Dispose();
            m_Results.Clear();
            m_UpdatesUntilRecalculation = 0;
            m_DiagnosticTick = 0;
        }

        protected override void OnUpdate()
        {

            if (m_CommuneQuery.IsEmptyIgnoreFilter)
            {
                m_UpdatesUntilRecalculation = 0;
                return;
            }

if (m_UpdatesUntilRecalculation > 0)
            {
                m_UpdatesUntilRecalculation--;
                return;
            }
            m_UpdatesUntilRecalculation = 31;

            NativeArray<Entity> communes = m_CommuneQuery.ToEntityArray(Allocator.Temp);

if (m_DiagnosticTick++ % 256 == 0)
            {
                Mod.log.Info($"[Communes][Diag] {communes.Length} commune(s) détectée(s), "
                    + $"{m_ResidentialCountQuery.CalculateEntityCount()} bâtiment(s) résidentiel(s) indexé(s).");
            }

for (int i = 0; i < communes.Length; i++)
            {
                Entity commune = communes[i];
                if (m_Results.TryGetValue(commune, out CommuneStats existing))
                {
                    existing.Reset();
                    m_Results[commune] = existing;
                }
                else
                {
                    m_Results[commune] = CommuneStats.Create(Allocator.Persistent);
                }
            }

for (int c = 0; c < communes.Length; c++)
            {
                bool hasHappinessParameters = !m_HappinessParameterQuery.IsEmptyIgnoreFilter;
                Entity commune = communes[c];
                if (!EntityManager.HasBuffer<CommuneBuildingMember>(commune))
                    continue;

                DynamicBuffer<CommuneBuildingMember> members = EntityManager.GetBuffer<CommuneBuildingMember>(commune, true);
                for (int b = 0; b < members.Length; b++)
                {
                    Entity building = members[b].m_Building;
                    if (!EntityManager.Exists(building)
                        || EntityManager.HasComponent<Deleted>(building)
                        || EntityManager.HasComponent<Temp>(building)
                        || !EntityManager.HasComponent<Building>(building))
                    {
                        continue;
                    }
                    if (!TryCountNativeResidentialBuilding(building)) continue;
                    Accumulate(commune, building, hasHappinessParameters);
                }
            }

            communes.Dispose();
        }

private void Accumulate(Entity commune, Entity building, bool hasHappinessParameters)
        {
            CommuneStats stats = m_Results[commune];
            stats.BuildingCount++;

            if (EntityManager.TryGetComponent(building, out PrefabRef prefabRef))
            {
                BuildingPropertyData propertyData;
                if (EntityManager.TryGetComponent(prefabRef.m_Prefab, out propertyData))
                {

                    stats.MaxHouseholds += math.max(propertyData.m_ResidentialProperties, 0);
                }
            }

            if (EntityManager.TryGetBuffer(building, true, out DynamicBuffer<Renter> renters))
            {
                for (int i = 0; i < renters.Length; i++)
                {
                    Entity household = renters[i].m_Renter;

if (!EntityManager.HasComponent<Household>(household))
                    {
                        continue;
                    }

                    if (!EntityManager.TryGetBuffer(household, true, out DynamicBuffer<HouseholdCitizen> citizens))
                    {
                        continue;
                    }

                    stats.HouseholdCount++;
                    stats.Households.Add(household);

                    for (int j = 0; j < citizens.Length; j++)
                    {
                        Entity citizenEntity = citizens[j].m_Citizen;

                        if (!EntityManager.TryGetComponent(citizenEntity, out Citizen citizen))
                        {
                            continue;
                        }

if (CitizenUtils.IsCorpsePickedByHearse(EntityManager, citizenEntity))
                        {
                            continue;
                        }

                        stats.ResidentCount++;

                        switch (citizen.GetAge())
                        {
                            case CitizenAge.Child:
                                stats.Age += new AgeData(1, 0, 0, 0);
                                break;
                            case CitizenAge.Teen:
                                stats.Age += new AgeData(0, 1, 0, 0);
                                break;
                            case CitizenAge.Adult:
                                stats.Age += new AgeData(0, 0, 1, 0);
                                break;
                            case CitizenAge.Elderly:
                                stats.Age += new AgeData(0, 0, 0, 1);
                                break;
                        }

                        stats.Education += EducationFor(citizen);

if (hasHappinessParameters && !CitizenUtils.IsDead(EntityManager, citizenEntity))
                        {
                            stats.HappinessSum += citizen.Happiness;
                            stats.HappinessCitizenCount++;
                        }
                    }

                    if (EntityManager.TryGetBuffer(household, true, out DynamicBuffer<HouseholdAnimal> animals))
                    {
                        stats.PetCount += animals.Length;
                    }
                }
            }

            m_Results[commune] = stats;
        }

private bool TryCountNativeResidentialBuilding(Entity building)
        {
            bool abandoned = EntityManager.HasComponent<Abandoned>(building);
            bool hasRenters = EntityManager.TryGetBuffer(building, true, out DynamicBuffer<Renter> renters)
                && renters.Length > 0;
            bool park = EntityManager.HasComponent<Game.Buildings.Park>(building);
            bool hasResidentialPrefab = EntityManager.TryGetComponent(building, out PrefabRef prefabRef)
                && EntityManager.TryGetComponent(prefabRef.m_Prefab, out BuildingPropertyData propertyData)
                && propertyData.m_ResidentialProperties > 0;
            return hasRenters && ((hasResidentialPrefab && !abandoned) || park || abandoned);
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
