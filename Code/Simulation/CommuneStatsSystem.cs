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
using Unity.Jobs;
using Unity.Mathematics;
using Colossal.Serialization.Entities;
using System;
using System.Collections.Generic;

namespace Communes
{

public partial class CommuneStatsSystem : GameSystemBase
    {
        private EntityQuery m_HappinessParameterQuery;

private NativeParallelHashMap<Entity, CommuneStats> m_Results;
        private readonly Dictionary<Entity, long> m_LastRefreshTime = new Dictionary<Entity, long>();
        private readonly HashSet<Entity> m_DirtyCommunes = new HashSet<Entity>();
        private readonly Dictionary<Entity, uint> m_RefreshRevisions = new Dictionary<Entity, uint>();

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
            m_LastRefreshTime.Clear();
            m_DirtyCommunes.Clear();
            m_RefreshRevisions.Clear();
        }

        protected override void OnUpdate()
        {
        }

        public bool Refresh(Entity commune)
        {
            long now = DateTime.UtcNow.Ticks;
            bool hasResult = m_Results.ContainsKey(commune);
            bool dirty = m_DirtyCommunes.Contains(commune);
            if (hasResult && !dirty
                && m_LastRefreshTime.TryGetValue(commune, out long previous)
                && now - previous < TimeSpan.FromSeconds(30).Ticks)
                return false;
            if (hasResult && dirty && m_LastRefreshTime.TryGetValue(commune, out previous)
                && now - previous < TimeSpan.TicksPerSecond)
                return false;

            m_LastRefreshTime[commune] = now;
            if (!EntityManager.Exists(commune)
                || !EntityManager.HasComponent<Commune>(commune)
                || !EntityManager.HasBuffer<CommuneBuildingMember>(commune))
                return false;

            if (m_Results.TryGetValue(commune, out CommuneStats existing))
            {
                existing.Reset();
                m_Results[commune] = existing;
            }
            else
            {
                m_Results[commune] = CommuneStats.Create(Allocator.Persistent);
            }

            bool hasHappinessParameters = !m_HappinessParameterQuery.IsEmptyIgnoreFilter;
            DynamicBuffer<CommuneBuildingMember> members = EntityManager.GetBuffer<CommuneBuildingMember>(commune, true);
            NativeArray<CommuneBuildingMember> memberArray = members.AsNativeArray();
            NativeArray<CommuneBuildingStats> buildingStats = new NativeArray<CommuneBuildingStats>(members.Length, Allocator.TempJob);
            try
            {
                if (members.Length > 0)
                {
                    new CommuneBuildingStatsJob
                    {
                        Members = memberArray,
                        Buildings = GetComponentLookup<Building>(true),
                        ResidentialProperties = GetComponentLookup<ResidentialProperty>(true),
                        Deleted = GetComponentLookup<Deleted>(true),
                        Temp = GetComponentLookup<Temp>(true),
                        PrefabRefs = GetComponentLookup<PrefabRef>(true),
                        PropertyData = GetComponentLookup<BuildingPropertyData>(true),
                        Parks = GetComponentLookup<Game.Buildings.Park>(true),
                        Abandoned = GetComponentLookup<Abandoned>(true),
                        Households = GetComponentLookup<Household>(true),
                        Renters = GetBufferLookup<Renter>(true),
                        HouseholdCitizens = GetBufferLookup<HouseholdCitizen>(true),
                        HouseholdAnimals = GetBufferLookup<HouseholdAnimal>(true),
                        Citizens = GetComponentLookup<Citizen>(true),
                        HealthProblems = GetComponentLookup<HealthProblem>(true),
                        TravelPurposes = GetComponentLookup<TravelPurpose>(true),
                        HappinessParametersAvailable = hasHappinessParameters,
                        Results = buildingStats,
                    }.Schedule(members.Length, 64, Dependency).Complete();
                }

                CommuneStats stats = m_Results[commune];
                for (int b = 0; b < members.Length; b++)
                {
                    CommuneBuildingStats building = buildingStats[b];
                    stats.BuildingCount += building.BuildingCount;
                    stats.HouseholdCount += building.HouseholdCount;
                    stats.MaxHouseholds += building.MaxHouseholds;
                    stats.ResidentCount += building.ResidentCount;
                    stats.PetCount += building.PetCount;
                    stats.HappinessSum += building.HappinessSum;
                    stats.HappinessCitizenCount += building.HappinessCitizenCount;
                    stats.Age += building.Age;
                    stats.Education += building.Education;
                    CollectHouseholds(members[b].m_Building, ref stats);
                }
                m_Results[commune] = stats;
                m_RefreshRevisions.TryGetValue(commune, out uint revision);
                m_RefreshRevisions[commune] = revision + 1;
            }
            finally
            {
                buildingStats.Dispose();
            }
            m_DirtyCommunes.Remove(commune);
            return true;
        }

        public void MarkDirty(Entity commune) => m_DirtyCommunes.Add(commune);

        public uint GetRefreshRevision(Entity commune) =>
            m_RefreshRevisions.TryGetValue(commune, out uint revision) ? revision : 0;

        private void CollectHouseholds(Entity building, ref CommuneStats stats)
        {
            if (!EntityManager.Exists(building)
                || EntityManager.HasComponent<Deleted>(building)
                || EntityManager.HasComponent<Temp>(building)
                || !TryCountNativeResidentialBuilding(building)
                || !EntityManager.TryGetBuffer(building, true, out DynamicBuffer<Renter> renters))
                return;

            for (int i = 0; i < renters.Length; i++)
            {
                Entity household = renters[i].m_Renter;
                if (EntityManager.HasComponent<Household>(household)
                    && EntityManager.HasBuffer<HouseholdCitizen>(household))
                    stats.Households.Add(household);
            }
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
            return EntityManager.HasComponent<ResidentialProperty>(building)
                && hasRenters && ((hasResidentialPrefab && !abandoned) || park || abandoned);
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
