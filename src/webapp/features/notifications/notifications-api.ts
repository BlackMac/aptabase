import { api } from "@fns/api";

export type NotificationChannel = {
  id: string;
  name: string;
  channelType: string;
  configJson: string;
  enabled: boolean;
  createdAt: string;
};

export type NotificationRule = {
  id: string;
  ruleType: string;
  configJson: string;
  enabled: boolean;
  channelIds: string[];
  createdAt: string;
};

export type NotificationLogEntry = {
  id: string;
  ruleId: string | null;
  channelId: string;
  channelName: string;
  ruleType: string | null;
  message: string;
  sentAt: string;
};

export const notificationsApi = {
  getChannels: (appId: string) =>
    api.get<NotificationChannel[]>(`/_apps/${appId}/notification-channels`),

  createChannel: (appId: string, body: { name: string; channelType: string; configJson: string }) =>
    api.post<NotificationChannel>(`/_apps/${appId}/notification-channels`, body),

  updateChannel: (appId: string, channelId: string, body: { name: string; configJson: string; enabled: boolean }) =>
    api.put<NotificationChannel>(`/_apps/${appId}/notification-channels/${channelId}`, body),

  deleteChannel: (appId: string, channelId: string) =>
    api.delete<{}>(`/_apps/${appId}/notification-channels/${channelId}`),

  testChannel: (appId: string, channelId: string) =>
    api.post<{}>(`/_apps/${appId}/notification-channels/${channelId}/test`),

  getRules: (appId: string) =>
    api.get<NotificationRule[]>(`/_apps/${appId}/notification-rules`),

  createRule: (appId: string, body: { ruleType: string; configJson: string; channelIds: string[] }) =>
    api.post<NotificationRule>(`/_apps/${appId}/notification-rules`, body),

  updateRule: (appId: string, ruleId: string, body: { configJson: string; enabled: boolean; channelIds: string[] }) =>
    api.put<NotificationRule>(`/_apps/${appId}/notification-rules/${ruleId}`, body),

  deleteRule: (appId: string, ruleId: string) =>
    api.delete<{}>(`/_apps/${appId}/notification-rules/${ruleId}`),

  getLog: (appId: string) =>
    api.get<NotificationLogEntry[]>(`/_apps/${appId}/notification-log`),

  getEventNames: (appId: string) =>
    api.get<string[]>(`/_apps/${appId}/event-names`),
};
