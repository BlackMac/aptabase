import { ErrorState } from "@components/ErrorState";
import { LoadingState } from "@components/LoadingState";
import { Application } from "@features/apps";
import { useQuery } from "@tanstack/react-query";
import { notificationsApi } from "./notifications-api";

type Props = { app: Application };

const ruleTypeLabels: Record<string, string> = {
  event_push: "Event Push",
  threshold: "Threshold",
  new_event_name: "New Event",
  dead_app: "Dead App",
  volume_anomaly: "Anomaly",
  new_app_version: "New Version",
  new_country: "New Country",
  scheduled_digest: "Digest",
};

export function NotificationHistory(props: Props) {
  const { isLoading, isError, data: logs } = useQuery({
    queryKey: ["notification-log", props.app.id],
    queryFn: () => notificationsApi.getLog(props.app.id),
  });

  if (isLoading) return <LoadingState />;
  if (isError) return <ErrorState />;

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">Recent notification history (last 50 entries).</p>

      {logs && logs.length === 0 && (
        <div className="text-center py-8 text-muted-foreground">
          No notifications sent yet.
        </div>
      )}

      {logs && logs.length > 0 && (
        <div className="border rounded-lg overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="text-left p-3 font-medium">Time</th>
                <th className="text-left p-3 font-medium">Type</th>
                <th className="text-left p-3 font-medium">Channel</th>
                <th className="text-left p-3 font-medium">Message</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {logs.map((log) => (
                <tr key={log.id}>
                  <td className="p-3 whitespace-nowrap text-muted-foreground">
                    {new Date(log.sentAt).toLocaleString()}
                  </td>
                  <td className="p-3">
                    {log.ruleType && (
                      <span className="text-xs font-medium px-2 py-1 rounded bg-secondary text-secondary-foreground">
                        {ruleTypeLabels[log.ruleType] || log.ruleType}
                      </span>
                    )}
                  </td>
                  <td className="p-3">{log.channelName}</td>
                  <td className="p-3 max-w-xs truncate">{log.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
