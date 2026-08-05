namespace ZEstateApi.Authorization;

public static class PolicyNames
{
    // Администратор: пълен достъп
    public const string Administrator = "Administrator";

    // Домоуправител (пълно управление на сградата) или Администратор
    public const string BuildingManagement = "BuildingManagement";

    // Касиер (управление на плащания/такси), Домоуправител или Администратор
    public const string PaymentsManagement = "PaymentsManagement";

    // Всеки автентикиран потребител, обвързан със сграда — Собственик/Живущ, Касиер, Домоуправител, Администратор
    public const string ResidentAccess = "ResidentAccess";
}
