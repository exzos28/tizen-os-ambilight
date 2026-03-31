using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Common;
using HueSync;
using Microsoft.CodeAnalysis;
using Tizen.Applications;
using Tizen.Applications.DataControl;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(/*Could not decode attribute arguments.*/)]
[assembly: TargetFramework("Tizen,Version=v6.0", FrameworkDisplayName = "")]
[assembly: AssemblyCompany("HueSyncService")]
[assembly: AssemblyConfiguration("FinalTV")]
[assembly: AssemblyFileVersion("1.8.55.0")]
[assembly: AssemblyInformationalVersion("1.8.55+50a0a7e0ea5077bc142763b4588f4446d2f1dc9c")]
[assembly: AssemblyProduct("HueSyncService")]
[assembly: AssemblyTitle("HueSyncService")]
[assembly: AssemblyVersion("1.8.55.0")]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : System.Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(/*Could not decode attribute arguments.*/)]
	internal sealed class NullableAttribute : System.Attribute
	{
		public readonly byte[] NullableFlags;

		public NullableAttribute(byte P_0)
		{
			NullableFlags = new byte[1] { P_0 };
		}

		public NullableAttribute(byte[] P_0)
		{
			NullableFlags = P_0;
		}
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(/*Could not decode attribute arguments.*/)]
	internal sealed class NullableContextAttribute : System.Attribute
	{
		public readonly byte Flag;

		public NullableContextAttribute(byte P_0)
		{
			Flag = P_0;
		}
	}
}
namespace HueSyncService.Tizen
{
	internal static class HueSyncAppBuilderTizen
	{
		[CompilerGenerated]
		private sealed class <Create>d__0 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder<HueSyncApp> <>t__builder;

			public IHueSyncAppStateMaintainer appStateMaintainer;

			private HueSyncAppSettings <settings>5__1;

			private HueSyncApp <hueSyncApp>5__2;

			private TaskAwaiter <>u__1;

			private void MoveNext()
			{
				//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
				//IL_0101: Unknown result type (might be due to invalid IL or missing references)
				//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				HueSyncApp result;
				try
				{
					TaskAwaiter val;
					if (num != 0)
					{
						HueSyncAppSettings hueSyncAppSettings = new HueSyncAppSettings
						{
							AppName = "Hue Sync",
							DeviceName = "Samsung TV",
							AppDataPath = Environment.GetFolderPath((SpecialFolder)26),
							OptionsFilePath = Application.Current.DirectoryInfo.Resource + "wgt/res/options.json",
							ForceTrustBridgeSelfSignedCertificate = false,
							AnalyticsKeyEncrypted = true
						};
						uint[] array = new uint[4];
						RuntimeHelpers.InitializeArray((System.Array)array, (RuntimeFieldHandle)/*OpCode not supported: LdMemberToken*/);
						hueSyncAppSettings.AnalyticsKey = array;
						<settings>5__1 = hueSyncAppSettings;
						<hueSyncApp>5__2 = new HueSyncApp(<settings>5__1, appStateMaintainer.AppState);
						val = <hueSyncApp>5__2.Create().GetAwaiter();
						if (!((TaskAwaiter)(ref val)).IsCompleted)
						{
							num = (<>1__state = 0);
							<>u__1 = val;
							<Create>d__0 <Create>d__ = this;
							<>t__builder.AwaitUnsafeOnCompleted<TaskAwaiter, <Create>d__0>(ref val, ref <Create>d__);
							return;
						}
					}
					else
					{
						val = <>u__1;
						<>u__1 = default(TaskAwaiter);
						num = (<>1__state = -1);
					}
					((TaskAwaiter)(ref val)).GetResult();
					result = <hueSyncApp>5__2;
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					<settings>5__1 = default(HueSyncAppSettings);
					<hueSyncApp>5__2 = null;
					<>t__builder.SetException(exception);
					return;
				}
				<>1__state = -2;
				<settings>5__1 = default(HueSyncAppSettings);
				<hueSyncApp>5__2 = null;
				<>t__builder.SetResult(result);
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
			}
		}

