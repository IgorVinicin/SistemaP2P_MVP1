START TRANSACTION;
ALTER TABLE "WithdrawalRequests" ADD "ValorSolicitado" numeric(18,2);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007220328_PreserveRequestedWithdrawalAmount', '10.0.8');

COMMIT;

