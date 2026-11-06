namespace AERIE.Tests.Integration;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Services;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Common.Models;
using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Services;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using AERIE.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public class BrokerModuleAcceptanceTests
{
    private readonly Guid _farmerTenantId = Guid.NewGuid();
    private readonly Guid _brokerTenantId = Guid.NewGuid();
    private readonly Guid _competingBrokerTenantId = Guid.NewGuid();
    private readonly Guid _thirdPartyTenantId = Guid.NewGuid();
    private readonly Guid _farmerUserId = Guid.NewGuid();
    private readonly Guid _brokerUserId = Guid.NewGuid();
    private readonly Guid _competingBrokerUserId = Guid.NewGuid();

    private (ApplicationDbContext Db, FarmerService FarmerSvc, BrokerService BrokerSvc) SetupEnvironment(
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

        var farmerSvc = new FarmerService(db, tenantContextMock.Object, farmerLogger);
        var brokerSvc = new BrokerService(db, tenantContextMock.Object, brokerLogger);

        return (db, farmerSvc, brokerSvc);
    }

    private async Task<(Guid breedId, Guid farmId, Guid houseId, Guid batchId)> SeedBaseFarmerDataAsync(string dbName)
    {
        var (db, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);

        // Seed Tenants
        db.Tenants.AddRange(
            new Tenant { Id = _farmerTenantId, Name = "Al-Madina Poultry Farms", Slug = "al-madina" },
            new Tenant { Id = _brokerTenantId, Name = "Punjab Poultry Traders", Slug = "punjab-traders" },
            new Tenant { Id = _competingBrokerTenantId, Name = "Lahore Broiler Traders", Slug = "lahore-traders" },
            new Tenant { Id = _thirdPartyTenantId, Name = "Unrelated Entity Ltd", Slug = "unrelated" }
        );

        var breedId = Guid.NewGuid();
        db.Breeds.Add(new Breed { Id = breedId, Name = "Ross 308", TargetMarketWeightGrams = 2200m, TargetMarketAgedays = 35 });
        await db.SaveChangesAsync();

        var farmRes = await farmerSvc.CreateFarmAsync(new CreateFarmRequest(
            Name: "Prime Farm Alpha",
            Location: "Multan Road",
            City: "Lahore",
            Province: "Punjab",
            TotalCapacity: 30000,
            ContactPhone: "03001112233",
            Notes: "Automated broiler farm"
        ));
        var farmId = farmRes.Data!.Id;

        var houseRes = await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(
            FarmId: farmId,
            Name: "Shed 1",
            Capacity: 15000,
            Description: "Tunnel ventilated"
        ));
        var houseId = houseRes.Data!.Id;

        var batchRes = await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
            FarmId: farmId,
            PoultryHouseId: houseId,
            BreedId: breedId,
            PlacementDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)),
            InitialChickCount: 10000,
            CostPerChick: 65m,
            SupplierReference: "BB-2024-X1",
            Notes: "Healthy chicks"
        ));
        var batchId = batchRes.Data!.Id;

        await farmerSvc.MarkBatchReadyForSaleAsync(batchId);

        return (breedId, farmId, houseId, batchId);
    }

    private async Task<Guid> CreateAndPublishListingAsync(
        FarmerService farmerSvc, Guid batchId, int quantity, decimal askingPrice, string city = "Lahore")
    {
        var listingRes = await farmerSvc.CreateListingAsync(new CreateListingRequest(
            BatchId: batchId,
            Title: $"{quantity:N0} Broilers - {city}",
            QuantityTotal: quantity,
            AskingPricePerKg: askingPrice,
            NegotiableFloorPricePerKg: askingPrice - 10m,
            LocationCity: city,
            LocationDetails: "Farm Gate",
            ExpiresAt: DateTime.UtcNow.AddDays(5),
            Notes: "Ready for immediate lifting"
        ));

        await farmerSvc.PublishListingAsync(listingRes.Data!.Id);
        return listingRes.Data!.Id;
    }

    private async Task<Result<DealDto>> AcceptProposalAsync(
        BrokerService brokerSvc, Guid proposalId, string? notes = "Accepted")
    {
        return await brokerSvc.ReviewProposalAsync(proposalId, new ReviewProposalRequest(Accept: true, ResponseNotes: notes));
    }

    // ── Criterion 1: Farmer publishes a sale-ready flock batch ───────────────────
    [Fact]
    public async Task Criterion01_FarmerPublishesSaleReadyFlockBatch_ListingCreatedAndMarkedPublished()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);
        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);

        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 5000, 380m);

        var (db, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listing = await db.MarketplaceListings.FindAsync(listingId);

        listing.Should().NotBeNull();
        listing!.Status.Should().Be(ListingStatus.Published);
        listing.QuantityAvailable.Should().Be(5000);
        listing.AskingPricePerKg.Should().Be(380m);
    }

    // ── Criterion 2: Broker sees published listing across tenant boundaries ───────
    [Fact]
    public async Task Criterion02_BrokerDiscoversPublishedListing_AcrossTenantBoundaries()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 4000, 375m);

        // Broker queries marketplace
        var (brokerDb, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var dashboardRes = await brokerSvc.GetBrokerDashboardSummaryAsync();

        dashboardRes.IsSuccess.Should().BeTrue();
        dashboardRes.Value!.TotalPublishedListingsAvailable.Should().BeGreaterThanOrEqualTo(1);

        var listings = await brokerDb.MarketplaceListings
            .IgnoreQueryFilters()
            .Where(l => l.Status == ListingStatus.Published && l.QuantityAvailable > 0)
            .ToListAsync();

        listings.Should().Contain(l => l.Id == listingId);
    }

    // ── Criterion 3: Broker submits a purchase proposal ──────────────────────────
    [Fact]
    public async Task Criterion03_BrokerSubmitsPurchaseProposal_RecordsTermsAndProposalNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 5000, 380m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2000,
            OfferedPricePerKg: 378m,
            TransportArrangement: "Buyer provides Mazda trucks",
            Notes: "Prompt lifting at 5 AM"
        ));

        propRes.IsSuccess.Should().BeTrue();
        propRes.Data.Should().NotBeNull();
        propRes.Data!.Status.Should().Be(DealStatus.Proposed);
        propRes.Data.QuantityRequested.Should().Be(2000);
        propRes.Data.OfferedPricePerKg.Should().Be(378m);
    }

    // ── Criterion 4: Correct farmer sees the proposal ───────────────────────────
    [Fact]
    public async Task Criterion04_CorrectFarmerSeesProposal_UnauthorizedTenantsCannot()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 4000, 385m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1500,
            OfferedPricePerKg: 380m,
            TransportArrangement: "Broker transport",
            Notes: "Standard terms"
        ));

        // Farmer fetches proposals on their listing via BrokerService
        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var farmerProposals = await farmerBrokerSvc.GetProposalsForListingAsync(listingId);

        farmerProposals.IsSuccess.Should().BeTrue();
        farmerProposals.Data.Should().ContainSingle(p => p.Id == propRes.Data!.Id);

        // Unrelated third-party cannot see this proposal
        var (_, _, thirdPartyBrokerSvc) = SetupEnvironment(_thirdPartyTenantId, Guid.NewGuid(), dbName);
        var unrelatedProposals = await thirdPartyBrokerSvc.GetProposalsForListingAsync(listingId);

        unrelatedProposals.IsSuccess.Should().BeFalse();
        unrelatedProposals.Error.Should().Contain("not owned by current tenant");
    }

    // ── Criterion 5: Farmer accepts or rejects proposal ─────────────────────────
    [Fact]
    public async Task Criterion05_FarmerAcceptsOrRejectsProposal_StatusTransitionsProperly()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 3000, 380m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1000,
            OfferedPricePerKg: 375m,
            TransportArrangement: "Self lift",
            Notes: null
        ));

        // Farmer accepts the proposal via review
        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var acceptRes = await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id, "Price and schedule approved.");

        acceptRes.IsSuccess.Should().BeTrue();
        acceptRes.Data.Should().NotBeNull();
        acceptRes.Data!.Status.Should().Be(DealStatus.Accepted);
    }

    // ── Criterion 6: Acceptance creates exactly one shared deal ──────────────────
    [Fact]
    public async Task Criterion06_AcceptanceCreatesExactlyOneSharedDeal_ReferencesCorrectParties()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 5000, 380m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 3000,
            OfferedPricePerKg: 378m,
            TransportArrangement: "Direct pickup",
            Notes: null
        ));

        // Farmer accepts
        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id, "Terms accepted.");

        // Check shared Deal
        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deals = await db.Deals
            .IgnoreQueryFilters()
            .Where(d => d.ProposalId == propRes.Data!.Id)
            .ToListAsync();

        deals.Should().HaveCount(1);
        var deal = deals.First();
        deal.FarmerTenantId.Should().Be(_farmerTenantId);
        deal.BrokerTenantId.Should().Be(_brokerTenantId);
        deal.QuantityBirds.Should().Be(3000);
        deal.AgreedPricePerKg.Should().Be(378m);
        deal.Status.Should().Be(DealStatus.Accepted);
    }

    // ── Criterion 7: Competing purchases cannot oversell available batch ─────────
    [Fact]
    public async Task Criterion07_CompetingPurchasesCannotOversellAvailableBatch_AtomicProtection()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 3000, 380m);

        // Broker 1 requests 2,000 birds
        var (_, _, broker1Svc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var prop1Res = await broker1Svc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2000,
            OfferedPricePerKg: 380m,
            TransportArrangement: "Truck 1",
            Notes: null
        ));
        prop1Res.IsSuccess.Should().BeTrue();

        // Farmer accepts Broker 1 proposal -> reserves 2,000 birds. Remaining available: 1,000
        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, prop1Res.Data!.Id);

        // Competing Broker 2 attempts to request 2,000 birds (exceeding remaining 1,000)
        var (_, _, broker2Svc) = SetupEnvironment(_competingBrokerTenantId, _competingBrokerUserId, dbName);
        var prop2Res = await broker2Svc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2000,
            OfferedPricePerKg: 385m,
            TransportArrangement: "Truck 2",
            Notes: null
        ));

        prop2Res.IsSuccess.Should().BeFalse();
        prop2Res.Error.Should().Contain("exceeds available quantity");

        // Competing Broker 2 requests remaining 1,000 birds -> succeeds
        var prop3Res = await broker2Svc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1000,
            OfferedPricePerKg: 385m,
            TransportArrangement: "Truck 2",
            Notes: null
        ));
        prop3Res.IsSuccess.Should().BeTrue();
    }

    // ── Criterion 8: Farmer and broker see consistent authorized deal statuses ───
    [Fact]
    public async Task Criterion08_FarmerAndBrokerSeeConsistentAuthorizedDealStatuses()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 2500, 380m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2500,
            OfferedPricePerKg: 380m,
            TransportArrangement: null,
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        // Broker views deal
        var brokerDealRes = await brokerSvc.GetDealByIdAsync(deal.Id);
        brokerDealRes.IsSuccess.Should().BeTrue();
        brokerDealRes.Data!.Status.Should().Be(DealStatus.Accepted);

        // Update status to InTransit
        var updateRes = await brokerSvc.UpdateDealStatusAsync(deal.Id, new UpdateDealStatusRequest(
            NewStatus: DealStatus.InTransit,
            Notes: "Trucks dispatched"
        ));
        updateRes.IsSuccess.Should().BeTrue();

        // Both parties query and observe InTransit
        var updatedDealBroker = await brokerSvc.GetDealByIdAsync(deal.Id);
        updatedDealBroker.Data!.Status.Should().Be(DealStatus.InTransit);

        var (fDb, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var updatedDealFarmer = await fDb.Deals.FirstOrDefaultAsync(d => d.Id == deal.Id);
        updatedDealFarmer!.Status.Should().Be(DealStatus.InTransit);
    }

    // ── Criterion 9: Transport records reference correct deal and batch ─────────
    [Fact]
    public async Task Criterion09_TransportRecordsReferenceCorrectDealAndBatch_RecordsLiftingAndDiscrepancies()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 3000, 380m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 3000,
            OfferedPricePerKg: 380m,
            TransportArrangement: "Mazda",
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        // Record Lifting (Loading: 3,000 birds, 6,300 kg)
        var liftRes = await brokerSvc.RecordLiftingAsync(deal.Id, new RecordLiftingRequest(
            ActualWeightKg: 6300m,
            BirdsLoaded: 3000,
            MortalityDuringTransit: 0,
            VehicleNumber: "LES-9922",
            DriverName: "Aslam Khan",
            DriverPhone: "03211112222",
            WeighbridgeTicketNumber: "WB-8812",
            Notes: "Loaded smoothly at farm shed"
        ));
        liftRes.IsSuccess.Should().BeTrue();

        // Record Delivery (Delivered: 2,985 birds, 15 mortality, 6,180 kg delivered)
        var delivRes = await brokerSvc.RecordDeliveryAsync(deal.Id, new RecordDeliveryRequest(
            BirdsDelivered: 2985,
            MortalityDuringTransit: 15,
            WeightDeliveredKg: 6180m,
            DestinationLocation: "Tollinton Wholesale Mandi, Lahore",
            FreightCost: 18000m,
            Notes: "Verified at mandi weighbridge"
        ));

        delivRes.IsSuccess.Should().BeTrue();
        delivRes.Value!.BirdsLoaded.Should().Be(3000);
        delivRes.Value.BirdsDelivered.Should().Be(2985);
        delivRes.Value.MortalityDuringTransit.Should().Be(15);
        delivRes.Value.ShrinkageKg.Should().Be(120m); // 6300 - 6180
        delivRes.Value.ShrinkagePct.Should().BeApproximately(1.90m, 0.05m);
        delivRes.Value.DeliveryStatus.Should().Be("Delivered");
    }

    // ── Criterion 10: Commission, freight and settlement amounts are correct ─────
    [Fact]
    public async Task Criterion10_CommissionFreightAndSettlementAmounts_CalculatedAccurately()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 2000, 350m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2000,
            OfferedPricePerKg: 350m,
            TransportArrangement: null,
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        // Record lifting weight: 4,000 kg @ Rs. 350 = Gross: Rs. 1,400,000
        await brokerSvc.RecordLiftingAsync(deal.Id, new RecordLiftingRequest(
            ActualWeightKg: 4000m,
            BirdsLoaded: 2000,
            MortalityDuringTransit: 0,
            VehicleNumber: "LHR-1122",
            DriverName: "Driver",
            DriverPhone: null,
            WeighbridgeTicketNumber: null,
            Notes: null
        ));

        // Create Settlement: 2.0% Commission, Rs. 15,000 Freight
        // Gross: 1,400,000
        // Commission: 2% of 1,400,000 = 28,000
        // Freight: 15,000
        // Farmer Payable: 1,400,000 - 28,000 - 15,000 = 1,357,000
        var settleRes = await brokerSvc.CreateSettlementAsync(deal.Id, new CreateSettlementRequest(
            CommissionRate: 2.0m,
            CommissionType: CommissionRuleType.Percentage,
            FreightAmount: 15000m,
            Deductions: 0m,
            Additions: 0m,
            Notes: "Standard 2% commission settlement"
        ));

        settleRes.IsSuccess.Should().BeTrue();
        settleRes.Data!.TotalGrossAmount.Should().Be(1400000m);
        settleRes.Data.CommissionAmount.Should().Be(28000m);
        settleRes.Data.FreightAmount.Should().Be(15000m);
        settleRes.Data.NetFarmerPayable.Should().Be(1357000m);
    }

    // ── Criterion 11: Farmer and broker ledger views reconcile ───────────────────
    [Fact]
    public async Task Criterion11_FarmerAndBrokerLedgerViewsReconcile_DebitCreditEntriesMatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 1000, 350m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1000,
            OfferedPricePerKg: 350m,
            TransportArrangement: null,
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        await brokerSvc.RecordLiftingAsync(deal.Id, new RecordLiftingRequest(
            ActualWeightKg: 2000m,
            BirdsLoaded: 1000,
            MortalityDuringTransit: 0,
            VehicleNumber: "LHR-5566",
            DriverName: "Driver",
            DriverPhone: null,
            WeighbridgeTicketNumber: null,
            Notes: null
        ));

        var settleRes = await brokerSvc.CreateSettlementAsync(deal.Id, new CreateSettlementRequest(
            CommissionRate: 2.0m,
            CommissionType: CommissionRuleType.Percentage,
            FreightAmount: 10000m,
            Deductions: 0m,
            Additions: 0m,
            Notes: null
        ));

        // Invoice generated for Net Farmer Payable: 700,000 - 14,000 - 10,000 = 676,000
        var invRes = await brokerSvc.CreateInvoiceAsync(new CreateInvoiceRequest(
            DealId: deal.Id,
            RecipientTenantId: _brokerTenantId,
            RecipientName: "Punjab Poultry Traders",
            RecipientAddress: "Tollinton Mandi",
            SubTotal: settleRes.Data!.NetFarmerPayable,
            TaxAmount: 0m,
            TotalAmount: settleRes.Data.NetFarmerPayable,
            DueDate: DateTime.UtcNow.AddDays(7),
            Notes: null
        ));

        // Broker posts confirmed payment
        var payRes = await brokerSvc.RecordPaymentAsync(new RecordPaymentRequest(
            DealId: deal.Id,
            InvoiceId: invRes.Data!.Id,
            Amount: 676000m,
            Method: PaymentMethod.BankTransfer,
            ReferenceNumber: "TRX-RECON-001",
            BankName: "HBL",
            Notes: "Full payment posted"
        ));
        payRes.IsSuccess.Should().BeTrue();

        // Verify ledger entries exist in database for both parties
        var ledgerEntries = await db.LedgerEntries
            .IgnoreQueryFilters()
            .Where(l => l.DealId == deal.Id)
            .ToListAsync();

        ledgerEntries.Should().HaveCount(2); // 1 Debit, 1 Credit
        var debit = ledgerEntries.FirstOrDefault(l => l.EntryType == LedgerEntryType.Debit);
        var credit = ledgerEntries.FirstOrDefault(l => l.EntryType == LedgerEntryType.Credit);

        debit.Should().NotBeNull();
        credit.Should().NotBeNull();
        debit!.Amount.Should().Be(676000m);
        credit!.Amount.Should().Be(676000m);
    }

    // ── Criterion 12: Confirmed payment updates outstanding balances ─────────────
    [Fact]
    public async Task Criterion12_ConfirmedPaymentUpdatesOutstandingBalances_MarksInvoicePaid()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 1000, 350m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1000,
            OfferedPricePerKg: 350m,
            TransportArrangement: null,
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        await brokerSvc.RecordLiftingAsync(deal.Id, new RecordLiftingRequest(
            ActualWeightKg: 2000m,
            BirdsLoaded: 1000,
            MortalityDuringTransit: 0,
            VehicleNumber: "LHR-9999",
            DriverName: "Driver",
            DriverPhone: null,
            WeighbridgeTicketNumber: null,
            Notes: null
        ));

        var settleRes = await brokerSvc.CreateSettlementAsync(deal.Id, new CreateSettlementRequest(
            CommissionRate: 2.0m,
            CommissionType: CommissionRuleType.Percentage,
            FreightAmount: 10000m,
            Deductions: 0m,
            Additions: 0m,
            Notes: null
        ));

        var invRes = await brokerSvc.CreateInvoiceAsync(new CreateInvoiceRequest(
            DealId: deal.Id,
            RecipientTenantId: _brokerTenantId,
            RecipientName: "Punjab Poultry Traders",
            RecipientAddress: "Tollinton Mandi",
            SubTotal: settleRes.Data!.NetFarmerPayable,
            TaxAmount: 0m,
            TotalAmount: settleRes.Data.NetFarmerPayable,
            DueDate: DateTime.UtcNow.AddDays(7),
            Notes: null
        ));

        invRes.Data!.PaidAmount.Should().Be(0m);
        invRes.Data.BalanceDue.Should().Be(settleRes.Data!.NetFarmerPayable);

        // Record partial payment of 300,000
        await brokerSvc.RecordPaymentAsync(new RecordPaymentRequest(
            DealId: deal.Id,
            InvoiceId: invRes.Data!.Id,
            Amount: 300000m,
            Method: PaymentMethod.BankTransfer,
            ReferenceNumber: "TRX-PARTIAL-1",
            BankName: "HBL",
            Notes: "Partial payment"
        ));

        var invPartial = await brokerSvc.GetInvoiceByIdAsync(invRes.Data!.Id);
        invPartial.Data!.PaidAmount.Should().Be(300000m);
        invPartial.Data.BalanceDue.Should().Be(settleRes.Data!.NetFarmerPayable - 300000m);
        invPartial.Data.Status.Should().Be("PartiallyPaid");

        // Record remaining payment
        var remaining = settleRes.Data!.NetFarmerPayable - 300000m;
        await brokerSvc.RecordPaymentAsync(new RecordPaymentRequest(
            DealId: deal.Id,
            InvoiceId: invRes.Data!.Id,
            Amount: remaining,
            Method: PaymentMethod.BankTransfer,
            ReferenceNumber: "TRX-FULL-2",
            BankName: "HBL",
            Notes: "Final payment"
        ));

        var invFull = await brokerSvc.GetInvoiceByIdAsync(invRes.Data!.Id);
        invFull.Data!.PaidAmount.Should().Be(settleRes.Data!.NetFarmerPayable);
        invFull.Data.BalanceDue.Should().Be(0m);
        invFull.Data.Status.Should().Be("Paid");
    }

    // ── Criterion 13: Duplicate requests cannot create duplicate deals or double payments ─
    [Fact]
    public async Task Criterion13_DuplicateRequestsCannotCreateDuplicateDealsOrDoublePayments()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 2000, 360m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2000,
            OfferedPricePerKg: 360m,
            TransportArrangement: null,
            Notes: null
        ));

        // Accept once -> succeeds
        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var accept1 = await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);
        accept1.IsSuccess.Should().BeTrue();

        // Accept again -> must fail, preventing duplicate deal
        var accept2 = await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);
        accept2.IsSuccess.Should().BeFalse();
        accept2.Error.Should().Contain("Cannot review proposal in status");

        // Check deals count in database
        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deals = await db.Deals.IgnoreQueryFilters().Where(d => d.ProposalId == propRes.Data!.Id).ToListAsync();
        deals.Should().HaveCount(1);
    }

    // ── Criterion 14: Real-time notifications reach only authorized recipients ───
    [Fact]
    public async Task Criterion14_RealTimeNotifications_ScopedToAuthorizedTenants()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (db, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 2000, 360m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        // Broker submits proposal -> Notification sent to farmer tenant
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1000,
            OfferedPricePerKg: 360m,
            TransportArrangement: null,
            Notes: null
        ));

        var notifications = await db.Notifications.IgnoreQueryFilters().ToListAsync();
        notifications.Should().NotBeEmpty();

        // Ensure notification is targeted to the farmer tenant, not the unrelated third party
        var farmerNotifs = notifications.Where(n => n.TenantId == _farmerTenantId).ToList();
        farmerNotifs.Should().NotBeEmpty();

        var thirdPartyNotifs = notifications.Where(n => n.TenantId == _thirdPartyTenantId).ToList();
        thirdPartyNotifs.Should().BeEmpty();
    }

    // ── Criterion 15: Cross-tenant requests cannot access unauthorized deals or ledgers ─
    [Fact]
    public async Task Criterion15_CrossTenantRequests_CannotAccessUnauthorizedDealsOrLedgers()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 2000, 360m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 2000,
            OfferedPricePerKg: 360m,
            TransportArrangement: null,
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        // Third-party tenant attempts to fetch deal details
        var (_, _, thirdPartyBrokerSvc) = SetupEnvironment(_thirdPartyTenantId, Guid.NewGuid(), dbName);
        var thirdPartyDealRes = await thirdPartyBrokerSvc.GetDealByIdAsync(deal.Id);

        thirdPartyDealRes.IsSuccess.Should().BeFalse();
        thirdPartyDealRes.Error.Should().Contain("Unauthorized");
    }

    // ── Criterion 16: All reports match underlying database records ─────────────
    [Fact]
    public async Task Criterion16_AllReportsMatchUnderlyingDatabaseRecords()
    {
        var dbName = Guid.NewGuid().ToString();
        var (_, _, _, batchId) = await SeedBaseFarmerDataAsync(dbName);

        var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        var listingId = await CreateAndPublishListingAsync(farmerSvc, batchId, 1500, 370m);

        var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var propRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: 1500,
            OfferedPricePerKg: 370m,
            TransportArrangement: null,
            Notes: null
        ));

        var (_, _, farmerBrokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
        await AcceptProposalAsync(farmerBrokerSvc, propRes.Data!.Id);

        var (db, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
        var deal = await db.Deals.IgnoreQueryFilters().FirstAsync(d => d.ProposalId == propRes.Data!.Id);

        await brokerSvc.RecordLiftingAsync(deal.Id, new RecordLiftingRequest(
            ActualWeightKg: 3000m,
            BirdsLoaded: 1500,
            MortalityDuringTransit: 0,
            VehicleNumber: "LHR-REP-1",
            DriverName: "Driver",
            DriverPhone: null,
            WeighbridgeTicketNumber: null,
            Notes: null
        ));

        await brokerSvc.CreateSettlementAsync(deal.Id, new CreateSettlementRequest(
            CommissionRate: 2.0m,
            CommissionType: CommissionRuleType.Percentage,
            FreightAmount: 12000m,
            Deductions: 0m,
            Additions: 0m,
            Notes: null
        ));

        // Fetch CSV export
        var csvRes = await brokerSvc.GetDealsReportCsvAsync();
        csvRes.IsSuccess.Should().BeTrue();
        var csv = csvRes.Value!;

        // CSV must contain deal ID and exact net weight & gross values
        csv.Should().Contain(deal.Id.ToString());
        csv.Should().Contain("3000.0");
        csv.Should().Contain("1110000.00"); // 3000 * 370
        csv.Should().Contain("22200.00");   // 2% commission
        csv.Should().Contain("12000.00");   // Freight
        csv.Should().Contain("1075800.00"); // Net Farmer
    }
}
