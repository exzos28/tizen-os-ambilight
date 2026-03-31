using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClipV2;
using ClipV2.Data;
using ClipV2.Events;
using ClipV2.Resources;
using Common;
using Common.Rest;
using Hue.BridgeDiscovery;
using Hue.StreamClient;
using Hue.StreamClient.Colors;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETStandard,Version=v2.0", FrameworkDisplayName = ".NET Standard 2.0")]
[assembly: AssemblyCompany("Signify Holding B.V.")]
[assembly: AssemblyConfiguration("FinalTV")]
[assembly: AssemblyCopyright("Copyright © 2019 Signify Holding")]
[assembly: AssemblyFileVersion("1.8.55.0")]
[assembly: AssemblyInformationalVersion("1.8.55+50a0a7e0ea5077bc142763b4588f4446d2f1dc9c")]
[assembly: AssemblyProduct("Philips Hue .NET Mini EDK")]
[assembly: AssemblyTitle("EDK")]
[assembly: AssemblyVersion("1.8.55.0")]
namespace Hue.EDK;

public class Area
{
	public static class LeftRight2
	{
		public static readonly Area Left = new Area(new Location(-1.0, 1.0, 1.0), new Location(0.0, -1.0, -1.0));

		public static readonly Area Right = new Area(new Location(0.0, 1.0, 1.0), new Location(1.0, -1.0, -1.0));
	}

	public static class LeftRight3
	{
		public static readonly Area Left = new Area(new Location(-1.0, 1.0, 1.0), new Location(-0.33, -1.0, -1.0));

		public static readonly Area Center = new Area(new Location(-0.33, 1.0, 1.0), new Location(0.33, -1.0, -1.0));

		public static readonly Area Right = new Area(new Location(0.33, 1.0, 1.0), new Location(1.0, -1.0, -1.0));
	}

	public static class Surround5
	{
		public static readonly Area LeftBack = new Area(new Location(-1.0, 0.0, 1.0), new Location(0.0, -1.0, -1.0));

		public static readonly Area LeftFront = new Area(new Location(-1.0, 1.0, 1.0), new Location(-0.33, 0.0, -1.0));

		public static readonly Area CenterFront = new Area(new Location(-0.33, 1.0, 1.0), new Location(0.33, 0.0, -1.0));

		public static readonly Area RightFront = new Area(new Location(0.33, 1.0, 1.0), new Location(1.0, 0.0, -1.0));

		public static readonly Area RightBack = new Area(new Location(0.0, 0.0, 1.0), new Location(1.0, -1.0, -1.0));
	}

	public static class Standard6
	{
		public static readonly Area Back = new Area(new Location(-1.0, 0.0, 1.0), new Location(1.0, -1.0, -1.0));

		public static readonly Area LeftFront = new Area(new Location(-1.0, 1.0, 1.0), new Location(-0.33, 0.0, -1.0));

		public static readonly Area CenterFrontTop = new Area(new Location(-0.33, 1.0, 1.0), new Location(0.33, 0.0, 0.33));

		public static readonly Area CenterFrontCenter = new Area(new Location(-0.33, 1.0, 0.33), new Location(0.33, 0.0, -0.33));

		public static readonly Area CenterFrontBottom = new Area(new Location(-0.33, 1.0, -0.33), new Location(0.33, 0.0, -1.0));

		public static readonly Area RightFront = new Area(new Location(0.33, 1.0, 1.0), new Location(1.0, 0.0, -1.0));
	}

	public static class Standard8
	{
		public static readonly Area LeftBack = new Area(new Location(-1.0, 0.0, 1.0), new Location(-0.33, -1.0, -1.0));

		public static readonly Area LeftFront = new Area(new Location(-1.0, 1.0, 1.0), new Location(-0.33, 0.0, -1.0));

		public static readonly Area CenterBack = new Area(new Location(-0.33, 0.0, 1.0), new Location(0.33, -1.0, -1.0));

		public static readonly Area CenterFrontTop = new Area(new Location(-0.33, 1.0, 1.0), new Location(0.33, 0.0, 0.33));

		public static readonly Area CenterFrontCenter = new Area(new Location(-0.33, 1.0, 0.33), new Location(0.33, 0.0, -0.33));

		public static readonly Area CenterFrontBottom = new Area(new Location(-0.33, 1.0, -0.33), new Location(0.33, 0.0, -1.0));

		public static readonly Area RightBack = new Area(new Location(0.33, 0.0, 1.0), new Location(1.0, -1.0, -1.0));

		public static readonly Area RightFront = new Area(new Location(0.33, 1.0, 1.0), new Location(1.0, 0.0, -1.0));
	}

	public static class Standard12
	{
		public static readonly Area LeftFrontBottom = new Area(new Location(-1.0, 1.0, -0.33), new Location(-0.33, 0.1, -1.0), "LeftFrontBottom");

		public static readonly Area LeftFrontCenter = new Area(new Location(-1.0, 1.0, 0.33), new Location(-0.33, 0.1, -0.33), "LeftFrontCenter");

		public static readonly Area LeftFrontTop = new Area(new Location(-1.0, 1.0, 1.0), new Location(-0.33, 0.1, 0.33), "CenterFrontTop");

		public static readonly Area CenterFrontTop = new Area(new Location(-0.33, 1.0, 1.0), new Location(0.33, 0.1, 0.33), "CenterFrontTop");

		public static readonly Area RightFrontTop = new Area(new Location(0.33, 1.0, 1.0), new Location(1.0, 0.1, 0.33), "RightFrontTop");

		public static readonly Area RightFrontCenter = new Area(new Location(0.33, 1.0, 0.33), new Location(1.0, 0.1, -0.33), "RightFrontCenter");

		public static readonly Area RightFrontBottom = new Area(new Location(0.33, 1.0, -0.33), new Location(1.0, 0.1, -1.0), "RightFrontBottom");

		public static readonly Area CenterFrontBottom = new Area(new Location(-0.33, 1.0, -0.33), new Location(0.33, 0.1, -1.0), "CenterFrontBottom");

		public static readonly Area CenterFrontCenter = new Area(new Location(-0.33, 1.0, 0.33), new Location(0.33, 0.1, -0.33), "CenterFrontCenter");

		public static readonly Area LeftBack = new Area(new Location(-1.0, 0.1, 1.0), new Location(-0.33, -1.0, -1.0), "LeftBack");

		public static readonly Area CenterBack = new Area(new Location(-0.33, 0.1, 1.0), new Location(0.33, -1.0, -1.0), "CenterBack");

		public static readonly Area RightBack = new Area(new Location(0.33, 0.1, 1.0), new Location(1.0, -1.0, -1.0), "RightBack");
	}

	public static readonly Area All = new Area(new Location(-1.0, 1.0, 1.0), new Location(1.0, -1.0, -1.0));

	public Location LeftFrontTop { get; set; }

	public Location RightBackBottom { get; set; }

	public string Name { get; set; }

	public Area()
	{
	}

	public Area(Location leftFrontTop, Location rightBackBottom, string name = "")
	{
		LeftFrontTop = leftFrontTop;
		RightBackBottom = rightBackBottom;
		Name = name;
	}

	public bool Contains(Position location)
	{
		return Coordinate.op_Implicit(location.X) >= LeftFrontTop.X && Coordinate.op_Implicit(location.X) <= RightBackBottom.X && Coordinate.op_Implicit(location.Y) <= LeftFrontTop.Y && Coordinate.op_Implicit(location.Y) >= RightBackBottom.Y && Coordinate.op_Implicit(location.Z) <= LeftFrontTop.Z && Coordinate.op_Implicit(location.Z) >= RightBackBottom.Z;
	}
}
internal class PushlinkPost
{
	[JsonProperty("devicetype")]
	public string DeviceType { get; set; }

	[JsonProperty("generateclientkey")]
	public bool GenerateClientKey { get; set; }

	public PushlinkPost()
	{
	}

	public PushlinkPost(string deviceType, bool generateClientKey)
	{
		DeviceType = deviceType;
		GenerateClientKey = generateClientKey;
	}
}
internal class PushlinkResult
{
	public bool IsSuccess => Success != null;

	public SuccessResponse Success { get; set; }

	public ErrorResponse Error { get; set; }
}
internal class ErrorResponse
{
	public string Description { get; set; }
}
internal class SuccessResponse
{
	public string Username { get; set; }

	public string Clientkey { get; set; }
}
internal class Authenticator
{
	private struct CertContainer
	{
		public byte[] cert;

		public void SetCert(byte[] data)
		{
			cert = data;
		}
	}

	private readonly string _appName;

	private readonly string _deviceName;

	private readonly int _timeoutMs;

	private readonly int _pollPeriodMs;

	public Authenticator(string appName, string deviceName, int timeoutMs = 60000, int pollPeriodMs = 1000)
	{
		_appName = appName;
		_deviceName = deviceName;
		_timeoutMs = timeoutMs;
		_pollPeriodMs = pollPeriodMs;
	}

	public async Task<Bridge> Authenticate(List<BridgeResult> bridgeResults, CancellationToken? cancelToken = null, bool forceTrustSelfSignedCertificate = false)
	{
		List<Bridge> bridges = new List<Bridge>();
		foreach (BridgeResult bridgeResult in bridgeResults)
		{
			bridges.Add(new Bridge(bridgeResult));
		}
		return await Authenticate(bridges, cancelToken, forceTrustSelfSignedCertificate);
	}

	public async Task<Bridge> Authenticate(Bridge bridge, CancellationToken? cancelToken = null, bool forceTrustSelfSignedCertificate = false)
	{
		return await Authenticate(new List<Bridge> { bridge }, cancelToken, forceTrustSelfSignedCertificate);
	}

	public async Task<Bridge> Authenticate(List<Bridge> bridges, CancellationToken? cancelToken = null, bool forceTrustSelfSignedCertificate = false)
	{
		if (bridges.Count == 0)
		{
			return new Bridge();
		}
		RestClient[] clipClientList = new RestClient[bridges.Count];
		int i = 0;
		while (i < bridges.Count)
		{
			clipClientList[i] = new RestClient(new RestClientSettings
			{
				BaseAddress = "https://" + bridges[i].Ip + "/",
				Certificates = CertificateStore.Certificates,
				ForceTrustSelfSignedCertificate = forceTrustSelfSignedCertificate
			});
			int num = i + 1;
			i = num;
		}
		for (int j = 0; j < _timeoutMs / _pollPeriodMs; j++)
		{
			int j2 = 0;
			while (j2 < bridges.Count)
			{
				cancelToken?.ThrowIfCancellationRequested();
				Bridge bridge = bridges[j2];
				RestClient clipClient = clipClientList[j2];
				PushlinkPost body = new PushlinkPost(_appName + "#" + _deviceName, generateClientKey: true);
				RestRequest request = new RestRequest("api", HttpMethod.Post);
				request.AddJsonBody(body);
				Task<RestResponse> result = clipClient.Execute(request);
				HttpResponse<List<PushlinkResult>> httpResponse = ClipV2ResponseExtensions.ToHttpResponse<List<PushlinkResult>>(result);
				if (httpResponse.Body != null)
				{
					foreach (PushlinkResult response in httpResponse.Body)
					{
						if (response.IsSuccess)
						{
							bridge.UserName = response.Success.Username;
							bridge.ClientKey = HexToBytes(response.Success.Clientkey);
							bridge.Certificate = clipClient.ExpectedCertificate;
							return bridge;
						}
					}
				}
				int num = j2 + 1;
				j2 = num;
			}
			await Task.Delay(_pollPeriodMs / bridges.Count);
			cancelToken?.ThrowIfCancellationRequested();
		}
		return new Bridge();
	}

