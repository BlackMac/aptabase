import { Button } from "@components/Button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, DialogDescription } from "@components/Dialog";
import { TextInput } from "@components/TextInput";
import { Application } from "@features/apps";
import { useState, useEffect } from "react";
import { toast } from "sonner";
import { notificationsApi, NotificationChannel } from "./notifications-api";

type Props = {
  app: Application;
  channel: NotificationChannel | null;
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
};

export function ChannelFormDialog(props: Props) {
  const isEditing = props.channel !== null;
  const [name, setName] = useState("");
  const [channelType, setChannelType] = useState("telegram");
  const [saving, setSaving] = useState(false);

  // Telegram fields
  const [botToken, setBotToken] = useState("");
  const [chatId, setChatId] = useState("");

  // Pushover fields
  const [userKey, setUserKey] = useState("");
  const [appToken, setAppToken] = useState("");

  // ntfy fields
  const [ntfyServerUrl, setNtfyServerUrl] = useState("https://ntfy.sh");
  const [ntfyTopic, setNtfyTopic] = useState("");
  const [ntfyToken, setNtfyToken] = useState("");

  useEffect(() => {
    if (props.channel) {
      setName(props.channel.name);
      setChannelType(props.channel.channelType);
      try {
        const config = JSON.parse(props.channel.configJson || "{}");
        setBotToken(config.bot_token || "");
        setChatId(config.chat_id || "");
        setUserKey(config.user_key || "");
        setAppToken(config.app_token || "");
        setNtfyServerUrl(config.server_url || "https://ntfy.sh");
        setNtfyTopic(config.topic || "");
        setNtfyToken(config.token || "");
      } catch {
        setBotToken("");
        setChatId("");
        setUserKey("");
        setAppToken("");
        setNtfyServerUrl("https://ntfy.sh");
        setNtfyTopic("");
        setNtfyToken("");
      }
    } else {
      setName("");
      setChannelType("telegram");
      setBotToken("");
      setChatId("");
      setUserKey("");
      setAppToken("");
      setNtfyServerUrl("https://ntfy.sh");
      setNtfyTopic("");
      setNtfyToken("");
    }
  }, [props.channel, props.open]);

  const buildConfigJson = () => {
    if (channelType === "telegram") {
      return JSON.stringify({ bot_token: botToken, chat_id: chatId });
    }
    if (channelType === "ntfy") {
      return JSON.stringify({
        server_url: ntfyServerUrl || "https://ntfy.sh",
        topic: ntfyTopic,
        ...(ntfyToken ? { token: ntfyToken } : {}),
      });
    }
    return JSON.stringify({ user_key: userKey, app_token: appToken });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const configJson = buildConfigJson();
      if (isEditing && props.channel) {
        await notificationsApi.updateChannel(props.app.id, props.channel.id, {
          name,
          configJson,
          enabled: props.channel.enabled,
        });
        toast("Channel updated");
      } else {
        await notificationsApi.createChannel(props.app.id, { name, channelType, configJson });
        toast("Channel created");
      }
      props.onSaved();
    } catch {
      toast.error("Failed to save channel");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={props.open} onOpenChange={(open) => !open && props.onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? "Edit Channel" : "New Channel"}</DialogTitle>
          <DialogDescription>
            {isEditing ? "Update your notification channel settings." : "Configure a new notification channel."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <TextInput
            label="Name"
            name="name"
            required
            value={name}
            maxLength={100}
            placeholder="e.g. Team Chat"
            onChange={(e) => setName(e.target.value)}
            description="A friendly label for this channel"
          />

          {!isEditing && (
            <div>
              <label className="text-sm mb-2 block font-medium">Channel Type</label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={channelType}
                onChange={(e) => setChannelType(e.target.value)}
              >
                <option value="telegram">Telegram</option>
                <option value="ntfy">ntfy</option>
                <option value="pushover">Pushover</option>
              </select>
            </div>
          )}

          {channelType === "telegram" && (
            <>
              <div className="rounded-md bg-muted p-3 text-sm text-muted-foreground space-y-1">
                <p className="font-medium text-foreground">How to set up Telegram</p>
                <ol className="list-decimal ml-4 space-y-0.5">
                  <li>Message <span className="font-mono">@BotFather</span> on Telegram and use <span className="font-mono">/newbot</span> to create a bot. Copy the token.</li>
                  <li>Send a message to your new bot (or add it to a group).</li>
                  <li>Open <span className="font-mono">https://api.telegram.org/bot<span className="italic">&lt;TOKEN&gt;</span>/getUpdates</span> in your browser to find the chat ID in the response.</li>
                </ol>
              </div>
              <TextInput
                label="Bot Token"
                name="botToken"
                required={!isEditing}
                value={botToken}
                placeholder={isEditing ? "Leave blank to keep existing" : "123456:ABC-DEF1234..."}
                onChange={(e) => setBotToken(e.target.value)}
                description="The token you received from @BotFather"
              />
              <TextInput
                label="Chat ID"
                name="chatId"
                required={!isEditing}
                value={chatId}
                placeholder={isEditing ? "Leave blank to keep existing" : "-1001234567890"}
                onChange={(e) => setChatId(e.target.value)}
                description="The numeric ID of the chat, group, or channel (found via getUpdates above)"
              />
            </>
          )}

          {channelType === "ntfy" && (
            <>
              <div className="rounded-md bg-muted p-3 text-sm text-muted-foreground space-y-1">
                <p className="font-medium text-foreground">How to set up ntfy</p>
                <ol className="list-decimal ml-4 space-y-0.5">
                  <li>Use the public server at <span className="font-mono">ntfy.sh</span> or self-host your own.</li>
                  <li>Pick a topic name (treat it like a password if using the public server).</li>
                  <li>Subscribe on your phone via the ntfy app or in your browser.</li>
                </ol>
              </div>
              <TextInput
                label="Server URL"
                name="ntfyServerUrl"
                value={ntfyServerUrl}
                placeholder="https://ntfy.sh"
                onChange={(e) => setNtfyServerUrl(e.target.value)}
                description="Leave as ntfy.sh for the public server, or enter your self-hosted URL"
              />
              <TextInput
                label="Topic"
                name="ntfyTopic"
                required
                value={ntfyTopic}
                placeholder="aptabase-alerts"
                onChange={(e) => setNtfyTopic(e.target.value)}
                description="The topic to publish notifications to"
              />
              <TextInput
                label="Access Token"
                name="ntfyToken"
                value={ntfyToken}
                placeholder={isEditing ? "Leave blank to keep existing" : "Optional — only needed if the topic requires auth"}
                onChange={(e) => setNtfyToken(e.target.value)}
                description="Bearer token for authentication (optional)"
              />
            </>
          )}

          {channelType === "pushover" && (
            <>
              <div className="rounded-md bg-muted p-3 text-sm text-muted-foreground space-y-1">
                <p className="font-medium text-foreground">How to set up Pushover</p>
                <ol className="list-decimal ml-4 space-y-0.5">
                  <li>Sign up at <span className="font-mono">pushover.net</span> and copy your <strong>User Key</strong> from the dashboard.</li>
                  <li>Create an application at <span className="font-mono">pushover.net/apps/build</span> and copy the <strong>API Token</strong>.</li>
                </ol>
              </div>
              <TextInput
                label="User Key"
                name="userKey"
                required={!isEditing}
                value={userKey}
                placeholder={isEditing ? "Leave blank to keep existing" : "uQiRzpo4DXghDmr9QzzfQu27cmVRsG"}
                onChange={(e) => setUserKey(e.target.value)}
                description="Your Pushover user key (or group key for team delivery)"
              />
              <TextInput
                label="App Token"
                name="appToken"
                required={!isEditing}
                value={appToken}
                placeholder={isEditing ? "Leave blank to keep existing" : "azGDORePK8gMaC0QOYAMyEEuzJnyUi"}
                onChange={(e) => setAppToken(e.target.value)}
                description="Your Pushover application API token"
              />
            </>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={props.onClose}>Cancel</Button>
            <Button type="submit" loading={saving}>{isEditing ? "Save" : "Create"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
