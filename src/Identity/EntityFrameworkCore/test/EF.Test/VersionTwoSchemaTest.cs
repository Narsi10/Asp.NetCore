// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Identity.EntityFrameworkCore.Test;

public class VersionTwoSchemaTest : IClassFixture<ScratchDatabaseFixture>
{
    private readonly ApplicationBuilder _builder;

    public VersionTwoSchemaTest(ScratchDatabaseFixture fixture)
    {
        var services = new ServiceCollection();

        services
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddDbContext<VersionTestDbContext>(o =>
                o.UseSqlite(fixture.Connection)
                    .ConfigureWarnings(b => b.Log(CoreEventId.ManyServiceProvidersCreatedWarning)))
            .AddIdentity<IdentityUser, IdentityRole>(o =>
            {
                o.Stores.SchemaVersion = IdentityVersions.Version2;
            })
            .AddEntityFrameworkStores<VersionTestDbContext>();

        services.AddLogging();

        _builder = new ApplicationBuilder(services.BuildServiceProvider());

        using (var scope = _builder.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VersionTestDbContext>();
            db.Database.EnsureCreated();
            Assert.True(db.OnModelCreatingVersion2Called);
        }
    }
    private static void VerifyDefaultSchema(VersionTestDbContext dbContext)
    {
        var sqlConn = (SqliteConnection)dbContext.Database.GetDbConnection();

        try
        {
            sqlConn.Open();
            Assert.True(DbUtil.VerifyColumns(sqlConn, "AspNetUsers", "Id", "UserName", "Email", "PasswordHash", "SecurityStamp",
                "EmailConfirmed", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled",
                "LockoutEnd", "AccessFailedCount", "ConcurrencyStamp", "NormalizedUserName", "NormalizedEmail"));
            Assert.True(DbUtil.VerifyColumns(sqlConn, "AspNetRoles", "Id", "Name", "NormalizedName", "ConcurrencyStamp"));
            Assert.True(DbUtil.VerifyColumns(sqlConn, "AspNetUserRoles", "UserId", "RoleId"));
            Assert.True(DbUtil.VerifyColumns(sqlConn, "AspNetUserClaims", "Id", "UserId", "ClaimType", "ClaimValue"));
            Assert.True(DbUtil.VerifyColumns(sqlConn, "AspNetUserLogins", "UserId", "ProviderKey", "LoginProvider", "ProviderDisplayName"));
            Assert.True(DbUtil.VerifyColumns(sqlConn, "AspNetUserTokens", "UserId", "LoginProvider", "Name", "Value"));

            Assert.True(DbUtil.VerifyMaxLength(dbContext, "AspNetUsers", 256, "UserName", "Email", "NormalizedUserName", "NormalizedEmail"));
            Assert.True(DbUtil.VerifyMaxLength(dbContext, "AspNetRoles", 256, "Name", "NormalizedName"));
            Assert.True(DbUtil.VerifyMaxLength(dbContext, "AspNetUserLogins", 128, "LoginProvider", "ProviderKey"));
            Assert.True(DbUtil.VerifyMaxLength(dbContext, "AspNetUserTokens", 128, "LoginProvider", "Name"));

            DbUtil.VerifyIndex(sqlConn, "AspNetRoles", "RoleNameIndex", isUnique: true);
            DbUtil.VerifyIndex(sqlConn, "AspNetUsers", "UserNameIndex", isUnique: true);
            DbUtil.VerifyIndex(sqlConn, "AspNetUsers", "EmailIndex");
        }
        finally
        {
            sqlConn.Close();
        }
    }
}