using Colossal.UI.Binding;

namespace Communes
{

public struct CommuneWealthData : IJsonWritable
    {
        private const string kNativeType = "Game.UI.InGame.ResidentsSection+HouseholdWealthData";

        public int income;
        public int rent;
        public int upkeep;
        public uint resourceCost;
        public int fees;

        public static CommuneWealthData operator +(CommuneWealthData left, CommuneWealthData right)
        {
            return new CommuneWealthData
            {
                income = left.income + right.income,
                rent = left.rent + right.rent,
                upkeep = left.upkeep + right.upkeep,
                resourceCost = left.resourceCost + right.resourceCost,
                fees = left.fees + right.fees,
            };
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(kNativeType);
            writer.PropertyName("income");
            writer.Write(income);
            writer.PropertyName("rent");
            writer.Write(rent);
            writer.PropertyName("upkeep");
            writer.Write(upkeep);
            writer.PropertyName("resourceCost");
            writer.Write(resourceCost);
            writer.PropertyName("fees");
            writer.Write(fees);
            writer.TypeEnd();
        }
    }
}
