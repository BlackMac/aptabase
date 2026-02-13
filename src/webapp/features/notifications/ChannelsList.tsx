import { Button } from "@components/Button";
import { ErrorState } from "@components/ErrorState";
import { LoadingState } from "@components/LoadingState";
import { Application } from "@features/apps";
import { IconBrandTelegram, IconBell, IconBellRinging, IconTrash, IconSend, IconPlus } from "@tabler/icons-react";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { notificationsApi, NotificationChannel } from "./notifications-api";
import { ChannelFormDialog } from "./ChannelFormDialog";

type Props = { app: Application };

export function ChannelsList(props: Props) {
  const [editingChannel, setEditingChannel] = useState<NotificationChannel | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [testingId, setTestingId] = useState<string | null>(null);

  const { isLoading, isError, data: channels, refetch } = useQuery({
    queryKey: ["notification-channels", props.app.id],
    queryFn: () => notificationsApi.getChannels(props.app.id),
  });

  const handleDelete = async (channelId: string) => {
    if (!confirm("Are you sure you want to delete this channel?")) return;
    await notificationsApi.deleteChannel(props.app.id, channelId);
    toast("Channel deleted");
    refetch();
  };

  const handleTest = async (channelId: string) => {
    setTestingId(channelId);
    try {
      await notificationsApi.testChannel(props.app.id, channelId);
      toast("Test notification sent!");
    } catch {
      toast.error("Failed to send test notification");
    } finally {
      setTestingId(null);
    }
  };

  const getTypeIcon = (type: string) => {
    switch (type) {
      case "telegram": return <IconBrandTelegram className="h-5 w-5 text-blue-500" />;
      case "ntfy": return <IconBellRinging className="h-5 w-5 text-green-500" />;
      case "pushover": return <IconBell className="h-5 w-5 text-orange-500" />;
      default: return <IconBell className="h-5 w-5" />;
    }
  };

  if (isLoading) return <LoadingState />;
  if (isError) return <ErrorState />;

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <p className="text-sm text-muted-foreground">
          Channels are the recipients of your notifications (e.g. a Telegram chat or a Pushover user).
        </p>
        <Button size="sm" onClick={() => setIsCreating(true)}>
          <IconPlus className="h-4 w-4 mr-1" /> Add Channel
        </Button>
      </div>

      {channels && channels.length === 0 && (
        <div className="text-center py-8 text-muted-foreground">
          No channels configured yet. Add a channel to start receiving notifications.
        </div>
      )}

      {channels && channels.length > 0 && (
        <div className="border rounded-lg divide-y">
          {channels.map((channel) => (
            <div key={channel.id} className="flex items-center justify-between p-4">
              <div className="flex items-center space-x-3">
                {getTypeIcon(channel.channelType)}
                <div>
                  <p className="font-medium">{channel.name}</p>
                  <p className="text-sm text-muted-foreground capitalize">{channel.channelType}</p>
                </div>
              </div>
              <div className="flex items-center space-x-2">
                <span className={`text-xs px-2 py-1 rounded-full ${channel.enabled ? "bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300" : "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400"}`}>
                  {channel.enabled ? "Enabled" : "Disabled"}
                </span>
                <Button variant="ghost" size="xs" loading={testingId === channel.id} onClick={() => handleTest(channel.id)}>
                  <IconSend className="h-4 w-4" />
                </Button>
                <Button variant="ghost" size="xs" onClick={() => setEditingChannel(channel)}>
                  Edit
                </Button>
                <Button variant="ghost" size="xs" onClick={() => handleDelete(channel.id)}>
                  <IconTrash className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      <ChannelFormDialog
        app={props.app}
        channel={editingChannel}
        open={isCreating || editingChannel !== null}
        onClose={() => { setIsCreating(false); setEditingChannel(null); }}
        onSaved={() => { setIsCreating(false); setEditingChannel(null); refetch(); }}
      />
    </div>
  );
}
