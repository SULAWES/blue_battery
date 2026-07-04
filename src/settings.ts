export type AppSettings = {
  schemaVersion: number;
  refreshIntervalSeconds: number;
  lowBatteryStatusEnabled: boolean;
  lowBatteryThreshold: number;
  lowBatterySystemNotificationEnabled: boolean;
  showPanelOnStartup: boolean;
};

export const DEFAULT_SETTINGS: AppSettings = {
  schemaVersion: 1,
  refreshIntervalSeconds: 60,
  lowBatteryStatusEnabled: true,
  lowBatteryThreshold: 20,
  lowBatterySystemNotificationEnabled: false,
  showPanelOnStartup: false,
};

export const REFRESH_INTERVAL_SECONDS = [30, 60, 120] as const;
export const LOW_BATTERY_THRESHOLDS = [10, 15, 20, 25] as const;

export function nextNumberOption(current: number, options: readonly number[]) {
  const index = options.indexOf(current);
  return options[(index + 1) % options.length] ?? options[0];
}
