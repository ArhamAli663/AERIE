CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE audit_logs (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "ActorUserId" uuid NOT NULL,
        "EntityName" text NOT NULL,
        "EntityId" uuid NOT NULL,
        "Action" text NOT NULL,
        "OldValues" text,
        "NewValues" text,
        "IpAddress" text,
        "Timestamp" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_audit_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE breeds (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Lineage" text,
        "TargetFcr" numeric NOT NULL,
        "TargetMarketAgedays" integer NOT NULL,
        "TargetMarketWeightGrams" numeric NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_breeds" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE commission_rules (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "RuleType" integer NOT NULL,
        "Rate" numeric(8,4) NOT NULL,
        "IsDefault" boolean NOT NULL,
        "EffectiveFrom" date NOT NULL,
        "EffectiveTo" date,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_commission_rules" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE notifications (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Type" integer NOT NULL,
        "Title" text NOT NULL,
        "Message" text NOT NULL,
        "ActionUrl" text,
        "IsRead" boolean NOT NULL,
        "RelatedEntityId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_notifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE roles (
        "Id" uuid NOT NULL,
        "Description" text,
        "Name" character varying(256),
        "NormalizedName" character varying(256),
        "ConcurrencyStamp" text,
        CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE tenants (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Slug" text NOT NULL,
        "LogoUrl" text,
        "ContactEmail" text,
        "ContactPhone" text,
        "Address" text,
        "Plan" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE market_prices (
        "Id" uuid NOT NULL,
        "BreedId" uuid,
        "City" text NOT NULL,
        "PricePerKg" numeric(10,2) NOT NULL,
        "Source" text,
        "RecordedAt" timestamp with time zone NOT NULL,
        "RecordedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_market_prices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_market_prices_breeds_BreedId" FOREIGN KEY ("BreedId") REFERENCES breeds ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE role_claims (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "RoleId" uuid NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_role_claims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_role_claims_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES roles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE farms (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Location" text NOT NULL,
        "City" text,
        "Province" text,
        "Latitude" numeric,
        "Longitude" numeric,
        "TotalCapacity" integer NOT NULL,
        "ContactPhone" text,
        "Notes" text,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_farms" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_farms_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "FirstName" text NOT NULL,
        "LastName" text NOT NULL,
        "AvatarUrl" text,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastLoginAt" timestamp with time zone,
        "UserName" character varying(256),
        "NormalizedUserName" character varying(256),
        "Email" character varying(256),
        "NormalizedEmail" character varying(256),
        "EmailConfirmed" boolean NOT NULL,
        "PasswordHash" text,
        "SecurityStamp" text,
        "ConcurrencyStamp" text,
        "PhoneNumber" text,
        "PhoneNumberConfirmed" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "LockoutEnabled" boolean NOT NULL,
        "AccessFailedCount" integer NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_users_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES tenants ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE poultry_houses (
        "Id" uuid NOT NULL,
        "FarmId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Capacity" integer NOT NULL,
        "Description" text,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_poultry_houses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_poultry_houses_farms_FarmId" FOREIGN KEY ("FarmId") REFERENCES farms ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE farm_staff (
        "Id" uuid NOT NULL,
        "FarmId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Role" text NOT NULL,
        "AssignedAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_farm_staff" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_farm_staff_farms_FarmId" FOREIGN KEY ("FarmId") REFERENCES farms ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_farm_staff_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE user_claims (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "UserId" uuid NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_user_claims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_user_claims_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE user_logins (
        "LoginProvider" text NOT NULL,
        "ProviderKey" text NOT NULL,
        "ProviderDisplayName" text,
        "UserId" uuid NOT NULL,
        CONSTRAINT "PK_user_logins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
        CONSTRAINT "FK_user_logins_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE user_roles (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_user_roles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_user_roles_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES roles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE user_tokens (
        "UserId" uuid NOT NULL,
        "LoginProvider" text NOT NULL,
        "Name" text NOT NULL,
        "Value" text,
        CONSTRAINT "PK_user_tokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
        CONSTRAINT "FK_user_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE flock_batches (
        "Id" uuid NOT NULL,
        "FarmId" uuid NOT NULL,
        "PoultryHouseId" uuid NOT NULL,
        "BreedId" uuid NOT NULL,
        "BatchCode" text NOT NULL,
        "PlacementDate" date NOT NULL,
        "InitialChickCount" integer NOT NULL,
        "CostPerChick" numeric(10,4) NOT NULL,
        "SupplierReference" text,
        "Status" integer NOT NULL,
        "Notes" text,
        "CurrentAliveCount" integer NOT NULL,
        "TotalMortality" numeric NOT NULL,
        "LatestAvgWeightGrams" numeric,
        "CalculatedFcr" numeric,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_flock_batches" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_flock_batches_breeds_BreedId" FOREIGN KEY ("BreedId") REFERENCES breeds ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_flock_batches_farms_FarmId" FOREIGN KEY ("FarmId") REFERENCES farms ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_flock_batches_poultry_houses_PoultryHouseId" FOREIGN KEY ("PoultryHouseId") REFERENCES poultry_houses ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE chick_placements (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "PlacementDate" date NOT NULL,
        "Quantity" integer NOT NULL,
        "CostPerChick" numeric(10,4) NOT NULL,
        "SupplierName" text,
        "InvoiceReference" text,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_chick_placements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_chick_placements_flock_batches_BatchId" FOREIGN KEY ("BatchId") REFERENCES flock_batches ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE daily_flock_records (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "RecordDate" date NOT NULL,
        "AliveCount" integer NOT NULL,
        "DailyMortality" integer NOT NULL,
        "FeedConsumedKg" numeric(10,3) NOT NULL,
        "WaterConsumedLiters" numeric(10,3) NOT NULL,
        "AverageWeightGrams" numeric(10,2),
        "AverageFeedPerBirdGrams" numeric,
        "Notes" text,
        "RecordedByUserId" uuid,
        "ClientRecordId" text,
        "RowVersion" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_daily_flock_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_daily_flock_records_flock_batches_BatchId" FOREIGN KEY ("BatchId") REFERENCES flock_batches ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE marketplace_listings (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "Title" text NOT NULL,
        "QuantityTotal" integer NOT NULL,
        "QuantityAvailable" integer NOT NULL,
        "AskingPricePerKg" numeric(10,2) NOT NULL,
        "NegotiableFloorPricePerKg" numeric(10,2),
        "LocationCity" text NOT NULL,
        "LocationDetails" text,
        "Status" integer NOT NULL,
        "PublishedAt" timestamp with time zone,
        "ExpiresAt" timestamp with time zone,
        "Notes" text,
        "RowVersion" bytea,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_marketplace_listings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_marketplace_listings_flock_batches_BatchId" FOREIGN KEY ("BatchId") REFERENCES flock_batches ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE mortality_records (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "RecordDate" date NOT NULL,
        "Count" integer NOT NULL,
        "Cause" text,
        "Notes" text,
        "RecordedByUserId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_mortality_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_mortality_records_flock_batches_BatchId" FOREIGN KEY ("BatchId") REFERENCES flock_batches ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE production_metrics (
        "Id" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "CalculatedAt" timestamp with time zone NOT NULL,
        "AgeDays" integer NOT NULL,
        "Fcr" numeric(6,3),
        "MortalityRatePct" numeric,
        "AdgGrams" numeric,
        "Epef" numeric(8,2),
        "TotalFeedKg" numeric,
        "TotalLiveWeightKg" numeric,
        "SampleSize" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_production_metrics" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_production_metrics_flock_batches_BatchId" FOREIGN KEY ("BatchId") REFERENCES flock_batches ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE deal_proposals (
        "Id" uuid NOT NULL,
        "ListingId" uuid NOT NULL,
        "BrokerTenantId" uuid NOT NULL,
        "BrokerCompanyName" text NOT NULL,
        "ProposedByUserId" uuid NOT NULL,
        "QuantityRequested" integer NOT NULL,
        "OfferedPricePerKg" numeric(10,2) NOT NULL,
        "TransportArrangement" text,
        "Notes" text,
        "Status" integer NOT NULL,
        "ProposedAt" timestamp with time zone NOT NULL,
        "FarmerResponse" text,
        "RespondedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_deal_proposals" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_deal_proposals_marketplace_listings_ListingId" FOREIGN KEY ("ListingId") REFERENCES marketplace_listings ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE deals (
        "Id" uuid NOT NULL,
        "ProposalId" uuid NOT NULL,
        "ListingId" uuid NOT NULL,
        "FarmerTenantId" uuid NOT NULL,
        "BrokerTenantId" uuid NOT NULL,
        "QuantityBirds" integer NOT NULL,
        "AgreedPricePerKg" numeric(10,2) NOT NULL,
        "EstimatedWeightKg" numeric,
        "ActualWeightKg" numeric(10,3),
        "Status" integer NOT NULL,
        "AcceptedAt" timestamp with time zone NOT NULL,
        "ClosedAt" timestamp with time zone,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_deals" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_deals_deal_proposals_ProposalId" FOREIGN KEY ("ProposalId") REFERENCES deal_proposals ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_deals_marketplace_listings_ListingId" FOREIGN KEY ("ListingId") REFERENCES marketplace_listings ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE deal_status_history (
        "Id" uuid NOT NULL,
        "DealId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "ChangedByUserId" uuid NOT NULL,
        "ChangedAt" timestamp with time zone NOT NULL,
        "Note" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_deal_status_history" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_deal_status_history_deals_DealId" FOREIGN KEY ("DealId") REFERENCES deals ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE invoices (
        "Id" uuid NOT NULL,
        "DealId" uuid NOT NULL,
        "InvoiceNumber" text NOT NULL,
        "IssuedAt" timestamp with time zone NOT NULL,
        "DueDate" timestamp with time zone,
        "TotalAmount" numeric(12,2) NOT NULL,
        "PaidAmount" numeric(12,2) NOT NULL,
        "Status" text NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_invoices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_invoices_deals_DealId" FOREIGN KEY ("DealId") REFERENCES deals ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE ledger_entries (
        "Id" uuid NOT NULL,
        "DealId" uuid,
        "EntryType" integer NOT NULL,
        "Amount" numeric(12,2) NOT NULL,
        "RunningBalance" numeric(14,2) NOT NULL,
        "Description" text NOT NULL,
        "EntryDate" timestamp with time zone NOT NULL,
        "RecordedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_ledger_entries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ledger_entries_deals_DealId" FOREIGN KEY ("DealId") REFERENCES deals ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE settlements (
        "Id" uuid NOT NULL,
        "DealId" uuid NOT NULL,
        "GrossAmount" numeric(12,2) NOT NULL,
        "CommissionAmount" numeric(12,2) NOT NULL,
        "FreightAmount" numeric(12,2) NOT NULL,
        "OtherExpenses" numeric NOT NULL,
        "NetPayableToFarmer" numeric(12,2) NOT NULL,
        "NetPayableToBroker" numeric(12,2) NOT NULL,
        "Notes" text,
        "SettledAt" timestamp with time zone NOT NULL,
        "SettledByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_settlements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_settlements_deals_DealId" FOREIGN KEY ("DealId") REFERENCES deals ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE transport_records (
        "Id" uuid NOT NULL,
        "DealId" uuid NOT NULL,
        "VehicleNumber" text,
        "DriverName" text,
        "DriverPhone" text,
        "DepartureAt" timestamp with time zone,
        "ArrivalAt" timestamp with time zone,
        "WeightLiftedKg" numeric(10,3),
        "WeightDeliveredKg" numeric(10,3),
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_transport_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_transport_records_deals_DealId" FOREIGN KEY ("DealId") REFERENCES deals ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE invoice_items (
        "Id" uuid NOT NULL,
        "InvoiceId" uuid NOT NULL,
        "Description" text NOT NULL,
        "Quantity" numeric NOT NULL,
        "UnitPrice" numeric NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_invoice_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_invoice_items_invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES invoices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE payments (
        "Id" uuid NOT NULL,
        "InvoiceId" uuid NOT NULL,
        "Amount" numeric(12,2) NOT NULL,
        "Method" integer NOT NULL,
        "ReferenceNumber" text,
        "Notes" text,
        "RecordedByUserId" uuid NOT NULL,
        "RecordedAt" timestamp with time zone NOT NULL,
        "ConfirmedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_payments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payments_invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES invoices ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE TABLE deal_expenses (
        "Id" uuid NOT NULL,
        "DealId" uuid NOT NULL,
        "Category" text NOT NULL,
        "Amount" numeric(12,2) NOT NULL,
        "Description" text,
        "RecordedByUserId" uuid NOT NULL,
        "SettlementId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_deal_expenses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_deal_expenses_settlements_SettlementId" FOREIGN KEY ("SettlementId") REFERENCES settlements ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_chick_placements_BatchId" ON chick_placements ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_daily_flock_records_BatchId_RecordDate" ON daily_flock_records ("BatchId", "RecordDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_daily_flock_records_ClientRecordId" ON daily_flock_records ("ClientRecordId") WHERE client_record_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_deal_expenses_SettlementId" ON deal_expenses ("SettlementId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_deal_proposals_ListingId" ON deal_proposals ("ListingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_deal_status_history_DealId" ON deal_status_history ("DealId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_deals_BrokerTenantId" ON deals ("BrokerTenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_deals_FarmerTenantId" ON deals ("FarmerTenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_deals_ListingId" ON deals ("ListingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_deals_ProposalId" ON deals ("ProposalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_farm_staff_FarmId" ON farm_staff ("FarmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_farm_staff_UserId" ON farm_staff ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_farms_TenantId" ON farms ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_flock_batches_BreedId" ON flock_batches ("BreedId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_flock_batches_FarmId" ON flock_batches ("FarmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_flock_batches_PoultryHouseId" ON flock_batches ("PoultryHouseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_flock_batches_TenantId_BatchCode" ON flock_batches ("TenantId", "BatchCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_invoice_items_InvoiceId" ON invoice_items ("InvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_invoices_DealId" ON invoices ("DealId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_invoices_TenantId_InvoiceNumber" ON invoices ("TenantId", "InvoiceNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_ledger_entries_DealId" ON ledger_entries ("DealId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_market_prices_BreedId" ON market_prices ("BreedId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_marketplace_listings_BatchId" ON marketplace_listings ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_mortality_records_BatchId" ON mortality_records ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_payments_InvoiceId" ON payments ("InvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_poultry_houses_FarmId" ON poultry_houses ("FarmId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_production_metrics_BatchId" ON production_metrics ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_role_claims_RoleId" ON role_claims ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON roles ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_settlements_DealId" ON settlements ("DealId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_tenants_Slug" ON tenants ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_transport_records_DealId" ON transport_records ("DealId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_user_claims_UserId" ON user_claims ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_user_logins_UserId" ON user_logins ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_user_roles_RoleId" ON user_roles ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "EmailIndex" ON users ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE INDEX "IX_users_TenantId" ON users ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON users ("NormalizedUserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025093905_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025093905_InitialCreate', '9.0.2');
    END IF;
END $EF$;
COMMIT;

