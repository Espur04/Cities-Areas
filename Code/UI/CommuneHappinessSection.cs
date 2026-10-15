using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.UI.InGame;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Communes
{
    public partial class CommuneHappinessSection : InfoSectionBase, IJsonWritable
    {
        private const string kNativeType = "Communes.CommuneHappinessSection";
        private const int kHappinessFactorCount = 29;

        private CommuneStatsSystem m_StatsSystem;
        private GroundPollutionSystem m_GroundPollutionSystem;
        private NoisePollutionSystem m_NoisePollutionSystem;
        private AirPollutionSystem m_AirPollutionSystem;
        private TelecomCoverageSystem m_TelecomCoverageSystem;
        private TaxSystem m_TaxSystem;
        private CitySystem m_CitySystem;
        private LocalEffectSystem m_LocalEffectSystem;

        private EntityQuery m_CitizenHappinessParameterQuery;
        private EntityQuery m_GarbageParameterQuery;
        private EntityQuery m_HappinessFactorParameterQuery;
        private EntityQuery m_HealthcareParameterQuery;
        private EntityQuery m_ParkParameterQuery;
        private EntityQuery m_EducationParameterQuery;
        private EntityQuery m_TelecomParameterQuery;
        private EntityQuery m_ServiceFeeParameterQuery;

        private NativeArray<int2> m_Factors;
        private readonly List<FactorInfo> m_HappinessFactors = new List<FactorInfo>(10);
        private Game.UI.InGame.CitizenHappiness m_AverageHappiness;
        private Entity m_CachedCommune = Entity.Null;
        private uint m_CachedStatsRevision;

        protected override string group => "AverageHappinessSection";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_StatsSystem = World.GetOrCreateSystemManaged<CommuneStatsSystem>();
            m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
            m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
            m_AirPollutionSystem = World.GetOrCreateSystemManaged<AirPollutionSystem>();
            m_TelecomCoverageSystem = World.GetOrCreateSystemManaged<TelecomCoverageSystem>();
            m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            m_LocalEffectSystem = World.GetOrCreateSystemManaged<LocalEffectSystem>();

            m_CitizenHappinessParameterQuery = GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
            m_GarbageParameterQuery = GetEntityQuery(ComponentType.ReadOnly<GarbageParameterData>());
            m_HappinessFactorParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HappinessFactorParameterData>());
            m_HealthcareParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>());
            m_ParkParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ParkParameterData>());
            m_EducationParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EducationParameterData>());
            m_TelecomParameterQuery = GetEntityQuery(ComponentType.ReadOnly<TelecomParameterData>());
            m_ServiceFeeParameterQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ServiceFeeParameterData>() },
                Options = EntityQueryOptions.IncludeSystems,
            });

            m_Factors = new NativeArray<int2>(kHappinessFactorCount, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (m_Factors.IsCreated)
                m_Factors.Dispose();
            base.OnDestroy();
        }

        protected override void Reset()
        {
            m_CachedStatsRevision = uint.MaxValue;
            if (m_CachedCommune == Entity.Null)
            {
                m_AverageHappiness = CitizenUIUtils.GetCitizenHappiness(0);
                m_HappinessFactors.Clear();
                if (m_Factors.IsCreated)
                {
                    for (int i = 0; i < m_Factors.Length; i++)
                        m_Factors[i] = int2.zero;
                }
            }
        }

        protected override void OnUpdate()
        {
            visible = false;
            Entity commune = selectedEntity;
            if (!EntityManager.HasComponent<Commune>(commune))
            {
                m_CachedCommune = Entity.Null;
                return;
            }

            m_StatsSystem.Refresh(commune);
            if (!m_StatsSystem.Results.TryGetValue(commune, out CommuneStats stats)
                || stats.HappinessCitizenCount <= 0)
            {
                visible = commune == m_CachedCommune;
                return;
            }

            uint revision = m_StatsSystem.GetRefreshRevision(commune);
            if (commune != m_CachedCommune || revision != m_CachedStatsRevision)
            {
                m_CachedCommune = commune;
                m_CachedStatsRevision = revision;
                m_AverageHappiness = CitizenUIUtils.GetCitizenHappiness(stats.HappinessSum / stats.HappinessCitizenCount);
                RebuildHappinessFactors(commune);
            }

            visible = true;
        }

        protected override void OnProcess()
        {
        }

        private bool ParametersReady()
        {
            return !m_CitizenHappinessParameterQuery.IsEmptyIgnoreFilter
                && !m_GarbageParameterQuery.IsEmptyIgnoreFilter
                && !m_HappinessFactorParameterQuery.IsEmptyIgnoreFilter
                && !m_HealthcareParameterQuery.IsEmptyIgnoreFilter
                && !m_ParkParameterQuery.IsEmptyIgnoreFilter
                && !m_EducationParameterQuery.IsEmptyIgnoreFilter
                && !m_TelecomParameterQuery.IsEmptyIgnoreFilter
                && !m_ServiceFeeParameterQuery.IsEmptyIgnoreFilter;
        }

        private void RebuildHappinessFactors(Entity commune)
        {
            m_HappinessFactors.Clear();
            if (!m_Factors.IsCreated || !ParametersReady()
                || !EntityManager.HasBuffer<CommuneBuildingMember>(commune)
                || !EntityManager.HasBuffer<ServiceFee>(m_CitySystem.City))
                return;

            for (int i = 0; i < m_Factors.Length; i++)
                m_Factors[i] = int2.zero;

            CitizenHappinessParameterData happinessParameters = m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>();
            GarbageParameterData garbageParameters = m_GarbageParameterQuery.GetSingleton<GarbageParameterData>();
            HealthcareParameterData healthcareParameters = m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>();
            ParkParameterData parkParameters = m_ParkParameterQuery.GetSingleton<ParkParameterData>();
            EducationParameterData educationParameters = m_EducationParameterQuery.GetSingleton<EducationParameterData>();
            TelecomParameterData telecomParameters = m_TelecomParameterQuery.GetSingleton<TelecomParameterData>();
            ServiceFeeParameterData serviceFeeParameters = m_ServiceFeeParameterQuery.GetSingleton<ServiceFeeParameterData>();
            Entity happinessParametersEntity = m_HappinessFactorParameterQuery.GetSingletonEntity();
            DynamicBuffer<HappinessFactorParameterData> happinessFactorParameters = EntityManager.GetBuffer<HappinessFactorParameterData>(happinessParametersEntity, true);

            var groundData = m_GroundPollutionSystem.GetData(readOnly: true, out JobHandle groundDependency);
            var noiseData = m_NoisePollutionSystem.GetData(readOnly: true, out JobHandle noiseDependency);
            var airData = m_AirPollutionSystem.GetData(readOnly: true, out JobHandle airDependency);
            CellMapData<TelecomCoverage> telecomCoverage = m_TelecomCoverageSystem.GetData(readOnly: true, out JobHandle telecomDependency);
            groundDependency.Complete();
            noiseDependency.Complete();
            airDependency.Complete();
            telecomDependency.Complete();

            DynamicBuffer<ServiceFee> cityFees = EntityManager.GetBuffer<ServiceFee>(m_CitySystem.City, true);
            float electricityDefault = serviceFeeParameters.m_ElectricityFee.m_Default;
            float waterDefault = serviceFeeParameters.m_WaterFee.m_Default;
            float relativeElectricityFee = electricityDefault != 0f
                ? ServiceFeeSystem.GetFee(PlayerResource.Electricity, cityFees) / electricityDefault : 0f;
            float relativeWaterFee = waterDefault != 0f
                ? ServiceFeeSystem.GetFee(PlayerResource.Water, cityFees) / waterDefault : 0f;
            LocalEffectSystem.ReadData localEffectData = m_LocalEffectSystem.GetReadData(out JobHandle localEffectDependency);

            DynamicBuffer<CommuneBuildingMember> memberBuffer = EntityManager.GetBuffer<CommuneBuildingMember>(commune, true);
            var job = new CommuneBuildingHappinessFactorsJob
            {
                Members = memberBuffer.AsNativeArray(),
                Factors = m_Factors,
                TaxRates = m_TaxSystem.GetTaxRates(),
                City = m_CitySystem.City,
                Prefabs = GetComponentLookup<PrefabRef>(true),
                SpawnableBuildings = GetComponentLookup<SpawnableBuildingData>(true),
                BuildingPropertyDatas = GetComponentLookup<BuildingPropertyData>(true),
                CityModifiers = GetBufferLookup<CityModifier>(true),
                Buildings = GetComponentLookup<Building>(true),
                ResidentialProperties = GetComponentLookup<ResidentialProperty>(true),
                ElectricityConsumers = GetComponentLookup<ElectricityConsumer>(true),
                WaterConsumers = GetComponentLookup<WaterConsumer>(true),
                ServiceCoverages = GetBufferLookup<Game.Net.ServiceCoverage>(true),
                Locked = GetComponentLookup<Locked>(true),
                Transforms = GetComponentLookup<Transform>(true),
                GarbageProducers = GetComponentLookup<GarbageProducer>(true),
                CrimeProducers = GetComponentLookup<CrimeProducer>(true),
                MailProducers = GetComponentLookup<MailProducer>(true),
                Renters = GetBufferLookup<Renter>(true),
                Citizens = GetComponentLookup<Citizen>(true),
                HouseholdCitizens = GetBufferLookup<HouseholdCitizen>(true),
                BuildingDatas = GetComponentLookup<BuildingData>(true),
                LocalEffectData = localEffectData,
                HappinessParameters = happinessParameters,
                GarbageParameters = garbageParameters,
                HealthcareParameters = healthcareParameters,
                ParkParameters = parkParameters,
                EducationParameters = educationParameters,
                TelecomParameters = telecomParameters,
                HappinessFactorParameters = happinessFactorParameters,
                GroundPollution = groundData.m_Buffer,
                NoisePollution = noiseData.m_Buffer,
                AirPollution = airData.m_Buffer,
                TelecomCoverage = telecomCoverage,
                RelativeElectricityFee = relativeElectricityFee,
                RelativeWaterFee = relativeWaterFee,
            };
            job.Schedule(JobHandle.CombineDependencies(Dependency, localEffectDependency)).Complete();

            for (int i = 0; i < m_Factors.Length; i++)
            {
                int count = m_Factors[i].x;
                if (count <= 0)
                    continue;

                int weight = (int)math.round((float)m_Factors[i].y / count);
                if (weight != 0)
                    m_HappinessFactors.Add(new FactorInfo(i, weight));
            }
            m_HappinessFactors.Sort();
        }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            writer.PropertyName("averageHappiness");
            writer.Write(m_AverageHappiness);
            writer.PropertyName("language");
            writer.Write((int)Mod.EffectiveLanguage);
            int count = Math.Min(10, m_HappinessFactors.Count);
            writer.PropertyName("happinessFactors");
            writer.ArrayBegin((uint)count);
            for (int i = 0; i < count; i++)
                m_HappinessFactors[i].WriteBuildingHappinessFactor(writer);
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
            writer.ArrayBegin(1u);
            writer.Write("District");
            writer.ArrayEnd();
            writer.PropertyName("tooltipTags");
            writer.ArrayBegin(0u);
            writer.ArrayEnd();
            OnWriteProperties(writer);
            writer.TypeEnd();
        }
    }

    [BurstCompile]
    internal struct CommuneBuildingHappinessFactorsJob : IJob
    {
        [ReadOnly] public NativeArray<CommuneBuildingMember> Members;
        public NativeArray<int2> Factors;
        [ReadOnly] public NativeArray<int> TaxRates;
        [ReadOnly] public Entity City;

        [ReadOnly] public ComponentLookup<PrefabRef> Prefabs;
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> SpawnableBuildings;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> BuildingPropertyDatas;
        [ReadOnly] public BufferLookup<CityModifier> CityModifiers;
        [ReadOnly] public ComponentLookup<Building> Buildings;
        [ReadOnly] public ComponentLookup<ResidentialProperty> ResidentialProperties;
        [ReadOnly] public ComponentLookup<ElectricityConsumer> ElectricityConsumers;
        [ReadOnly] public ComponentLookup<WaterConsumer> WaterConsumers;
        [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> ServiceCoverages;
        [ReadOnly] public ComponentLookup<Locked> Locked;
        [ReadOnly] public ComponentLookup<Transform> Transforms;
        [ReadOnly] public ComponentLookup<GarbageProducer> GarbageProducers;
        [ReadOnly] public ComponentLookup<CrimeProducer> CrimeProducers;
        [ReadOnly] public ComponentLookup<MailProducer> MailProducers;
        [ReadOnly] public BufferLookup<Renter> Renters;
        [ReadOnly] public ComponentLookup<Citizen> Citizens;
        [ReadOnly] public BufferLookup<HouseholdCitizen> HouseholdCitizens;
        [ReadOnly] public ComponentLookup<BuildingData> BuildingDatas;
        [ReadOnly] public LocalEffectSystem.ReadData LocalEffectData;

        public CitizenHappinessParameterData HappinessParameters;
        public GarbageParameterData GarbageParameters;
        public HealthcareParameterData HealthcareParameters;
        public ParkParameterData ParkParameters;
        public EducationParameterData EducationParameters;
        public TelecomParameterData TelecomParameters;
        [ReadOnly] public DynamicBuffer<HappinessFactorParameterData> HappinessFactorParameters;
        [ReadOnly] public NativeArray<GroundPollution> GroundPollution;
        [ReadOnly] public NativeArray<NoisePollution> NoisePollution;
        [ReadOnly] public NativeArray<AirPollution> AirPollution;
        [ReadOnly] public CellMapData<TelecomCoverage> TelecomCoverage;
        public float RelativeElectricityFee;
        public float RelativeWaterFee;

        public void Execute()
        {
            for (int i = 0; i < Members.Length; i++)
            {
                Entity building = Members[i].m_Building;
                if (!Buildings.HasComponent(building)
                    || !ResidentialProperties.HasComponent(building)
                    || !Prefabs.HasComponent(building)
                    || !Renters.HasBuffer(building))
                    continue;

                Entity prefab = Prefabs[building].m_Prefab;
                if (!SpawnableBuildings.HasComponent(prefab))
                    continue;

                BuildingHappiness.GetResidentialBuildingHappinessFactors(
                    City, TaxRates, building, Factors,
                    ref Prefabs, ref SpawnableBuildings, ref BuildingPropertyDatas,
                    ref CityModifiers, ref Buildings, ref ElectricityConsumers,
                    ref WaterConsumers, ref ServiceCoverages, ref Locked, ref Transforms,
                    ref GarbageProducers, ref CrimeProducers, ref MailProducers,
                    ref Renters, ref Citizens, ref HouseholdCitizens, ref BuildingDatas,
                    ref LocalEffectData, HappinessParameters, GarbageParameters,
                    HealthcareParameters, ParkParameters, EducationParameters,
                    TelecomParameters, HappinessFactorParameters, GroundPollution,
                    NoisePollution, AirPollution, TelecomCoverage,
                    RelativeElectricityFee, RelativeWaterFee);
            }
        }
    }
}
