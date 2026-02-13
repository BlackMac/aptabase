using FluentMigrator;

namespace Aptabase.Data.Migrations;

[Migration(0013)]
public class AddNotifications : Migration
{
    public override void Up()
    {
        Create.Table("notification_channels")
            .WithNanoIdColumn("id").PrimaryKey()
            .WithNanoIdColumn("app_id").ForeignKey("apps", "id")
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("channel_type").AsString(20).NotNullable()
            .WithColumn("config_json").AsString(int.MaxValue).NotNullable()
            .WithColumn("enabled").AsBoolean().WithDefaultValue(true).NotNullable()
            .WithTimestamps();

        Create.Table("notification_rules")
            .WithNanoIdColumn("id").PrimaryKey()
            .WithNanoIdColumn("app_id").ForeignKey("apps", "id")
            .WithColumn("rule_type").AsString(30).NotNullable()
            .WithColumn("config_json").AsString(int.MaxValue).NotNullable()
            .WithColumn("enabled").AsBoolean().WithDefaultValue(true).NotNullable()
            .WithTimestamps();

        Create.Table("notification_rule_channels")
            .WithNanoIdColumn("rule_id").ForeignKey("notification_rules", "id")
            .WithNanoIdColumn("channel_id").ForeignKey("notification_channels", "id");

        Create.PrimaryKey("pk_notification_rule_channels")
            .OnTable("notification_rule_channels")
            .Columns("rule_id", "channel_id");

        Create.Table("notification_log")
            .WithNanoIdColumn("id").PrimaryKey()
            .WithNanoIdColumn("app_id").ForeignKey("apps", "id")
            .WithColumn("rule_id").AsString(22).Nullable().ForeignKey("notification_rules", "id")
            .WithNanoIdColumn("channel_id").ForeignKey("notification_channels", "id")
            .WithColumn("message").AsString(int.MaxValue).NotNullable()
            .WithColumn("sent_at").AsDateTimeOffset().NotNullable()
            .WithColumn("dedup_key").AsString(200).Nullable();

        Create.Table("notification_known_values")
            .WithNanoIdColumn("app_id").ForeignKey("apps", "id")
            .WithColumn("value_type").AsString(20).NotNullable()
            .WithColumn("value").AsString(200).NotNullable()
            .WithColumn("first_seen_at").AsDateTimeOffset().NotNullable();

        Create.PrimaryKey("pk_notification_known_values")
            .OnTable("notification_known_values")
            .Columns("app_id", "value_type", "value");

        // Indexes
        Create.Index("idx_notification_channels_app_id")
            .OnTable("notification_channels")
            .OnColumn("app_id");

        Create.Index("idx_notification_rules_app_id")
            .OnTable("notification_rules")
            .OnColumn("app_id");

        Create.Index("idx_notification_rule_channels_channel_id")
            .OnTable("notification_rule_channels")
            .OnColumn("channel_id");

        Create.Index("idx_notification_log_app_id_sent_at")
            .OnTable("notification_log")
            .OnColumn("app_id").Ascending()
            .OnColumn("sent_at").Descending();

        Create.Index("idx_notification_log_dedup_key")
            .OnTable("notification_log")
            .OnColumn("dedup_key");

        Create.Index("idx_notification_known_values_app_id_value_type")
            .OnTable("notification_known_values")
            .OnColumn("app_id").Ascending()
            .OnColumn("value_type").Ascending();
    }

    public override void Down()
    {
        Delete.Table("notification_known_values");
        Delete.Table("notification_log");
        Delete.Table("notification_rule_channels");
        Delete.Table("notification_rules");
        Delete.Table("notification_channels");
    }
}
