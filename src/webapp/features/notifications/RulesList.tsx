import { Button } from "@components/Button";
import { ErrorState } from "@components/ErrorState";
import { LoadingState } from "@components/LoadingState";
import { Application } from "@features/apps";
import { IconPlus, IconTrash } from "@tabler/icons-react";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { notificationsApi, NotificationChannel, NotificationRule } from "./notifications-api";
import { RuleFormDialog } from "./RuleFormDialog";

type Props = { app: Application };

const ruleTypeLabels: Record<string, string> = {
  event_push: "Event Push",
  threshold: "Threshold Alert",
  new_event_name: "New Event Name",
  dead_app: "Dead App",
  volume_anomaly: "Volume Anomaly",
  new_app_version: "New App Version",
  new_country: "New Country",
  scheduled_digest: "Scheduled Digest",
};

function getRuleDescription(rule: NotificationRule): string {
  try {
    const config = JSON.parse(rule.configJson);
    switch (rule.ruleType) {
      case "event_push":
        return `Events: ${(config.event_names || []).join(", ")}`;
      case "threshold":
        return `${config.event_name || "events"} > ${config.threshold} per ${config.period || "day"}`;
      case "dead_app":
        return `No events for ${config.hours || 24} hours`;
      case "volume_anomaly":
        return `Sensitivity: ${config.sensitivity || "medium"}`;
      case "scheduled_digest":
        return `Schedule: ${config.schedule || "daily"}`;
      default:
        return "";
    }
  } catch {
    return "";
  }
}

export function RulesList(props: Props) {
  const [editingRule, setEditingRule] = useState<NotificationRule | null>(null);
  const [isCreating, setIsCreating] = useState(false);

  const { isLoading, isError, data: rules, refetch } = useQuery({
    queryKey: ["notification-rules", props.app.id],
    queryFn: () => notificationsApi.getRules(props.app.id),
  });

  const { data: channels } = useQuery({
    queryKey: ["notification-channels", props.app.id],
    queryFn: () => notificationsApi.getChannels(props.app.id),
  });

  const getChannelNames = (channelIds: string[]) => {
    if (!channels) return "";
    return channelIds
      .map((id) => channels.find((c: NotificationChannel) => c.id === id)?.name)
      .filter(Boolean)
      .join(", ");
  };

  const handleDelete = async (ruleId: string) => {
    if (!confirm("Are you sure you want to delete this rule?")) return;
    await notificationsApi.deleteRule(props.app.id, ruleId);
    toast("Rule deleted");
    refetch();
  };

  if (isLoading) return <LoadingState />;
  if (isError) return <ErrorState />;

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <p className="text-sm text-muted-foreground">
          Rules define when notifications are triggered and which channels receive them.
        </p>
        <Button size="sm" onClick={() => setIsCreating(true)}>
          <IconPlus className="h-4 w-4 mr-1" /> Add Rule
        </Button>
      </div>

      {rules && rules.length === 0 && (
        <div className="text-center py-8 text-muted-foreground">
          No rules configured yet. Add a rule to start triggering notifications.
        </div>
      )}

      {rules && rules.length > 0 && (
        <div className="border rounded-lg divide-y">
          {rules.map((rule) => (
            <div key={rule.id} className="flex items-center justify-between p-4">
              <div>
                <div className="flex items-center space-x-2">
                  <span className="text-xs font-medium px-2 py-1 rounded bg-secondary text-secondary-foreground">
                    {ruleTypeLabels[rule.ruleType] || rule.ruleType}
                  </span>
                  <span className={`text-xs px-2 py-1 rounded-full ${rule.enabled ? "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300" : "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400"}`}>
                    {rule.enabled ? "Enabled" : "Disabled"}
                  </span>
                </div>
                <p className="text-sm text-muted-foreground mt-1">{getRuleDescription(rule)}</p>
                {rule.channelIds.length > 0 && (
                  <p className="text-xs text-muted-foreground mt-1">
                    Channels: {getChannelNames(rule.channelIds)}
                  </p>
                )}
              </div>
              <div className="flex items-center space-x-2">
                <Button variant="ghost" size="xs" onClick={() => setEditingRule(rule)}>
                  Edit
                </Button>
                <Button variant="ghost" size="xs" onClick={() => handleDelete(rule.id)}>
                  <IconTrash className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      <RuleFormDialog
        app={props.app}
        rule={editingRule}
        channels={channels || []}
        open={isCreating || editingRule !== null}
        onClose={() => { setIsCreating(false); setEditingRule(null); }}
        onSaved={() => { setIsCreating(false); setEditingRule(null); refetch(); }}
      />
    </div>
  );
}
