using System;
using System.Collections.Generic;
using System.Text;

namespace ZEstate.Infrastructure.Data.Enums
{
    public enum RepairStatus
    {
        Planned,
        InProgress,
        Completed
    }
    public enum VoteValue
    {
        Yes,
        No,
        Abstain
    }
    public enum MeetingStatus
    {
        Upcoming,
        Active,
        Closed
    }
    public enum PaymentMethod
    {
        Manual,
        Stripe
    }
    // Which physical/virtual "till" money actually sits in - drives the two balance
    // tiles and internal transfers between them.
    public enum CashAccountType
    {
        Cash,
        Bank
    }
    public enum ObligationStatus
    {
        Pending,
        PartiallyPaid,
        Paid,
        Overdue
    }
    public enum FeeType
    {
        Fixed,
        PerIdealPart,
        Repair
    }
    public enum FeePriority
    {
        Low,
        Normal,
        High,
        Urgent
    }
    public enum ApartmentRole
    {
        Owner,
        Resident,
        HouseManager
    }
    public enum DocumentType
    {
        Protocol,
        Contract,
        Invoice,
        Other
    }
    public enum DocumentAccess
    {
        All,
        ManagerOnly
    }
    public enum JoinRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }
    public enum InviteCodeAction
    {
        Regenerated,
        Revoked,
        LimitsUpdated
    }
    public enum FeeFrequency
    {
        OneTime,
        Monthly
    }
    public enum DebtHandling
    {
        TransfersToNewOwner,
        StaysWithPreviousOwner
    }
}
