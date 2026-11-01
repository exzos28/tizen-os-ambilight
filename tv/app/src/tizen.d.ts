// The few Tizen Web APIs the launcher uses (window.tizen exists only on the TV).

declare namespace TizenApi {
  interface MessagePortDataItem {
    key: string;
    value: string | string[];
  }
  interface RemoteMessagePort {
    sendMessage(data: MessagePortDataItem[], localPort?: LocalMessagePort): void;
  }
  interface LocalMessagePort {
    addMessagePortListener(cb: (data: MessagePortDataItem[], remote: RemoteMessagePort | null) => void): number;
  }
  interface MessagePortManager {
    requestLocalMessagePort(name: string): LocalMessagePort;
    requestRemoteMessagePort(appId: string, name: string): RemoteMessagePort;
  }
  interface ApplicationControlData {}
  interface ApplicationControl {}
  interface Application {
    exit(): void;
    appInfo: { version: string };
  }
  interface ApplicationManager {
    getCurrentApplication(): Application;
    launchAppControl(
      control: ApplicationControl,
      appId: string,
      onSuccess?: () => void,
      onError?: (err: { message: string }) => void,
    ): void;
  }
  interface SystemInfo {
    getCapability(key: string): unknown;
  }
  interface Tizen {
    messageport: MessagePortManager;
    application: ApplicationManager;
    systeminfo: SystemInfo;
    ApplicationControl: new (
      operation: string,
      uri?: string | null,
      mime?: string | null,
      category?: string | null,
      data?: ApplicationControlData[],
    ) => ApplicationControl;
    ApplicationControlData: new (key: string, value: string[]) => ApplicationControlData;
  }
}

interface Window {
  tizen?: TizenApi.Tizen;
}
