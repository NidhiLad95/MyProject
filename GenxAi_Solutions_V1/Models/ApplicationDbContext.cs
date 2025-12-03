using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace GenxAi_Solutions_V1.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> UserMaster { get; set; }
    }
}