	public async Task<Bridge> MigrateToBridgeV3(Bridge bridgeV2, List<Bridge> bridgeV3List)
	{
		if (bridgeV2 == null || bridgeV3List == null || bridgeV3List.Count == 0)
		{
			return null;
		}
		int i = 0;
		while (i < bridgeV3List.Count)
		{
			Bridge bridgeV3 = bridgeV3List[i];
			RestClient clipClient = new RestClient(new RestClientSettings
			{
				BaseAddress = "https://" + bridgeV3.Ip + "/",
				Certificates = CertificateStore.Certificates,
				ForceTrustSelfSignedCertificate = false,
				DefaultHeaders = new Dictionary<string, string> { { "hue-application-key", bridgeV2.UserName } }
			});
			RestRequest restRequest = new RestRequest("auth/v1", HttpMethod.Get);
			RestResponse response = await clipClient.Execute(restRequest);
			HttpStatusCode statusCode = response.StatusCode;
			HttpStatusCode httpStatusCode = statusCode;
			if (httpStatusCode == HttpStatusCode.OK)
			{
				bridgeV3.UserName = bridgeV2.UserName;
				bridgeV3.ClientKey = bridgeV2.ClientKey;
				bridgeV3.Certificate = clipClient.ExpectedCertificate;
				bridgeV3.Authorized = true;
				bridgeV3.Connected = true;
				bridgeV3.ActiveEntertainmentConfigurationId = bridgeV2.ActiveEntertainmentConfigurationId;
				return bridgeV3;
			}
			Logger.Instance.Log("[Authenticator.MigrateToBridgeV3] Failed: " + response.StatusCode, Logger.LogLevel.ERROR);
			int num = i + 1;
			i = num;
		}
		return null;
	}

	private static byte[] HexToBytes(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		int num = 0;
		for (int i = 0; i < hex.Length; i += 2)
		{
			array[num] = Convert.ToByte(hex.Substring(i, 2), 16);
			num++;
		}
		return array;
	}
}
public class Bridge
{
	private const int minClipV2ApiVersion = 1944193080;

	private const int minModelVersion = 2;

	public string Id { get; set; } = "";

	public string Model { get; set; } = "";

	public string Ip { get; set; } = "";

	public string Version { get; set; } = "";

	public string SWVersion { get; set; } = "";

	public string Name { get; set; } = "";

	public string UserName { get; set; } = "";

	public byte[] ClientKey { get; set; }

	public byte[] Certificate { get; set; }

	public string ActiveEntertainmentConfigurationId { get; set; } = "";

	public string AppId { get; set; } = "";

	public bool Connected { get; set; } = false;

	public bool Authorized { get; set; } = false;

	public int MaxStream { get; set; } = 1;

	public Bridge()
	{
	}

	public Bridge(BridgeResult bridgeResult)
	{
		Init(bridgeResult);
	}

	public void Init(BridgeResult bridgeResult)
	{
		Id = bridgeResult.Id;
		Model = bridgeResult.Model;
		Ip = bridgeResult.Ip;
		Version = bridgeResult.Version;
		SWVersion = bridgeResult.SWVersion;
		Name = bridgeResult.Name;
	}

	public bool IsDiscovered()
	{
		return (Ip?.Length ?? 0) > 0;
	}

	public bool IsAuthenticated()
	{
		return (UserName?.Length ?? 0) > 0;
	}

	public bool IsStreaming()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		if (!Connected)
		{
			return false;
		}
		EntertainmentConfigurationGet activeEntertainmentConfiguration = GetActiveEntertainmentConfiguration();
		ResourceIdentifierGet activeStreamer = activeEntertainmentConfiguration.ActiveStreamer;
		return (int)activeEntertainmentConfiguration.Status == 0 && activeStreamer != null && GUID.op_Implicit(activeStreamer.Rid) == AppId;
	}

	public bool IsValidEntertainmentConfigurationSelected()
	{
		List<ResourceIdentifierGet> lightServices = GetActiveEntertainmentConfiguration().LightServices;
		return lightServices != null && lightServices.Count > 0;
	}

	public bool IsValidApiVersion()
	{
		try
		{
			return int.Parse(SWVersion) >= 1944193080;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool IsValidModel()
	{
		Regex regex = new Regex("(?:BSB|HSE)(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		MatchCollection matchCollection = regex.Matches(Model);
		if (matchCollection.Count != 1 || matchCollection[0].Groups.Count != 2)
		{
			return false;
		}
		return int.Parse(matchCollection[0].Groups[1].Value) >= 2;
	}

	public EntertainmentConfigurationGet GetActiveEntertainmentConfiguration()
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		if (ResourceManager.GetInstance() != null && ResourceManager.GetInstance().EntertainmentConfigurations != null)
		{
			foreach (EntertainmentConfigurationGet entertainmentConfiguration in ResourceManager.GetInstance().EntertainmentConfigurations)
			{
				if (GUID.op_Implicit(((ResourceGet)entertainmentConfiguration).Id) == ActiveEntertainmentConfigurationId)
				{
					return entertainmentConfiguration;
				}
			}
		}
		return new EntertainmentConfigurationGet();
	}

	public bool SetActiveEntertainmentConfiguration(string id)
	{
		if (id == "")
		{
			ActiveEntertainmentConfigurationId = id;
			return true;
		}
		if (ResourceManager.GetInstance() != null && ResourceManager.GetInstance().EntertainmentConfigurations != null)
		{
			foreach (EntertainmentConfigurationGet entertainmentConfiguration in ResourceManager.GetInstance().EntertainmentConfigurations)
			{
				if (GUID.op_Implicit(((ResourceGet)entertainmentConfiguration).Id) == id)
				{
					ActiveEntertainmentConfigurationId = id;
					return true;
				}
			}
		}
		return false;
	}

	public List<EntertainmentConfigurationGet> GetEntertainmentConfigurations()
	{
		if (Connected)
		{
			return ResourceManager.GetInstance().EntertainmentConfigurations;
		}
		return null;
	}

	public EntertainmentConfigurationGet GetEntertainmentConfigurationById(string id)
	{
		List<EntertainmentConfigurationGet> entertainmentConfigurations = ResourceManager.GetInstance().EntertainmentConfigurations;
		if (entertainmentConfigurations != null)
		{
			foreach (EntertainmentConfigurationGet entertainmentConfiguration in ResourceManager.GetInstance().EntertainmentConfigurations)
			{
				if (GUID.op_Implicit(((ResourceGet)entertainmentConfiguration).Id) == id)
				{
					return entertainmentConfiguration;
				}
			}
		}
		return null;
	}

	public List<EntertainmentConfigurationGet> GetEntertainmentConfigurationsOwnByOtherClient()
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		List<EntertainmentConfigurationGet> list = new List<EntertainmentConfigurationGet>();
		if (ResourceManager.GetInstance() != null && ResourceManager.GetInstance().EntertainmentConfigurations != null)
		{
			foreach (EntertainmentConfigurationGet entertainmentConfiguration in ResourceManager.GetInstance().EntertainmentConfigurations)
			{
				if ((int)entertainmentConfiguration.Status == 0 && entertainmentConfiguration.ActiveStreamer != null && GUID.op_Implicit(entertainmentConfiguration.ActiveStreamer.Rid) != AppId)
				{
					list.Add(entertainmentConfiguration);
				}
			}
		}
		return list;
	}

	public List<EntertainmentConfigurationGet> GetEntertainmentConfigurationsOwnBySelf()
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		List<EntertainmentConfigurationGet> list = new List<EntertainmentConfigurationGet>();
		if (ResourceManager.GetInstance() != null && ResourceManager.GetInstance().EntertainmentConfigurations != null)
		{
			foreach (EntertainmentConfigurationGet entertainmentConfiguration in ResourceManager.GetInstance().EntertainmentConfigurations)
			{
				if ((int)entertainmentConfiguration.Status == 0 && entertainmentConfiguration.ActiveStreamer != null && GUID.op_Implicit(entertainmentConfiguration.ActiveStreamer.Rid) == AppId)
				{
					list.Add(entertainmentConfiguration);
				}
			}
		}
		return list;
	}
}
public static class ChannelMapper
{
	public static List<ChannelDataRgb> MapChannelColorsToLights(Dictionary<Area, ColorRgb> channels, List<EntertainmentChannelGet> entertainmentChannels)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		List<ChannelDataRgb> list = new List<ChannelDataRgb>();
		if (entertainmentChannels == null)
		{
			return list;
		}
		foreach (EntertainmentChannelGet entertainmentChannel in entertainmentChannels)
		{
			foreach (KeyValuePair<Area, ColorRgb> channel in channels)
			{
				if (channel.Key.Contains(entertainmentChannel.Position))
				{
					list.Add(new ChannelDataRgb(entertainmentChannel.ChannelId, new Rgb(channel.Value), (AddressType)0));
					break;
				}
			}
		}
		return list;
	}

	public static List<ChannelDataRgb> MapChannelColorsToLights(Dictionary<int, ColorRgb> channels)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		List<ChannelDataRgb> list = new List<ChannelDataRgb>();
		if (channels == null)
		{
			return list;
		}
		foreach (KeyValuePair<int, ColorRgb> channel in channels)
		{
			list.Add(new ChannelDataRgb(channel.Key, new Rgb(channel.Value), (AddressType)0));
		}
		return list;
	}
}
internal class ConnectionMonitor
{
	private Thread _thread;

	private Bridge _bridge;

	private bool _stop = true;

	private Action<HttpStatusCode> _disconnectCallback;

	private Action _reconnectCallback;

	private Action _unauthorizedCallback;

	private int _timesDoingFastPoll;

	private int _sleepTime;

	private int _resumeHandled = 0;

	private NetworkDisconnectEvent _networkDisconnectHandler = null;

	private Mutex _fence = new Mutex();

	public int CheckInterval { get; set; }

	public bool FirstChanceError { get; set; }

