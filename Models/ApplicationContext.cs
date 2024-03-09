using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WSTKNG.Models
{
    public class ApplicationContext: DbContext
    {
        public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
        {

        }

        public DbSet<Series> Series { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Series>().ToTable("Series");
            modelBuilder.Entity<Chapter>().ToTable("Chapters").HasIndex(c => c.URL);
            modelBuilder.Entity<Template>().ToTable("Templates");
            modelBuilder.Entity<Setting>().ToTable("Settings");
        }
    }
}