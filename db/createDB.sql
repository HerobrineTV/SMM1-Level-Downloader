-- SMM1DB.Levels definition

CREATE TABLE `Levels` (
  `EntryID` bigint(20) NOT NULL AUTO_INCREMENT,
  `clears` bigint(20) DEFAULT NULL,
  `creator_mii_data` varchar(1000) DEFAULT NULL,
  `creator_pid` bigint(20) DEFAULT NULL,
  `course_name` varchar(1000) DEFAULT NULL,
  `failures` bigint(20) DEFAULT NULL,
  `total_attempts` bigint(20) DEFAULT NULL,
  `clears_total_attempts` double DEFAULT NULL,
  `creator_nnid` varchar(1000) DEFAULT NULL,
  `id` bigint(20) DEFAULT NULL,
  `retrieval_date` datetime DEFAULT NULL,
  `stars` bigint(20) DEFAULT NULL,
  `upload_time` datetime DEFAULT NULL,
  `user_plays` bigint(20) DEFAULT NULL,
  `world_record_best_time_ms` bigint(20) DEFAULT NULL,
  `world_record_best_time_player_mii_data` varchar(1000) DEFAULT NULL,
  `url` varchar(1000) DEFAULT NULL,
  `world_record_best_time_player_nnid` varchar(1000) DEFAULT NULL,
  `world_record_best_time_player_pid` bigint(20) DEFAULT NULL,
  `world_record_created_time` datetime DEFAULT NULL,
  `world_record_first_complete_player_mii_data` varchar(1000) DEFAULT NULL,
  `world_record_first_complete_player_nnid` varchar(1000) DEFAULT NULL,
  `world_record_first_complete_player_pid` bigint(20) DEFAULT NULL,
  `world_record_updated_time` datetime DEFAULT NULL,
  PRIMARY KEY (`EntryID`)
) ENGINE=InnoDB AUTO_INCREMENT=7507867 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;