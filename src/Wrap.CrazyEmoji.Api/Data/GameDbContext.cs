using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Data.Entities;

namespace Wrap.CrazyEmoji.Api.Data;

public partial class GameDbContext : DbContext
{
    public GameDbContext()
    {
    }

    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActiveRoom> ActiveRooms { get; set; }

    public virtual DbSet<RoomMember> RoomMembers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("graphql", "pg_graphql")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<ActiveRoom>(entity =>
        {
            entity.HasKey(e => e.RoomCode).HasName("ActiveRooms_pkey");

            entity.HasOne(d => d.RoomCreatorNavigation).WithMany(p => p.ActiveRooms).HasConstraintName("FkUserCreator");
        });

        modelBuilder.Entity<RoomMember>(entity =>
        {
            entity.HasKey(e => new { e.RoomCode, e.Username }).HasName("RoomMembers_pkey");

            entity.Property(e => e.Role).HasDefaultValueSql("'Player'::character varying");

            entity.HasOne(d => d.RoomCodeNavigation).WithMany(p => p.RoomMembers).HasConstraintName("FkRoomMember");

            entity.HasOne(d => d.UsernameNavigation).WithMany(p => p.RoomMembers).HasConstraintName("FkUserMember");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Username).HasName("User_pkey");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .UseIdentityAlwaysColumn();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
