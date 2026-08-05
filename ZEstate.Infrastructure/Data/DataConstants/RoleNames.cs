namespace ZEstate.Infrastructure.Data.DataConstants
{
    public static class RoleNames
    {
        public const string HouseManager = "HouseManager";
        public const string Resident = "Resident";
        public const string Cashier = "Cashier";
        public const string Administrator = "Administrator";

        public static readonly string[] All = { HouseManager, Resident, Cashier, Administrator };

        // Roles assignable through the role-change endpoint (see UsersController).
        // Administrator and HouseManager are structural roles granted manually for now.
        public static readonly string[] Assignable = { Resident, Cashier };
    }
}