	public ConnectionMonitor(Action<HttpStatusCode> disconnectedCallback, Action reconnectCallback, Action unauthorizedCallback)
	{
		_disconnectCallback = disconnectedCallback;
		_reconnectCallback = reconnectCallback;
		_unauthorizedCallback = unauthorizedCallback;
		Platform.Instance.Sleep += OnSleep;
		Platform.Instance.Resume += OnResume;
	}

	public void Reset()
	{
		Platform.Instance.Sleep -= OnSleep;
		Platform.Instance.Resume -= OnResume;
	}

	public void Start(Bridge bridge, int checkInterval)
	{
		Logger.Instance.Log("[ConnectionMonitor.Start]");
		if (!_stop)
		{
			return;
		}
		_bridge = bridge;
		CheckInterval = checkInterval;
		StartMonitor();
		_networkDisconnectHandler = (NetworkDisconnectEvent)Delegate.Combine(_networkDisconnectHandler, (NetworkDisconnectEvent)delegate(bool networkConnected, bool internetConnected)
		{
			Logger.Instance.Log($"[ConnectionMonitor.Start] Network disconnected: {networkConnected}, {internetConnected}");
			_fence.WaitOne();
			if (!networkConnected && _bridge.Connected)
			{
				Logger.Instance.Log("[ConnectionMonitor.Start] Network disconnected, triggering disconnect");
				_disconnectCallback?.Invoke(HttpStatusCode.GatewayTimeout);
			}
			_fence.ReleaseMutex();
		});
		Common.Module.ServiceProvider.GetService<INetworkMonitor>().NetworkDisconnect += _networkDisconnectHandler;
	}

	private void StartMonitor()
	{
		Logger.Instance.Log("[ConnectionMonitor.StartMonitor]");
		_thread?.Join();
		_stop = false;
		_thread = new Thread(OnMonitor);
		_thread.Start();
	}

	public void Stop(int waitMS = -1)
	{
		Logger.Instance.Log("[ConnectionMonitor.Stop] waitMS");
		if (!_stop)
		{
			_stop = true;
			Common.Module.ServiceProvider.GetService<INetworkMonitor>().NetworkDisconnect -= _networkDisconnectHandler;
			_thread?.Join(waitMS);
		}
	}

	private void OnMonitor()
	{
		Logger.Instance.Log("[ConnectionMonitor.OnMonitor] start thread " + _bridge.Connected);
		bool flag = false;
		while (!_stop)
		{
			try
			{
				bool connected = _bridge.Connected;
				bool authorized = _bridge.Authorized;
				Task<string> applicationId = ResourceManager.GetInstance().GetApplicationId(_bridge, FirstChanceError && !flag);
				applicationId.Wait();
				if (_stop)
				{
					break;
				}
				string result = applicationId.Result;
				flag = false;
				if (result == "")
				{
					flag = true;
				}
				_fence.WaitOne();
				if (connected && !_bridge.Connected && Common.Module.ServiceProvider.GetService<INetworkMonitor>().IsNetworkConnected())
				{
					Logger.Instance.Log("[ConnectionMonitor.OnMonitor] Bridge status: connected is " + _bridge.Connected + " authorized is " + _bridge.Authorized);
					Task.Run(delegate
					{
						_disconnectCallback?.Invoke(ResourceManager.GetInstance().LastHttpStatusCode);
					});
				}
				else if (!connected && _bridge.Connected)
				{
					Logger.Instance.Log("[ConnectionMonitor.OnMonitor] Reconnect");
					Task.Run(delegate
					{
						_reconnectCallback?.Invoke();
					});
				}
				else if (authorized && !_bridge.Authorized)
				{
					Logger.Instance.Log("[ConnectionMonitor.OnMonitor] Unauthorized");
					Task.Run(delegate
					{
						_unauthorizedCallback?.Invoke();
					});
				}
				_fence.ReleaseMutex();
				goto IL_01e8;
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[ConnectionMonitor.OnMonitor] Exception trying to fetch app id: " + ex);
				goto IL_01e8;
			}
			IL_01e8:
			if (_stop)
			{
				break;
			}
			SleepUntilCheckInterval();
		}
		Logger.Instance.Log("[ConnectionMonitor.OnMonitor] end thread");
	}

	private void SleepUntilCheckInterval()
	{
		if (_timesDoingFastPoll > 0)
		{
			_timesDoingFastPoll--;
			_sleepTime = 1000;
		}
		else
		{
			_sleepTime = CheckInterval;
		}
		while (_sleepTime > 0)
		{
			Thread.Sleep(1000);
			_sleepTime -= 1000;
		}
	}

	private void OnSleep()
	{
		if (_bridge == null)
		{
			Logger.Instance.Log("[ConnectionMonitor.OnSleep] no bridge");
			return;
		}
		Logger.Instance.Log("[ConnectionMonitor.OnSleep] triggering disconnect");
		Stop(500);
		_disconnectCallback?.Invoke(HttpStatusCode.Gone);
	}

	private void OnResume()
	{
		if (_bridge == null)
		{
			Logger.Instance.Log("[ConnectionMonitor.OnResume] no bridge");
			return;
		}
		_resumeHandled = 0;
		INetworkMonitor nm = Common.Module.ServiceProvider.GetService<INetworkMonitor>();
		NetworkReconnectEvent handler = null;
		handler = delegate(bool networkConnected, bool internetConnected)
		{
			if (networkConnected)
			{
				nm.NetworkReconnect -= handler;
				if (Interlocked.CompareExchange(ref _resumeHandled, 1, 0) == 0)
				{
					OnResumeInternal();
				}
			}
		};
		nm.NetworkReconnect += handler;
		if (!nm.IsNetworkConnected())
		{
			Logger.Instance.Log("[ConnectionMonitor.OnResume] Network not connected, delaying bridge reconnect");
		}
		else if (Interlocked.CompareExchange(ref _resumeHandled, 1, 0) == 0)
		{
			OnResumeInternal();
			nm.NetworkReconnect -= handler;
		}
	}

	private void OnResumeInternal()
	{
		Logger.Instance.Log("[ConnectionMonitor.OnResume] triggering reconnect, bridge is connected: " + _bridge.Connected);
		_sleepTime = 0;
		_timesDoingFastPoll = 10;
		_reconnectCallback?.Invoke();
	}
}
public enum ActivationOverrideLevel
{
	Never,
	SameGroup,
	Always
}
public enum ConnectionFailedReason
{
	BridgeNotFound,
	AuthenticationFailed,
	BridgeInvalidModel,
	BridgeInvalidSwVersion,
	Cancelled,
	InternalError,
	None
}
public enum StreamingConnectingFailedReason
{
	NoEntertainmentConfigurationSelected,
	NoEntertainmentConfigurationAvailable,
	BridgeNotConnected,
	HttpError,
	Exception,
	Timeout
}
public delegate void BusyDelegate(bool busy);
public class Edk
{
	private static Edk _instance;

	private PersistentData persistentData = null;

	private readonly Authenticator authenticator;

	private readonly IFileDataStore dataStore;

	private readonly string dataStorePath;

	private HueStreamClient hueStreamClient;

	private ConnectionMonitor _connectionMonitor;

	private int _busy = 0;

	private bool _streaming = false;

	private bool _streamingIsBroken = false;

	private bool _wasStreaming = false;

	private Stopwatch _disconnectedTime = new Stopwatch();

	private Mutex _streamLock = new Mutex();

	private CancellationTokenSource _cancelTokenSource = null;

	private bool _forceTrustSelfSignedCertificate = false;

	private TimeSpan _lastSendFrameTime = TimeSpan.Zero;

	private TimeSpan _currentSendFrameTime = TimeSpan.Zero;

	private Task _streamStopTaskOnDisconnect = null;

	private ClipV2Client _clipv2Client;

	public static Edk Instance => _instance;

	public event BusyDelegate BusyChanged;

	private Edk(string appName, string deviceName, string appDataPath, bool forceTrustSelfSignedCertificate = false)
	{
		authenticator = new Authenticator(appName, deviceName);
		string[] obj = new string[5] { appDataPath, null, null, null, null };
		char directorySeparatorChar = Path.DirectorySeparatorChar;
		obj[1] = directorySeparatorChar.ToString();
		obj[2] = appName;
		directorySeparatorChar = Path.DirectorySeparatorChar;
		obj[3] = directorySeparatorChar.ToString();
		obj[4] = "PersistentData.json";
		dataStorePath = string.Concat(obj);
		dataStore = Module.ServiceProvider.GetService<IFileDataStore>();
		dataStore.Path = dataStorePath;
		_connectionMonitor = new ConnectionMonitor(OnDisconnect, OnReconnect, OnUnauthorized);
		_forceTrustSelfSignedCertificate = forceTrustSelfSignedCertificate;
	}

	~Edk()
	{
		_streamLock.Dispose();
	}

	public Task ConnectAsync(bool newBridge = false, bool removePersistentData = false)
	{
		if (IsBusy())
		{
			Logger.Instance.Log("[Edk.ConnectAsync] busy " + _busy);
			return Task.CompletedTask;
		}
		_cancelTokenSource = new CancellationTokenSource();
		return Task.Run(delegate
		{
			try
			{
				Connect(newBridge, _cancelTokenSource.Token, removePersistentData).Wait(_cancelTokenSource.Token);
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[Edk.ConnectAsync] expection: " + ex);
				FinishConnection(result: false, null, ConnectionFailedReason.Cancelled).Wait();
			}
		}, _cancelTokenSource.Token);
	}

	public Task ConnectWithIpAsync(string ip)
	{
		if (IsBusy())
		{
			Logger.Instance.Log("[Edk.ConnectWithIpAsync] busy " + _busy);
			return Task.CompletedTask;
		}
		_cancelTokenSource = new CancellationTokenSource();
		return Task.Run(delegate
		{
			try
			{
				ConnectWithIp(ip, _cancelTokenSource.Token).Wait(_cancelTokenSource.Token);
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[Edk.ConnectAsync] expection: " + ex);
				FinishConnection(result: false, null, ConnectionFailedReason.Cancelled).Wait();
			}
		});
	}

	public void CancelConnection()
	{
		_cancelTokenSource?.Cancel();
	}

	public static async Task Init(string appName, string deviceName, string appDataPath, bool resetAllData = false, bool forceTrustSelfSignedCertificate = false)
	{
		_instance = new Edk(appName, deviceName, appDataPath, forceTrustSelfSignedCertificate);
		if (resetAllData)
		{
			_instance.DeleteKnownBridges().Wait();
		}
		Edk instance = _instance;
		instance.persistentData = await _instance.dataStore.Load<PersistentData>();
		Bridge bridge = _instance.persistentData.GetActiveBridge();
		if (bridge != null)
		{
			bridge.Connected = false;
		}
	}

