using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Tailgrab.Common;

namespace tailgrab.src.Models
{
    public partial class GroupManagement
    {
        public string GroupId { get; set; }

        public string GroupName { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public GroupManagement()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"GroupId: {GroupId}, GroupName: {GroupName}, CreatedAt: {CreatedAt}, UpdatedAt: {UpdatedAt}";
        }
    }
}
