import { Button } from "@components/Button";
import { Page, PageHeading } from "@components/Page";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/Tabs";
import { useCurrentApp } from "@features/apps";
import { IconSend } from "@tabler/icons-react";
import { useState } from "react";
import { Navigate } from "react-router-dom";
import { toast } from "sonner";
import { ChannelsList } from "./ChannelsList";
import { NotificationHistory } from "./NotificationHistory";
import { notificationsApi } from "./notifications-api";
import { RulesList } from "./RulesList";

Component.displayName = "NotificationsPage";
export function Component() {
  const app = useCurrentApp();
  const [testing, setTesting] = useState(false);

  if (!app || !app.hasOwnership) return <Navigate to="/" />;

  const handleTestAll = async () => {
    setTesting(true);
    try {
      const result = await notificationsApi.testAllChannels(app.id);
      toast(`Test sent to ${result.sent}/${result.total} channels`);
    } catch {
      toast.error("Failed to send test notifications. Do you have enabled channels?");
    } finally {
      setTesting(false);
    }
  };

  return (
    <Page title={`${app.name} - Notifications`}>
      <div className="flex items-center justify-between">
        <PageHeading title="Notifications" subtitle="Configure alerts and notifications for your app" />
        <Button size="sm" variant="outline" loading={testing} onClick={handleTestAll}>
          <IconSend className="h-4 w-4 mr-1" /> Test All Channels
        </Button>
      </div>

      <Tabs defaultValue="channels" className="mt-8">
        <TabsList>
          <TabsTrigger value="channels">Channels</TabsTrigger>
          <TabsTrigger value="rules">Rules</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>
        <TabsContent value="channels">
          <ChannelsList app={app} />
        </TabsContent>
        <TabsContent value="rules">
          <RulesList app={app} />
        </TabsContent>
        <TabsContent value="history">
          <NotificationHistory app={app} />
        </TabsContent>
      </Tabs>
    </Page>
  );
}
