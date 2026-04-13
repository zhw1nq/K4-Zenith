using FluentMigrator;

namespace Zenith.Migrations
{
	[Migration(202411101)]
	public class Bans_AddCustomOverrides : Migration
	{
		public override void Up()
		{
			if (!Schema.Table("zenith_bans_player_overrides").Exists())
			{
				Create.Table("zenith_bans_player_overrides")
					.WithColumn("id").AsInt32().PrimaryKey().Identity()
					.WithColumn("player_rank_id").AsInt32().ForeignKey("FK_player_overrides_rank_id", "zenith_bans_player_ranks", "id")
					.WithColumn("command").AsString(100).NotNullable()
					.WithColumn("value").AsBoolean();

				Execute.Sql("ALTER TABLE zenith_bans_player_overrides CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
			}
		}

		public override void Down()
		{
			if (Schema.Table("zenith_bans_player_overrides").Exists())
			{
				Delete.Table("zenith_bans_player_overrides");
			}
		}
	}
}
