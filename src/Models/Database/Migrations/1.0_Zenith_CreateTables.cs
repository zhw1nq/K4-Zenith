using FluentMigrator;

namespace Zenith.Migrations
{
	[Migration(202410191)]
	public class Default_CreatePlayerDataTables : Migration
	{
		public override void Up()
		{
			if (!Schema.Table("zenith_player_settings").Exists())
			{
				Create.Table("zenith_player_settings")
					.WithColumn("steam_id").AsString(32).PrimaryKey()
					.WithColumn("name").AsString(255).Nullable()
					.WithColumn("last_online").AsCustom("TIMESTAMP").WithDefault(SystemMethods.CurrentDateTime).Nullable();

				Execute.Sql("ALTER TABLE zenith_player_settings CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
			}

			if (!Schema.Table("zenith_player_storage").Exists())
			{
				Create.Table("zenith_player_storage")
					.WithColumn("steam_id").AsString(32).PrimaryKey()
					.WithColumn("name").AsString(255).Nullable()
					.WithColumn("last_online").AsCustom("TIMESTAMP").WithDefault(SystemMethods.CurrentDateTime).Nullable();

				Execute.Sql("ALTER TABLE zenith_player_storage CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
			}
		}

		public override void Down()
		{
			if (Schema.Table("zenith_player_settings").Exists())
				Delete.Table("zenith_player_settings");

			if (Schema.Table("zenith_player_storage").Exists())
				Delete.Table("zenith_player_storage");
		}
	}
}