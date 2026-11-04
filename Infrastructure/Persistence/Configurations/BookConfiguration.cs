using Application.Identity;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Author).IsRequired().HasMaxLength(150);
        builder.Property(b => b.Isbn).IsRequired().HasMaxLength(13);
        builder.Property(b => b.PublishedYear).IsRequired();
        builder.Property(b => b.TotalCopies).IsRequired();
        builder.Property(b => b.AvailableCopies).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();

        builder.HasIndex(b => b.Isbn).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
