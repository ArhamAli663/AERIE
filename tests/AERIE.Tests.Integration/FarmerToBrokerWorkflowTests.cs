namespace AERIE.Tests.Integration;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Services;
using AERIE.Application.Common.Interfaces;
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

public class FarmerToBrokerWorkflowTests
{
    private readonly Guid _farmerTenantId = Guid.NewGuid();
    private readonly Guid _brokerTenantId = Guid.NewGuid();
    private readonly Guid _farmerUserId = Guid.NewGuid();
    private readonly Guid _brokerUserId = Guid.NewGuid();

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

    [Fact]
    public async Task CompleteFarmerToBrokerWorkflow_FromFlockToSettlementAndPayment_ExecutesSuccessfully()
    {
        var dbName = Guid.NewGuid().ToString();

        // ── Seed Reference Data & Tenant Companies ────────────────────────────
        var breedId = Guid.NewGuid();
        {
            var (db, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            db.Tenants.AddRange(
                new Tenant { Id = _farmerTenantId, Name = "Al-Madina Poultry Farms", Slug = "al-madina" },
                new Tenant { Id = _brokerTenantId, Name = "Punjab Poultry Traders", Slug = "punjab-traders" }
            );

            db.Breeds.Add(new Breed { Id = breedId, Name = "Cobb 500", TargetMarketWeightGrams = 2100m, TargetMarketAgedays = 35 });
            await db.SaveChangesAsync();
        }

        // ── Step 1: Farmer creates farm and poultry house ─────────────────────
        Guid farmId;
        Guid houseId;
        {
            var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farmRes = await farmerSvc.CreateFarmAsync(new CreateFarmRequest(
                Name: "Green Pastures Farm",
                Location: "Sheikhupura Road",
                City: "Lahore",
                Province: "Punjab",
                TotalCapacity: 25000,
                ContactPhone: "03001234567",
                Notes: "Modern environmentally controlled sheds"
            ));
            farmRes.IsSuccess.Should().BeTrue();
            farmId = farmRes.Data!.Id;

            var houseRes = await farmerSvc.CreatePoultryHouseAsync(new CreatePoultryHouseRequest(
                FarmId: farmId,
                Name: "Shed #1",
                Capacity: 10000,
                Description: "Pad-cooling system equipped"
            ));
            houseRes.IsSuccess.Should().BeTrue();
            houseId = houseRes.Data!.Id;
        }

        // ── Step 2: Farmer registers flock batch and chick placement ──────────
        Guid batchId;
        {
            var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var batchRes = await farmerSvc.CreateFlockBatchAsync(new CreateFlockBatchRequest(
                FarmId: farmId,
                PoultryHouseId: houseId,
                BreedId: breedId,
                PlacementDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35)),
                InitialChickCount: 5000,
                CostPerChick: 85.00m,
                SupplierReference: "Jadeed Chicks Ltd",
                Notes: "Vaccinated for ND and IB"
            ));
            batchRes.IsSuccess.Should().BeTrue();
            batchId = batchRes.Data!.Id;
            batchRes.Data.CurrentAliveCount.Should().Be(5000);
        }

        // ── Step 3: Farmer records daily observations ─────────────────────────
        {
            var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var recordRes = await farmerSvc.RecordDailyDataAsync(new CreateDailyRecordRequest(
                BatchId: batchId,
                RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
                AliveCount: 4850,
                DailyMortality: 15,
                FeedConsumedKg: 450.0m,
                WaterConsumedLiters: 900.0m,
                AverageWeightGrams: 2150.0m,
                Notes: "Flock active, good appetite",
                ClientRecordId: Guid.NewGuid().ToString()
            ));
            recordRes.IsSuccess.Should().BeTrue();
            recordRes.Data!.AliveCount.Should().Be(4850);
        }

        // ── Step 4: System calculates metrics (FCR, ADG, Mortality) ───────────
        {
            var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var metricsRes = await farmerSvc.GetProductionMetricsAsync(batchId);
            metricsRes.IsSuccess.Should().BeTrue();
            metricsRes.Data!.MortalityRatePct.Should().BeGreaterThan(0);
        }

        // ── Step 5: Farmer marks batch as ready for sale ──────────────────────
        {
            var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var readyRes = await farmerSvc.MarkBatchReadyForSaleAsync(batchId);
            readyRes.IsSuccess.Should().BeTrue();
            readyRes.Data!.Status.Should().Be(BatchStatus.ReadyForSale);
        }

