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
}
