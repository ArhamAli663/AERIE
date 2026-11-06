namespace AERIE.Tests.Integration;

using AERIE.Application.Common.Interfaces;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using AERIE.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

public class TenantIsolationTests
{
    private readonly Guid _tenantAId = Guid.NewGuid();
    private readonly Guid _tenantBId = Guid.NewGuid();

    private ApplicationDbContext CreateDbContext(Guid currentTenantId)
    {
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.TenantId).Returns(currentTenantId);
        tenantContextMock.Setup(t => t.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"Aerie_TestDb_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options, tenantContextMock.Object);
    }

    [Fact]
    public async Task GlobalQueryFilter_WhenTenantAQueriesFarms_CannotSeeTenantBFarms()
    {
        // Arrange: DbContext as Tenant A inserts a farm
        var dbName = Guid.NewGuid().ToString();
        var mockContextA = new Mock<ITenantContext>();
        mockContextA.Setup(t => t.TenantId).Returns(_tenantAId);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using (var dbA = new ApplicationDbContext(options, mockContextA.Object))
        {
            dbA.Farms.Add(new Farm
            {
                TenantId = _tenantAId,
                Name = "Tenant A Farm",
                Location = "Lahore",
                TotalCapacity = 10000
            });
            await dbA.SaveChangesAsync();
        }

        // DbContext as Tenant B inserts a farm
        var mockContextB = new Mock<ITenantContext>();
        mockContextB.Setup(t => t.TenantId).Returns(_tenantBId);

        using (var dbB = new ApplicationDbContext(options, mockContextB.Object))
        {
            dbB.Farms.Add(new Farm
            {
                TenantId = _tenantBId,
                Name = "Tenant B Farm",
                Location = "Faisalabad",
                TotalCapacity = 20000
            });
            await dbB.SaveChangesAsync();
        }

        // Act: Tenant A queries all farms
        using (var queryDbA = new ApplicationDbContext(options, mockContextA.Object))
        {
            var farms = await queryDbA.Farms.ToListAsync();

            // Assert: Tenant A ONLY sees their own farm
            farms.Should().HaveCount(1);
            farms[0].Name.Should().Be("Tenant A Farm");
            farms[0].TenantId.Should().Be(_tenantAId);
        }

        // Act: Tenant B queries all farms
        using (var queryDbB = new ApplicationDbContext(options, mockContextB.Object))
        {
            var farms = await queryDbB.Farms.ToListAsync();

            // Assert: Tenant B ONLY sees their own farm
            farms.Should().HaveCount(1);
            farms[0].Name.Should().Be("Tenant B Farm");
            farms[0].TenantId.Should().Be(_tenantBId);
        }
    }

    [Fact]
    public async Task MarketplaceSharing_OnlyPublishedListingsAreVisibleAcrossTenants()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var mockFarmer = new Mock<ITenantContext>();
        mockFarmer.Setup(t => t.TenantId).Returns(_tenantAId);

        var batchId = Guid.NewGuid();

        using (var db = new ApplicationDbContext(options, mockFarmer.Object))
        {
            // Farmer creates 1 Draft listing and 1 Published listing
            db.MarketplaceListings.AddRange(
                new MarketplaceListing
                {
                    TenantId = _tenantAId,
                    BatchId = batchId,
                    Title = "Private Draft Batch",
                    QuantityTotal = 1000,
                    QuantityAvailable = 1000,
                    AskingPricePerKg = 350m,
                    Status = ListingStatus.Draft
                },
                new MarketplaceListing
                {
                    TenantId = _tenantAId,
                    BatchId = batchId,
                    Title = "Public Published Batch",
                    QuantityTotal = 2000,
                    QuantityAvailable = 2000,
                    AskingPricePerKg = 360m,
                    Status = ListingStatus.Published
                }
            );
            await db.SaveChangesAsync();
        }

        // Broker queries public marketplace
        var mockBroker = new Mock<ITenantContext>();
        mockBroker.Setup(t => t.TenantId).Returns(_tenantBId);

        using (var db = new ApplicationDbContext(options, mockBroker.Object))
        {
            var publicListings = await db.MarketplaceListings
                .IgnoreQueryFilters()
                .Where(l => l.Status == ListingStatus.Published && l.QuantityAvailable > 0)
                .ToListAsync();

            // Assert: Broker sees ONLY the published listing, NOT the private draft!
            publicListings.Should().HaveCount(1);
            publicListings[0].Title.Should().Be("Public Published Batch");
        }
    }
}