	public static void Reset()
	{
		if (_instance != null)
		{
			_instance._connectionMonitor.Stop();
			_instance._connectionMonitor.Reset();
			_instance._connectionMonitor = null;
			_instance.persistentData = null;
			HueStreamClient obj = _instance.hueStreamClient;
			if (obj != null)
			{
				obj.Close();
			}
			_instance.hueStreamClient = null;
			_instance._cancelTokenSource = null;
			ClipV2Client clipv2Client = _instance._clipv2Client;
			if (clipv2Client != null)
			{
				clipv2Client.Dispose();
			}
			_instance = null;
		}
	}

	public async Task Connect(bool newBridge = false, CancellationToken? cancelToken = null, bool removePersistentData = false)
	{
		if (!StartConnection())
		{
			return;
		}
		ResourceManager.GetInstance().Reset();
		if (newBridge && removePersistentData)
		{
			persistentData.RemoveAll();
		}
		bool bridgesAreAvailable = persistentData.Bridges.Count != 0;
		if (newBridge || !bridgesAreAvailable)
		{
			bridgesAreAvailable = await Discover(newBridge, cancelToken);
			cancelToken?.ThrowIfCancellationRequested();
		}
		Bridge bridge = persistentData.GetActiveBridge();
		if (bridge == null || bridge.Ip.Length == 0)
		{
			await FinishConnection(result: false, bridge, bridgesAreAvailable ? ConnectionFailedReason.AuthenticationFailed : ConnectionFailedReason.BridgeNotFound);
			return;
		}
		Tuple<bool, bool> result = await ConnectAsyncInternal();
		if (!result.Item1 && result.Item2)
		{
			bridgesAreAvailable = await Discover(newBridge: false, cancelToken);
			cancelToken?.ThrowIfCancellationRequested();
			bridge = persistentData.GetActiveBridge();
			if (!bridgesAreAvailable || bridge == null || bridge.Ip.Length == 0)
			{
				await FinishConnection(result: false, bridge, bridgesAreAvailable ? ConnectionFailedReason.AuthenticationFailed : ConnectionFailedReason.BridgeNotFound);
				return;
			}
			result = await ConnectAsyncInternal();
		}
		await FinishConnection(result.Item1, bridge);
	}

	public async Task ConnectWithIp(string ip, CancellationToken? cancelToken = null)
	{
		if (!StartConnection())
		{
			return;
		}
		Task<BridgeResult> configResult = Module.ServiceProvider.GetService<IConfigRetriever>().Retrieve(ip, (string)null);
		configResult.Wait();
		cancelToken?.ThrowIfCancellationRequested();
		if (configResult.Result == null)
		{
			await FinishConnection(result: false, null, ConnectionFailedReason.BridgeNotFound);
			return;
		}
		await Authenticate(new List<BridgeResult> { configResult.Result }, save: false, cancelToken);
		cancelToken?.ThrowIfCancellationRequested();
		Bridge bridge = persistentData.GetActiveBridge();
		if (bridge == null || bridge.Ip.Length == 0)
		{
			await FinishConnection(result: false, bridge, ConnectionFailedReason.AuthenticationFailed);
		}
		else
		{
			await FinishConnection((await ConnectAsyncInternal(cancelToken)).Item1, bridge);
		}
	}

	public List<Bridge> GetKnownBridges()
	{
		return persistentData.Bridges;
	}

	public async Task DeleteKnownBridges()
	{
		if (IsBusy())
		{
			Logger.Instance.Log("[Edk.DeleteKnownBridges] busy " + _busy);
			return;
		}
		ResourceManager.GetInstance().Reset();
		await StopStreamAsync();
		dataStore.Delete();
		await dataStore.Save(new PersistentData());
		persistentData?.RemoveAll();
	}

	private async Task<Tuple<bool, bool>> ConnectAsyncInternal(CancellationToken? cancelToken = null)
	{
		Bridge bridge = persistentData.GetActiveBridge();
		try
		{
			ClipV2Client clipv2Client = _clipv2Client;
			if (clipv2Client != null)
			{
				clipv2Client.Dispose();
			}
			_clipv2Client = CreateClipv2Client(bridge, _forceTrustSelfSignedCertificate);
			if (!bridge.IsValidApiVersion() || !bridge.IsValidModel())
			{
				return new Tuple<bool, bool>(item1: false, item2: false);
			}
			return new Tuple<bool, bool>(await ResourceManager.GetInstance().Init(_clipv2Client, OnResourceEvent, bridge, cancelToken), item2: true);
		}
		catch (Exception ex)
		{
			Logger.Instance.Log("[Edk.ConnectAsyncInternal] exception: " + ex, Logger.LogLevel.ERROR);
			return new Tuple<bool, bool>(item1: false, item2: true);
		}
	}