		[AsyncStateMachine(typeof(<Create>d__0))]
		[DebuggerStepThrough]
		internal static async System.Threading.Tasks.Task<HueSyncApp> Create(IHueSyncAppStateMaintainer appStateMaintainer)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			HueSyncAppSettings hueSyncAppSettings = new HueSyncAppSettings
			{
				AppName = "Hue Sync",
				DeviceName = "Samsung TV",
				AppDataPath = Environment.GetFolderPath((SpecialFolder)26),
				OptionsFilePath = Application.Current.DirectoryInfo.Resource + "wgt/res/options.json",
				ForceTrustBridgeSelfSignedCertificate = false,
				AnalyticsKeyEncrypted = true
			};
			uint[] array = new uint[4];
			RuntimeHelpers.InitializeArray((System.Array)array, (RuntimeFieldHandle)/*OpCode not supported: LdMemberToken*/);
			hueSyncAppSettings.AnalyticsKey = array;
			HueSyncAppSettings settings = hueSyncAppSettings;
			HueSyncApp hueSyncApp = new HueSyncApp(settings, appStateMaintainer.AppState);
			await hueSyncApp.Create();
			return hueSyncApp;
		}
	}
	internal class App : ServiceApplication
	{
		[CompilerGenerated]
		private sealed class <<OnCreate>b__3_0>d : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder <>t__builder;

			public App <>4__this;

			private HueSyncApp <>s__1;

			private TaskAwaiter<HueSyncApp> <>u__1;

			private TaskAwaiter <>u__2;

			private void MoveNext()
			{
				//IL_0061: Unknown result type (might be due to invalid IL or missing references)
				//IL_0066: Unknown result type (might be due to invalid IL or missing references)
				//IL_006d: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
				//IL_0119: Unknown result type (might be due to invalid IL or missing references)
				//IL_011e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0125: Unknown result type (might be due to invalid IL or missing references)
				//IL_002a: Unknown result type (might be due to invalid IL or missing references)
				//IL_002f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_0044: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				try
				{
					TaskAwaiter val;
					TaskAwaiter<HueSyncApp> val2;
					if (num != 0)
					{
						if (num == 1)
						{
							val = <>u__2;
							<>u__2 = default(TaskAwaiter);
							num = (<>1__state = -1);
							goto IL_0134;
						}
						val2 = HueSyncAppBuilderTizen.Create(<>4__this._appState).GetAwaiter();
						if (!val2.IsCompleted)
						{
							num = (<>1__state = 0);
							<>u__1 = val2;
							<<OnCreate>b__3_0>d <<OnCreate>b__3_0>d = this;
							((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<TaskAwaiter<HueSyncApp>, <<OnCreate>b__3_0>d>(ref val2, ref <<OnCreate>b__3_0>d);
							return;
						}
					}
					else
					{
						val2 = <>u__1;
						<>u__1 = default(TaskAwaiter<HueSyncApp>);
						num = (<>1__state = -1);
					}
					<>s__1 = val2.GetResult();
					<>4__this._hueSyncApp = <>s__1;
					<>s__1 = null;
					HueSyncApp.RequestTerminate += ((Application)<>4__this).Exit;
					HueSyncApp.RequestTerminate += <>4__this._hueSyncPublicControl.Terminate;
					val = <>4__this._hueSyncApp.Start().GetAwaiter();
					if (!((TaskAwaiter)(ref val)).IsCompleted)
					{
						num = (<>1__state = 1);
						<>u__2 = val;
						<<OnCreate>b__3_0>d <<OnCreate>b__3_0>d = this;
						((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<TaskAwaiter, <<OnCreate>b__3_0>d>(ref val, ref <<OnCreate>b__3_0>d);
						return;
					}
					goto IL_0134;
					IL_0134:
					((TaskAwaiter)(ref val)).GetResult();
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					((AsyncTaskMethodBuilder)(ref <>t__builder)).SetException(exception);
					return;
				}
				<>1__state = -2;
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetResult();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
			}
		}

		private HueSyncStateTizen _appState;

		private HueSyncApp _hueSyncApp;

		private HueSyncPublicControl _hueSyncPublicControl;

		protected override void OnCreate()
		{
			((CoreApplication)this).OnCreate();
			_appState = new HueSyncStateTizen("HueSyncState");
			((Provider)_appState).Run();
			_hueSyncPublicControl = new HueSyncPublicControl("HueSyncControl", _appState.AppState);
			((Provider)_hueSyncPublicControl).Run();
			System.Threading.Tasks.Task.Run((Func<System.Threading.Tasks.Task>)([AsyncStateMachine(typeof(<<OnCreate>b__3_0>d))] [DebuggerStepThrough] [CompilerGenerated] () =>
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				<<OnCreate>b__3_0>d <<OnCreate>b__3_0>d = new <<OnCreate>b__3_0>d
				{
					<>t__builder = AsyncTaskMethodBuilder.Create(),
					<>4__this = this,
					<>1__state = -1
				};
				((AsyncTaskMethodBuilder)(ref <<OnCreate>b__3_0>d.<>t__builder)).Start<<<OnCreate>b__3_0>d>(ref <<OnCreate>b__3_0>d);
				return ((AsyncTaskMethodBuilder)(ref <<OnCreate>b__3_0>d.<>t__builder)).Task;
			}));
		}

		protected override void OnTerminate()
		{
			_hueSyncApp?.Terminate();
			((CoreApplication)this).OnTerminate();
		}

		protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
		{
			Platform platform = (Platform)Platform.Instance;
			platform.OnAppControlReceived(e);
			((CoreApplication)this).OnAppControlReceived(e);
		}

		private static void Main(string[] args)
		{
			App app = new App();
			((Application)app).Run(args);
		}
	}
	internal class HueSyncStateTizen : HueSyncStateTizenBase, IHueSyncAppStateMaintainer
	{
		public global::HueSync.HueSync HueSync
		{
			get
			{
				return AppState.HueSync;
			}
			set
			{
				AppState.HueSync = value;
			}
		}

		[field: CompilerGenerated]
		[field: DebuggerBrowsable(/*Could not decode attribute arguments.*/)]
		public HueSyncAppState AppState
		{
			[CompilerGenerated]
			get;
		}

		public HueSyncStateTizen(string dataId)
			: base(dataId)
		{
			AppState = new HueSyncAppState();
			AppState.StateChange += StateOnStateChange;
		}

		protected override MapGetResult OnMapGet(string key)
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Expected O, but got Unknown
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Expected O, but got Unknown
			try
			{
				string text = AppState.Get(key);
				return new MapGetResult(new string[1] { text }, true);
			}
			catch (System.Exception)
			{
				return new MapGetResult(new string[0], false);
			}
		}

		protected override MapSetResult OnMapSet(string key, string oldValue, string newValue)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			try
			{
				AppState.Set(key, newValue);
				return new MapSetResult(true);
			}
			catch (System.Exception)
			{
				return new MapSetResult(false);
			}
		}

		private void StateOnStateChange(Dictionary<string, string> data)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			Bundle val = new Bundle();
			Enumerator<string, string> enumerator = data.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					KeyValuePair<string, string> current = enumerator.Current;
					val.AddItem(current.Key, current.Value);
				}
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
			((Provider)this).SendDataChange((ChangeType)5, val);
		}
	}
	internal class HueSyncStateTizenBase : Provider
	{
		public HueSyncStateTizenBase(string dataID)
			: base(dataID)
		{
		}

		protected override SelectResult OnSelect(string query, string where, string[] columList, int columnCount, string order, int pageNum, int countPerPage)
		{
			return null;
		}

		protected override InsertResult OnInsert(string query, Bundle insertData)
		{
			return null;
		}

		protected override UpdateResult OnUpdate(string query, string where, Bundle updateData)
		{
			return null;
		}

		protected override DeleteResult OnDelete(string query, string where)
		{
			return null;
		}

		protected override MapAddResult OnMapAdd(string key, string value)
		{
			return null;
		}

		protected override MapBulkAddResult OnMapBulkAdd(BulkData bulkAddData)
		{
			return null;
		}

		protected override MapRemoveResult OnMapRemove(string key, string value)
		{
			return null;
		}

		protected override DataChangeListenResult OnDataChangeListenRequest(string requestAppID)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected O, but got Unknown
			return new DataChangeListenResult((ResultType)0);
		}
	}
}
