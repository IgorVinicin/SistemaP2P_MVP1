START TRANSACTION;
ALTER TABLE "LoanInstallments" ADD "JurosMoraCarregados" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "JurosMoraPagos" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "JurosPagos" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "MultaCarregada" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "MultaPaga" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "PrincipalDistribuido" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "PrincipalPago" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "LoanInstallments" ADD "Version" bigint NOT NULL DEFAULT 0;

ALTER TABLE "Investments" ADD "Version" bigint NOT NULL DEFAULT 0;

ALTER TABLE "IdempotencyRecords" ADD "RequestHash" text NOT NULL DEFAULT '';

CREATE TABLE "DebtRollovers" (
    "Id" uuid NOT NULL,
    "SourceInstallmentId" uuid NOT NULL,
    "TargetInstallmentId" uuid NOT NULL,
    "Principal" numeric(18,2) NOT NULL,
    "Interest" numeric(18,2) NOT NULL,
    "Penalty" numeric(18,2) NOT NULL,
    "LateInterest" numeric(18,2) NOT NULL,
    "Status" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "AppliedAt" timestamp with time zone,
    "Actor" text NOT NULL,
    CONSTRAINT "PK_DebtRollovers" PRIMARY KEY ("Id")
);

CREATE TABLE "GatewayCharges" (
    "Id" uuid NOT NULL,
    "PaymentId" text,
    "Kind" text NOT NULL,
    "ReferenceId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Value" numeric(18,2) NOT NULL,
    "NetValue" numeric(18,2),
    "Status" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "QrCodeBase64" text,
    "CopiaECola" text,
    "InvoiceUrl" text,
    "ExpirationDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "SettledAt" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_GatewayCharges" PRIMARY KEY ("Id")
);

CREATE TABLE "GatewayTransfers" (
    "Id" uuid NOT NULL,
    "TransferId" text,
    "Kind" text NOT NULL,
    "ReferenceId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Value" numeric(18,2) NOT NULL,
    "Fee" numeric(18,2),
    "PixKey" text NOT NULL,
    "PixKeyType" text NOT NULL,
    "Status" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "SettledAt" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_GatewayTransfers" PRIMARY KEY ("Id")
);

CREATE TABLE "GatewayWebhookEvents" (
    "EventId" character varying(150) NOT NULL,
    "Event" text NOT NULL,
    "ResourceId" text NOT NULL,
    "Status" text NOT NULL,
    "Detail" text,
    "ReceivedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_GatewayWebhookEvents" PRIMARY KEY ("EventId")
);

CREATE INDEX "IX_DebtRollovers_SourceInstallmentId" ON "DebtRollovers" ("SourceInstallmentId");

CREATE INDEX "IX_DebtRollovers_TargetInstallmentId" ON "DebtRollovers" ("TargetInstallmentId");

CREATE UNIQUE INDEX "IX_GatewayCharges_Kind_ReferenceId" ON "GatewayCharges" ("Kind", "ReferenceId") WHERE "IsActive" = TRUE;

CREATE INDEX "IX_GatewayCharges_Kind_ReferenceId_Status" ON "GatewayCharges" ("Kind", "ReferenceId", "Status");

CREATE UNIQUE INDEX "IX_GatewayCharges_PaymentId" ON "GatewayCharges" ("PaymentId");

CREATE UNIQUE INDEX "IX_GatewayTransfers_Kind_ReferenceId" ON "GatewayTransfers" ("Kind", "ReferenceId") WHERE "IsActive" = TRUE;

CREATE UNIQUE INDEX "IX_GatewayTransfers_TransferId" ON "GatewayTransfers" ("TransferId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007213016_GatewaySafetyAndFinancialConcurrency', '10.0.8');

COMMIT;

