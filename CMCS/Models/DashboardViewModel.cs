using System;
using System.Collections.Generic;

namespace CMCS.Models
{
    public class DashboardViewModel
    {
        public int PendingClaims { get; set; }
        public int RejectedClaims { get; set; }
        public int AcceptedClaims { get; set; }
        public int CoordinatorApprovedClaims { get; set; }
        public int TotalClaims { get; set; }
        public List<Claim> RecentClaims { get; set; } = new List<Claim>();
        public List<Notification> Notifications { get; set; } = new List<Notification>();
        public List<Message> Messages { get; set; } = new List<Message>();
    }
}