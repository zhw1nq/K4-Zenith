-- --------------------------------------------------------
-- Host:                         192.168.1.145
-- Server version:               10.11.13-MariaDB-0ubuntu0.24.04.1 - Ubuntu 24.04
-- Server OS:                    debian-linux-gnu
-- HeidiSQL Version:             12.14.0.7165
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


-- Dumping database structure for vhming_test
CREATE DATABASE IF NOT EXISTS `vhming_test` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci */;
USE `vhming_test`;

-- Dumping structure for table vhming_test.VersionInfo
CREATE TABLE IF NOT EXISTS `VersionInfo` (
  `Version` bigint(20) NOT NULL,
  `AppliedOn` datetime DEFAULT NULL,
  `Description` varchar(1024) CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci DEFAULT NULL,
  UNIQUE KEY `UC_Version` (`Version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_admin_group_permissions
CREATE TABLE IF NOT EXISTS `zenith_bans_admin_group_permissions` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `group_id` int(11) NOT NULL,
  `permission` varchar(100) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_admin_group_permissions_group_id` (`group_id`),
  CONSTRAINT `FK_admin_group_permissions_group_id` FOREIGN KEY (`group_id`) REFERENCES `zenith_bans_admin_groups` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_admin_groups
CREATE TABLE IF NOT EXISTS `zenith_bans_admin_groups` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(50) NOT NULL,
  `immunity` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_zenith_bans_admin_groups_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_ip_addresses
CREATE TABLE IF NOT EXISTS `zenith_bans_ip_addresses` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_id` int(11) NOT NULL,
  `ip_address` varchar(45) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unique_player_ip` (`player_id`,`ip_address`),
  CONSTRAINT `FK_ip_addresses_player_id` FOREIGN KEY (`player_id`) REFERENCES `zenith_bans_players` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_player_groups
CREATE TABLE IF NOT EXISTS `zenith_bans_player_groups` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_rank_id` int(11) NOT NULL,
  `group_name` varchar(50) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_player_groups_rank_id` (`player_rank_id`),
  CONSTRAINT `FK_player_groups_rank_id` FOREIGN KEY (`player_rank_id`) REFERENCES `zenith_bans_player_ranks` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_player_overrides
CREATE TABLE IF NOT EXISTS `zenith_bans_player_overrides` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_rank_id` int(11) NOT NULL,
  `command` varchar(100) NOT NULL,
  `value` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_player_overrides_rank_id` (`player_rank_id`),
  CONSTRAINT `FK_player_overrides_rank_id` FOREIGN KEY (`player_rank_id`) REFERENCES `zenith_bans_player_ranks` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_player_permissions
CREATE TABLE IF NOT EXISTS `zenith_bans_player_permissions` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_rank_id` int(11) NOT NULL,
  `permission` varchar(100) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_player_permissions_rank_id` (`player_rank_id`),
  CONSTRAINT `FK_player_permissions_rank_id` FOREIGN KEY (`player_rank_id`) REFERENCES `zenith_bans_player_ranks` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_player_ranks
CREATE TABLE IF NOT EXISTS `zenith_bans_player_ranks` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_id` int(11) NOT NULL,
  `server_ip` varchar(50) NOT NULL,
  `immunity` int(11) DEFAULT NULL,
  `rank_expiry` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unique_player_server` (`player_id`,`server_ip`),
  CONSTRAINT `FK_player_ranks_player_id` FOREIGN KEY (`player_id`) REFERENCES `zenith_bans_players` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_players
CREATE TABLE IF NOT EXISTS `zenith_bans_players` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `steam_id` bigint(20) NOT NULL,
  `name` varchar(255) CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci DEFAULT NULL,
  `last_online` datetime DEFAULT NULL,
  `current_server` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `IX_zenith_bans_players_steam_id` (`steam_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_punishments
CREATE TABLE IF NOT EXISTS `zenith_bans_punishments` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_id` int(11) NOT NULL,
  `status` enum('active','warn_ban','expired','removed','removed_console') NOT NULL DEFAULT 'active',
  `type` enum('mute','gag','silence','ban','warn','kick') DEFAULT NULL,
  `duration` int(11) DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  `expires_at` datetime DEFAULT NULL,
  `admin_id` int(11) DEFAULT NULL,
  `removed_at` datetime DEFAULT NULL,
  `remove_admin_id` int(11) DEFAULT NULL,
  `server_ip` varchar(50) NOT NULL DEFAULT 'all',
  `reason` text DEFAULT NULL,
  `remove_reason` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_punishments_admin_id` (`admin_id`),
  KEY `FK_punishments_remove_admin_id` (`remove_admin_id`),
  KEY `FK_punishments_player_id` (`player_id`),
  CONSTRAINT `FK_punishments_admin_id` FOREIGN KEY (`admin_id`) REFERENCES `zenith_bans_players` (`id`),
  CONSTRAINT `FK_punishments_player_id` FOREIGN KEY (`player_id`) REFERENCES `zenith_bans_players` (`id`),
  CONSTRAINT `FK_punishments_remove_admin_id` FOREIGN KEY (`remove_admin_id`) REFERENCES `zenith_bans_players` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_bans_punishments_old
CREATE TABLE IF NOT EXISTS `zenith_bans_punishments_old` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `steam_id` bigint(20) NOT NULL,
  `type` enum('mute','gag','silence','ban','warn','kick') CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `duration` int(11) DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  `expires_at` datetime DEFAULT NULL,
  `admin_steam_id` bigint(20) DEFAULT NULL,
  `removed_at` datetime DEFAULT NULL,
  `remove_admin_steam_id` bigint(20) DEFAULT NULL,
  `server_ip` varchar(50) CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci NOT NULL DEFAULT 'all',
  `reason` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `status` enum('active','warn_ban','expired','removed','removed_console') NOT NULL DEFAULT 'active',
  `remove_reason` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_punishments_steam_id` (`steam_id`),
  KEY `FK_punishments_admin_steam_id` (`admin_steam_id`),
  KEY `FK_punishments_remove_admin_steam_id` (`remove_admin_steam_id`),
  CONSTRAINT `FK_punishments_admin_steam_id` FOREIGN KEY (`admin_steam_id`) REFERENCES `zenith_bans_players` (`steam_id`),
  CONSTRAINT `FK_punishments_remove_admin_steam_id` FOREIGN KEY (`remove_admin_steam_id`) REFERENCES `zenith_bans_players` (`steam_id`),
  CONSTRAINT `FK_punishments_steam_id` FOREIGN KEY (`steam_id`) REFERENCES `zenith_bans_players` (`steam_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_map_stats
CREATE TABLE IF NOT EXISTS `zenith_map_stats` (
  `steam_id` varchar(32) NOT NULL,
  `map_name` varchar(64) NOT NULL,
  `kills` int(11) NOT NULL DEFAULT 0,
  `first_blood` int(11) NOT NULL DEFAULT 0,
  `deaths` int(11) NOT NULL DEFAULT 0,
  `assists` int(11) NOT NULL DEFAULT 0,
  `shoots` int(11) NOT NULL DEFAULT 0,
  `hits_taken` int(11) NOT NULL DEFAULT 0,
  `hits_given` int(11) NOT NULL DEFAULT 0,
  `headshots` int(11) NOT NULL DEFAULT 0,
  `head_hits` int(11) NOT NULL DEFAULT 0,
  `chest_hits` int(11) NOT NULL DEFAULT 0,
  `stomach_hits` int(11) NOT NULL DEFAULT 0,
  `left_arm_hits` int(11) NOT NULL DEFAULT 0,
  `right_arm_hits` int(11) NOT NULL DEFAULT 0,
  `left_leg_hits` int(11) NOT NULL DEFAULT 0,
  `right_leg_hits` int(11) NOT NULL DEFAULT 0,
  `neck_hits` int(11) NOT NULL DEFAULT 0,
  `unused_hits` int(11) NOT NULL DEFAULT 0,
  `gear_hits` int(11) NOT NULL DEFAULT 0,
  `special_hits` int(11) NOT NULL DEFAULT 0,
  `grenades` int(11) NOT NULL DEFAULT 0,
  `mvp` int(11) NOT NULL DEFAULT 0,
  `round_win` int(11) NOT NULL DEFAULT 0,
  `round_lose` int(11) NOT NULL DEFAULT 0,
  `game_win` int(11) NOT NULL DEFAULT 0,
  `game_lose` int(11) NOT NULL DEFAULT 0,
  `rounds_overall` int(11) NOT NULL DEFAULT 0,
  `rounds_ct` int(11) NOT NULL DEFAULT 0,
  `rounds_t` int(11) NOT NULL DEFAULT 0,
  `bomb_planted` int(11) NOT NULL DEFAULT 0,
  `bomb_defused` int(11) NOT NULL DEFAULT 0,
  `hostage_rescued` int(11) NOT NULL DEFAULT 0,
  `hostage_killed` int(11) NOT NULL DEFAULT 0,
  `no_scope_kill` int(11) NOT NULL DEFAULT 0,
  `penetrated_kill` int(11) NOT NULL DEFAULT 0,
  `thru_smoke_kill` int(11) NOT NULL DEFAULT 0,
  `flashed_kill` int(11) NOT NULL DEFAULT 0,
  `dominated_kill` int(11) NOT NULL DEFAULT 0,
  `revenge_kill` int(11) NOT NULL DEFAULT 0,
  `assist_flash` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`steam_id`,`map_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_player_settings
CREATE TABLE IF NOT EXISTS `zenith_player_settings` (
  `steam_id` varchar(32) NOT NULL,
  `name` varchar(255) CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci DEFAULT NULL,
  `last_online` timestamp NULL DEFAULT current_timestamp(),
  `K4-Zenith-TimeStats.settings` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith-TimeStats.settings`)),
  `K4-Zenith-Ranks.settings` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith-Ranks.settings`)),
  `K4-Zenith.settings` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith.settings`)),
  PRIMARY KEY (`steam_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_player_storage
CREATE TABLE IF NOT EXISTS `zenith_player_storage` (
  `steam_id` varchar(32) NOT NULL,
  `name` varchar(255) CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci DEFAULT NULL,
  `last_online` timestamp NULL DEFAULT current_timestamp(),
  `K4-Zenith-Stats.storage` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith-Stats.storage`)),
  `K4-Zenith-TimeStats.storage` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith-TimeStats.storage`)),
  `K4-Zenith-Ranks.storage` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith-Ranks.storage`)),
  `K4-Zenith-CustomTags.storage` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`K4-Zenith-CustomTags.storage`)),
  PRIMARY KEY (`steam_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

-- Dumping structure for table vhming_test.zenith_weapon_stats
CREATE TABLE IF NOT EXISTS `zenith_weapon_stats` (
  `steam_id` varchar(32) NOT NULL,
  `weapon` varchar(64) NOT NULL,
  `kills` int(11) NOT NULL DEFAULT 0,
  `shots` int(11) NOT NULL DEFAULT 0,
  `hits` int(11) NOT NULL DEFAULT 0,
  `headshots` int(11) NOT NULL DEFAULT 0,
  `head_hits` int(11) NOT NULL DEFAULT 0,
  `chest_hits` int(11) NOT NULL DEFAULT 0,
  `stomach_hits` int(11) NOT NULL DEFAULT 0,
  `left_arm_hits` int(11) NOT NULL DEFAULT 0,
  `right_arm_hits` int(11) NOT NULL DEFAULT 0,
  `left_leg_hits` int(11) NOT NULL DEFAULT 0,
  `right_leg_hits` int(11) NOT NULL DEFAULT 0,
  `neck_hits` int(11) NOT NULL DEFAULT 0,
  `gear_hits` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`steam_id`,`weapon`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- Data exporting was unselected.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
