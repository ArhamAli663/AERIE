namespace AERIE.Infrastructure.Persistence;

using AERIE.Domain.Constants;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class AerieDatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IServiceProvider serviceProvider, ILogger logger)
    {
        try
        {
            // Seed Identity Roles & Users first
            var roleManager = serviceProvider.GetService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetService<UserManager<ApplicationUser>>();

            if (roleManager != null && userManager != null)
            {
                foreach (var role in new[] { "Farmer", "Broker", "Admin" })
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new ApplicationRole { Name = role });
                    }
                }

                if (await userManager.FindByEmailAsync("farmer@aerie.pk") == null)
                {
                    var farmerUser = new ApplicationUser
                    {
                        Id = AerieDemoConstants.FarmerUserId,
                        UserName = "farmer@aerie.pk",
                        Email = "farmer@aerie.pk",
                        FirstName = "Haji Muhammad",
                        LastName = "Aslam",
                        TenantId = AerieDemoConstants.FarmerTenantId,
                        IsActive = true,
                        EmailConfirmed = true
                    };
                    var r1 = await userManager.CreateAsync(farmerUser, "Password123!");
                    if (r1.Succeeded)
                    {
                        await userManager.AddToRoleAsync(farmerUser, "Farmer");
                    }
                }

                if (await userManager.FindByEmailAsync("broker@aerie.pk") == null)
                {
                    var brokerUser = new ApplicationUser
                    {
                        Id = AerieDemoConstants.BrokerUserId,
                        UserName = "broker@aerie.pk",
                        Email = "broker@aerie.pk",
                        FirstName = "Mian Tariq",
                        LastName = "Mehmood",
                        TenantId = AerieDemoConstants.BrokerTenantId,
                        IsActive = true,
                        EmailConfirmed = true
                    };
                    var r2 = await userManager.CreateAsync(brokerUser, "Password123!");
                    if (r2.Succeeded)
                    {
                        await userManager.AddToRoleAsync(brokerUser, "Broker");
                    }
                }
            }

            if (await context.Tenants.IgnoreQueryFilters().AnyAsync())
            {
                return; // Already seeded
            }

            logger.LogInformation("[AERIE Seeder] Seeding initial demonstrator ERP data...");

            // 1. Tenants
            var farmerTenant = new Tenant
            {
                Id = AerieDemoConstants.FarmerTenantId,
                Name = "Al-Madina Poultry Farms",
                Slug = "al-madina-poultry",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var brokerTenant = new Tenant
            {
                Id = AerieDemoConstants.BrokerTenantId,
                Name = "Punjab Poultry Traders",
                Slug = "punjab-poultry-traders",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Tenants.AddRange(farmerTenant, brokerTenant);

            // 2. Breeds
            var cobb500 = new Breed
            {
                Id = Guid.NewGuid(),
                Name = "Cobb 500",
                Lineage = "Cobb-Vantress",
                TargetFcr = 1.68m,
                TargetMarketAgedays = 35,
                TargetMarketWeightGrams = 2200m,
                CreatedAt = DateTime.UtcNow
            };

            var ross308 = new Breed
            {
                Id = Guid.NewGuid(),
                Name = "Ross 308",
                Lineage = "Aviagen",
                TargetFcr = 1.72m,
                TargetMarketAgedays = 36,
                TargetMarketWeightGrams = 2250m,
                CreatedAt = DateTime.UtcNow
            };

            context.Breeds.AddRange(cobb500, ross308);

            // 3. Farms & Sheds (Farmer Tenant)
            var chunianFarm = new Farm
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                Name = "Chunian Model Poultry Farm",
                Location = "Chunian Bypass, Kasur District",
                City = "Kasur",
                Province = "Punjab",
                TotalCapacity = 50000,
                ContactPhone = "+92 300 1234567",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var shed1 = new PoultryHouse
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                FarmId = chunianFarm.Id,
                Name = "Shed-1 (Environment Controlled)",
                Capacity = 25000,
                Description = "Modern tunnel ventilated shed with automatic feeding",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var shed2 = new PoultryHouse
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                FarmId = chunianFarm.Id,
                Name = "Shed-2 (Environment Controlled)",
                Capacity = 25000,
                Description = "Modern tunnel ventilated shed with pad cooling",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Farms.Add(chunianFarm);
            context.PoultryHouses.AddRange(shed1, shed2);

            // 4. Flock Batches
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var batch1 = new FlockBatch
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                FarmId = chunianFarm.Id,
                PoultryHouseId = shed1.Id,
                BreedId = cobb500.Id,
                BatchCode = "FL-2026-CH01",
                PlacementDate = today.AddDays(-32),
                InitialChickCount = 25000,
                CurrentAliveCount = 24120,
                CostPerChick = 85.00m,
                CalculatedFcr = 1.64m,
                TotalMortality = 3.52m,
                LatestAvgWeightGrams = 1780.0m,
                Status = BatchStatus.Active,
                SupplierReference = "Big Bird Hatcheries",
                CreatedAt = DateTime.UtcNow
            };

            var batch2 = new FlockBatch
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                FarmId = chunianFarm.Id,
                PoultryHouseId = shed2.Id,
                BreedId = ross308.Id,
                BatchCode = "FL-2026-CH02",
                PlacementDate = today.AddDays(-38),
                InitialChickCount = 20000,
                CurrentAliveCount = 19450,
                CostPerChick = 82.50m,
                CalculatedFcr = 1.66m,
                TotalMortality = 2.75m,
                LatestAvgWeightGrams = 2100.0m,
                Status = BatchStatus.ReadyForSale,
                SupplierReference = "Jadeed Hatcheries",
                CreatedAt = DateTime.UtcNow
            };

            context.FlockBatches.AddRange(batch1, batch2);

            // Chick Placements
            context.ChickPlacements.AddRange(
                new ChickPlacement
                {
                    Id = Guid.NewGuid(),
                    TenantId = AerieDemoConstants.FarmerTenantId,
                    BatchId = batch1.Id,
                    PlacementDate = batch1.PlacementDate,
                    Quantity = batch1.InitialChickCount,
                    CostPerChick = batch1.CostPerChick,
                    SupplierName = "Big Bird Hatcheries",
                    CreatedAt = DateTime.UtcNow
                },
                new ChickPlacement
                {
                    Id = Guid.NewGuid(),
                    TenantId = AerieDemoConstants.FarmerTenantId,
                    BatchId = batch2.Id,
                    PlacementDate = batch2.PlacementDate,
                    Quantity = batch2.InitialChickCount,
                    CostPerChick = batch2.CostPerChick,
                    SupplierName = "Jadeed Hatcheries",
                    CreatedAt = DateTime.UtcNow
                }
            );

            // 5. Daily Records sample
            context.DailyFlockRecords.Add(new DailyFlockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                BatchId = batch1.Id,
                RecordDate = today,
                OpeningCount = 24132,
                DailyMortality = 12,
                ClosingCount = 24120,
                AliveCount = 24120,
                FeedConsumedKg = 2450.0m,
                WaterConsumedLiters = 5100.0m,
                AverageWeightGrams = 1780.0m,
                FeedCategory = "Finisher",
                CreatedAt = DateTime.UtcNow
            });

            // 6. Marketplace Listing for Batch 2
            var listing = new MarketplaceListing
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.FarmerTenantId,
                BatchId = batch2.Id,
                Title = "High-Yield Ready Broiler Batch (Ross 308) - 15,000 birds",
                AskingPricePerKg = 340.00m,
                NegotiableFloorPricePerKg = 335.00m,
                QuantityTotal = 15000,
                QuantityAvailable = 15000,
                LocationCity = chunianFarm.City ?? "Kasur",
                LocationDetails = chunianFarm.Location,
                Status = ListingStatus.Published,
                PublishedAt = DateTime.UtcNow,
                Notes = "Excellent feed conversion, weighted and ready for immediate lifting.",
                CreatedAt = DateTime.UtcNow
            };

            context.MarketplaceListings.Add(listing);

            // 7. Deal Proposal and Active Deal in Transit
            var proposal = new DealProposal
            {
                Id = Guid.NewGuid(),
                TenantId = AerieDemoConstants.BrokerTenantId,
                BrokerTenantId = AerieDemoConstants.BrokerTenantId,
                ListingId = listing.Id,
                BrokerCompanyName = brokerTenant.Name,
                ProposedByUserId = AerieDemoConstants.BrokerUserId,
                QuantityRequested = 5000,
                OfferedPricePerKg = 342.00m,
                TransportArrangement = "Broker Arranged (Direct Lahore Lifting)",
                Notes = "Lifting via 6-wheel carrier LES-4921",
                Status = DealStatus.Accepted,
                ProposedAt = DateTime.UtcNow.AddHours(-5),
                RespondedAt = DateTime.UtcNow.AddHours(-4),
                CreatedAt = DateTime.UtcNow
            };

            context.DealProposals.Add(proposal);

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                ProposalId = proposal.Id,
                ListingId = listing.Id,
                FarmerTenantId = AerieDemoConstants.FarmerTenantId,
                BrokerTenantId = AerieDemoConstants.BrokerTenantId,
                QuantityBirds = 5000,
                AgreedPricePerKg = 342.00m,
                EstimatedWeightKg = 10500.0m,
                Status = DealStatus.InTransit,
                AcceptedAt = DateTime.UtcNow.AddHours(-4),
                Notes = "Lifting completed at farm weighbridge, truck dispatched to Lahore Tollinton Mandi.",
                CreatedAt = DateTime.UtcNow
            };

            context.Deals.Add(deal);

            var transport = new TransportRecord
            {
                Id = Guid.NewGuid(),
                DealId = deal.Id,
                VehicleNumber = "LES-4921",
                DriverName = "Muhammad Akram",
                DriverPhone = "+92 301 7654321",
                PickupLocation = chunianFarm.Location,
                DestinationLocation = "Tollinton Market, Lahore",
                BirdsLoaded = 5000,
                WeightLiftedKg = 10500.0m,
                DeliveryStatus = "InTransit",
                FreightCost = 28000.0m,
                DepartureAt = DateTime.UtcNow.AddHours(-3),
                CreatedAt = DateTime.UtcNow
            };

            context.TransportRecords.Add(transport);

            // 8. Live Market Prices — All Major Pakistan Cities (Punjab, Sindh, KPK, Balochistan, AJK)
            context.MarketPrices.AddRange(
                new MarketPrice { Id = Guid.NewGuid(), City = "Lahore", MarketName = "Tollinton Mandi", PricePerKg = 345.00m, PreviousPricePerKg = 340.00m, Source = "Lahore Poultry Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Rawalpindi", MarketName = "Gunj Mandi", PricePerKg = 352.00m, PreviousPricePerKg = 345.00m, Source = "Rawalpindi Poultry Traders Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Islamabad", MarketName = "I-11 Wholesale Mandi", PricePerKg = 354.00m, PreviousPricePerKg = 348.00m, Source = "Capital Market Committee", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Karachi", MarketName = "Lee Market & Water Pump Mandi", PricePerKg = 365.00m, PreviousPricePerKg = 360.00m, Source = "Karachi Wholesalers Poultry Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Faisalabad", MarketName = "Ghalla Mandi", PricePerKg = 340.00m, PreviousPricePerKg = 340.00m, Source = "Faisalabad Broiler Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Gujranwala", MarketName = "Main Wholesale Mandi", PricePerKg = 342.00m, PreviousPricePerKg = 340.00m, Source = "District Market Committee", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Multan", MarketName = "Grain & Poultry Market", PricePerKg = 338.00m, PreviousPricePerKg = 340.00m, Source = "Multan Traders Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Sargodha", MarketName = "Satellite Town Mandi", PricePerKg = 339.00m, PreviousPricePerKg = 335.00m, Source = "Sargodha Poultry Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Sialkot", MarketName = "Kashmir Road Mandi", PricePerKg = 346.00m, PreviousPricePerKg = 342.00m, Source = "Sialkot Broiler Board", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Peshawar", MarketName = "Charsadda Road Mandi", PricePerKg = 358.00m, PreviousPricePerKg = 352.00m, Source = "KP Poultry Wholesalers Federation", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Quetta", MarketName = "Joint Road Mandi", PricePerKg = 372.00m, PreviousPricePerKg = 368.00m, Source = "Balochistan Broiler Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Hyderabad", MarketName = "Tower Market", PricePerKg = 360.00m, PreviousPricePerKg = 355.00m, Source = "Hyderabad Traders Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Bahawalpur", MarketName = "Commercial Mandi", PricePerKg = 336.00m, PreviousPricePerKg = 338.00m, Source = "Bahawalpur Broiler Committee", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Sahiwal", MarketName = "Main Ghalla Mandi", PricePerKg = 337.00m, PreviousPricePerKg = 335.00m, Source = "Sahiwal Mandi Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Rahim Yar Khan", MarketName = "Model Town Mandi", PricePerKg = 335.00m, PreviousPricePerKg = 335.00m, Source = "RYK Poultry Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Abbottabad", MarketName = "Supply Mandi", PricePerKg = 362.00m, PreviousPricePerKg = 358.00m, Source = "Hazara Poultry Traders", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Mardan", MarketName = "Swabi Road Mandi", PricePerKg = 356.00m, PreviousPricePerKg = 350.00m, Source = "Mardan Poultry Committee", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Gujrat", MarketName = "Circular Road Mandi", PricePerKg = 344.00m, PreviousPricePerKg = 342.00m, Source = "Gujrat Broiler Board", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Jhelum", MarketName = "Civil Lines Mandi", PricePerKg = 348.00m, PreviousPricePerKg = 345.00m, Source = "Jhelum Traders Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Okara", MarketName = "Katchery Bazaar Mandi", PricePerKg = 338.00m, PreviousPricePerKg = 336.00m, Source = "Okara Broiler Council", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Sheikhupura", MarketName = "Lahore Road Mandi", PricePerKg = 342.00m, PreviousPricePerKg = 340.00m, Source = "District Sheikhupura Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Kasur", MarketName = "Raja Bazaar Mandi", PricePerKg = 341.00m, PreviousPricePerKg = 338.00m, Source = "Kasur Broiler Traders", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Sukkur", MarketName = "Bunder Road Mandi", PricePerKg = 355.00m, PreviousPricePerKg = 350.00m, Source = "Upper Sindh Poultry Union", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Larkana", MarketName = "Resham Gali Market", PricePerKg = 358.00m, PreviousPricePerKg = 352.00m, Source = "Larkana Poultry Committee", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Muzaffarabad", MarketName = "Bank Road Mandi", PricePerKg = 366.00m, PreviousPricePerKg = 360.00m, Source = "AJK Broiler Federation", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
                new MarketPrice { Id = Guid.NewGuid(), City = "Mirpur", MarketName = "Sector F-1 Mandi", PricePerKg = 355.00m, PreviousPricePerKg = 350.00m, Source = "Mirpur Traders Association", RecordedByUserId = AerieDemoConstants.BrokerUserId, RecordedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
            );

            // 9. Poultry Shops, Wholesale Mandis & Retail Outlets (Google-Analyzed Directory)
            context.PoultryShops.AddRange(
                // Lahore
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Al-Rehman Poultry & Meat Shop",
                    City = "Lahore",
                    Province = "Punjab",
                    MarketName = "Tollinton Market",
                    Address = "Shop #14, Main Tollinton Market, Jail Road, Lahore",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Haji Abdul Rehman",
                    ContactPhone = "+92 300 4567891",
                    DailyCapacityKg = 3800m,
                    CurrentBroilerMeatRatePerKg = 560m,
                    CurrentLiveBroilerBuyRatePerKg = 345m,
                    GoogleRating = 4.7,
                    GoogleReviewsCount = 94,
                    IsGoogleVerified = true,
                    OperatingHours = "06:00 AM - 10:00 PM",
                    Specialties = "Wholesale crates, live bird cutting, boneless fillets, daily bulk hotel supply",
                    Notes = "Top rated supplier in Tollinton market. Directly connected to Kasur and Okara farms."
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Bismillah Chicken & Poultry Traders",
                    City = "Lahore",
                    Province = "Punjab",
                    MarketName = "Sheranwala Gate Mandi",
                    Address = "Circular Road near Sheranwala Gate, Walled City, Lahore",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Mian Naveed Iqbal",
                    ContactPhone = "+92 321 8765432",
                    DailyCapacityKg = 3200m,
                    CurrentBroilerMeatRatePerKg = 555m,
                    CurrentLiveBroilerBuyRatePerKg = 344m,
                    GoogleRating = 4.5,
                    GoogleReviewsCount = 68,
                    IsGoogleVerified = true,
                    OperatingHours = "05:30 AM - 09:30 PM",
                    Specialties = "Daily live bird lifting, wholesale chicken delivery, restaurants supply",
                    Notes = "High volume wholesale trader. Buys from Sheikhupura and Gujranwala."
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Madina Poultry Center",
                    City = "Lahore",
                    Province = "Punjab",
                    MarketName = "DHA Commercial",
                    Address = "Plaza 22-B, Commercial Area, DHA Phase 5, Lahore",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Chaudhry Salman",
                    ContactPhone = "+92 301 9876543",
                    DailyCapacityKg = 1400m,
                    CurrentBroilerMeatRatePerKg = 580m,
                    CurrentLiveBroilerBuyRatePerKg = 346m,
                    GoogleRating = 4.8,
                    GoogleReviewsCount = 112,
                    IsGoogleVerified = true,
                    OperatingHours = "08:00 AM - 11:00 PM",
                    Specialties = "Hygienic machine-cut chicken, organic desi chicken, vacuum pack fillets",
                    Notes = "Premium retail buyer in DHA. High quality standards."
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Sabroso & Birdland Premium Chicken Hub",
                    City = "Lahore",
                    Province = "Punjab",
                    MarketName = "Model Town Link Road",
                    Address = "Main Boulevard, Model Town Link Road, Lahore",
                    ShopType = "Corporate Farm Outlet",
                    ContactPerson = "Kashif Mehmood",
                    ContactPhone = "+92 302 3456789",
                    DailyCapacityKg = 2100m,
                    CurrentBroilerMeatRatePerKg = 590m,
                    CurrentLiveBroilerBuyRatePerKg = 347m,
                    GoogleRating = 4.9,
                    GoogleReviewsCount = 230,
                    IsGoogleVerified = true,
                    OperatingHours = "09:00 AM - 11:00 PM",
                    Specialties = "Chilled poultry, frozen cuts, ready-to-cook items, institutional orders"
                },

                // Rawalpindi & Islamabad
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Chaudhry Poultry Commission Shop #42",
                    City = "Rawalpindi",
                    Province = "Punjab",
                    MarketName = "Gunj Mandi",
                    Address = "Shop #42, Main Gunj Mandi Wholesale Market, Rawalpindi",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Chaudhry Zafar Iqbal",
                    ContactPhone = "+92 300 5123456",
                    DailyCapacityKg = 5200m,
                    CurrentBroilerMeatRatePerKg = 570m,
                    CurrentLiveBroilerBuyRatePerKg = 352m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 82,
                    IsGoogleVerified = true,
                    OperatingHours = "05:00 AM - 08:00 PM",
                    Specialties = "Direct farm lifting, truckload weighbridge receiving, wholesale distribution",
                    Notes = "Leading commission arhti in Gunj Mandi Rawalpindi. Lifts from Chakwal and Attock."
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Khan Poultry & Live Bird Depot",
                    City = "Rawalpindi",
                    Province = "Punjab",
                    MarketName = "Pirwadhai Mandi",
                    Address = "Near General Bus Stand, Pirwadhai, Rawalpindi",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Sardar Gul Khan",
                    ContactPhone = "+92 333 5234567",
                    DailyCapacityKg = 3600m,
                    CurrentBroilerMeatRatePerKg = 565m,
                    CurrentLiveBroilerBuyRatePerKg = 351m,
                    GoogleRating = 4.5,
                    GoogleReviewsCount = 64,
                    IsGoogleVerified = true,
                    OperatingHours = "05:30 AM - 09:00 PM",
                    Specialties = "Live broiler supply, crate leasing, night lifting from KP and Punjab"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Islamabad Fresh Chicken Point",
                    City = "Islamabad",
                    Province = "Islamabad Capital Territory",
                    MarketName = "Aabpara Market",
                    Address = "Shop #8, Municipal Market, Sector G-6/1, Aabpara, Islamabad",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Malik Rizwan",
                    ContactPhone = "+92 312 5345678",
                    DailyCapacityKg = 1800m,
                    CurrentBroilerMeatRatePerKg = 585m,
                    CurrentLiveBroilerBuyRatePerKg = 354m,
                    GoogleRating = 4.7,
                    GoogleReviewsCount = 145,
                    IsGoogleVerified = true,
                    OperatingHours = "07:30 AM - 10:30 PM",
                    Specialties = "Clean dressed broiler, boneless cuts, hotel & hostel daily contracts"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Capital Poultry Specialists",
                    City = "Islamabad",
                    Province = "Islamabad Capital Territory",
                    MarketName = "F-10 Markaz",
                    Address = "Basement Plaza 14-D, F-10 Markaz, Islamabad",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Sheikh Daniyal",
                    ContactPhone = "+92 334 5456789",
                    DailyCapacityKg = 1600m,
                    CurrentBroilerMeatRatePerKg = 595m,
                    CurrentLiveBroilerBuyRatePerKg = 355m,
                    GoogleRating = 4.8,
                    GoogleReviewsCount = 178,
                    IsGoogleVerified = true,
                    OperatingHours = "08:00 AM - 11:00 PM",
                    Specialties = "Air-conditioned retail point, custom weight packaging, card payment accepted"
                },

                // Karachi
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Karachi Broiler Center (Arhti #18)",
                    City = "Karachi",
                    Province = "Sindh",
                    MarketName = "Lee Market",
                    Address = "Shop 18, Lee Market Wholesale Poultry Ground, Lyari, Karachi",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Haji Noor Muhammad",
                    ContactPhone = "+92 300 2134567",
                    DailyCapacityKg = 6500m,
                    CurrentBroilerMeatRatePerKg = 610m,
                    CurrentLiveBroilerBuyRatePerKg = 365m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 120,
                    IsGoogleVerified = true,
                    OperatingHours = "04:30 AM - 09:00 PM",
                    Specialties = "Large fleet lifting from Thatta and Hyderabad, wholesale supply across South Karachi",
                    Notes = "Major wholesale buyer in Sindh. Cash & fast bank settlement."
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Al-Madina Poultry Traders",
                    City = "Karachi",
                    Province = "Sindh",
                    MarketName = "Water Pump Market",
                    Address = "Block 14, Federal B Area near Water Pump Chowrangi, Karachi",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Muhammad Farooq",
                    ContactPhone = "+92 321 2245678",
                    DailyCapacityKg = 4200m,
                    CurrentBroilerMeatRatePerKg = 605m,
                    CurrentLiveBroilerBuyRatePerKg = 364m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 89,
                    IsGoogleVerified = true,
                    OperatingHours = "06:00 AM - 10:30 PM",
                    Specialties = "Bulk crate lifting, direct restaurant supply, catering poultry arrangements"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Saddar Fresh Chicken & Meat Hub",
                    City = "Karachi",
                    Province = "Sindh",
                    MarketName = "Empress Market",
                    Address = "Empress Market Inner Courtyard, Saddar, Karachi",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Syed Bilal Shah",
                    ContactPhone = "+92 333 2356789",
                    DailyCapacityKg = 3100m,
                    CurrentBroilerMeatRatePerKg = 615m,
                    CurrentLiveBroilerBuyRatePerKg = 366m,
                    GoogleRating = 4.7,
                    GoogleReviewsCount = 160,
                    IsGoogleVerified = true,
                    OperatingHours = "07:00 AM - 11:00 PM",
                    Specialties = "Historic retail poultry hub, bulk liver and gizzard packs, whole birds"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Defence Poultry & Organic Cuts",
                    City = "Karachi",
                    Province = "Sindh",
                    MarketName = "DHA Phase 5",
                    Address = "Khayaban-e-Shamsheer, Phase 5 Commercial, DHA, Karachi",
                    ShopType = "Corporate Farm Outlet",
                    ContactPerson = "Owais Siddiqui",
                    ContactPhone = "+92 345 2467890",
                    DailyCapacityKg = 1900m,
                    CurrentBroilerMeatRatePerKg = 630m,
                    CurrentLiveBroilerBuyRatePerKg = 368m,
                    GoogleRating = 4.9,
                    GoogleReviewsCount = 210,
                    IsGoogleVerified = true,
                    OperatingHours = "09:00 AM - 11:30 PM",
                    Specialties = "Organic feed broilers, antibiotic-free birds, customized chef portions"
                },

                // Faisalabad
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Haji Asghar Poultry Commission Shop",
                    City = "Faisalabad",
                    Province = "Punjab",
                    MarketName = "Ghalla Mandi",
                    Address = "Wholesale Mandi Gate 2, Ghalla Mandi, Faisalabad",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Haji Asghar Ali",
                    ContactPhone = "+92 300 6678901",
                    DailyCapacityKg = 4500m,
                    CurrentBroilerMeatRatePerKg = 545m,
                    CurrentLiveBroilerBuyRatePerKg = 340m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 74,
                    IsGoogleVerified = true,
                    OperatingHours = "05:00 AM - 08:30 PM",
                    Specialties = "Direct lifting from Jhang, Toba Tek Singh, and Samundri farms"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Lyallpur Fresh Chicken House",
                    City = "Faisalabad",
                    Province = "Punjab",
                    MarketName = "Clock Tower Bazaar",
                    Address = "Katchery Bazaar near Clock Tower, Faisalabad",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Mian Tariq",
                    ContactPhone = "+92 321 6789012",
                    DailyCapacityKg = 2400m,
                    CurrentBroilerMeatRatePerKg = 550m,
                    CurrentLiveBroilerBuyRatePerKg = 341m,
                    GoogleRating = 4.7,
                    GoogleReviewsCount = 92,
                    IsGoogleVerified = true,
                    OperatingHours = "07:00 AM - 10:00 PM",
                    Specialties = "Daily consumer retail cuts, hotel bulk supply, wedding catering orders"
                },

                // Multan
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "South Punjab Broiler Traders",
                    City = "Multan",
                    Province = "Punjab",
                    MarketName = "Grain & Poultry Market",
                    Address = "Vehari Road near New Ghalla Mandi, Multan",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Malik Khadim Hussain",
                    ContactPhone = "+92 300 7890123",
                    DailyCapacityKg = 3900m,
                    CurrentBroilerMeatRatePerKg = 540m,
                    CurrentLiveBroilerBuyRatePerKg = 338m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 58,
                    IsGoogleVerified = true,
                    OperatingHours = "05:00 AM - 09:00 PM",
                    Specialties = "Lifting across Multan, Shujabad, and Lodhran. Fast RTGS payment."
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Multan Model Poultry Outlet",
                    City = "Multan",
                    Province = "Punjab",
                    MarketName = "Ghanta Ghar Market",
                    Address = "Main Chowk Ghanta Ghar, Old City, Multan",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Rana Waqas",
                    ContactPhone = "+92 301 7901234",
                    DailyCapacityKg = 2200m,
                    CurrentBroilerMeatRatePerKg = 545m,
                    CurrentLiveBroilerBuyRatePerKg = 339m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 81,
                    IsGoogleVerified = true,
                    OperatingHours = "07:00 AM - 10:30 PM",
                    Specialties = "Fresh broiler cuts, liver/gizzard, boneless handi boti packs"
                },

                // Peshawar
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Khyber Poultry Depot & Commission Shop",
                    City = "Peshawar",
                    Province = "Khyber Pakhtunkhwa",
                    MarketName = "Charsadda Road Mandi",
                    Address = "Charsadda Road Wholesale Poultry Ground, Peshawar",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Haji Gulzar Khan",
                    ContactPhone = "+92 300 9012345",
                    DailyCapacityKg = 4800m,
                    CurrentBroilerMeatRatePerKg = 590m,
                    CurrentLiveBroilerBuyRatePerKg = 358m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 76,
                    IsGoogleVerified = true,
                    OperatingHours = "05:00 AM - 08:30 PM",
                    Specialties = "Wholesale birds from Punjab entering KP, major distributor for Peshawar and Mardan"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Peshawar Cantt Fresh Chicken",
                    City = "Peshawar",
                    Province = "Khyber Pakhtunkhwa",
                    MarketName = "Saddar Cantt",
                    Address = "Saddar Road near Gora Bazaar, Peshawar Cantt",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Arbab Jahangir",
                    ContactPhone = "+92 333 9123456",
                    DailyCapacityKg = 2100m,
                    CurrentBroilerMeatRatePerKg = 595m,
                    CurrentLiveBroilerBuyRatePerKg = 359m,
                    GoogleRating = 4.7,
                    GoogleReviewsCount = 110,
                    IsGoogleVerified = true,
                    OperatingHours = "07:30 AM - 10:00 PM",
                    Specialties = "Clean machine cutting, shinwari karahi portions, namak mandi bulk cuts"
                },

                // Gujranwala & Sialkot
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Gujranwala Broiler Supply Center",
                    City = "Gujranwala",
                    Province = "Punjab",
                    MarketName = "G.T. Road Mandi",
                    Address = "G.T. Road Bypass near Mandi Gate, Gujranwala",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Mian Arshad Cheema",
                    ContactPhone = "+92 300 6456789",
                    DailyCapacityKg = 3400m,
                    CurrentBroilerMeatRatePerKg = 550m,
                    CurrentLiveBroilerBuyRatePerKg = 342m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 62,
                    IsGoogleVerified = true,
                    OperatingHours = "05:30 AM - 09:00 PM",
                    Specialties = "Direct farm lifting from Hafizabad, Wazirabad, and Daska"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Sialkot Fresh Chicken Point",
                    City = "Sialkot",
                    Province = "Punjab",
                    MarketName = "Kashmir Road",
                    Address = "Kashmir Road near Defence Chowk, Sialkot",
                    ShopType = "Retail Chicken Point",
                    ContactPerson = "Chaudhry Nadeem",
                    ContactPhone = "+92 321 6567890",
                    DailyCapacityKg = 1800m,
                    CurrentBroilerMeatRatePerKg = 560m,
                    CurrentLiveBroilerBuyRatePerKg = 346m,
                    GoogleRating = 4.7,
                    GoogleReviewsCount = 95,
                    IsGoogleVerified = true,
                    OperatingHours = "07:00 AM - 10:30 PM",
                    Specialties = "Export workers hostel supply, fresh broiler cuts, marinated chicken"
                },

                // Quetta & Hyderabad
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Bolan Poultry Center",
                    City = "Quetta",
                    Province = "Balochistan",
                    MarketName = "Liaquat Bazaar",
                    Address = "Liaquat Bazaar Wholesale Line, Quetta",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Mir Dost Muhammad",
                    ContactPhone = "+92 300 8345678",
                    DailyCapacityKg = 3200m,
                    CurrentBroilerMeatRatePerKg = 635m,
                    CurrentLiveBroilerBuyRatePerKg = 372m,
                    GoogleRating = 4.5,
                    GoogleReviewsCount = 53,
                    IsGoogleVerified = true,
                    OperatingHours = "06:00 AM - 08:30 PM",
                    Specialties = "Receives long-distance refrigerated and ventilated carriers from South Punjab and Sindh"
                },
                new PoultryShop
                {
                    Id = Guid.NewGuid(),
                    Name = "Sindh Broiler Traders",
                    City = "Hyderabad",
                    Province = "Sindh",
                    MarketName = "Resham Bazaar",
                    Address = "Station Road near Resham Bazaar, Hyderabad",
                    ShopType = "Wholesale Mandi Shop",
                    ContactPerson = "Qazi Tariq",
                    ContactPhone = "+92 301 3234567",
                    DailyCapacityKg = 3100m,
                    CurrentBroilerMeatRatePerKg = 595m,
                    CurrentLiveBroilerBuyRatePerKg = 360m,
                    GoogleRating = 4.6,
                    GoogleReviewsCount = 67,
                    IsGoogleVerified = true,
                    OperatingHours = "06:00 AM - 09:30 PM",
                    Specialties = "Wholesale distribution for Latifabad, Qasimabad, and Kotri"
                }
            );

            await context.SaveChangesAsync();
            logger.LogInformation("[AERIE Seeder] Demonstrator database seeded successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AERIE Seeder] Error occurred while seeding demonstrator data.");
        }
    }
}
