using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOL
{
    public class Notification
    {
        public long Id { get; init; }
        public int CompanyId { get; init; }    // NEW
        public int UserId { get; init; }
        public string Title { get; init; } = "";
        public string Message { get; init; } = "";
        public string? LinkUrl { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public string Process { get; init; } = "";
        public string ModuleName { get; init; } = "";
        public long? RefId { get; init; }
        public string Outcome { get; init; } = ""; // "success" | "fail"
    }
}