	public static ClipV2Client CreateClipv2Client(Bridge bridge, bool forceTrustSelfSignedCertificate = false)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		Settings val = default(Settings);
		((Settings)(ref val)).Params = new Dictionary<string, string> { { "bridge", bridge.Ip } };
		((Settings)(ref val)).Headers = new Dictionary<string, string> { { "hue-application-key", bridge.UserName } };
		((Settings)(ref val)).CN = bridge.Id;
		((Settings)(ref val)).Certificate = bridge.Certificate;
		((Settings)(ref val)).ForceTrustSelfSignedCertificate = forceTrustSelfSignedCertificate;
		return new ClipV2Client(val);
	}

	private bool StartConnection()
	{
		StopStream();
		if (Interlocked.CompareExchange(ref _busy, 1, 0) == 1)
		{
			return false;
		}
		this.BusyChanged?.Invoke(busy: true);
		Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.ConnectingStart));
		_connectionMonitor.Stop();
		return true;
	}

	private async Task FinishConnection(bool result, Bridge bridge, ConnectionFailedReason? reason = null)
	{
		if (Interlocked.CompareExchange(ref _busy, 2, 1) != 0)
		{
			if (result && bridge != null)
			{
				ResourceManager.GetInstance().FillBridgeProperties(bridge);
				await dataStore.Save(persistentData);
				_connectionMonitor.Start(bridge, 15000);
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.ReadyToStream));
			}
			else if (reason.HasValue && reason != ConnectionFailedReason.None)
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.ConnectingFailed, null, new Dictionary<string, object> { { "reason", reason } }));
			}
			else
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.ConnectingFailed, null, new Dictionary<string, object> { 
				{
					"reason",
					(bridge == null) ? ConnectionFailedReason.InternalError : ((!bridge.Authorized) ? ConnectionFailedReason.AuthenticationFailed : ((!bridge.IsValidModel()) ? ConnectionFailedReason.BridgeInvalidModel : ((!bridge.IsValidApiVersion()) ? ConnectionFailedReason.BridgeInvalidSwVersion : ConnectionFailedReason.InternalError)))
				} }));
			}
			_busy = 0;
			this.BusyChanged?.Invoke(busy: false);
		}
	}

	public bool IsBusy()
	{
		return !object.Equals(_busy, 0);
	}

	private bool IsConnecting()
	{
		return object.Equals(_busy, 1);
	}

	public bool SelectEntertainmentConfiguration(string entertainmentConfigurationId)
	{
		if (persistentData == null || !persistentData.HasActiveBridge())
		{
			return false;
		}
		if (Interlocked.CompareExchange(ref _busy, 3, 0) != 0)
		{
			Logger.Instance.Log("[Edk.SelectEntertainmentConfiguration] busy " + _busy);
			return false;
		}
		this.BusyChanged?.Invoke(busy: true);
		bool wasStreaming = _streaming;
		if (wasStreaming)
		{
			_streamLock.WaitOne();
		}
		using (new ScopeExit(delegate
		{
			if (wasStreaming)
			{
				_streamLock.ReleaseMutex();
			}
			_busy = 0;
			this.BusyChanged?.Invoke(busy: false);
		}))
		{
			Bridge activeBridge = persistentData.GetActiveBridge();
			string activeEntertainmentConfigurationId = activeBridge.ActiveEntertainmentConfigurationId;
			bool flag = persistentData.GetActiveBridge().SetActiveEntertainmentConfiguration(entertainmentConfigurationId);
			dataStore.Save(persistentData).Wait();
			if (flag && wasStreaming)
			{
				if (!StopStreamInternal(broadcastUIEvent: false, activeEntertainmentConfigurationId))
				{
					Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingDisconnectingFailed));
					return false;
				}
				if (!StartStreamInternal(broadcastUIEvent: false))
				{
					Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingDisconnected));
					return false;
				}
				return true;
			}
			return flag;
		}
	}

	public Task StartStreamAsync(ActivationOverrideLevel overrideLevel = ActivationOverrideLevel.SameGroup)
	{
		return Task.Run(delegate
		{
			StartStream(overrideLevel);
		});
	}

	public void StartStream(ActivationOverrideLevel overrideLevel = ActivationOverrideLevel.SameGroup)
	{
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Expected O, but got Unknown
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		if (Interlocked.CompareExchange(ref _busy, 3, 0) != 0)
		{
			Logger.Instance.Log("[Edk.StartStream] busy " + _busy);
			return;
		}
		this.BusyChanged?.Invoke(busy: true);
		using (new ScopeExit(delegate
		{
			_busy = 0;
			this.BusyChanged?.Invoke(busy: false);
		}))
		{
			Bridge activeBridge = persistentData.GetActiveBridge();
			if (activeBridge == null || !activeBridge.Connected)
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnectingFailed, null, new Dictionary<string, object> { 
				{
					"reason",
					StreamingConnectingFailedReason.BridgeNotConnected
				} }));
				return;
			}
			if (!activeBridge.IsValidEntertainmentConfigurationSelected())
			{
				List<EntertainmentConfigurationGet> entertainmentConfigurations = ResourceManager.GetInstance().EntertainmentConfigurations;
				if (entertainmentConfigurations.Count == 0)
				{
					Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnectingFailed, null, new Dictionary<string, object> { 
					{
						"reason",
						StreamingConnectingFailedReason.NoEntertainmentConfigurationAvailable
					} }));
					Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.CreateEntertainmentConfiguration));
				}
				else
				{
					Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnectingFailed, null, new Dictionary<string, object> { 
					{
						"reason",
						StreamingConnectingFailedReason.NoEntertainmentConfigurationSelected
					} }));
					Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.SelectEntertainmentConfiguration));
				}
				return;
			}
			EntertainmentConfigurationGet activeEntertainmentConfiguration = activeBridge.GetActiveEntertainmentConfiguration();
			EntertainmentConfigurationPut val = new EntertainmentConfigurationPut
			{
				Action = (EntertainmentConfigurationAction)0,
				Metadata = null,
				ConfigurationType = activeEntertainmentConfiguration.ConfigurationType,
				StreamProxy = null,
				Locations = null
			};
			bool flag = overrideLevel != ActivationOverrideLevel.Never;
			if ((int)activeEntertainmentConfiguration.Status == 0 && activeEntertainmentConfiguration.ActiveStreamer != null && GUID.op_Implicit(activeEntertainmentConfiguration.ActiveStreamer.Rid) != activeBridge.AppId && flag && (overrideLevel == ActivationOverrideLevel.Always || overrideLevel == ActivationOverrideLevel.SameGroup))
			{
				val.Action = (EntertainmentConfigurationAction)1;
				HttpResponse<ClipMessageResourceIdentifierPut> httpResponse = new ClientResourceEntertainmentConfigurationById(_clipv2Client).Put(((ResourceGet)activeEntertainmentConfiguration).Id, val);
				if (httpResponse.StatusCode != HttpStatusCode.OK)
				{
					Logger.Instance.Log("[Edk.StartStream] Failed to stop streaming on group " + ResourceName.op_Implicit(activeEntertainmentConfiguration.Name) + ", status code: " + httpResponse.StatusCode, Logger.LogLevel.ERROR);
				}
				else
				{
					WaitEntertainmentConfigurationStatus(((ResourceGet)activeEntertainmentConfiguration).Id, (EntertainmentConfigurationStatus)1);
				}
			}
			List<EntertainmentConfigurationGet> entertainmentConfigurationsOwnByOtherClient = activeBridge.GetEntertainmentConfigurationsOwnByOtherClient();
			if (entertainmentConfigurationsOwnByOtherClient.Count == activeBridge.MaxStream && flag && overrideLevel == ActivationOverrideLevel.Always && entertainmentConfigurationsOwnByOtherClient.Count != 0)
			{
				EntertainmentConfigurationGet val2 = entertainmentConfigurationsOwnByOtherClient[0];
				val.Action = (EntertainmentConfigurationAction)1;
				HttpResponse<ClipMessageResourceIdentifierPut> httpResponse2 = new ClientResourceEntertainmentConfigurationById(_clipv2Client).Put(((ResourceGet)val2).Id, val);
				if (httpResponse2.StatusCode != HttpStatusCode.OK)
				{
					Logger.Instance.Log("[Edk.StartStream] Failed to stop streaming on group " + ResourceName.op_Implicit(val2.Name) + ", status code: " + httpResponse2.StatusCode, Logger.LogLevel.ERROR);
				}
				else
				{
					WaitEntertainmentConfigurationStatus(((ResourceGet)val2).Id, (EntertainmentConfigurationStatus)1);
				}
			}
			if (StartStreamInternal())
			{
				_connectionMonitor.CheckInterval = 5000;
				_connectionMonitor.FirstChanceError = false;
				_streaming = true;
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnected));
			}
		}
	}

	private bool StartStreamInternal(bool broadcastUIEvent = true)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Expected O, but got Unknown
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		Bridge activeBridge = persistentData.GetActiveBridge();
		EntertainmentConfigurationGet activeEntertainmentConfiguration = activeBridge.GetActiveEntertainmentConfiguration();
		EntertainmentConfigurationPut val = new EntertainmentConfigurationPut
		{
			Action = (EntertainmentConfigurationAction)0,
			Metadata = null,
			ConfigurationType = activeEntertainmentConfiguration.ConfigurationType,
			StreamProxy = null,
			Locations = null
		};
		List<EntertainmentConfigurationGet> entertainmentConfigurationsOwnBySelf = activeBridge.GetEntertainmentConfigurationsOwnBySelf();
		if (entertainmentConfigurationsOwnBySelf.Count > 0)
		{
			Logger.Instance.Log("[Edk.StartStreamInternal] Old streaming session still active!");
			val.Action = (EntertainmentConfigurationAction)1;
			foreach (EntertainmentConfigurationGet item in entertainmentConfigurationsOwnBySelf)
			{
				HttpResponse<ClipMessageResourceIdentifierPut> httpResponse = new ClientResourceEntertainmentConfigurationById(_clipv2Client).Put(((ResourceGet)item).Id, val);
				if (httpResponse.StatusCode != HttpStatusCode.OK)
				{
					Logger.Instance.Log("[Edk.StartStreamInternal] Failed to stop previous streaming session, status code: " + httpResponse.StatusCode, Logger.LogLevel.ERROR);
					if (broadcastUIEvent)
					{
						Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnectingFailed, null, new Dictionary<string, object>
						{
							{
								"reason",
								StreamingConnectingFailedReason.HttpError
							},
							{ "error_code", httpResponse.StatusCode }
						}));
					}
					return false;
				}
				WaitEntertainmentConfigurationStatus(((ResourceGet)item).Id, (EntertainmentConfigurationStatus)1);
			}
		}
		Logger.Instance.Log($"[Edk.StartStreamInternal] eaType: {activeEntertainmentConfiguration.ConfigurationType} eaStatus: {activeEntertainmentConfiguration.Status} eaId: {((ResourceGet)activeEntertainmentConfiguration).Id} bridgeEAId: {activeBridge.ActiveEntertainmentConfigurationId} bridgeIp: {activeBridge.Ip} bridgeAppId: {activeBridge.AppId}");
		IPAddress.TryParse(activeBridge.Ip, out IPAddress address);
		hueStreamClient = new HueStreamClient(activeBridge.AppId, activeBridge.ClientKey, address, 2100, true);
		val.Action = (EntertainmentConfigurationAction)0;
		HttpResponse<ClipMessageResourceIdentifierPut> httpResponse2 = new ClientResourceEntertainmentConfigurationById(_clipv2Client).Put(GUID.op_Implicit(activeBridge.ActiveEntertainmentConfigurationId), val);
		if (httpResponse2.StatusCode != HttpStatusCode.OK)
		{
			Logger.Instance.Log("[Edk.StartStreamInternal] Failed to start streaming, status code: " + httpResponse2.StatusCode, Logger.LogLevel.ERROR);
			if (broadcastUIEvent)
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnectingFailed, null, new Dictionary<string, object>
				{
					{
						"reason",
						StreamingConnectingFailedReason.HttpError
					},
					{ "error_code", httpResponse2.StatusCode }
				}));
			}
			return false;
		}
		WaitEntertainmentConfigurationStatus(((ResourceGet)activeEntertainmentConfiguration).Id, (EntertainmentConfigurationStatus)0);
		try
		{
			if (!hueStreamClient.Connect(new TimeSpan(0, 0, 10)))
			{
				Logger.Instance.Log("[Edk.StartStreamInternal] Timeout connecting to huestream client!", Logger.LogLevel.ERROR);
				HandleStartStreamFailure(broadcastUIEvent, StreamingConnectingFailedReason.Timeout, null);
				return false;
			}
		}
		catch (Exception ex)
		{
			Logger.Instance.Log("[Edk.StartStreamInternal] dtls connect failed with exception: " + ex, Logger.LogLevel.ERROR);
			HandleStartStreamFailure(broadcastUIEvent, StreamingConnectingFailedReason.Exception, ex.Message);
			return false;
		}
		return true;
	}

	private void HandleStartStreamFailure(bool broadcastUIEvent, StreamingConnectingFailedReason failedReason, object error_code)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		Bridge activeBridge = persistentData.GetActiveBridge();
		EntertainmentConfigurationGet activeEntertainmentConfiguration = activeBridge.GetActiveEntertainmentConfiguration();
		EntertainmentConfigurationPut val = new EntertainmentConfigurationPut
		{
			Action = (EntertainmentConfigurationAction)1,
			Metadata = null,
			ConfigurationType = activeEntertainmentConfiguration.ConfigurationType,
			StreamProxy = null,
			Locations = null
		};
		HttpResponse<ClipMessageResourceIdentifierPut> httpResponse = new ClientResourceEntertainmentConfigurationById(_clipv2Client).Put(GUID.op_Implicit(activeBridge.ActiveEntertainmentConfigurationId), val);
		if (httpResponse.StatusCode != HttpStatusCode.OK)
		{
			Logger.Instance.Log("[Edk.HandleStartStreamFailure] Failed to stop streaming, status code: " + httpResponse.StatusCode, Logger.LogLevel.ERROR);
		}
		if (broadcastUIEvent)
		{
			Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingConnectingFailed, null, new Dictionary<string, object>
			{
				{ "reason", failedReason },
				{ "error_code", error_code }
			}));
		}
	}

	public bool SendFrame<T>(T frame)
	{
		if (_streamingIsBroken)
		{
			return false;
		}
		_streamLock.WaitOne();
		using (new ScopeExit(delegate
		{
			_streamLock.ReleaseMutex();
		}))
		{
			Bridge activeBridge = persistentData.GetActiveBridge();
			if (!_streamingIsBroken && activeBridge.IsStreaming())
			{
				EntertainmentConfigurationGet activeEntertainmentConfiguration = activeBridge.GetActiveEntertainmentConfiguration();
				try
				{
					_lastSendFrameTime = _currentSendFrameTime;
					_currentSendFrameTime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
					if (_lastSendFrameTime != TimeSpan.Zero && (_currentSendFrameTime - _lastSendFrameTime).TotalSeconds >= 8.0)
					{
						Logger.Instance.Log($"[Edk.SendFrame] Warning bridge was starved for {(_currentSendFrameTime - _lastSendFrameTime).TotalSeconds} seconds, streaming might get disconnected.");
					}
					dynamic val = frame;
					if (frame.GetType() == typeof(Dictionary<Area, ColorRgb>))
					{
						hueStreamClient.SendChannelData(ChannelMapper.MapChannelColorsToLights(val, activeEntertainmentConfiguration.Channels), ((ResourceGet)activeEntertainmentConfiguration).Id);
					}
					else if (frame.GetType() == typeof(Dictionary<int, ColorRgb>))
					{
						hueStreamClient.SendChannelData(ChannelMapper.MapChannelColorsToLights(val), ((ResourceGet)activeEntertainmentConfiguration).Id);
					}
				}
				catch (Exception ex)
				{
					Logger.Instance.Log("[Edk.SendFrame] Unable to send frame: " + ex);
					_streamingIsBroken = true;
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public Task StopStreamAsync()
	{
		return Task.Run(delegate
		{
			StopStream();
		});
	}

	public void StopStream(HttpStatusCode? httpStatusCode = null)
	{
		if (!_streaming)
		{
			return;
		}
		if (Interlocked.CompareExchange(ref _busy, 3, 0) != 0)
		{
			Logger.Instance.Log("[Edk.StopStream] busy " + _busy);
			return;
		}
		this.BusyChanged?.Invoke(busy: true);
		_streamLock.WaitOne();
		Bridge activeBridge = persistentData.GetActiveBridge();
		using (new ScopeExit(delegate
		{
			_streamLock.ReleaseMutex();
			_busy = 0;
			this.BusyChanged?.Invoke(busy: false);
		}))
		{
			if (StopStreamInternal())
			{
				StopStreamFinalize(httpStatusCode);
			}
		}
	}

	private bool StopStreamInternal(bool broadcastUIEvent = true, string entertainmentConfigurationId = null)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		HueStreamClient obj = hueStreamClient;
		if (obj != null)
		{
			obj.Close();
		}
		Bridge activeBridge = persistentData.GetActiveBridge();
		if (!activeBridge.Connected)
		{
			return true;
		}
		EntertainmentConfigurationPut val = new EntertainmentConfigurationPut
		{
			Action = (EntertainmentConfigurationAction)1,
			Metadata = null,
			ConfigurationType = activeBridge.GetActiveEntertainmentConfiguration().ConfigurationType,
			StreamProxy = null,
			Locations = null
		};
		HttpResponse<ClipMessageResourceIdentifierPut> httpResponse = new ClientResourceEntertainmentConfigurationById(_clipv2Client).Put(GUID.op_Implicit((entertainmentConfigurationId != null) ? entertainmentConfigurationId : activeBridge.ActiveEntertainmentConfigurationId), val);
		if (httpResponse.StatusCode != HttpStatusCode.OK)
		{
			Logger.Instance.Log("[Edk.StopStreamInternal] Failed to stop streaming, status code: " + httpResponse.StatusCode, Logger.LogLevel.ERROR);
			if (broadcastUIEvent)
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingDisconnectingFailed));
			}
			return false;
		}
		WaitEntertainmentConfigurationStatus(GUID.op_Implicit((entertainmentConfigurationId != null) ? entertainmentConfigurationId : activeBridge.ActiveEntertainmentConfigurationId), (EntertainmentConfigurationStatus)1);
		return true;
	}

	public Bridge GetActiveBridge()
	{
		if (persistentData == null)
		{
			return null;
		}
		return persistentData.GetActiveBridge();
	}

	private async Task<bool> Discover(bool newBridge = false, CancellationToken? cancelToken = null)
	{
		cancelToken?.ThrowIfCancellationRequested();
		Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.Searching));
		IBridgeDiscover discoverer = Module.ServiceProvider.GetService<IBridgeDiscover>();
		List<BridgeResult> bridges = await discoverer.Discover();
		cancelToken?.ThrowIfCancellationRequested();
		bool bridgesAvailable = bridges.Count != 0;
		if (!bridgesAvailable)
		{
			Logger.Instance.Log("Edk.Discover] No bridge found!", Logger.LogLevel.WARNING);
		}
		if (newBridge)
		{
			persistentData.RemoveKnownBridges(bridges);
			if (bridges.Count != 0)
			{
				await Authenticate(bridges, save: false, cancelToken);
			}
			else
			{
				Logger.Instance.Log("[Edk.Discover] No new bridge found!");
			}
		}
		else
		{
			Bridge activeBridge = persistentData.GetActiveBridge();
			bool activeBridgeWasFactoryReset = persistentData.ActiveBridgeWasFactoryReset(bridges);
			if (activeBridge == null || (!activeBridge.Authorized && !activeBridgeWasFactoryReset))
			{
				await Authenticate(bridges, save: false, cancelToken);
			}
			else
			{
				bool rediscoverKnownBridge = true;
				if (activeBridge.Model.ToLower() == "bsb002")
				{
					bool activeBridgeWasRediscovered = persistentData.ActiveBridgeWasRediscovered(bridges);
					if (activeBridgeWasFactoryReset || !activeBridgeWasRediscovered)
					{
						List<Bridge> potentialMigratedV3Bridge = persistentData.DiscoverPotentialMigratedV3Bridge(bridges);
						if (potentialMigratedV3Bridge != null && potentialMigratedV3Bridge.Count > 0)
						{
							Task<Bridge> migrated = authenticator.MigrateToBridgeV3(activeBridge, potentialMigratedV3Bridge);
							if (migrated.Result != null)
							{
								persistentData.SetActiveBridge(migrated.Result);
								rediscoverKnownBridge = false;
							}
						}
					}
				}
				if (rediscoverKnownBridge && !persistentData.RediscoverKnownBridge(bridges))
				{
					return false;
				}
			}
		}
		cancelToken?.ThrowIfCancellationRequested();
		await dataStore.Save(persistentData);
		return bridgesAvailable;
	}

	private async Task Authenticate(List<BridgeResult> bridges, bool save, CancellationToken? cancelToken = null)
	{
		if (bridges.Count != 0)
		{
			Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.PressPushlink));
			Bridge bridge = await authenticator.Authenticate(bridges, cancelToken, _forceTrustSelfSignedCertificate);
			cancelToken?.ThrowIfCancellationRequested();
			if (!persistentData.SetActiveBridge(bridge))
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.PushLinkTimeout));
			}
			else
			{
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.ConnectingAuthorized));
			}
			if (save)
			{
				await dataStore.Save(persistentData);
			}
		}
	}

	private void OnDisconnect(HttpStatusCode httpStatusCode)
	{
		Logger.Instance.Log($"[Edk.OnDisconnect] {httpStatusCode}");
		_wasStreaming = _streaming && httpStatusCode != HttpStatusCode.Gone;
		_disconnectedTime.Restart();
		if (_streaming)
		{
			Logger.Instance.Log("[Edk.OnDisconnect] stopping stream");
			if (httpStatusCode != HttpStatusCode.Gone)
			{
				persistentData.GetActiveBridge().Connected = false;
			}
			_streamStopTaskOnDisconnect = StopStreamAsync();
			_streamStopTaskOnDisconnect.ContinueWith(delegate
			{
				persistentData.GetActiveBridge().Connected = false;
				Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.Disconnected, null, new Dictionary<string, object> { { "http_status", httpStatusCode } }));
			});
		}
		else
		{
			persistentData.GetActiveBridge().Connected = false;
			Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.Disconnected, null, new Dictionary<string, object> { { "http_status", httpStatusCode } }));
		}
	}

	private async void OnUnauthorized()
	{
		Logger.Instance.Log("[Edk.OnUnauthorized]");
		Bridge bridge = persistentData.GetActiveBridge();
		if (bridge.IsStreaming())
		{
			StopStream();
		}
		_connectionMonitor.Stop();
		await ConnectAsync();
	}

	private async void OnReconnect()
	{
		_connectionMonitor.Stop();
		long reconnectDelay = _disconnectedTime.ElapsedMilliseconds;
		_disconnectedTime.Stop();
		await ConnectAsync();
		if (_wasStreaming && GetActiveBridge().Connected && reconnectDelay <= 30000)
		{
			Logger.Instance.Log("[Edk.OnReconnect] resume stream");
			await StartStreamAsync();
		}
	}

	private void StopStreamFinalize(HttpStatusCode? httpStatusCode = null)
	{
		_connectionMonitor.CheckInterval = 15000;
		_connectionMonitor.FirstChanceError = false;
		_streaming = false;
		_streamingIsBroken = false;
		Dictionary<string, object> eventParams = (httpStatusCode.HasValue ? new Dictionary<string, object> { { "disconnect_http_status", httpStatusCode.Value } } : new Dictionary<string, object> { { "disconnect_http_status", "None" } });
		Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, new UIEventArgs(UIState.StreamingDisconnected, null, eventParams));
	}

	private void OnResourceEvent(UIEventArgs e)
	{
		if (IsConnecting())
		{
			Logger.Instance.Log($"[Edk.OnResourceEvent] receiving an event {e.EventType} while connecting to a bridge, ignoring it.");
			return;
		}
		if (e.EventType == UIState.StreamingDisconnected)
		{
			_streamLock.WaitOne();
			if (_streaming)
			{
				Logger.Instance.Log($"[Edk.OnResourceEvent] streaming disconnected, current frame sent at: {_currentSendFrameTime} last frame sent at: {_lastSendFrameTime}");
				StopStreamFinalize();
			}
			_streamLock.ReleaseMutex();
			return;
		}
		if (e.EventType == UIState.EntertainmentConfigurationDeleted && persistentData.GetActiveBridge().ActiveEntertainmentConfigurationId.Length == 0 && _streaming)
		{
			_streamLock.WaitOne();
			using (new ScopeExit(delegate
			{
				_streamLock.ReleaseMutex();
			}))
			{
				try
				{
					HueStreamClient obj = hueStreamClient;
					if (obj != null)
					{
						obj.Close();
					}
					StopStreamFinalize();
				}
				catch (Exception ex)
				{
					Logger.Instance.Log(ex?.ToString() ?? "");
				}
			}
		}
		Module.ServiceProvider.GetService<IEventDispatcher>().Dispatch(this, e);
	}

	private unsafe void WaitEntertainmentConfigurationStatus(GUID eaId, EntertainmentConfigurationStatus status)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		Logger.Instance.Log("[Edk.WaitEntertainmentConfigurationStatus] start for " + GUID.op_Implicit(eaId));
		Bridge activeBridge = persistentData.GetActiveBridge();
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		EntertainmentConfigurationGet entertainmentConfigurationById = activeBridge.GetEntertainmentConfigurationById(GUID.op_Implicit(eaId));
		while (entertainmentConfigurationById != null && entertainmentConfigurationById.Status != status && stopwatch.ElapsedMilliseconds < 1500)
		{
			Thread.Sleep(25);
			entertainmentConfigurationById = activeBridge.GetEntertainmentConfigurationById(GUID.op_Implicit(eaId));
		}
		Logger.Instance.Log("[Edk.WaitEntertainmentConfigurationStatus] waited " + stopwatch.ElapsedMilliseconds + "ms for status " + ((object)(*(EntertainmentConfigurationStatus*)(&status))/*cast due to .constrained prefix*/).ToString());
	}
}
public class EventDispatcher : IEventDispatcher
{
	private ConcurrentQueue<Tuple<object, UIEventArgs>> _uiEventQueue = new ConcurrentQueue<Tuple<object, UIEventArgs>>();

