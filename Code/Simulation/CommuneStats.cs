using Colossal.Mathematics;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Communes
{

public struct CommuneStats
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

public NativeList<Entity> Households;

        public bool IsEmpty => BuildingCount == 0 && HouseholdCount == 0 && ResidentCount == 0;

public static CommuneStats Create(Allocator allocator)
        {
            return new CommuneStats
            {
                Households = new NativeList<Entity>(16, allocator),
            };
        }

public void Reset()
        {
            BuildingCount = 0;
            HouseholdCount = 0;
            MaxHouseholds = 0;
            ResidentCount = 0;
            PetCount = 0;
            HappinessSum = 0;
            HappinessCitizenCount = 0;
            Age = default;
            Education = default;
            Households.Clear();
        }

        public void Dispose()
        {
            if (Households.IsCreated)
            {
                Households.Dispose();
            }
        }
    }
}
