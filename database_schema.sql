CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `Establishments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Establishments` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Users` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Role` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ManagerId` int NULL,
    `PcfBalance` decimal(12,2) NOT NULL DEFAULT 0.0,
    `DailyStartingFloat` decimal(12,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Users_Users_ManagerId` FOREIGN KEY (`ManagerId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `AuditItems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `BuyerId` int NOT NULL,
    `EstablishmentId` int NOT NULL,
    `Amount` decimal(12,2) NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EntryDate` date NOT NULL,
    `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Pending',
    `Notes` longtext CHARACTER SET utf8mb4 NULL,
    `ReceiptImageUrl` varchar(255) CHARACTER SET utf8mb4 NULL,
    `VerifiedById` int NULL,
    `VerificationDate` datetime(6) NULL,
    CONSTRAINT `PK_AuditItems` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AuditItems_Establishments_EstablishmentId` FOREIGN KEY (`EstablishmentId`) REFERENCES `Establishments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_AuditItems_Users_BuyerId` FOREIGN KEY (`BuyerId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_AuditItems_Users_VerifiedById` FOREIGN KEY (`VerifiedById`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `AuditItemDetails` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `AuditItemId` int NOT NULL,
    `ItemName` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Quantity` int NOT NULL DEFAULT 1,
    `Price` decimal(12,2) NOT NULL,
    `Total` decimal(12,2) NOT NULL,
    CONSTRAINT `PK_AuditItemDetails` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AuditItemDetails_AuditItems_AuditItemId` FOREIGN KEY (`AuditItemId`) REFERENCES `AuditItems` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_AuditItemDetails_AuditItemId` ON `AuditItemDetails` (`AuditItemId`);

CREATE INDEX `IX_AuditItems_BuyerId` ON `AuditItems` (`BuyerId`);

CREATE INDEX `IX_AuditItems_EstablishmentId` ON `AuditItems` (`EstablishmentId`);

CREATE INDEX `IX_AuditItems_VerifiedById` ON `AuditItems` (`VerifiedById`);

CREATE UNIQUE INDEX `IX_Establishments_Name` ON `Establishments` (`Name`);

CREATE UNIQUE INDEX `IX_Users_Email` ON `Users` (`Email`);

CREATE INDEX `IX_Users_ManagerId` ON `Users` (`ManagerId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260802121325_InitialCreate', '9.0.0');

COMMIT;