	private Thread _thread = null;

	private readonly AutoResetEvent signalEvent = new AutoResetEvent(initialState: false);

	public event EventHandler<UIEventArgs> UIEventHandler;

	public EventDispatcher()
	{
		_thread = new Thread(OnDispatch);
	}

	public void Dispatch(object sender, UIEventArgs uiEvent)
	{
		_uiEventQueue.Enqueue(new Tuple<object, UIEventArgs>(sender, uiEvent));
		if (!_thread.IsAlive)
		{
			_thread.IsBackground = true;
			_thread.Start();
		}
		else
		{
			signalEvent.Set();
		}
	}

	private void OnDispatch()
	{
		while (true)
		{
			bool flag = true;
			while (!_uiEventQueue.IsEmpty)
			{
				if (_uiEventQueue.TryDequeue(out var result))
				{
					this.UIEventHandler?.Invoke(result.Item1, result.Item2);
				}
			}
			signalEvent.WaitOne();
		}
	}
}
public interface IEventDispatcher
{
	event EventHandler<UIEventArgs> UIEventHandler;

	void Dispatch(object sender, UIEventArgs uiEvent);
}
public class Location
{
	public double X { get; set; }

	public double Y { get; set; }

	public double Z { get; set; }

	public Location()
	{
	}

	public Location(double x, double y, double z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
public class Module
{
	public static IServiceProvider ServiceProvider { get; set; }

	public static void RegisterServices(IServiceCollection collection)
	{
		collection.AddSingleton<IEventDispatcher, EventDispatcher>();
	}
}
[Serializable]
internal class PersistentData
{
	public List<Bridge> Bridges { get; set; }

	public PersistentData()
	{
		Bridges = new List<Bridge>();
	}

	public PersistentData(List<Bridge> bridges)
	{
		Bridges = bridges;
	}

	public Bridge GetActiveBridge()
	{
		return Bridges.FirstOrDefault();
	}

	public bool SetActiveBridge(Bridge bridge)
	{
		if (!bridge.IsAuthenticated())
		{
			return false;
		}
		List<Bridge> list = new List<Bridge>();
		list.Add(bridge);
		copyExcept(bridge, Bridges, list);
		Bridges = list;
		return true;
	}

	public bool HasActiveBridge()
	{
		return Bridges.Count > 0;
	}

	public bool RemoveBridge(Bridge bridge)
	{
		List<Bridge> list = new List<Bridge>();
		bool result = copyExcept(bridge, Bridges, list);
		Bridges = list;
		return result;
	}

	public void RemoveAll()
	{
		Bridges = new List<Bridge>();
	}

	public bool RediscoverKnownBridge(List<BridgeResult> bridgeResults)
	{
		foreach (Bridge bridge in Bridges)
		{
			foreach (BridgeResult bridgeResult in bridgeResults)
			{
				if (bridgeResult.Id == bridge.Id)
				{
					bridge.Ip = bridgeResult.Ip;
					SetActiveBridge(bridge);
					return true;
				}
			}
		}
		return false;
	}

	public bool ActiveBridgeWasRediscovered(List<BridgeResult> bridgeResults)
	{
		Bridge activeBridge = GetActiveBridge();
		if (activeBridge == null)
		{
			return false;
		}
		foreach (BridgeResult bridgeResult in bridgeResults)
		{
			if (bridgeResult.Id == activeBridge.Id)
			{
				return true;
			}
		}
		return false;
	}

	public bool ActiveBridgeWasFactoryReset(List<BridgeResult> bridgeResults)
	{
		Bridge activeBridge = GetActiveBridge();
		if (activeBridge == null)
		{
			return false;
		}
		foreach (BridgeResult bridgeResult in bridgeResults)
		{
			if (bridgeResult.Id == activeBridge.Id)
			{
				return bridgeResult.FactoryNew;
			}
		}
		return false;
	}

	public List<Bridge> DiscoverPotentialMigratedV3Bridge(List<BridgeResult> bridgeResults)
	{
		List<Bridge> list = new List<Bridge>();
		if (HasActiveBridge())
		{
			foreach (BridgeResult bridgeResult in bridgeResults)
			{
				if (bridgeResult.Model.ToLower() == "bsb003" || bridgeResult.Model.ToLower() == "bsb002_beta")
				{
					list.Add(new Bridge(bridgeResult));
				}
			}
		}
		return list;
	}

	public void RemoveKnownBridges(List<BridgeResult> bridgeResults)
	{
		foreach (Bridge bridge in Bridges)
		{
			foreach (BridgeResult bridgeResult in bridgeResults)
			{
				if (bridgeResult.Id == bridge.Id)
				{
					bridgeResults.Remove(bridgeResult);
					break;
				}
			}
		}
	}

	private bool copyExcept(Bridge bridge, List<Bridge> from, List<Bridge> to)
	{
		bool result = false;
		foreach (Bridge item in from)
		{
			if (item.Id == bridge.Id)
			{
				result = true;
			}
			else
			{
				to.Add(item);
			}
		}
		return result;
	}
}
internal class ResourceManager
{
	private static ResourceManager _instance;

	private ClipV2Client _clipClient = null;

	private JsonSerializerSettings _jsonSettings = null;

	private Action<UIEventArgs> _eventHandler = null;

	private Mutex _mutex = new Mutex();

	private bool _refreshECCache = false;

	private ClipMessageEntertainmentConfigurationGet _clipEntertainmentConfigurations = null;

	private List<EntertainmentConfigurationGet> _entertainmentConfigurationCache = null;

	private Bridge _bridge = null;

	private HttpStatusCode _lastHttpStatusCode = HttpStatusCode.OK;

	public HttpStatusCode LastHttpStatusCode => _lastHttpStatusCode;

	public List<EntertainmentConfigurationGet> EntertainmentConfigurations
	{
		get
		{
			if (_clipEntertainmentConfigurations == null)
			{
				return null;
			}
			if (_refreshECCache || _entertainmentConfigurationCache == null)
			{
				_mutex.WaitOne();
				string text = JsonConvert.SerializeObject((object)_clipEntertainmentConfigurations.Data);
				_refreshECCache = false;
				_mutex.ReleaseMutex();
				try
				{
					_entertainmentConfigurationCache = JsonConvert.DeserializeObject<List<EntertainmentConfigurationGet>>(text);
				}
				catch (Exception ex)
				{
					Logger.Instance.Log("[ResourceManager.EntertainmentConfiguration] exception: " + ex, Logger.LogLevel.ERROR);
				}
			}
			return _entertainmentConfigurationCache;
		}
	}

	~ResourceManager()
	{
		_mutex.Dispose();
	}

	public static ResourceManager GetInstance()
	{
		if (_instance == null)
		{
			_instance = new ResourceManager();
		}
		return _instance;
	}

	private ResourceManager()
	{
	}

	public async Task<bool> Init(ClipV2Client clipClient, Action<UIEventArgs> eventHandler, Bridge bridge, CancellationToken? cancelToken = null)
	{
		_clipClient = clipClient;
		_eventHandler = eventHandler;
		_bridge = bridge;
		ResourceManager resourceManager = this;
		JsonSerializerSettings val = new JsonSerializerSettings();
		val.ContractResolver = (IContractResolver)new DefaultContractResolver
		{
			NamingStrategy = (NamingStrategy)new SnakeCaseNamingStrategy()
		};
		val.Formatting = (Formatting)0;
		val.Converters.Add((JsonConverter)new StringEnumConverter(typeof(SnakeCaseNamingStrategy)));
		val.Converters.Add((JsonConverter)(object)new DiscriminatedJsonConverter(typeof(DependencyGetDiscriminatorOptions)));
		val.Converters.Add((JsonConverter)(object)new DiscriminatedJsonConverter(typeof(EventResourceGetDiscriminatorOptions)));
		resourceManager._jsonSettings = val;
		string appId = await GetApplicationId(bridge);
		cancelToken?.ThrowIfCancellationRequested();
		_bridge.AppId = appId;
		if (appId.Length == 0)
		{
			return false;
		}
		_mutex.WaitOne();
		_clipEntertainmentConfigurations = new ClientResourceEntertainmentConfiguration(_clipClient).Get().Body;
		_refreshECCache = true;
		_mutex.ReleaseMutex();
		if (_clipEntertainmentConfigurations == null)
		{
			return false;
		}
		cancelToken?.ThrowIfCancellationRequested();
		AutoResetEvent signalEvent = new AutoResetEvent(initialState: false);
		_clipClient.RestClient.ListenToSSE(SSEEventHandler, signalEvent);
		signalEvent.WaitOne();
		return true;
	}

	public void Reset()
	{
		ClipV2Client clipClient = _clipClient;
		if (clipClient != null)
		{
			clipClient.RestClient.CancelSSE();
		}
		_clipEntertainmentConfigurations = null;
		_entertainmentConfigurationCache = null;
		_refreshECCache = false;
		_bridge = null;
	}

	public void SSEEventHandler(string rawSSE)
	{
		JArray val = JArray.Parse(rawSSE);
		List<Task> list = new List<Task>();
		foreach (JToken item in val)
		{
			_mutex.WaitOne();
			ServerSentEvent serverSentEvent = item.ToObject<ServerSentEvent>();
			if (serverSentEvent.Type == "update")
			{
				HandleUpdate(serverSentEvent, item, list);
			}
			else if (serverSentEvent.Type == "add")
			{
				HandleAdd(serverSentEvent, item, list);
			}
			else if (serverSentEvent.Type == "delete")
			{
				HandleDelete(serverSentEvent, item, list);
			}
			else
			{
				Console.WriteLine("Unknown sse type: " + serverSentEvent.Type);
			}
			_mutex.ReleaseMutex();
		}
		Logger.Instance.Log("[ResourceManager.SSEEventHandler] Waiting for " + list.Count + " event tasks to complete.");
		foreach (Task item2 in list)
		{
			item2.Start();
			item2.Wait();
		}
		Logger.Instance.Log("[ResourceManager.SSEEventHandler] Done waiting.");
	}

	private void HandleUpdate(ServerSentEvent sse, JToken rawSSE, List<Task> eventTaskList)
	{
		for (int i = 0; i < sse.Data.Count; i++)
		{
			if (sse.Data[i].Type == "entertainment_configuration")
			{
				EntertainmentConfigurationEvent val = JsonConvert.DeserializeObject<EntertainmentConfigurationEvent>(((object)rawSSE).ToString(), _jsonSettings);
				UpdateEntertainmentConfiguration(val.Data[i], eventTaskList);
			}
			else if (!(sse.Data[i].Type == "light"))
			{
			}
		}
	}

	private void HandleAdd(ServerSentEvent sse, JToken rawSSE, List<Task> eventTaskList)
	{
		for (int i = 0; i < sse.Data.Count; i++)
		{
			if (sse.Data[i].Type == "entertainment_configuration")
			{
				EntertainmentConfigurationEvent val = JsonConvert.DeserializeObject<EntertainmentConfigurationEvent>(((object)rawSSE).ToString(), _jsonSettings);
				AddEntertainmentConfiguration(val.Data[i], eventTaskList);
			}
			else if (!(sse.Data[i].Type == "light"))
			{
			}
		}
	}

	private void HandleDelete(ServerSentEvent sse, JToken rawSSE, List<Task> eventTaskList)
	{
		foreach (ServerSentEventId datum in sse.Data)
		{
			if (datum.Type == "entertainment_configuration")
			{
				DeleteEntertainmentConfiguration(datum.Id, eventTaskList);
			}
			else if (!(datum.Type == "light"))
			{
			}
		}
	}

	private void UpdateEntertainmentConfiguration(EntertainmentConfigurationEventData ecData, List<Task> eventTaskList)
	{
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Invalid comparison between Unknown and I4
		EntertainmentConfigurationGet ec = GetEntertainmentConfigurationById(GUID.op_Implicit(((ResourceGet)ecData).Id));
		if (ec == null)
		{
			Logger.Instance.Log("Entertainment configuration with id " + GUID.op_Implicit(((ResourceGet)ecData).Id) + " does not exist!");
			return;
		}
		bool flag = _bridge.IsStreaming();
		if (((EntertainmentConfigurationGet)ecData).Metadata != null)
		{
			ec.Metadata = ((EntertainmentConfigurationGet)ecData).Metadata;
		}
		if (((EntertainmentConfigurationGet)ecData).Name != null)
		{
			ec.Name = ((EntertainmentConfigurationGet)ecData).Name;
		}
		if (((EntertainmentConfigurationGet)ecData).ActiveStreamer != null)
		{
			ec.ActiveStreamer = ((EntertainmentConfigurationGet)ecData).ActiveStreamer;
		}
		if (ecData.ConfigurationType.HasValue)
		{
			ec.ConfigurationType = ecData.ConfigurationType.Value;
		}
		if (((EntertainmentConfigurationGet)ecData).StreamProxy != null)
		{
			ec.StreamProxy = ((EntertainmentConfigurationGet)ecData).StreamProxy;
		}
		if (ecData.Status.HasValue)
		{
			ec.Status = ecData.Status.Value;
			if ((int)ec.Status == 1 && ec.ActiveStreamer != null)
			{
				ec.ActiveStreamer.Rid = GUID.op_Implicit("");
			}
		}
		if (((EntertainmentConfigurationGet)ecData).Channels != null)
		{
			ec.Channels = ((EntertainmentConfigurationGet)ecData).Channels;
		}
		if (((EntertainmentConfigurationGet)ecData).LightServices != null)
		{
			ec.LightServices = ((EntertainmentConfigurationGet)ecData).LightServices;
		}
		if (((EntertainmentConfigurationGet)ecData).Locations != null)
		{
			ec.Locations = ((EntertainmentConfigurationGet)ecData).Locations;
		}
		_refreshECCache = true;
		eventTaskList.Add(new Task(delegate
		{
			_eventHandler?.Invoke(new UIEventArgs(UIState.EntertainmentConfigurationUpdated, ec));
		}));
		if (flag && !_bridge.IsStreaming())
		{
			Logger.Instance.Log("[ResourceManager] Streaming was disconnected.");
			eventTaskList.Add(new Task(delegate
			{
				_eventHandler?.Invoke(new UIEventArgs(UIState.StreamingDisconnected, ec));
			}));
		}
	}

	private void AddEntertainmentConfiguration(EntertainmentConfigurationEventData ecData, List<Task> eventTaskList)
	{
		string text = JsonConvert.SerializeObject((object)ecData);
		EntertainmentConfigurationGet newEC = JsonConvert.DeserializeObject<EntertainmentConfigurationGet>(text);
		_clipEntertainmentConfigurations.Data.Add(newEC);
		_refreshECCache = true;
		eventTaskList.Add(new Task(delegate
		{
			_eventHandler?.Invoke(new UIEventArgs(UIState.EntertainmentConfigurationAdded, newEC));
		}));
	}

	private void DeleteEntertainmentConfiguration(string id, List<Task> eventTaskList)
	{
		for (int i = 0; i < _clipEntertainmentConfigurations.Data.Count; i++)
		{
			EntertainmentConfigurationGet ec = _clipEntertainmentConfigurations.Data[i];
			if (!(GUID.op_Implicit(((ResourceGet)ec).Id) == id))
			{
				continue;
			}
			_clipEntertainmentConfigurations.Data.RemoveAt(i);
			_refreshECCache = true;
			if (_bridge.ActiveEntertainmentConfigurationId == id)
			{
				_bridge.SetActiveEntertainmentConfiguration("");
			}
			eventTaskList.Add(new Task(delegate
			{
				_eventHandler?.Invoke(new UIEventArgs(UIState.EntertainmentConfigurationDeleted, ec));
			}));
			if (_clipEntertainmentConfigurations.Data.Count == 0)
			{
				eventTaskList.Add(new Task(delegate
				{
					_eventHandler?.Invoke(new UIEventArgs(UIState.CreateEntertainmentConfiguration, ec));
				}));
			}
			else if (!_bridge.IsValidEntertainmentConfigurationSelected())
			{
				eventTaskList.Add(new Task(delegate
				{
					_eventHandler?.Invoke(new UIEventArgs(UIState.SelectEntertainmentConfiguration, ec));
				}));
			}
			break;
		}
	}

	private EntertainmentConfigurationGet GetEntertainmentConfigurationById(string id)
	{
		if (_clipEntertainmentConfigurations != null)
		{
			foreach (EntertainmentConfigurationGet datum in _clipEntertainmentConfigurations.Data)
			{
				if (GUID.op_Implicit(((ResourceGet)datum).Id) == id)
				{
					return datum;
				}
			}
		}
		return null;
	}

	public async Task<string> GetApplicationId(Bridge bridge, bool ignoreFailure = false)
	{
		RestRequest restRequest = new RestRequest("https://{bridge}/auth/v1", HttpMethod.Get);
		RestResponse response = await _clipClient.RestClient.Execute(restRequest);
		_lastHttpStatusCode = response.StatusCode;
		switch (response.StatusCode)
		{
		case HttpStatusCode.OK:
		{
			IEnumerator<string> enumData = response.Headers.GetValues("hue-application-id").GetEnumerator();
			enumData.MoveNext();
			bridge.Authorized = true;
			bridge.Connected = true;
			return enumData.Current;
		}
		case HttpStatusCode.Forbidden:
			if (!ignoreFailure)
			{
				bridge.Connected = true;
				bridge.Authorized = false;
			}
			break;
		default:
			if (!ignoreFailure)
			{
				bridge.Connected = false;
			}
			Logger.Instance.Log("Unable to retrieve app id: " + response.StatusCode.ToString() + " Ignore failure: " + ignoreFailure, Logger.LogLevel.ERROR);
			break;
		}
		return "";
	}

	public void FillBridgeProperties(Bridge bridge)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Invalid comparison between Unknown and I4
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		if (bridge == null)
		{
			return;
		}
		ClipMessageBridgeGet body = new ClientResourceBridge(_clipClient).Get().Body;
		if (body == null || body.Data.Count != 1)
		{
			Logger.Instance.Log($"[ResourceManager.FillBridgeProperties] Error, can't get bridge: {body} {((body != null) ? new int?(body.Data.Count) : ((int?)null))}", Logger.LogLevel.ERROR);
			return;
		}
		if (body.Data[0].BridgeId.ToLower() != bridge.Id.ToLower())
		{
			Logger.Instance.Log("[ResourceManager.FillBridgeProperties] Error, wrong bridge id: " + body.Data[0].BridgeId + " " + bridge.Id, Logger.LogLevel.ERROR);
			return;
		}
		GUID rid = ((OwnedResourceGet)body.Data[0]).Owner.Rid;
		ClipMessageDeviceGet body2 = new ClientResourceDeviceById(_clipClient).Get(rid).Body;
		if (body2.Data == null || body2.Data.Count != 1)
		{
			Logger.Instance.Log($"[ResourceManager.FillBridgeProperties] Error, can't get device with id: {rid} {((body2 != null) ? new int?(body2.Data.Count) : ((int?)null))}", Logger.LogLevel.ERROR);
			return;
		}
		foreach (ResourceIdentifierGet item in (List<ResourceIdentifierGet>)(object)((GroupGet)body2.Data[0]).Services)
		{
			if ((int)item.Rtype == 10)
			{
				ClipMessageEntertainmentGet body3 = new ClientResourceEntertainmentById(_clipClient).Get(item.Rid).Body;
				if (body3 != null)
				{
					bridge.MaxStream = body3.Data[0].MaxStreams;
				}
				break;
			}
		}
	}
}
public class EventResourceGetDiscriminatorOptions : ResourceGetDiscriminatorOptions
{
	public override IEnumerable<(string TypeName, Type Type)> GetDiscriminatedTypes()
	{
		yield return (TypeName: "ScriptDefinition", Type: typeof(ScriptDefinitionGet));
		yield return (TypeName: "behavior_script", Type: typeof(BehaviorScriptGet));
		yield return (TypeName: "ScriptInstance", Type: typeof(ScriptInstanceGet));
		yield return (TypeName: "behavior_instance", Type: typeof(BehaviorInstanceGet));
		yield return (TypeName: "bridge", Type: typeof(BridgeGet));
		yield return (TypeName: "entertainment_configuration", Type: typeof(EntertainmentConfigurationEventData));
		yield return (TypeName: "geofence_client", Type: typeof(GeofenceClientGet));
		yield return (TypeName: "geolocation", Type: typeof(GeolocationGet));
		yield return (TypeName: "grouped_light", Type: typeof(GroupedLightGet));
		yield return (TypeName: "Group", Type: typeof(GroupGet));
		yield return (TypeName: "device", Type: typeof(DeviceGet));
		yield return (TypeName: "bridge_home", Type: typeof(BridgeHomeGet));
		yield return (TypeName: "room", Type: typeof(RoomGet));
		yield return (TypeName: "zone", Type: typeof(ZoneGet));
		yield return (TypeName: "homekit", Type: typeof(HomekitGet));
		yield return (TypeName: "OwnedResource", Type: typeof(OwnedResourceGet));
		yield return (TypeName: "ConnectivityService", Type: typeof(ConnectivityServiceGet));
		yield return (TypeName: "zigbee_connectivity", Type: typeof(ZigbeeConnectivityGet));
		yield return (TypeName: "zgp_connectivity", Type: typeof(ZgpConnectivityGet));
		yield return (TypeName: "device_power", Type: typeof(DevicePowerGet));
		yield return (TypeName: "entertainment", Type: typeof(EntertainmentGet));
		yield return (TypeName: "light", Type: typeof(LightGet));
		yield return (TypeName: "SensingService", Type: typeof(SensingServiceGet));
		yield return (TypeName: "motion", Type: typeof(MotionGet));
		yield return (TypeName: "light_level", Type: typeof(LightLevelGet));
		yield return (TypeName: "temperature", Type: typeof(TemperatureGet));
		yield return (TypeName: "button", Type: typeof(ButtonGet));
		yield return (TypeName: "relative_rotary", Type: typeof(RelativeRotaryGet));
		yield return (TypeName: "SceneService", Type: typeof(SceneServiceGet));
		yield return (TypeName: "scene", Type: typeof(SceneGet));
	}
}
public class UIEventArgs : EventArgs
{
	public UIState EventType { get; }

	public object EventSource { get; }

	public Dictionary<string, object> EventParams { get; }

	public UIEventArgs(UIState type, object source = null, Dictionary<string, object> eventParams = null)
	{
		EventType = type;
		EventSource = source;
		EventParams = eventParams;
	}
}
public enum UIState
{
	Disconnected,
	Searching,
	PressPushlink,
	CreateEntertainmentConfiguration,
	SelectEntertainmentConfiguration,
	ReadyToStream,
	ConnectingStart,
	ConnectingFailed,
	ConnectingAuthorized,
	StreamingConnected,
	StreamingConnectingFailed,
	StreamingDisconnected,
	StreamingDisconnectingFailed,
	EntertainmentConfigurationUpdated,
	EntertainmentConfigurationAdded,
	EntertainmentConfigurationDeleted,
	PushLinkTimeout
}
