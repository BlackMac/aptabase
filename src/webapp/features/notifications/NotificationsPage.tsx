import { Page, PageHeading } from "@components/Page";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/Tabs";
import { useCurrentApp } from "@features/apps";
import { Navigate } from "react-router-dom";
import { ChannelsList } from "./ChannelsList";
import { RulesList } from "./RulesList";
import { NotificationHistory } from "./NotificationHistory";

Component.displayName = "NotificationsPage";
export function Component() {
  const app = useCurrentApp();

  if (!app || !app.hasOwnership) return <Navigate to="/" />;

  return (
    <Page title={`${app.name} - Notifications`}>
      <PageHeading title="Notifications" subtitle="Configure alerts and notifications for your app" />

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
