import { Button } from "@components/Button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, DialogDescription } from "@components/Dialog";
import { TextInput } from "@components/TextInput";
import { Application } from "@features/apps";
import { useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { notificationsApi, NotificationChannel, NotificationRule } from "./notifications-api";

type Props = {
  app: Application;
  rule: NotificationRule | null;
  channels: NotificationChannel[];
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
};

const ruleTypes = [
  { value: "event_push", label: "Event Push", description: "Notify when specific events occur" },
  { value: "threshold", label: "Threshold Alert", description: "Alert when event count exceeds a threshold" },
  { value: "new_event_name", label: "New Event Name", description: "Alert on first occurrence of a new event name" },
  { value: "dead_app", label: "Dead App", description: "Alert when no events received for a period" },
  { value: "volume_anomaly", label: "Volume Anomaly", description: "Alert on unusual traffic spikes or drops" },
  { value: "new_app_version", label: "New App Version", description: "Alert when a new app version is seen" },
  { value: "new_country", label: "New Country", description: "Alert when events from a new country are seen" },
  { value: "scheduled_digest", label: "Scheduled Digest", description: "Send a daily or weekly metrics summary" },
];

export function RuleFormDialog(props: Props) {
  const isEditing = props.rule !== null;
  const [ruleType, setRuleType] = useState("event_push");
  const [saving, setSaving] = useState(false);
  const [selectedChannelIds, setSelectedChannelIds] = useState<string[]>([]);

  // Config fields
  const [eventNames, setEventNames] = useState("");
  const [dedup, setDedup] = useState(true);
  const [dedupWindowMinutes, setDedupWindowMinutes] = useState("60");
  const [thresholdEventName, setThresholdEventName] = useState("");
  const [threshold, setThreshold] = useState("1000");
  const [thresholdPeriod, setThresholdPeriod] = useState("day");
  const [deadHours, setDeadHours] = useState("24");
  const [sensitivity, setSensitivity] = useState("medium");
  const [schedule, setSchedule] = useState("daily");

  const { data: knownEventNames } = useQuery({
    queryKey: ["event-names", props.app.id],
    queryFn: () => notificationsApi.getEventNames(props.app.id),
    enabled: props.open,
  });

  useEffect(() => {
    if (props.rule) {
      setRuleType(props.rule.ruleType);
      setSelectedChannelIds(props.rule.channelIds);
      try {
        const config = JSON.parse(props.rule.configJson);
        setEventNames((config.event_names || []).join(", "));
        setDedup(config.dedup ?? true);
        setDedupWindowMinutes(String(config.dedup_window_minutes ?? 60));
        setThresholdEventName(config.event_name || "");
        setThreshold(String(config.threshold ?? 1000));
        setThresholdPeriod(config.period || "day");
        setDeadHours(String(config.hours ?? 24));
        setSensitivity(config.sensitivity || "medium");
        setSchedule(config.schedule || "daily");
      } catch { /* ignore parse errors */ }
    } else {
      setRuleType("event_push");
      setSelectedChannelIds([]);
      setEventNames("");
      setDedup(true);
      setDedupWindowMinutes("60");
      setThresholdEventName("");
      setThreshold("1000");
      setThresholdPeriod("day");
      setDeadHours("24");
      setSensitivity("medium");
      setSchedule("daily");
    }
  }, [props.rule, props.open]);

  const buildConfigJson = () => {
    switch (ruleType) {
      case "event_push":
        return JSON.stringify({
          event_names: eventNames.split(",").map((s) => s.trim()).filter(Boolean),
          dedup,
          dedup_window_minutes: parseInt(dedupWindowMinutes) || 60,
        });
      case "threshold":
        return JSON.stringify({
          event_name: thresholdEventName,
          threshold: parseInt(threshold) || 1000,
          period: thresholdPeriod,
        });
      case "dead_app":
        return JSON.stringify({ hours: parseInt(deadHours) || 24 });
      case "volume_anomaly":
        return JSON.stringify({ sensitivity });
      case "scheduled_digest":
        return JSON.stringify({ schedule });
      default:
        return "{}";
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedChannelIds.length === 0) {
      toast.error("Please select at least one channel");
      return;
    }
    setSaving(true);
    try {
      const configJson = buildConfigJson();
      if (isEditing && props.rule) {
        await notificationsApi.updateRule(props.app.id, props.rule.id, {
          configJson,
          enabled: props.rule.enabled,
          channelIds: selectedChannelIds,
        });
        toast("Rule updated");
      } else {
        await notificationsApi.createRule(props.app.id, { ruleType, configJson, channelIds: selectedChannelIds });
        toast("Rule created");
      }
      props.onSaved();
    } catch {
      toast.error("Failed to save rule");
    } finally {
      setSaving(false);
    }
  };

  const toggleChannel = (channelId: string) => {
    setSelectedChannelIds((prev) =>
      prev.includes(channelId) ? prev.filter((id) => id !== channelId) : [...prev, channelId]
    );
  };

  return (
    <Dialog open={props.open} onOpenChange={(open) => !open && props.onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{isEditing ? "Edit Rule" : "New Rule"}</DialogTitle>
          <DialogDescription>
            {isEditing ? "Update your notification rule." : "Configure when and how you want to be notified."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 max-h-[60vh] overflow-y-auto pr-2">
          {!isEditing && (
            <div>
              <label className="text-sm mb-2 block font-medium">Rule Type</label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={ruleType}
                onChange={(e) => setRuleType(e.target.value)}
              >
                {ruleTypes.map((rt) => (
                  <option key={rt.value} value={rt.value}>{rt.label}</option>
                ))}
              </select>
              <p className="mt-1 text-sm text-muted-foreground">
                {ruleTypes.find((rt) => rt.value === ruleType)?.description}
              </p>
            </div>
          )}

          {ruleType === "event_push" && (
            <>
              <TextInput
                label="Event Names"
                name="eventNames"
                required
                value={eventNames}
                placeholder="purchase, error, signup"
                onChange={(e) => setEventNames(e.target.value)}
                description={`Comma-separated event names${knownEventNames?.length ? `. Known: ${knownEventNames.join(", ")}` : ""}`}
              />
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  id="dedup"
                  checked={dedup}
                  onChange={(e) => setDedup(e.target.checked)}
                  className="h-4 w-4 rounded border-input"
                />
                <label htmlFor="dedup" className="text-sm">Enable deduplication</label>
              </div>
              {dedup && (
                <TextInput
                  label="Dedup Window (minutes)"
                  name="dedupWindow"
                  type="number"
                  value={dedupWindowMinutes}
                  onChange={(e) => setDedupWindowMinutes(e.target.value)}
                />
              )}
            </>
          )}

          {ruleType === "threshold" && (
            <>
              <TextInput
                label="Event Name"
                name="thresholdEventName"
                required
                value={thresholdEventName}
                placeholder="error"
                onChange={(e) => setThresholdEventName(e.target.value)}
              />
              <TextInput
                label="Threshold Count"
                name="threshold"
                type="number"
                required
                value={threshold}
                onChange={(e) => setThreshold(e.target.value)}
              />
              <div>
                <label className="text-sm mb-2 block font-medium">Period</label>
                <select
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                  value={thresholdPeriod}
                  onChange={(e) => setThresholdPeriod(e.target.value)}
                >
                  <option value="hour">Hour</option>
                  <option value="day">Day</option>
                </select>
              </div>
            </>
          )}

          {ruleType === "dead_app" && (
            <TextInput
              label="Hours Without Events"
              name="deadHours"
              type="number"
              required
              value={deadHours}
              onChange={(e) => setDeadHours(e.target.value)}
              description="Alert if no events received for this many hours"
            />
          )}

          {ruleType === "volume_anomaly" && (
            <div>
              <label className="text-sm mb-2 block font-medium">Sensitivity</label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={sensitivity}
                onChange={(e) => setSensitivity(e.target.value)}
              >
                <option value="low">Low (3 sigma deviation)</option>
                <option value="medium">Medium (2 sigma deviation)</option>
                <option value="high">High (1.5 sigma deviation)</option>
              </select>
            </div>
          )}

          {ruleType === "scheduled_digest" && (
            <div>
              <label className="text-sm mb-2 block font-medium">Schedule</label>
              <select
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={schedule}
                onChange={(e) => setSchedule(e.target.value)}
              >
                <option value="daily">Daily</option>
                <option value="weekly">Weekly (Mondays)</option>
              </select>
            </div>
          )}

          <div>
            <label className="text-sm mb-2 block font-medium">Notify Channels</label>
            {props.channels.length === 0 ? (
              <p className="text-sm text-muted-foreground">No channels available. Create a channel first.</p>
            ) : (
              <div className="space-y-2">
                {props.channels.map((channel) => (
                  <label key={channel.id} className="flex items-center space-x-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={selectedChannelIds.includes(channel.id)}
                      onChange={() => toggleChannel(channel.id)}
                      className="h-4 w-4 rounded border-input"
                    />
                    <span className="text-sm">{channel.name}</span>
                    <span className="text-xs text-muted-foreground capitalize">({channel.channelType})</span>
                  </label>
                ))}
              </div>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={props.onClose}>Cancel</Button>
            <Button type="submit" loading={saving} disabled={selectedChannelIds.length === 0}>
              {isEditing ? "Save" : "Create"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
