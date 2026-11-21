-- Schema for the DataField42 database.
--
-- Run this against an empty database to stand up a fresh instance, or use the application's own
-- `init-db` command, which creates the database and applies this file.
--
-- Every statement is idempotent, so running it again against an existing database changes
-- nothing. It therefore never drops or alters an existing table: this schema is shared with the
-- central server and the servers tracker, and a schema change belongs in a deliberate migration
-- applied by hand, not in a script the application runs.
--
-- The AdminUser table is not here -- the application owns that one and creates it on startup.

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS `Author` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Mod` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `folderName` varchar(255) DEFAULT NULL,
  `fullName` varchar(255) DEFAULT NULL,
  `version` varchar(255) DEFAULT NULL,
  `lastUpdate` datetime DEFAULT NULL,
  `authorID` int(11) DEFAULT NULL,
  `modDependencies` varchar(8000) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Map` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `nameRaw` varchar(255) DEFAULT NULL,
  `authorID` int(11) DEFAULT NULL,
  `folder` varchar(255) DEFAULT NULL,
  `readme` varchar(255) DEFAULT NULL,
  `images` varchar(4000) DEFAULT NULL,
  `gpm_cq` int(1) DEFAULT NULL,
  `gpm_ctf` int(1) DEFAULT NULL,
  `gpm_tdm` int(1) DEFAULT NULL,
  `gpm_coop` int(1) DEFAULT NULL,
  `gpm_objectivemode` int(1) DEFAULT NULL,
  `modID` int(11) DEFAULT NULL,
  `controlPointsInfo` varchar(4000) DEFAULT NULL,
  `objectSpawnsInfo` longtext DEFAULT NULL,
  `gameMapId` varchar(40) DEFAULT NULL,
  `multiplayerBriefingObjectives` text DEFAULT NULL,
  `dateOldestRFA` datetime DEFAULT NULL,
  `dateNewestRFA` datetime DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- `path` is compared case-sensitively, because the server the files live on is case-sensitive
-- and two archives whose names differ only in case are two different files.
CREATE TABLE IF NOT EXISTS `RFA` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `path` varchar(255) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL,
  `patchNumber` int(11) DEFAULT NULL,
  `note` varchar(255) DEFAULT NULL,
  `crc32` varchar(255) DEFAULT NULL,
  `client` int(1) DEFAULT NULL,
  `recommended` int(1) DEFAULT NULL,
  `mapID` int(11) DEFAULT NULL,
  `date` datetime DEFAULT NULL,
  `battleCraftLevelPath` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Tag` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `color` varchar(255) DEFAULT NULL,
  `colorHover` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `TagLink` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `mapID` int(11) DEFAULT NULL,
  `tagID` int(11) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Server` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `IP` varchar(255) DEFAULT NULL,
  `gamePort` int(11) DEFAULT NULL,
  `queryPort` int(11) DEFAULT NULL,
  `name` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `Round` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `serverIP` varchar(255) DEFAULT NULL,
  `serverGamePort` int(11) DEFAULT NULL,
  `serverID` int(11) DEFAULT NULL,
  `startTime` datetime DEFAULT NULL,
  `endTime` datetime DEFAULT NULL,
  `mapName` varchar(255) DEFAULT NULL,
  `modName` varchar(255) DEFAULT NULL,
  `gameplayMode` varchar(255) DEFAULT NULL,
  `team1StartTickets` int(11) DEFAULT NULL,
  `team2StartTickets` int(11) DEFAULT NULL,
  `gameStartDelay` int(11) DEFAULT NULL,
  `roundTime` int(11) DEFAULT NULL,
  `team1EndTickets` int(11) DEFAULT NULL,
  `team2EndTickets` int(11) DEFAULT NULL,
  `roundTimeRemaining` int(11) DEFAULT NULL,
  `winningTeamNumber` int(11) DEFAULT NULL,
  `playerStats` longtext DEFAULT NULL,
  `averageNumberOfPlayers` float DEFAULT NULL,
  `maximumNumberOfPlayers` int(11) DEFAULT NULL,
  `hasHadPlayers` int(1) DEFAULT 0,
  PRIMARY KEY (`ID`),
  KEY `idx_server_round` (`serverID`,`startTime`,`winningTeamNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `PlayerNumber` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `date` datetime DEFAULT NULL,
  `roundID` int(11) DEFAULT NULL,
  `players` int(11) DEFAULT NULL,
  `playersRaw` int(11) DEFAULT NULL,
  PRIMARY KEY (`ID`),
  KEY `idx_round` (`ID`,`roundID`),
  KEY `idx_playernumber_round` (`roundID`,`ID`),
  KEY `idx_playernumber_round_date` (`roundID`,`date`),
  KEY `idx_playernumber_date` (`date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `RequestLog` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `type` int(11) DEFAULT NULL,
  `date` datetime DEFAULT NULL,
  `ip` varchar(255) DEFAULT NULL,
  `field1` varchar(255) DEFAULT NULL,
  `field2` varchar(255) DEFAULT NULL,
  `field3` varchar(255) DEFAULT NULL,
  `field4` varchar(2048) DEFAULT NULL,
  `field5` varchar(255) DEFAULT NULL,
  `field6` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Every map/mod combination servers have been seen playing.
CREATE OR REPLACE VIEW `MapDistinct` AS
  SELECT DISTINCT `Round`.`mapName` AS `mapName`, `Round`.`modName` AS `modName`
  FROM `Round`;

-- Every map/mod combination the file database holds.
CREATE OR REPLACE VIEW `MapDistinctDB` AS
  SELECT `Map`.`name` AS `mapName`, `Mod`.`folderName` AS `modName`
  FROM `Map` LEFT JOIN `Mod` ON `Map`.`modID` = `Mod`.`ID`;

-- What has been played but is not held: the maps still to be collected.
CREATE OR REPLACE VIEW `MapNotInDB` AS
  SELECT `MapDistinct`.`mapName` AS `mapName`, `MapDistinct`.`modName` AS `modName`
  FROM `MapDistinct`
  LEFT JOIN `MapDistinctDB`
    ON `MapDistinct`.`mapName` = `MapDistinctDB`.`mapName`
   AND `MapDistinct`.`modName` = `MapDistinctDB`.`modName`
  WHERE `MapDistinctDB`.`mapName` IS NULL;