        // ── Step 6: Farmer creates marketplace listing and publishes it ───────
        Guid listingId;
        {
            var (_, farmerSvc, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var listingRes = await farmerSvc.CreateListingAsync(new CreateListingRequest(
                BatchId: batchId,
                Title: "4,850 Premium Cobb 500 Broilers - 2.15kg Avg",
                QuantityTotal: 4850,
                AskingPricePerKg: 340.0m,
                NegotiableFloorPricePerKg: 335.0m,
                LocationCity: "Lahore",
                LocationDetails: "Near Toll Plaza, Sheikhupura Rd",
                ExpiresAt: DateTime.UtcNow.AddDays(3),
                Notes: "Ready for immediate lifting"
            ));
            listingRes.IsSuccess.Should().BeTrue();
            listingId = listingRes.Data!.Id;

            var publishRes = await farmerSvc.PublishListingAsync(listingId);
            publishRes.IsSuccess.Should().BeTrue();
            publishRes.Data!.Status.Should().Be(ListingStatus.Published.ToString());
        }

        // ── Step 7: Broker views published marketplace listing ────────────────
        {
            var (_, farmerSvc, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var publicListings = await farmerSvc.GetPublicMarketplaceListingsAsync();
            publicListings.IsSuccess.Should().BeTrue();
            publicListings.Data!.Should().Contain(l => l.Id == listingId);
        }

        // ── Step 8: Broker creates purchase proposal ──────────────────────────
        Guid proposalId;
        {
            var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var proposalRes = await brokerSvc.CreateProposalAsync(new CreateProposalRequest(
                ListingId: listingId,
                QuantityRequested: 2000,
                OfferedPricePerKg: 338.0m,
                TransportArrangement: "Buyer will provide 2 trucks",
                Notes: "Delivery to Tollinton Market"
            ));
            proposalRes.IsSuccess.Should().BeTrue();
            proposalId = proposalRes.Data!.Id;
            proposalRes.Data.Status.Should().Be(DealStatus.Proposed);
        }

        // ── Step 9 & 10: Farmer reviews and accepts proposal -> Deal created ──
        Guid dealId;
        {
            var (_, _, brokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var reviewRes = await brokerSvc.ReviewProposalAsync(proposalId, new ReviewProposalRequest(
                Accept: true,
                ResponseNotes: "Accepted. Please lift tomorrow morning."
            ));
            reviewRes.IsSuccess.Should().BeTrue();
            dealId = reviewRes.Data!.Id;
            reviewRes.Data.Status.Should().Be(DealStatus.Accepted);
            reviewRes.Data.QuantityBirds.Should().Be(2000);
            reviewRes.Data.AgreedPricePerKg.Should().Be(338.0m);
        }

        // ── Step 11: Verify both parties see the same accepted deal ───────────
        {
            var (_, _, brokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farmerDeal = await brokerSvc.GetDealByIdAsync(dealId);
            farmerDeal.IsSuccess.Should().BeTrue();
            farmerDeal.Data!.QuantityBirds.Should().Be(2000);
        }

        {
            var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var brokerDeal = await brokerSvc.GetDealByIdAsync(dealId);
            brokerDeal.IsSuccess.Should().BeTrue();
            brokerDeal.Data!.QuantityBirds.Should().Be(2000);
        }

        // ── Step 12: Record lifting/transport ─────────────────────────────────
        {
            var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var liftRes = await brokerSvc.RecordLiftingAsync(dealId, new RecordLiftingRequest(
                ActualWeightKg: 4320.0m,
                BirdsLoaded: 2000,
                MortalityDuringTransit: 5,
                VehicleNumber: "LES-4589",
                DriverName: "Muhammad Aslam",
                DriverPhone: "03217654321",
                WeighbridgeTicketNumber: "WB-2024-8841",
                Notes: "Loaded at 5:30 AM"
            ));
            liftRes.IsSuccess.Should().BeTrue();
            liftRes.Data!.NetWeightKg.Should().Be(4320.0m);
        }

        // ── Step 13: Calculate settlement (commission, freight, net amounts) ──
        {
            var (_, _, brokerSvc) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            // Gross = 4320 kg * 338 Rs/kg = 1,460,160 Rs
            // Commission (2%) = 29,203.20 Rs
            // Freight = 15,000 Rs
            // Net to Farmer = 1,460,160 - 29,203.20 - 15,000 = 1,415,956.80 Rs
            var settleRes = await brokerSvc.CreateSettlementAsync(dealId, new CreateSettlementRequest(
                CommissionRate: 2.0m,
                CommissionType: CommissionRuleType.Percentage,
                FreightAmount: 15000.0m,
                Deductions: 0m,
                Additions: 0m,
                Notes: "Standard 2% brokerage applied"
            ));
            settleRes.IsSuccess.Should().BeTrue();
            settleRes.Data!.TotalGrossAmount.Should().Be(1460160.00m);
            settleRes.Data!.CommissionAmount.Should().Be(29203.20m);
            settleRes.Data!.NetFarmerPayable.Should().Be(1415956.80m);
        }

        // ── Step 14: Generate invoice ─────────────────────────────────────────
        Guid invoiceId;
        {
            var (_, _, brokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var invRes = await brokerSvc.CreateInvoiceAsync(new CreateInvoiceRequest(
                DealId: dealId,
                RecipientTenantId: _brokerTenantId,
                RecipientName: "Punjab Poultry Traders",
                RecipientAddress: "Tollinton Market, Lahore",
                SubTotal: 1415956.80m,
                TaxAmount: 0m,
                TotalAmount: 1415956.80m,
                DueDate: DateTime.UtcNow.AddDays(7),
                Notes: "Payment due within 7 days"
            ));
            invRes.IsSuccess.Should().BeTrue();
            invoiceId = invRes.Data!.Id;
            invRes.Data.InvoiceNumber.Should().StartWith("INV-");
            invRes.Data.BalanceDue.Should().Be(1415956.80m);
        }

        // ── Step 15: Record payment and write ledger entries ──────────────────
        {
            var (_, _, brokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var payRes = await brokerSvc.RecordPaymentAsync(new RecordPaymentRequest(
                DealId: dealId,
                InvoiceId: invoiceId,
                Amount: 1415956.80m,
                Method: PaymentMethod.BankTransfer,
                ReferenceNumber: "HBL-TRX-998822",
                BankName: "Habib Bank Limited",
                Notes: "Full payment received"
            ));
            payRes.IsSuccess.Should().BeTrue();
            payRes.Data!.Status.Should().Be("Confirmed");

            // Verify deal status is now Paid
            var finalDeal = await brokerSvc.GetDealByIdAsync(dealId);
            finalDeal.Data!.Status.Should().Be(DealStatus.Paid);
        }

        // ── Step 16: Verify ledger entries were recorded for audit ────────────
        {
            // Farmer ledger sees Credit (sales proceeds)
            var (db, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var farmerLedger = await db.LedgerEntries.Where(l => l.DealId == dealId).ToListAsync();
            farmerLedger.Should().NotBeEmpty();
            farmerLedger.Should().Contain(l => l.EntryType == LedgerEntryType.Credit && l.Amount == 1415956.80m);

            // Broker ledger sees Debit (trade settlement disbursement)
            var (brokerDb, _, _) = SetupEnvironment(_brokerTenantId, _brokerUserId, dbName);
            var brokerLedger = await brokerDb.LedgerEntries.Where(l => l.DealId == dealId).ToListAsync();
            brokerLedger.Should().NotBeEmpty();
            brokerLedger.Should().Contain(l => l.EntryType == LedgerEntryType.Debit && l.Amount == 1415956.80m);
        }
    }

    [Fact]
    public async Task OversellingPrevention_WhenProposalExceedsAvailableQuantity_AcceptanceFails()
    {
        var dbName = Guid.NewGuid().ToString();

        // Arrange: Listing has 500 birds available
        var listingId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        {
            var (db, _, _) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            db.Tenants.AddRange(
                new Tenant { Id = _farmerTenantId, Name = "Farmer Co", Slug = "farmer-co" },
                new Tenant { Id = _brokerTenantId, Name = "Broker Co", Slug = "broker-co" }
            );

            var batch = new FlockBatch
            {
                Id = Guid.NewGuid(),
                TenantId = _farmerTenantId,
                BatchCode = "BATCH-TEST",
                InitialChickCount = 500,
                CurrentAliveCount = 500,
                Status = BatchStatus.ReadyForSale
            };
            db.FlockBatches.Add(batch);

            var listing = new MarketplaceListing
            {
                Id = listingId,
                TenantId = _farmerTenantId,
                BatchId = batch.Id,
                Batch = batch,
                Title = "Test Flock",
                QuantityTotal = 500,
                QuantityAvailable = 500, // Only 500 available
                AskingPricePerKg = 300m,
                Status = ListingStatus.Published
            };
            db.MarketplaceListings.Add(listing);

            // Broker creates a proposal for 600 birds (more than available)
            var proposal = new DealProposal
            {
                Id = proposalId,
                TenantId = _brokerTenantId,
                BrokerTenantId = _brokerTenantId,
                ListingId = listingId,
                Listing = listing,
                ProposedByUserId = _brokerUserId,
                QuantityRequested = 600, // Exceeds 500!
                OfferedPricePerKg = 300m,
                Status = DealStatus.Proposed
            };
            db.DealProposals.Add(proposal);

            await db.SaveChangesAsync();
        }

        // Act: Farmer attempts to accept proposal for 600 birds
        {
            var (_, _, brokerSvc) = SetupEnvironment(_farmerTenantId, _farmerUserId, dbName);
            var result = await brokerSvc.ReviewProposalAsync(proposalId, new ReviewProposalRequest(
                Accept: true,
                ResponseNotes: "Accepting"
            ));

            // Assert: Must fail to prevent overselling!
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Available quantity");
        }
    }
}
