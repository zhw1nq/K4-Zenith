using FluentMigrator;

namespace Zenith.Migrations
{
	[Migration(202410192)]
	public class Stats_CreateZenithStatsTables : Migration
	{
		public override void Up()
		{
			if (!Schema.Table("zenith_weapon_stats").Exists())
			{
				Create.Table("zenith_weapon_stats")
					.WithColumn("steam_id").AsString(32).NotNullable()
					.WithColumn("weapon").AsString(64).NotNullable()
					.WithColumn("kills").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("shots").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("headshots").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("head_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("chest_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("stomach_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("left_arm_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("right_arm_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("left_leg_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("right_leg_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("neck_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("gear_hits").AsInt32().NotNullable().WithDefaultValue(0);
				Create.PrimaryKey("PK_zenith_weapon_stats").OnTable("zenith_weapon_stats").Columns("steam_id", "weapon");

				Execute.Sql("ALTER TABLE zenith_weapon_stats CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
			}

			if (!Schema.Table("zenith_map_stats").Exists())
			{
				Create.Table("zenith_map_stats")
					.WithColumn("steam_id").AsString(32).NotNullable()
					.WithColumn("map_name").AsString(64).NotNullable()
					.WithColumn("kills").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("first_blood").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("deaths").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("assists").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("shoots").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("hits_taken").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("hits_given").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("headshots").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("head_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("chest_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("stomach_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("left_arm_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("right_arm_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("left_leg_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("right_leg_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("neck_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("unused_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("gear_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("special_hits").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("grenades").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("mvp").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("round_win").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("round_lose").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("game_win").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("game_lose").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("rounds_overall").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("rounds_ct").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("rounds_t").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("bomb_planted").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("bomb_defused").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("hostage_rescued").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("hostage_killed").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("no_scope_kill").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("penetrated_kill").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("thru_smoke_kill").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("flashed_kill").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("dominated_kill").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("revenge_kill").AsInt32().NotNullable().WithDefaultValue(0)
					.WithColumn("assist_flash").AsInt32().NotNullable().WithDefaultValue(0);
				Create.PrimaryKey("PK_zenith_map_stats").OnTable("zenith_map_stats").Columns("steam_id", "map_name");

				Execute.Sql("ALTER TABLE zenith_map_stats CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
			}
		}

		public override void Down()
		{
			if (Schema.Table("zenith_weapon_stats").Exists())
			{
				Delete.Table("zenith_weapon_stats");
			}

			if (Schema.Table("zenith_map_stats").Exists())
			{
				Delete.Table("zenith_map_stats");
			}
		}
	}
}
