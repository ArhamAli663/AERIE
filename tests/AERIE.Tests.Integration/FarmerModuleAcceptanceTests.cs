namespace AERIE.Tests.Integration;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Services;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Services;
using AERIE.Application.Sync.DTOs;
using AERIE.Application.Sync.Services;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using AERIE.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public class FarmerModuleAcceptanceTests
{
    private readonly Guid _farmerTenantId = Guid.NewGuid();
    private readonly Guid _otherFarmerTenantId = Guid.NewGuid();
    private readonly Guid _brokerTenantId = Guid.NewGuid();
    private readonly Guid _farmerUserId = Guid.NewGuid();
    private readonly Guid _brokerUserId = Guid.NewGuid();

    private (ApplicationDbContext Db, FarmerService FarmerSvc, BrokerService BrokerSvc, SyncService SyncSvc) SetupEnvironment(
        Guid activeTenantId, Guid activeUserId, string dbName)
    {
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.TenantId).Returns(activeTenantId);
        tenantContextMock.Setup(t => t.UserId).Returns(activeUserId);
        tenantContextMock.Setup(t => t.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var db = new ApplicationDbContext(options, tenantContextMock.Object);

        var farmerLogger = NullLogger<FarmerService>.Instance;
        var brokerLogger = NullLogger<BrokerService>.Instance;
        var syncLogger = NullLogger<SyncService>.Instance;

        var farmerSvc = new FarmerService(db, tenantContextMock.Object, farmerLogger);
        var brokerSvc = new BrokerService(db, tenantContextMock.Object, brokerLogger);
        var syncSvc = new SyncService(db, tenantContextMock.Object, farmerSvc, syncLogger);

        return (db, farmerSvc, brokerSvc, syncSvc);
    }

    private async Task SeedTenantsAndBreedAsync(string dbName, Guid breedId)
    {
        var (db, _, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        db.Tenants.AddRange(
            new Tenant { Id = _farmerTenantId, Name = "Al-Rehman Poultry Farms", Slug = "al-rehman" },
            new Tenant { Id = _otherFarmerTenantId, Name = "Sunrise Agro Farms", Slug = "sunrise" },
            new Tenant { Id = _brokerTenantId, Name = "Lahore Mandi Brokers", Slug = "lahore-mandi" }
        );

        db.Breeds.Add(new Breed
        {
            Id = breedId,
            Name = "Ross 308",
            Lineage = "Aviagen",
            TargetFcr = 1.68m,
            TargetMarketWeightGrams = 2200m,
            TargetMarketAgedays = 35
        });

        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Farm and poultry house CRUD with authorization & safe deactivation
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_01_FarmAndHouseCrudWithSafeDeactivation_PreventsDeactivationWithActiveBatches()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);

        // Create Farm
        var farmRes = await farmerSvc.CreateFarmAsync(new CreateFarmRequest(
            Name: "Green Valley Farm",
            Location: "Multan Road",
            City: "Lahore",
            Province: "Punjab",
            TotalCapacity: 20000,
            ContactPhone: "03211112222",
            Notes: "Tunnel ventilated"
        ));
        farmRes.IsSuccess.Should().BeTrue();
        var farmId = farmRes.Data!.Id;

        // Update Farm
        var updateFarmRes = await farmerSvc.UpdateFarmAsync(farmId, new UpdateFarmRequest(
            Name: "Green Valley Farm Expanded",
            Location: "Multan Road, Near Ring Road",
            City: "Lahore",
            Province: "Punjab",
            TotalCapacity: 25000,
            ContactPhone: "03211112222",
            Notes: "Upgraded capacity",
            IsActive: true
        ));
        updateFarmRes.IsSuccess.Should().BeTrue();
        updateFarmRes.Data!.TotalCapacity.Should().Be(25000);

        // Create House
        var houseRes = await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(
            FarmId: farmId,
            Name: "Shed Alpha",
            Capacity: 12000,
            Description: "Climate controlled"
        ));
        houseRes.IsSuccess.Should().BeTrue();
        var houseId = houseRes.Data!.Id;

        // Place an active batch in the farm/house
        var batchRes = await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            FarmId: farmId,
            PoultryHouseId: houseId,
            BreedId: breedId,
            PlacementDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            InitialChickCount: 5000,
            CostPerChick: 80.0m,
            SupplierReference: "National Hatcheries",
            Notes: "Healthy chicks"
        ));
        batchRes.IsSuccess.Should().BeTrue();

        // Attempt safe deactivation while batch is active -> MUST FAIL
        var deactFail = await farmerSvc.DeactivateFarmAsync(farmId);
        deactFail.IsSuccess.Should().BeFalse();
        deactFail.Error.Should().Contain("active");

        // Close batch
        var (db, _, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var batch = await db.FlockBatches.FindAsync(batchRes.Data!.Id);
        batch!.Status = BatchStatus.Closed;
        await db.SaveChangesAsync();

        // Deactivating farm now succeeds safely
        var deactSuccess = await farmerSvc.DeactivateFarmAsync(farmId);
        deactSuccess.IsSuccess.Should().BeTrue();

        var updatedFarm = await farmerSvc.GetFarmByIdAsync(farmId);
        updatedFarm.Data!.IsActive.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Batch registration and correct bird-count tracking
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_02_BatchRegistration_TracksInitialAndCurrentBirdCountsAndAge()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Test Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
        var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 10000, null))).Data!;

        var placementDaysAgo = 21;
        var placementDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-placementDaysAgo));

        var batchRes = await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            FarmId: farm.Id,
            PoultryHouseId: house.Id,
            BreedId: breedId,
            PlacementDate: placementDate,
            InitialChickCount: 4000,
            CostPerChick: 75.0m,
            SupplierReference: "Hatchery A",
            Notes: "Batch 02 test"
        ));

        batchRes.IsSuccess.Should().BeTrue();
        batchRes.Data!.InitialChickCount.Should().Be(4000);
        batchRes.Data.CurrentAliveCount.Should().Be(4000);
        batchRes.Data.AgeDays.Should().Be(placementDaysAgo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Daily mortality validation and cumulative calculations
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_03_DailyMortality_ValidatesAgainstAvailableFlockAndComputesCumulative()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Mortality Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
        var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 10000, null))).Data!;
        var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), 1000, 70m, null, null))).Data!;

        // Mortality exceeding alive birds must fail
        var invalidRecord = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batch.Id,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            AliveCount: 0,
            DailyMortality: 1500, // Exceeds 1000
            FeedConsumedKg: 100m,
            WaterConsumedLiters: 200m,
            AverageWeightGrams: 300m,
            Notes: "Excess mortality test",
            ClientRecordId: Guid.NewGuid().ToString()
        ));
        invalidRecord.IsSuccess.Should().BeFalse();
        invalidRecord.Error.Should().Contain("cannot exceed");

        // Valid mortality record: 25 birds dead
        var validRecord1 = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batch.Id,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            AliveCount: 0,
            DailyMortality: 25,
            FeedConsumedKg: 120m,
            WaterConsumedLiters: 240m,
            AverageWeightGrams: 320m,
            Notes: "Day 1 mortality",
            ClientRecordId: Guid.NewGuid().ToString()
        ));
        validRecord1.IsSuccess.Should().BeTrue();
        validRecord1.Data!.OpeningCount.Should().Be(1000);
        validRecord1.Data.ClosingCount.Should().Be(975);

        // Day 2 record: 15 birds dead -> closing count 960, total batch mortality 40
        var validRecord2 = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batch.Id,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
            AliveCount: 0,
            DailyMortality: 15,
            FeedConsumedKg: 130m,
            WaterConsumedLiters: 260m,
            AverageWeightGrams: 360m,
            Notes: "Day 2 mortality",
            ClientRecordId: Guid.NewGuid().ToString()
        ));
        validRecord2.IsSuccess.Should().BeTrue();
        validRecord2.Data!.OpeningCount.Should().Be(975);
        validRecord2.Data.ClosingCount.Should().Be(960);

        var updatedBatch = await farmerSvc.GetFlockBatchByIdAsync(batch.Id);
        updatedBatch.Data.Should().NotBeNull();
        updatedBatch.Data!.CurrentAliveCount.Should().Be(960);
        (updatedBatch.Data.InitialChickCount - updatedBatch.Data.CurrentAliveCount).Should().Be(40);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Feed & water log persistence & Feed Inventory deduction
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_04_FeedAndWaterLogs_PersistAndDeductFromFeedInventoryStock()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Feed Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
        var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 10000, null))).Data!;
        var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), 2000, 75m, null, null))).Data!;

        // Restock feed inventory with 500 kg of Starter feed
        var restockRes = await farmerSvc.RestockFeedAsync(new RestockFeedRequest(
            FarmId: farm.Id,
            FeedCategory: "Starter",
            QuantityKg: 500m,
            UnitPricePerKg: 135.0m,
            SupplierName: "Supreme Feeds",
            Notes: "Bulk purchase"
        ));
        restockRes.IsSuccess.Should().BeTrue();
        restockRes.Data!.CurrentStockKg.Should().Be(500m);

        // Record daily consumption of 120 kg Starter
        var logRes = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batch.Id,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
            AliveCount: 2000,
            DailyMortality: 5,
            FeedConsumedKg: 120m,
            WaterConsumedLiters: 250m,
            AverageWeightGrams: 450m,
            Notes: "Feed consumption test",
            ClientRecordId: Guid.NewGuid().ToString(),
            FeedCategory: "Starter"
        ));
        logRes.IsSuccess.Should().BeTrue();

        // Feed inventory balance should now be 380 kg (500 - 120)
        var inventories = await farmerSvc.GetFeedInventoriesAsync(farm.Id);
        inventories.IsSuccess.Should().BeTrue();
        var starterInv = inventories.Data!.First(i => i.FeedCategory == "Starter");
        starterInv.CurrentStockKg.Should().Be(380m);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. FCR calculations & invalid-data handling
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_05_FcrCalculations_ComputesAccuratelyAndHandlesZeroWeightGainGracefully()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("FCR Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
        var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 10000, null))).Data!;
        var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), 1000, 75m, null, null))).Data!;

        // Record log with 0 weight gain (averageWeightGrams = null) -> Handled safely without error
        var log1 = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batch.Id,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            AliveCount: 1000,
            DailyMortality: 2,
            FeedConsumedKg: 150m,
            WaterConsumedLiters: 300m,
            AverageWeightGrams: null,
            Notes: "No weight entry",
            ClientRecordId: Guid.NewGuid().ToString()
        ));
        log1.IsSuccess.Should().BeTrue();

        // Record subsequent log with realistic body weight
        // 1000 birds alive * 1.8 kg = 1800 kg total biomass
        // Cumulative feed = 150 + 2850 = 3000 kg -> FCR = 3000 / 1800 = 1.67
        var log2 = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batch.Id,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
            AliveCount: 998,
            DailyMortality: 0,
            FeedConsumedKg: 2850m,
            WaterConsumedLiters: 600m,
            AverageWeightGrams: 1800m,
            Notes: "Weight gain recorded",
            ClientRecordId: Guid.NewGuid().ToString()
        ));
        log2.IsSuccess.Should().BeTrue();

        var updatedBatch = await farmerSvc.GetFlockBatchByIdAsync(batch.Id);
        updatedBatch.Data!.LatestFcr.Should().NotBeNull();
        updatedBatch.Data.LatestFcr!.Value.Should().BeApproximately(1.67m, 0.1m);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Marketplace listing publication and cancellation
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_06_MarketplaceListing_CanPublishAndCancelListingSubjectToReservations()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        var (db, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Market Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
        var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 10000, null))).Data!;
        var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)), 5000, 75m, null, null))).Data!;

        // Mark ready for sale
        await farmerSvc.MarkBatchReadyForSaleAsync(batch.Id);

        // Publish listing
        var listing = new MarketplaceListing
        {
            TenantId = _farmerTenantId,
            BatchId = batch.Id,
            Title = "5,000 Premium Cobb Broilers",
            QuantityTotal = 4000,
            QuantityAvailable = 4000,
            AskingPricePerKg = 340.0m,
            LocationCity = "Lahore",
            Status = ListingStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        db.MarketplaceListings.Add(listing);
        await db.SaveChangesAsync();

        listing.Status.Should().Be(ListingStatus.Published);

        // Cancel listing
        listing.Status = ListingStatus.Cancelled;
        await db.SaveChangesAsync();

        var reloadedListing = await db.MarketplaceListings.FindAsync(listing.Id);
        reloadedListing!.Status.Should().Be(ListingStatus.Cancelled);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Prevention of overselling or double reservation
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_07_PreventionOfOverselling_PreventsSubmittingProposalExceedingAvailableQuantity()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        Guid listingId;
        {
            var (db, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Reserve Farm", "Loc", "LHR", "PB", 5000, null, null))).Data!;
            var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 5000, null))).Data!;
            var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
                farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)), 3000, 75m, null, null))).Data!;

            var listing = new MarketplaceListing
            {
                TenantId = _farmerTenantId,
                BatchId = batch.Id,
                Title = "Limited 2,000 birds",
                QuantityTotal = 2000,
                QuantityAvailable = 2000,
                AskingPricePerKg = 350m,
                LocationCity = "Lahore",
                Status = ListingStatus.Published,
                PublishedAt = DateTime.UtcNow
            };
            db.MarketplaceListings.Add(listing);
            await db.SaveChangesAsync();
            listingId = listing.Id;
        }

        // Broker attempts to create proposal for 2,500 birds (exceeding available 2,000)
        {
            var (_, _, brokerSvc, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var invalidProposal = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
                ListingId: listingId,
                QuantityRequested: 2500, // Exceeds 2000
                OfferedPricePerKg: 345m,
                TransportArrangement: "Buyer Transport",
                Notes: "Excess request"
            ));

            invalidProposal.IsSuccess.Should().BeFalse();
            invalidProposal.Error.Should().Contain("exceeds available");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8 & 9 & 10. Broker proposal visibility, Acceptance, and Linkage to Deal
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criteria_08_09_10_BrokerProposal_Visibility_AcceptanceAndDealLinkage_AtomicallyReservesFlock()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        Guid batchId;
        Guid listingId;
        {
            var (db, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Trade Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
            var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 10000, null))).Data!;
            var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
                farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)), 5000, 75m, null, null))).Data!;
            batchId = batch.Id;

            var listing = new MarketplaceListing
            {
                TenantId = _farmerTenantId,
                BatchId = batch.Id,
                Title = "5,000 Ready Cobb Birds",
                QuantityTotal = 5000,
                QuantityAvailable = 5000,
                AskingPricePerKg = 350m,
                LocationCity = "Lahore",
                Status = ListingStatus.Published,
                PublishedAt = DateTime.UtcNow
            };
            db.MarketplaceListings.Add(listing);
            await db.SaveChangesAsync();
            listingId = listing.Id;
        }

        // Broker submits Proposal
        Guid proposalId;
        {
            var (_, _, brokerSvc, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
                ListingId: listingId,
                QuantityRequested: 2000,
                OfferedPricePerKg: 348m,
                TransportArrangement: "Broker Logistics",
                Notes: "Cash on delivery deal"
            ));
            propRes.IsSuccess.Should().BeTrue();
            proposalId = propRes.Data!.Id;
        }

        // Farmer reviews and accepts proposal
        {
            var (_, _, brokerSvcForFarmer, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var acceptRes = await brokerSvcForFarmer.ReviewProposalAsync(proposalId, new ReviewProposalRequest(
                Accept: true,
                ResponseNotes: "Offer accepted by farmer"
            ));
            acceptRes.IsSuccess.Should().BeTrue();
            var dealDto = acceptRes.Data!;
            dealDto.QuantityBirds.Should().Be(2000);
            dealDto.AgreedPricePerKg.Should().Be(348m);
            dealDto.ListingId.Should().Be(listingId);
            dealDto.FarmerTenantId.Should().Be(_farmerTenantId);
            dealDto.BrokerTenantId.Should().Be(_brokerTenantId);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Farmer Financial View: Invoices, Receivables and Settlements
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_11_FarmerFinancialView_ReflectsInvoicesReceivablesAndPaymentsAccurately()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        {
            var (db, _, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farm = new Farm { TenantId = _farmerTenantId, Name = "Finance Farm", Location = "LHR", TotalCapacity = 5000 };
            var batch = new FlockBatch { TenantId = _farmerTenantId, Farm = farm, BatchCode = "FIN-01", InitialChickCount = 3000, CurrentAliveCount = 3000, PlacementDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)) };
            var listing = new MarketplaceListing { TenantId = _farmerTenantId, Batch = batch, Title = "Birds", QuantityTotal = 3000, QuantityAvailable = 1000, AskingPricePerKg = 340m, LocationCity = "LHR" };

            var proposal = new DealProposal
            {
                Listing = listing,
                BrokerTenantId = _brokerTenantId,
                ProposedByUserId = _brokerUserId,
                QuantityRequested = 2000,
                OfferedPricePerKg = 340.0m,
                Status = DealStatus.Settled
            };

            var deal = new Deal
            {
                Proposal = proposal,
                Listing = listing,
                FarmerTenantId = _farmerTenantId,
                BrokerTenantId = _brokerTenantId,
                QuantityBirds = 2000,
                AgreedPricePerKg = 340.0m,
                Status = DealStatus.Settled
            };

            var settlement = new Settlement
            {
                Deal = deal,
                GrossAmount = 1360000m,
                CommissionAmount = 12000m,
                FreightAmount = 8000m,
                NetPayableToFarmer = 1340000m,
                SettledAt = DateTime.UtcNow
            };

            var invoice = new Invoice
            {
                TenantId = _farmerTenantId,
                Deal = deal,
                InvoiceNumber = "INV-2026-001",
                TotalAmount = 1340000m,
                PaidAmount = 1000000m,
                IssuedAt = DateTime.UtcNow,
                Status = "PartiallyPaid"
            };

            var payment = new Payment
            {
                Invoice = invoice,
                Amount = 1000000m,
                Method = PaymentMethod.BankTransfer,
                ReferenceNumber = "TRX-998877",
                RecordedAt = DateTime.UtcNow
            };

            db.Farms.Add(farm);
            db.FlockBatches.Add(batch);
            db.MarketplaceListings.Add(listing);
            db.DealProposals.Add(proposal);
            db.Deals.Add(deal);
            db.Settlements.Add(settlement);
            db.Invoices.Add(invoice);
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
        }

        // Query Farmer Financial Summary
        {
            var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var finRes = await farmerSvc.GetFarmerFinancialSummaryAsync();
            finRes.IsSuccess.Should().BeTrue();
            var summary = finRes.Data!;

            summary.TotalGrossSales.Should().Be(1360000m);
            summary.TotalBrokerCommissions.Should().Be(12000m);
            summary.TotalFreightDeductions.Should().Be(8000m);
            summary.NetFarmerReceivables.Should().Be(1340000m);
            summary.TotalPaymentsReceived.Should().Be(1000000m);
            summary.OutstandingBalance.Should().Be(340000m); // 1,340,000 - 1,000,000
            summary.Invoices.Should().HaveCount(1);
            summary.Payments.Should().HaveCount(1);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. Offline entry, synchronization retries and duplicate prevention
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_12_OfflineSync_IsIdempotentAndPreventsDuplicateDailyEntries()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        Guid batchId;
        {
            var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Sync Farm", "Loc", "LHR", "PB", 5000, null, null))).Data!;
            var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 5000, null))).Data!;
            var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
                farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), 2000, 75m, null, null))).Data!;
            batchId = batch.Id;
        }

        var clientRecordId = Guid.NewGuid().ToString();
        var syncItem = new DailyRecordSyncItem(
            ClientRecordId: clientRecordId,
            BatchId: batchId,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
            AliveCount: 1980,
            DailyMortality: 20,
            FeedConsumedKg: 200m,
            WaterConsumedLiters: 400m,
            AverageWeightGrams: 550m,
            Notes: "Captured offline in SQLite",
            ClientVersion: 1,
            ClientTimestamp: DateTime.UtcNow
        );

        // First push
        {
            var (_, _, _, syncSvc1) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var res1 = await syncSvc1.PushSyncAsync(new SyncPushRequest([syncItem]));
            res1.IsSuccess.Should().BeTrue();
            res1.Data!.Results[0].Success.Should().BeTrue();
        }

        // Replay retry with same ClientRecordId
        {
            var (db2, _, _, syncSvc2) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var res2 = await syncSvc2.PushSyncAsync(new SyncPushRequest([syncItem]));
            res2.IsSuccess.Should().BeTrue();
            res2.Data!.Results[0].Success.Should().BeTrue();

            // Verify exactly 1 record persisted in database
            var recordCount = await db2.DailyFlockRecords.CountAsync(r => r.BatchId == batchId);
            recordCount.Should().Be(1);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13. Cross-tenant access prevention
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_13_CrossTenantAccess_FarmerCannotAccessOrModifyAnotherTenantsData()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        Guid tenantAFarmId;
        {
            var (_, farmerSvcA, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farm = (await farmerSvcA.CreateFarmAsync(new CreateFarmRequest("Farmer A Private Farm", "Loc", "LHR", "PB", 10000, null, null))).Data!;
            tenantAFarmId = farm.Id;
        }

        // Farmer B attempts to retrieve Farmer A's farm -> MUST return NotFound
        {
            var (_, farmerSvcB, _, _) = SetupEnvironment(_otherFarmerTenantId, Guid.NewGuid(), dbName);
            var crossFarm = await farmerSvcB.GetFarmByIdAsync(tenantAFarmId);
            crossFarm.IsSuccess.Should().BeFalse();
            crossFarm.Error.Should().Contain("not found");

            // Farmer B attempts to deactivate Farmer A's farm -> MUST fail
            var deactRes = await farmerSvcB.DeactivateFarmAsync(tenantAFarmId);
            deactRes.IsSuccess.Should().BeFalse();
            deactRes.Error.Should().Contain("not found");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14. Report totals and export generation
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Criterion_14_ReportsAndExports_GeneratesProductionAndFinancialReportsAccurately()
    {
        var dbName = Guid.NewGuid().ToString();
        var breedId = Guid.NewGuid();
        await SeedTenantsAndBreedAsync(dbName, breedId);

        Guid batchId;
        {
            var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farm = (await farmerSvc.CreateFarmAsync(new CreateFarmRequest("Report Farm", "Loc", "LHR", "PB", 5000, null, null))).Data!;
            var house = (await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(farm.Id, "Shed 1", 5000, null))).Data!;
            var batch = (await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
                farm.Id, house.Id, breedId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), 2000, 75m, null, null))).Data!;
            batchId = batch.Id;

            await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
                BatchId: batch.Id,
                RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
                AliveCount: 1985,
                DailyMortality: 15,
                FeedConsumedKg: 200m,
                WaterConsumedLiters: 450m,
                AverageWeightGrams: 550m,
                Notes: "Report log",
                ClientRecordId: Guid.NewGuid().ToString(),
                FeedCategory: "Starter"
            ));
        }

        {
            var (_, farmerSvc, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);

            // 1. Structured Report
            var reportRes = await farmerSvc.GetFlockProductionReportAsync(batchId);
            reportRes.IsSuccess.Should().BeTrue();
            var report = reportRes.Data!;
            report.DailyRows.Should().HaveCount(1);
            report.DailyRows.First().DailyMortality.Should().Be(15);
            report.DailyRows.First().FeedConsumedKg.Should().Be(200m);

            // 2. Production CSV Export
            var csvRes = await farmerSvc.GetProductionReportCsvAsync(batchId);
            csvRes.IsSuccess.Should().BeTrue();
            csvRes.Data!.Should().Contain("Date,AgeDays,OpeningBirds,Mortality,ClosingBirds,FeedCategory,FeedKg,WaterLiters");
            csvRes.Data.Should().Contain("Starter");

            // 3. Financial CSV Export
            var finCsvRes = await farmerSvc.GetFarmerFinancialReportCsvAsync();
            finCsvRes.IsSuccess.Should().BeTrue();
            finCsvRes.Data!.Should().Contain("DealNumber,BrokerBuyer,AgreedRatePerKg,QuantityBirds");
        }
    }
}
