using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using Tizen.NUI;
using Tizen.TV.Security.Privilege;
using Tizen.TV.System.ContentAnalysis.Configs;
using Tizen.TV.System.ContentAnalysis.Controls;
using Tizen.TV.System.ContentAnalysis.Helpers;
using Tizen.TV.System.ContentAnalysis.InteropControl;
using Tizen.TV.System.ContentAnalysis.Models;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETStandard,Version=v2.0", FrameworkDisplayName = "")]
[assembly: AssemblyCompany("Samsung Electronics")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyCopyright("© Samsung Electronics Co., Ltd All Rights Reserved")]
[assembly: AssemblyDescription("Provide Device API for Tizen .NET")]
[assembly: AssemblyFileVersion("1.0.0")]
[assembly: AssemblyInformationalVersion("9.9.4.3")]
[assembly: AssemblyProduct("Tizen.TV.System.ContentAnalysis")]
[assembly: AssemblyTitle("Tizen.TV.System.ContentAnalysis")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
internal static class Interop
{
	internal static class Libraries
	{
		public enum sec_video_ret_type
		{
			SEC_VIDEO_OK = 0,
			SEC_VIDEO_ERROR = -1,
			SEC_VIDEO_ERR_AGAIN = -2,
			SEC_VIDEO_ERR_SOURCE_NOT_READY = -3,
			SEC_VIDEO_ERR_COPY_PROTECTION = -4,
			SEC_VIDEO_ERR_MEM = -5
		}

		public enum secvideo_color_format
		{
			SECVIDEO_CAPTURE_COLOR_YUV420,
			SECVIDEO_CAPTURE_COLOR_YUV422,
			SECVIDEO_CAPTURE_COLOR_YUV444,
			SECVIDEO_CAPTURE_COLOR_NONE
		}

		public enum secvideo_3dmode
		{
			SECVIDEO_CAPTURE_3D_2D,
			SECVIDEO_CAPTURE_3D_FRAMEPACKING,
			SECVIDEO_CAPTURE_3D_FRAMESEQ,
			SECVIDEO_CAPTURE_3D_TOPBOTTOM,
			SECVIDEO_CAPTURE_3D_SIDEBYSIDE,
			SECVIDEO_CAPTURE_3D_MAX
		}

		public enum secvideo_flip
		{
			SECVIDEO_CAPTURE_NO_FLIP,
			SECVIDEO_CAPTURE_H_FLIP,
			SECVIDEO_CAPTURE_V_FLIP,
			SECVIDEO_CAPTURE_HV_FLIP,
			SECVIDEO_CAPTURE_FLIP_MAX
		}

		public enum secvideo_rotation
		{
			CAPTURE_DEGREE_0,
			CAPTURE_DEGREE_90,
			CAPTURE_DEGREE_180,
			CAPTURE_DEGREE_270
		}

		public enum secvideo_lock_type
		{
			CAPTURE_LOCK_POST,
			CAPTURE_LOCK_INPUT,
			CAPTURE_LOCK_MAX
		}

		public struct testArry
		{
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
			public char[] data;
		}

		public struct secvideo_capture_param
		{
			public uint uYSize;

			public uint uCSize;

			public int ret_width;

			public int ret_height;

			public IntPtr pYAddr;

			public IntPtr pCAddr;

			public int no_lock_no_copy;

			private secvideo_color_format color_format;

			public uint protected_on;

			private secvideo_3dmode capt_3dmod;

			public uint ret_interlaced;

			public uint ret_y_pitch;

			public uint ret_c_pitch;

			public ulong timestamp_nsec;

			public uint do_auto_correct_flip;

			private secvideo_flip flip_type;

			private secvideo_rotation ret_rotation;

			public int no_cache_requst_capture;

			public uint is_cache;
		}

		public struct VEPPIRgbMeasureInfo_t
		{
			public int measureBlockCnt;

			public int measureBlockWidth;

			public int measureBlockHeight;

			public int measureDelay;

			public int measureFullWidth;

			public int measureFullHeight;
		}

		public struct VEPPIRgbMeasure_t
		{
			public int mean_R;

			public int mean_G;

			public int mean_B;

			public int lowInputLagOnOff;
		}

		public const string VIDEO_CAPTURE_LIB = "libcapi-video-capture.so";

		public const string VIDEOENHANCE_LIB = "libvideoenhance.so";
	}
}
internal static class VideoCaptureInterop
{
	internal static class VideoCapture
	{
		[DllImport("libcapi-video-capture.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int secvideo_api_capture_video_main(int req_width, int req_height, IntPtr secvideo_capture_param);

		[DllImport("libcapi-video-capture.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int secvideo_api_capture_lock(global::Interop.Libraries.secvideo_lock_type lock_type, int virt_rsc_id);

		[DllImport("libcapi-video-capture.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int secvideo_api_capture_unlock(global::Interop.Libraries.secvideo_lock_type lock_type, int virt_rsc_id);

		[DllImport("libcapi-video-capture.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int secvideo_api_capture_is_protect(out int isProtect);
	}
}
internal static class VideoEnhanceInterop
{
	internal static class VideoEnhance
	{
		[DllImport("libvideoenhance.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int ppi_ve_get_rgb_measure_condition(out global::Interop.Libraries.VEPPIRgbMeasureInfo_t measureCondition);

		[DllImport("libvideoenhance.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int ppi_ve_set_rgb_measure_position(int idx, int x, int y);

		[DllImport("libvideoenhance.so", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int ppi_ve_get_rgb_measure_pixel(int idx, out global::Interop.Libraries.VEPPIRgbMeasure_t info);

		[DllImport("libvideoenhance.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ve_get_rgb_measure_condition")]
		internal static extern int ve_api_get_rgb_measure_condition(out global::Interop.Libraries.VEPPIRgbMeasureInfo_t measureCondition);

		[DllImport("libvideoenhance.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ve_set_rgb_measure_position")]
		internal static extern int ve_api_set_rgb_measure_position(int idx, int x, int y);

		[DllImport("libvideoenhance.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ve_get_rgb_measure_pixel")]
		internal static extern int ve_api_get_rgb_measure_pixel(int idx, out global::Interop.Libraries.VEPPIRgbMeasure_t info);
	}
}
namespace Tizen.TV.System.Contentanalysis.Helpers
{
	internal class ImageHelper
	{
		public static void SaveImgFileFromRaw(byte[] rgbBuf, string imgPath, PixelFormat pixelFormat, uint size, uint width, uint height)
		{
			Log.Error("contentanalysis", "Not implemented", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Helpers/ImageHelper.cs", "SaveImgFileFromRaw", 9);
		}
	}
}
namespace Tizen.TV.System.ContentAnalysis
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class ContentAnalysis : IContentAnalysis
	{
		public delegate void RectInfoHandler(Dictionary<int, RectInfo> RectInfos);

		public delegate void PartialRectInfoHandler(RectInfo RectInfo);

		private bool IsInitializeDone = false;

		private bool IsEnableCaptureMonitor = false;

		private Dictionary<int, RectInfo> SwRectInfoDic = null;

		private Dictionary<int, RectInfo> HwRectInfoDic = null;

		private Dictionary<string, RectInfoHandler> RectInfoHandlerDic = null;

		private Dictionary<string, PartialRectInfoHandler> PartialRectInfoHandlerDic = null;

		private SwCaptureControl SwCaptureCtrl = null;

		private HwCaptureControl HwCaptureCtrl = null;

		private Thread SwCaptureThread = null;

		private Thread HwCaptureThread = null;

		private Thread RectInfoInvokeThread = null;

		private ConfigRGBAvg ConfigRGBAvg = null;

		private int SwCaptureErrCnt = 0;

		private static object lockObj = new object();

		private Tizen.TV.System.ContentAnalysis.InteropControl.InteropControl interopCtrl = new Tizen.TV.System.ContentAnalysis.InteropControl.InteropControl();

		private event RectInfoHandler RectInfoReceiveEvent;

		private event PartialRectInfoHandler PartialRectInfoReceiveEvent;

		public ContentAnalysis()
		{
			string text = "";
			Log.Error("contentanalysis", "ContentAnalysis create.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", ".ctor", 94);
			if (Cynara.CynaraInitialize(false) != 0)
			{
				throw new Exception("Cynara Initialization Fail");
			}
			try
			{
				string text2 = File.ReadAllText("/proc/self/attr/current");
				string uid = Cynara.GetUid();
				Log.Error("contentanalysis", "Start check privilege", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", ".ctor", 105);
				int num = Cynara.CynaraCheck(text2, text, uid, "http://tizen.org/privilege/internal/default/platform");
				int num2 = Cynara.CynaraCheck(text2, text, uid, "http://developer.samsung.com/privilege/contentanalysis");
				bool flag = num == 2;
				bool flag2 = num2 == 2;
				Log.Error("contentanalysis", $"[privilege] platformPrivilege: {num}, contentAnalysisPrivilege: {num2} || isPlatformPrivilege: {flag}, isContentAnalysisPrivilege: {flag2}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", ".ctor", 111);
				if (!flag && !flag2)
				{
					throw new MethodAccessException("http://developer.samsung.com/privilege/contentanalysis");
				}
			}
			finally
			{
				Cynara.CynaraFinish();
			}
		}

		~ContentAnalysis()
		{
			Cynara.CynaraFinish();
		}

		public Enums.RET_TYPE Initialize(ConfigRGBAvg configRGBAvg = null)
		{
			Log.Error("contentanalysis", "Initialize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "Initialize", 140);
			if (IsEnableCaptureMonitor)
			{
				Log.Error("contentanalysis", "Fail to Initialize. Stop func should be call before initialize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "Initialize", 143);
				return Enums.RET_TYPE.ERR;
			}
			if (((configRGBAvg != null && configRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM) || (configRGBAvg != null && configRGBAvg.CpatureType == Enums.CAPTURE_TYPE.AUTO_CAPTURE)) && !IsSupportHWCapture())
			{
				Log.Error("contentanalysis", "Fail to Initialize. This device is not support HW capture", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "Initialize", 151);
				return Enums.RET_TYPE.ERR_NOT_SUPPORT;
			}
			SwCaptureErrCnt = 0;
			if (configRGBAvg == null)
			{
				configRGBAvg = new ConfigRGBAvg();
			}
			if (configRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM)
			{
				if (configRGBAvg.PositionList == null || configRGBAvg.PositionList.Length == 0)
				{
					Log.Error("contentanalysis", "Fail to Initialize. PostionList is empty.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "Initialize", 165);
					configRGBAvg.Dump();
					return Enums.RET_TYPE.ERR;
				}
			}
			else
			{
				if (configRGBAvg.NumOfRow <= 0 || configRGBAvg.NumOfRow > 10 || configRGBAvg.NumOfColumn <= 0 || configRGBAvg.NumOfColumn > 14)
				{
					Log.Error("contentanalysis", "Fail to Initialize. Please check numOfRow and NumOfColumn", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "Initialize", 175);
					configRGBAvg.Dump();
					return Enums.RET_TYPE.ERR;
				}
				if (configRGBAvg.PositionList != null && configRGBAvg.PositionList.Length != 0)
				{
					Log.Error("contentanalysis", "Fail to Initialize. Only HW_CAPTURE_CUSTOM support specific position config.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "Initialize", 182);
					configRGBAvg.Dump();
					return Enums.RET_TYPE.ERR_NOT_SUPPORT;
				}
			}
			if (SwRectInfoDic != null)
			{
				SwRectInfoDic.Clear();
			}
			if (HwRectInfoDic != null)
			{
				HwRectInfoDic.Clear();
			}
			if (RectInfoHandlerDic != null)
			{
				foreach (RectInfoHandler value in RectInfoHandlerDic.Values)
				{
					RectInfoReceiveEvent -= value;
				}
				RectInfoHandlerDic.Clear();
			}
			if (PartialRectInfoHandlerDic != null)
			{
				foreach (PartialRectInfoHandler value2 in PartialRectInfoHandlerDic.Values)
				{
					PartialRectInfoReceiveEvent -= value2;
				}
				PartialRectInfoHandlerDic.Clear();
			}
			SwRectInfoDic = new Dictionary<int, RectInfo>();
			HwRectInfoDic = new Dictionary<int, RectInfo>();
			RectInfoHandlerDic = new Dictionary<string, RectInfoHandler>();
			PartialRectInfoHandlerDic = new Dictionary<string, PartialRectInfoHandler>();
			switch (configRGBAvg.CpatureType)
			{
			case Enums.CAPTURE_TYPE.SW_CAPTURE:
				SwCaptureCtrl = new SwCaptureControl();
				SwCaptureCtrl.Initialize();
				break;
			case Enums.CAPTURE_TYPE.HW_CAPTURE:
			case Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM:
				if (HwCaptureCtrl != null)
				{
					HwCaptureCtrl.Terminate();
				}
				HwCaptureCtrl = new HwCaptureControl();
				if (!HwCaptureCtrl.Initialize(configRGBAvg))
				{
					return Enums.RET_TYPE.ERR;
				}
				break;
			case Enums.CAPTURE_TYPE.AUTO_CAPTURE:
				SwCaptureCtrl = new SwCaptureControl();
				SwCaptureCtrl.Initialize();
				if (HwCaptureCtrl != null)
				{
					HwCaptureCtrl.Terminate();
				}
				HwCaptureCtrl = new HwCaptureControl();
				if (!HwCaptureCtrl.Initialize(configRGBAvg))
				{
					return Enums.RET_TYPE.ERR;
				}
				break;
			}
			ConfigRGBAvg = configRGBAvg;
			IsInitializeDone = true;
			IsEnableCaptureMonitor = false;
			return Enums.RET_TYPE.SUCCESS;
		}

		public Enums.RET_TYPE Terminate()
		{
			StopCaptureRGBAvg();
			IsInitializeDone = false;
			IsEnableCaptureMonitor = false;
			lock (lockObj)
			{
				if (SwRectInfoDic != null)
				{
					SwRectInfoDic.Clear();
					SwRectInfoDic = null;
				}
				if (HwRectInfoDic != null)
				{
					HwRectInfoDic.Clear();
					HwRectInfoDic = null;
				}
				if (RectInfoHandlerDic != null)
				{
					foreach (RectInfoHandler value in RectInfoHandlerDic.Values)
					{
						RectInfoReceiveEvent -= value;
					}
					RectInfoHandlerDic.Clear();
					RectInfoHandlerDic = null;
				}
				if (PartialRectInfoHandlerDic != null)
				{
					foreach (PartialRectInfoHandler value2 in PartialRectInfoHandlerDic.Values)
					{
						PartialRectInfoReceiveEvent -= value2;
					}
					PartialRectInfoHandlerDic.Clear();
					PartialRectInfoHandlerDic = null;
				}
				if (SwCaptureCtrl != null)
				{
					SwCaptureCtrl.Terminate();
					SwCaptureCtrl = null;
				}
				if (HwCaptureCtrl != null)
				{
					HwCaptureCtrl.Terminate();
					HwCaptureCtrl = null;
				}
				return Enums.RET_TYPE.SUCCESS;
			}
		}

		public Enums.RET_TYPE UpdatePosition(Position[] positionList)
		{
			if (!IsInitializeDone)
			{
				Log.Error("contentanalysis", "Fail UpdatePosition. It is called before initialize", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "UpdatePosition", 323);
				return Enums.RET_TYPE.ERR;
			}
			if (ConfigRGBAvg.CpatureType != Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM)
			{
				Log.Error("contentanalysis", "Fail UpdatePosition. Only HW_CAPTURE_CUSTOM support UpdatePosition func", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "UpdatePosition", 329);
				return Enums.RET_TYPE.ERR_NOT_SUPPORT;
			}
			ConfigRGBAvg configRGBAvg = new ConfigRGBAvg
			{
				CpatureType = ConfigRGBAvg.CpatureType,
				NumOfRow = ConfigRGBAvg.NumOfRow,
				NumOfColumn = ConfigRGBAvg.NumOfColumn,
				PositionList = positionList
			};
			lock (lockObj)
			{
				if (HwCaptureCtrl.IsVaildConfig(configRGBAvg) && HwCaptureCtrl.Initialize(configRGBAvg))
				{
					ConfigRGBAvg = configRGBAvg;
					return Enums.RET_TYPE.SUCCESS;
				}
			}
			return Enums.RET_TYPE.ERR;
		}

		public Enums.RET_TYPE AddRGBAvgListener(RectInfoHandler eventHandler, string appId)
		{
			if (!IsInitializeDone || RectInfoHandlerDic == null)
			{
				Log.Error("contentanalysis", "Can't add listner before initailize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgListener", 362);
				return Enums.RET_TYPE.ERR;
			}
			if (RectInfoHandlerDic.ContainsKey(appId))
			{
				Log.Error("contentanalysis", "Already Added to listener", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgListener", 367);
				return Enums.RET_TYPE.ERR;
			}
			Log.Error("contentanalysis", "AddRGBAvgListener. appId: " + appId, "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgListener", 370);
			RectInfoReceiveEvent += eventHandler;
			RectInfoHandlerDic.Add(appId, eventHandler);
			return Enums.RET_TYPE.SUCCESS;
		}

		public Enums.RET_TYPE RemoveRGBAvgListener(RectInfoHandler eventHandler, string appId)
		{
			if (!IsInitializeDone || RectInfoHandlerDic == null)
			{
				Log.Error("contentanalysis", "Can't remove listner before initailize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "RemoveRGBAvgListener", 386);
				return Enums.RET_TYPE.ERR;
			}
			Log.Error("contentanalysis", "RemoveRGBAvgListener. appId: " + appId, "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "RemoveRGBAvgListener", 390);
			Dictionary<string, RectInfoHandler> rectInfoHandlerDic = RectInfoHandlerDic;
			if (rectInfoHandlerDic != null && rectInfoHandlerDic.ContainsKey(appId))
			{
				RectInfoReceiveEvent -= eventHandler;
				RectInfoHandlerDic.Remove(appId);
			}
			return Enums.RET_TYPE.SUCCESS;
		}

		public Enums.RET_TYPE AddRGBAvgPartialListener(PartialRectInfoHandler eventHandler, string appId)
		{
			if (!IsInitializeDone || PartialRectInfoHandlerDic == null)
			{
				Log.Error("contentanalysis", "Can't add listner before initailize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgPartialListener", 409);
				return Enums.RET_TYPE.ERR;
			}
			if (ConfigRGBAvg.CpatureType != Enums.CAPTURE_TYPE.HW_CAPTURE && ConfigRGBAvg.CpatureType != Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM)
			{
				Log.Error("contentanalysis", "Fail to add AddRGBAvgPartialListener. Only HW_CAPTURE support RGBAvgPartialListener.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgPartialListener", 414);
				return Enums.RET_TYPE.ERR;
			}
			if (PartialRectInfoHandlerDic.ContainsKey(appId))
			{
				Log.Error("contentanalysis", "Already Added to listener", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgPartialListener", 419);
				return Enums.RET_TYPE.ERR;
			}
			Log.Error("contentanalysis", "AddRGBAvgPartialListener. appId: " + appId, "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "AddRGBAvgPartialListener", 422);
			PartialRectInfoReceiveEvent += eventHandler;
			PartialRectInfoHandlerDic.Add(appId, eventHandler);
			return Enums.RET_TYPE.SUCCESS;
		}

		public Enums.RET_TYPE RemoveRGBAvgPartialListener(PartialRectInfoHandler eventHandler, string appId)
		{
			if (!IsInitializeDone || PartialRectInfoHandlerDic == null)
			{
				Log.Error("contentanalysis", "Can't remove listner before initailize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "RemoveRGBAvgPartialListener", 438);
				return Enums.RET_TYPE.ERR;
			}
			Log.Error("contentanalysis", "RemoveRGBAvgPartialListener. appId: " + appId, "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "RemoveRGBAvgPartialListener", 442);
			Dictionary<string, PartialRectInfoHandler> partialRectInfoHandlerDic = PartialRectInfoHandlerDic;
			if (partialRectInfoHandlerDic != null && partialRectInfoHandlerDic.ContainsKey(appId))
			{
				PartialRectInfoReceiveEvent -= eventHandler;
				PartialRectInfoHandlerDic.Remove(appId);
			}
			return Enums.RET_TYPE.SUCCESS;
		}

		public Enums.RET_TYPE StartCaptureRGBAvg()
		{
			if (!IsInitializeDone)
			{
				Log.Error("contentanalysis", "Can't start before initialized", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "StartCaptureRGBAvg", 460);
				return Enums.RET_TYPE.ERR;
			}
			if (IsEnableCaptureMonitor)
			{
				Log.Error("contentanalysis", "Already capture monitor is started. skip start.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "StartCaptureRGBAvg", 465);
				return Enums.RET_TYPE.ERR;
			}
			Log.Error("contentanalysis", "StartCaptureRGBAvg", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "StartCaptureRGBAvg", 469);
			IsEnableCaptureMonitor = true;
			if (ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.SW_CAPTURE || ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.AUTO_CAPTURE)
			{
				if (SwCaptureThread != null)
				{
					SwCaptureThread.Join();
					SwCaptureThread = null;
				}
				SwCaptureThread = new Thread((ParameterizedThreadStart)delegate
				{
					while (IsEnableCaptureMonitor)
					{
						lock (lockObj)
						{
							bool flag = false;
							Dictionary<int, RectInfo> resultRectInfoDic = null;
							flag = SwCaptureCtrl.GetRGBAvg(ConfigRGBAvg, out resultRectInfoDic);
							if (!flag)
							{
								SwCaptureErrCnt++;
								if (SwCaptureErrCnt > 5)
								{
									SwCaptureErrCnt = 0;
									if (ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.AUTO_CAPTURE)
									{
										SwRectInfoDic?.Clear();
										SwRectInfoDic = null;
									}
								}
							}
							else
							{
								SwCaptureErrCnt = 0;
							}
							Log.Debug("contentanalysis", $"GetRGBAvg ret: {flag}. SwRectInfoDic.Count: {SwRectInfoDic?.Count}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "StartCaptureRGBAvg", 509);
							if (resultRectInfoDic != null)
							{
								SwRectInfoDic = new Dictionary<int, RectInfo>(resultRectInfoDic);
							}
						}
						Thread.Sleep(1);
					}
				});
				SwCaptureThread.Start();
			}
			if (ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE || ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM || ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.AUTO_CAPTURE)
			{
				if (HwCaptureThread != null)
				{
					HwCaptureThread.Join();
					HwCaptureThread = null;
				}
				HwCaptureThread = new Thread((ParameterizedThreadStart)delegate
				{
					while (IsEnableCaptureMonitor)
					{
						lock (lockObj)
						{
							bool flag = false;
							flag = HwCaptureCtrl.GetRGBAvg(ConfigRGBAvg, out var resultRectInfoDic, out var resultParialRectInfos);
							if (resultParialRectInfos != null)
							{
								foreach (RectInfo item in resultParialRectInfos)
								{
									if (item != null)
									{
										this.PartialRectInfoReceiveEvent?.Invoke(item);
									}
								}
							}
							Log.Debug("contentanalysis", $"GetRGBAvg ret: {flag}. HwRectInfoDic.Count: {HwRectInfoDic?.Count}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "StartCaptureRGBAvg", 558);
							if (resultRectInfoDic != null)
							{
								HwRectInfoDic = new Dictionary<int, RectInfo>(resultRectInfoDic);
							}
						}
						Thread.Sleep(1);
					}
				});
				HwCaptureThread.Start();
			}
			if (RectInfoInvokeThread != null)
			{
				RectInfoInvokeThread.Join();
				RectInfoInvokeThread = null;
			}
			RectInfoInvokeThread = new Thread((ParameterizedThreadStart)delegate
			{
				while (IsEnableCaptureMonitor)
				{
					if (ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.SW_CAPTURE)
					{
						if (SwRectInfoDic != null && SwRectInfoDic.Count != 0)
						{
							this.RectInfoReceiveEvent?.Invoke(SwRectInfoDic);
						}
					}
					else if (ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE || ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM)
					{
						if (HwRectInfoDic != null && HwRectInfoDic.Count != 0)
						{
							this.RectInfoReceiveEvent?.Invoke(HwRectInfoDic);
						}
					}
					else if (ConfigRGBAvg.CpatureType == Enums.CAPTURE_TYPE.AUTO_CAPTURE)
					{
						if (SwRectInfoDic != null && SwRectInfoDic.Count != 0)
						{
							this.RectInfoReceiveEvent?.Invoke(SwRectInfoDic);
						}
						else if (HwRectInfoDic != null && HwRectInfoDic.Count != 0)
						{
							this.RectInfoReceiveEvent?.Invoke(HwRectInfoDic);
						}
					}
					Thread.Sleep(80);
				}
			});
			RectInfoInvokeThread.Start();
			return Enums.RET_TYPE.SUCCESS;
		}

		public Enums.RET_TYPE StopCaptureRGBAvg()
		{
			Log.Error("contentanalysis", "StopCaptureRGBAvg", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "StopCaptureRGBAvg", 619);
			IsEnableCaptureMonitor = false;
			lock (lockObj)
			{
				if (RectInfoInvokeThread != null)
				{
					RectInfoInvokeThread.Join();
					RectInfoInvokeThread = null;
				}
				if (SwCaptureThread != null)
				{
					SwCaptureThread.Join();
					SwCaptureThread = null;
				}
				if (HwCaptureThread != null)
				{
					HwCaptureThread.Join();
					HwCaptureThread = null;
				}
				return Enums.RET_TYPE.SUCCESS;
			}
		}

		public Enums.RET_TYPE GetHwCondition(out HwCondition condition)
		{
			Log.Error("contentanalysis", "GetHwCondition.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "GetHwCondition", 650);
			condition = null;
			if (HwCaptureCtrl == null)
			{
				HwCaptureCtrl = new HwCaptureControl();
			}
			if (!HwCaptureCtrl.GetHwCondition(out var cond) || cond == null)
			{
				return Enums.RET_TYPE.ERR;
			}
			condition = cond;
			condition?.Dump();
			return Enums.RET_TYPE.SUCCESS;
		}

		private bool IsSupportHWCapture()
		{
			try
			{
				if (!interopCtrl.GetRGBMeasureCondition(out var measureCondition) || measureCondition.measureBlockCnt < 1)
				{
					return false;
				}
			}
			catch (Exception arg)
			{
				Log.Error("contentanalysis", $"Fail GetRGBMeasureCondition. err: {arg}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/ContentAnalysis.cs", "IsSupportHWCapture", 678);
				return false;
			}
			return true;
		}
	}
	internal interface IContentAnalysis
	{
		Enums.RET_TYPE Initialize(ConfigRGBAvg configRGBAvg);

		Enums.RET_TYPE UpdatePosition(Position[] positionList);

		Enums.RET_TYPE Terminate();

		Enums.RET_TYPE StartCaptureRGBAvg();

		Enums.RET_TYPE StopCaptureRGBAvg();

		Enums.RET_TYPE AddRGBAvgListener(ContentAnalysis.RectInfoHandler eventHandler, string appId);

		Enums.RET_TYPE RemoveRGBAvgListener(ContentAnalysis.RectInfoHandler eventHandler, string appId);

		Enums.RET_TYPE AddRGBAvgPartialListener(ContentAnalysis.PartialRectInfoHandler eventHandler, string appId);

		Enums.RET_TYPE RemoveRGBAvgPartialListener(ContentAnalysis.PartialRectInfoHandler eventHandler, string appId);

		Enums.RET_TYPE GetHwCondition(out HwCondition condition);
	}
}
namespace Tizen.TV.System.ContentAnalysis.Models
{
	public class RGB
	{
		internal int sumR;

		internal int sumG;

		internal int sumB;

		public int R;

		public int G;

		public int B;

		public RGB()
		{
			sumR = 0;
			sumG = 0;
			sumB = 0;
			R = 0;
			G = 0;
			B = 0;
		}

		public RGB(int r, int g, int b)
		{
			R = r;
			G = g;
			B = b;
		}

		internal void CalculateAvg(int numOfPixel)
		{
			R = sumR / numOfPixel;
			G = sumG / numOfPixel;
			B = sumB / numOfPixel;
			bool flag = false;
		}

		internal void Dump()
		{
			Log.Debug("contentanalysis", $"[RGB] R: {R}, G: {G}, B: {B}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Common.cs", "Dump", 101);
		}
	}
	public class ConfigRGBAvg
	{
		public Enums.CAPTURE_TYPE CpatureType;

		public int NumOfRow;

		public int NumOfColumn;

		public Position[] PositionList;

		public ConfigRGBAvg(Enums.CAPTURE_TYPE captureType = Enums.CAPTURE_TYPE.SW_CAPTURE, int numOfRow = 4, int numOfColumn = 6, Position[] positionList = null)
		{
			CpatureType = captureType;
			NumOfRow = numOfRow;
			NumOfColumn = numOfColumn;
			PositionList = positionList;
		}

		internal void Dump()
		{
			Log.Debug("contentanalysis", $"[Configuration] CpatureType: {CpatureType}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 82);
			Log.Debug("contentanalysis", $"[Configuration] NumOfRow: {NumOfRow}, NumOfColumn: {NumOfColumn}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 83);
			if (PositionList != null)
			{
				for (int i = 0; i < PositionList.Length; i++)
				{
					PositionList[i].Dump(i);
				}
			}
		}
	}
	public class HwCondition
	{
		public int BlockCount;

		public int FullWidth;

		public int FullHeight;

		public int CaptureWidth;

		public int CaptureHeight;

		public int Delay;

		public HwCondition()
		{
		}

		public HwCondition(int blockCount, int captureWidth, int captureHeight, int fullWidth, int fuillHeight, int delay)
		{
			BlockCount = blockCount;
			CaptureWidth = captureWidth;
			CaptureHeight = captureHeight;
			FullWidth = fullWidth;
			FullHeight = fuillHeight;
			Delay = delay;
		}

		internal void Dump()
		{
			Log.Debug("contentanalysis", $"[HwCondition] BlockCount: {BlockCount}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 152);
			Log.Debug("contentanalysis", $"[HwCondition] FullWidth: {FullWidth}, FullHeight: {FullHeight}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 153);
			Log.Debug("contentanalysis", $"[HwCondition] CaptureWidth: {CaptureWidth}, : CaptureHeight: {CaptureHeight}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 154);
			Log.Debug("contentanalysis", $"[HwCondition] Delay: {Delay}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 155);
		}
	}
	public class Position
	{
		public int x;

		public int y;

		public Position(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		internal void Dump(int index = -1)
		{
			if (index == -1)
			{
				Log.Debug("contentanalysis", $"[Position] {x}, {y}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 191);
			}
			else
			{
				Log.Debug("contentanalysis", $"[Position][index: {index}] {x}, {y}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/Configuration.cs", "Dump", 195);
			}
		}
	}
	internal class ConvertJob
	{
		public int Index = 0;

		public int ImgWidth = 0;

		public int ImgSize = 0;

		public int JobOffset = 0;

		public int JobSize = 0;

		public bool IsComplete = false;

		public ConvertJob(int index, int imgWidth, int imgSize, int jobOffset, int jobSize, bool isComplete)
		{
			Index = index;
			ImgWidth = imgWidth;
			ImgSize = imgSize;
			JobOffset = jobOffset;
			JobSize = jobSize;
			IsComplete = isComplete;
		}

		public void Dump()
		{
			Log.Debug("contentanalysis", $"[ConvertJob: {Index}][IsComplete: {IsComplete}] ImgWidth: {ImgWidth}, ImgSize: {ImgSize}, JobOffset: {JobOffset}, JobSize: {JobSize}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/ConvertJob.cs", "Dump", 28);
		}
	}
	public class RectInfo
	{
		internal bool isCompleted;

		internal int imgWidth;

		internal int imgHeight;

		public int index;

		public int x;

		public int y;

		public int w;

		public int h;

		public RGB rgb;

		private RectInfo rectInfo;

		public RectInfo()
		{
			index = 0;
			isCompleted = false;
			x = 0;
			y = 0;
			w = 0;
			h = 0;
			rgb = new RGB();
		}

		public RectInfo(RectInfo rectInfo)
		{
			this.rectInfo = rectInfo;
		}

		internal RectInfo(int _index, bool _isCompleted, int _imgWidth, int _imgHeight, int _x, int _y, int _w, int _h, RGB _rgb)
		{
			index = _index;
			isCompleted = _isCompleted;
			imgWidth = _imgWidth;
			imgHeight = _imgHeight;
			x = _x;
			y = _y;
			w = _w;
			h = _h;
			rgb = _rgb;
		}

		internal void Dump()
		{
			Log.Debug("contentanalysis", $"[RectInfo][index: {index}][isCompleted: {isCompleted}][imgSize: {imgWidth}x{imgHeight}] x: {x}, y: {y}, w: {w}, h: {h}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Models/RectInfo.cs", "Dump", 113);
			rgb.Dump();
		}
	}
}
namespace Tizen.TV.System.ContentAnalysis.Helpers
{
	internal class ConvertHelper
	{
		public static void YUV2RGB(byte y, byte u, byte v, out byte r, out byte g, out byte b)
		{
			int cy = y - 16;
			int cu = u - 128;
			int cv = v - 128;
			r = Clamp(ConverR(cy, cu, cv));
			g = Clamp(ConverG(cy, cu, cv));
			b = Clamp(ConverB(cy, cu, cv));
		}

		public static byte Clamp(int val)
		{
			byte b = byte.MaxValue;
			if (val < 0)
			{
				val = 0;
			}
			else if (val > b)
			{
				val = b;
			}
			return (byte)val;
		}

		public static int ConverR(int cy, int cu, int cv)
		{
			return 298 * cy + 409 * cv + 128 >> 8;
		}

		public static int ConverG(int cy, int cu, int cv)
		{
			return 298 * cy - 100 * cu - 208 * cv + 128 >> 8;
		}

		public static int ConverB(int cy, int cu, int cv)
		{
			return 298 * cy + 409 * cu + 128 >> 8;
		}
	}
}
namespace Tizen.TV.System.ContentAnalysis.Controls
{
	internal class HwCaptureControl
	{
		private Dictionary<int, RectInfo> RectInfoDic = null;

		private HwCondition HwCondition = null;

		private int RowOffset = 0;

		private int ColumnOffset = 0;

		private int CurIndex = 0;

		private bool IsFisrtCaptureDone = false;

		private long StartTime = 0L;

		private bool IsLowInputLag = true;

		public byte[] yBuf = null;

		public byte[] cBuf = null;

		public byte[] rgbBuf = null;

		private Tizen.TV.System.ContentAnalysis.InteropControl.InteropControl interopCtrl = new Tizen.TV.System.ContentAnalysis.InteropControl.InteropControl();

		public bool GetHwCondition(out HwCondition cond)
		{
			cond = new HwCondition();
			global::Interop.Libraries.VEPPIRgbMeasureInfo_t measureCondition;
			bool rGBMeasureCondition = interopCtrl.GetRGBMeasureCondition(out measureCondition);
			if (!rGBMeasureCondition || measureCondition.measureBlockCnt < 1)
			{
				Log.Debug("contentanalysis", $"Fail GetHwCondition. isSuccess: {rGBMeasureCondition}, measureBlockCnt: {measureCondition.measureBlockCnt}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetHwCondition", 38);
				return false;
			}
			cond = new HwCondition(measureCondition.measureBlockCnt, measureCondition.measureBlockWidth, measureCondition.measureBlockHeight, measureCondition.measureFullWidth, measureCondition.measureFullHeight, measureCondition.measureDelay);
			return true;
		}

		public bool IsVaildConfig(ConfigRGBAvg configRGBAvg)
		{
			if (HwCondition == null)
			{
				global::Interop.Libraries.VEPPIRgbMeasureInfo_t measureCondition;
				bool rGBMeasureCondition = interopCtrl.GetRGBMeasureCondition(out measureCondition);
				if (!rGBMeasureCondition || measureCondition.measureBlockCnt < 1)
				{
					Log.Debug("contentanalysis", $"Fail Initialize. isSuccess: {rGBMeasureCondition}, measureBlockCnt: {measureCondition.measureBlockCnt}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "IsVaildConfig", 60);
					return false;
				}
				HwCondition = new HwCondition(measureCondition.measureBlockCnt, measureCondition.measureBlockWidth, measureCondition.measureBlockHeight, measureCondition.measureFullWidth, measureCondition.measureFullHeight, measureCondition.measureDelay);
				HwCondition.Dump();
			}
			if (configRGBAvg.PositionList != null && configRGBAvg.PositionList.Length != 0)
			{
				int num = 140;
				if (configRGBAvg.PositionList.Length > num)
				{
					Log.Error("contentanalysis", $"Max PositionList lenth is ${num}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "IsVaildConfig", 79);
					configRGBAvg.Dump();
					return false;
				}
				for (int i = 0; i < configRGBAvg.PositionList.Length; i++)
				{
					if (configRGBAvg.PositionList[i].x < 0 || configRGBAvg.PositionList[i].x > HwCondition.FullWidth)
					{
						Log.Error("contentanalysis", "invalid x position.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "IsVaildConfig", 87);
						configRGBAvg.PositionList[i].Dump(i);
						return false;
					}
					if (configRGBAvg.PositionList[i].y < 0 || configRGBAvg.PositionList[i].y > HwCondition.FullHeight)
					{
						Log.Error("contentanalysis", "invalid y position.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "IsVaildConfig", 93);
						configRGBAvg.PositionList[i].Dump(i);
						return false;
					}
				}
				Log.Debug("contentanalysis", $"PositionList len: {configRGBAvg.PositionList.Length}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "IsVaildConfig", 98);
			}
			return true;
		}

		public bool Initialize(ConfigRGBAvg configRGBAvg)
		{
			Log.Debug("contentanalysis", "HwCaptureControl Initialize", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "Initialize", 105);
			if (RectInfoDic != null)
			{
				RectInfoDic.Clear();
				RectInfoDic = null;
			}
			RectInfoDic = new Dictionary<int, RectInfo>();
			CurIndex = 0;
			IsFisrtCaptureDone = false;
			if (!IsVaildConfig(configRGBAvg))
			{
				Log.Error("contentanalysis", "Fail Initialize.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "Initialize", 118);
				return false;
			}
			if (configRGBAvg.CpatureType == Enums.CAPTURE_TYPE.HW_CAPTURE_CUSTOM)
			{
				for (int i = 0; i < configRGBAvg.PositionList.Length; i++)
				{
					configRGBAvg.PositionList[i].Dump(i);
					int num = configRGBAvg.PositionList[i].x;
					int num2 = configRGBAvg.PositionList[i].y;
					if (num + HwCondition.CaptureWidth > HwCondition.FullWidth)
					{
						num = HwCondition.FullWidth - HwCondition.CaptureWidth;
					}
					else if (num < 0)
					{
						num = 0;
					}
					if (num2 + HwCondition.CaptureHeight > HwCondition.FullHeight)
					{
						num2 = HwCondition.FullHeight - HwCondition.CaptureHeight;
					}
					else if (num2 < 0)
					{
						num2 = 0;
					}
					RectInfo value = new RectInfo
					{
						index = i,
						isCompleted = false,
						imgWidth = HwCondition.FullWidth,
						imgHeight = HwCondition.FullHeight,
						x = num,
						y = num2,
						w = HwCondition.CaptureWidth,
						h = HwCondition.CaptureHeight,
						rgb = new RGB()
					};
					RectInfoDic.Add(i, value);
				}
			}
			else
			{
				RowOffset = HwCondition.FullWidth / configRGBAvg.NumOfColumn;
				ColumnOffset = HwCondition.FullHeight / configRGBAvg.NumOfRow;
				int num3 = 0;
				for (int j = 0; j < configRGBAvg.NumOfRow; j++)
				{
					for (int k = 0; k < configRGBAvg.NumOfColumn; k++)
					{
						RectInfo rectInfo = new RectInfo
						{
							index = num3,
							isCompleted = false,
							imgWidth = HwCondition.FullWidth,
							imgHeight = HwCondition.FullHeight,
							x = RowOffset * k + (RowOffset - HwCondition.CaptureWidth) / 2,
							y = ColumnOffset * j + (ColumnOffset - HwCondition.CaptureHeight) / 2,
							w = HwCondition.CaptureWidth,
							h = HwCondition.CaptureHeight,
							rgb = new RGB()
						};
						rectInfo.Dump();
						RectInfoDic.Add(num3, rectInfo);
						num3++;
					}
				}
			}
			return true;
		}

		public void Terminate()
		{
			if (RectInfoDic != null)
			{
				RectInfoDic.Clear();
				RectInfoDic = null;
			}
			CurIndex = 0;
			IsFisrtCaptureDone = false;
			HwCondition = null;
			Log.Error("contentanalysis", "Terminate", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "Terminate", 204);
		}

		public bool GetRGBAvg(ConfigRGBAvg configRGBAvg, out Dictionary<int, RectInfo> resultRectInfoDic, out List<RectInfo> resultParialRectInfos)
		{
			bool result = true;
			Log.Debug("contentanalysis", "start GetRGBAvg", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 213);
			resultRectInfoDic = null;
			resultParialRectInfos = null;
			if (RectInfoDic == null)
			{
				Log.Debug("contentanalysis", "RectInfoDic is null", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 218);
				if (HwCondition != null && HwCondition.Delay > 0)
				{
					Thread.Sleep(IsLowInputLag ? (HwCondition.Delay * 2) : HwCondition.Delay);
				}
				return false;
			}
			if (CurIndex >= RectInfoDic?.Count)
			{
				CurIndex = 0;
			}
			if (CurIndex == 0)
			{
				long num = DateTime.Now.Ticks / 10000 - StartTime;
				if (num > 0)
				{
					Log.Debug("contentanalysis", $"[GetRGBAvg] total time spent. {num}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 236);
				}
				StartTime = DateTime.Now.Ticks / 10000;
			}
			int curIndex = CurIndex;
			for (int i = 0; i < HwCondition.BlockCount && !(curIndex + i >= RectInfoDic?.Count); i++)
			{
				Log.Debug("contentanalysis", $"[GetRGBAvg] i: {i}, index: {curIndex + i}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 248);
				bool flag = interopCtrl.SetRgbMeasurePosition(i, RectInfoDic[curIndex + i].x, RectInfoDic[curIndex + i].y);
				if (!flag)
				{
					result = false;
					Log.Debug("contentanalysis", $"SetRgbMeasurePosition error ret: {flag}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 253);
				}
			}
			if (HwCondition.Delay > 0)
			{
				Thread.Sleep(IsLowInputLag ? (HwCondition.Delay * 2) : HwCondition.Delay);
			}
			curIndex = CurIndex;
			for (int j = 0; j < HwCondition.BlockCount && curIndex + j < RectInfoDic.Count; j++)
			{
				global::Interop.Libraries.VEPPIRgbMeasure_t info;
				bool ggbMeasurePixel = interopCtrl.GetGgbMeasurePixel(j, out info);
				if (!ggbMeasurePixel)
				{
					result = false;
					Log.Debug("contentanalysis", $"GetGgbMeasurePixel error ret: {ggbMeasurePixel}, color: {info}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 273);
				}
				else
				{
					if (info.mean_R > 1023 || info.mean_G > 1023 || info.mean_B > 1023)
					{
						Log.Debug("contentanalysis", $"color overflow R: {info.mean_R}, G: {info.mean_G}, B: {info.mean_B}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 279);
					}
					else
					{
						RectInfoDic[curIndex + j].rgb = new RGB(info.mean_R, info.mean_G, info.mean_B);
					}
					RectInfoDic[curIndex + j].isCompleted = true;
					RectInfoDic[curIndex + j]?.rgb?.Dump();
					bool flag2 = info.lowInputLagOnOff == 1;
					if (IsLowInputLag != flag2)
					{
						Log.Error("contentanalysis", $"lowInputLagOnOff. color.lowInputLagOnOff: {info.lowInputLagOnOff}, pre: {IsLowInputLag}, post: {flag2}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 298);
					}
					IsLowInputLag = flag2;
				}
				if (!IsFisrtCaptureDone && curIndex + j >= RectInfoDic.Count - 1)
				{
					Log.Debug("contentanalysis", "set IsFisrtCaptureDone true.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/HwCaptureControl.cs", "GetRGBAvg", 305);
					IsFisrtCaptureDone = true;
				}
				if (IsFisrtCaptureDone)
				{
					resultRectInfoDic = new Dictionary<int, RectInfo>(RectInfoDic);
				}
				if (resultParialRectInfos == null)
				{
					resultParialRectInfos = new List<RectInfo>();
				}
				resultParialRectInfos.Add(RectInfoDic[curIndex + j]);
			}
			CurIndex += HwCondition.BlockCount;
			return result;
		}
	}
	internal class SwCaptureControl
	{
		private Dictionary<int, ConvertJob> ConvertJobDic = null;

		private Dictionary<int, RectInfo> RectInfoDic = null;

		public byte[] yBuf = null;

		public byte[] cBuf = null;

		public byte[] rgbBuf = null;

		public void Initialize()
		{
			Log.Debug("contentanalysis", "SwCaptureControl Initialize", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "Initialize", 22);
			RectInfoDic = new Dictionary<int, RectInfo>();
		}

		public bool GetRGBAvg(ConfigRGBAvg configRGBAvg, out Dictionary<int, RectInfo> resultRectInfoDic)
		{
			int num = 0;
			int num2 = 0;
			bool flag = false;
			resultRectInfoDic = null;
			Log.Debug("contentanalysis", "start GetRGBAvg", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 33);
			IntPtr intPtr = Marshal.AllocHGlobal(518400);
			IntPtr intPtr2 = Marshal.AllocHGlobal(518400);
			global::Interop.Libraries.secvideo_capture_param structure = new global::Interop.Libraries.secvideo_capture_param
			{
				pYAddr = intPtr,
				pCAddr = intPtr2,
				uYSize = 518400u,
				uCSize = 518400u,
				ret_width = 0,
				ret_height = 0
			};
			IntPtr intPtr3 = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
			Marshal.StructureToPtr(structure, intPtr3, fDeleteOld: false);
			int num3 = VideoCaptureInterop.VideoCapture.secvideo_api_capture_video_main(960, 540, intPtr3);
			Log.Debug("contentanalysis", $"secvideo_api_capture_video_main ret: ${num3}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 57);
			if (num3 < 0)
			{
				Log.Debug("contentanalysis", "Fail secvideo_api_capture", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 64);
				Marshal.FreeHGlobal(intPtr3);
				Marshal.FreeHGlobal(intPtr);
				Marshal.FreeHGlobal(intPtr2);
				return false;
			}
			structure = Marshal.PtrToStructure<global::Interop.Libraries.secvideo_capture_param>(intPtr3);
			num = structure.ret_width;
			num2 = structure.ret_height;
			if (num == 0 || num2 == 0)
			{
				Log.Debug("contentanalysis", $"invalid width: {num}, height: {num2}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 77);
				return false;
			}
			int num4 = num * num2;
			if (num4 > 518400)
			{
				Log.Debug("contentanalysis", $"imgSize:{num4} overflow. width: {num}, height: {num2}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 84);
				return false;
			}
			Log.Debug("contentanalysis", $"[capture image] width: {num}, height: {num2}, imgSize: {num4}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 88);
			yBuf = new byte[num4];
			cBuf = new byte[num4];
			try
			{
				Marshal.Copy(intPtr, yBuf, 0, num4);
				Marshal.Copy(intPtr2, cBuf, 0, num4);
			}
			catch (Exception ex)
			{
				Log.Debug("contentanalysis", "exception err: " + ex.Message, "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 98);
				return false;
			}
			Marshal.FreeHGlobal(intPtr3);
			Marshal.FreeHGlobal(intPtr);
			Marshal.FreeHGlobal(intPtr2);
			rgbBuf = new byte[num4 * 4];
			if (ConvertJobDic == null)
			{
				ConvertJobDic = new Dictionary<int, ConvertJob>();
			}
			else
			{
				ConvertJobDic.Clear();
			}
			int num5 = 0;
			int num6 = 0;
			int num7 = num4 / 8;
			if (num7 % 2 != 0)
			{
				num7++;
			}
			Thread[] thList = new Thread[8];
			for (int i = 0; i < 8; i++)
			{
				num5 = i * num7;
				ConvertJob value = new ConvertJob(i, num, num4, num5, i switch
				{
					7 => num4, 
					0 => num7, 
					_ => num5 + num7, 
				}, isComplete: false);
				ConvertJobDic.Add(i, value);
			}
			foreach (ConvertJob job in ConvertJobDic.Values)
			{
				Thread thread = new Thread((ParameterizedThreadStart)delegate
				{
					thList[job.Index] = new Thread((ParameterizedThreadStart)delegate
					{
						Thread_ConvertYUV2RGB(job);
					});
					thList[job.Index].Start();
				});
				thread.Start();
				thread.Join();
			}
			Thread.Sleep(10);
			Dictionary<int, ConvertJob> dictionary;
			while (true)
			{
				bool flag2 = true;
				dictionary = new Dictionary<int, ConvertJob>(ConvertJobDic);
				foreach (ConvertJob value2 in dictionary.Values)
				{
					if (!value2.IsComplete)
					{
						flag2 = false;
						break;
					}
				}
				if (flag2)
				{
					break;
				}
				Thread.Sleep(1);
			}
			dictionary.Clear();
			dictionary = null;
			for (int num8 = 0; num8 < 8; num8++)
			{
				thList[num8]?.Join();
			}
			Log.Debug("contentanalysis", "finish convert YUV to RGB", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 179);
			flag = CalculateRGBAvg(num, num2, configRGBAvg);
			if (flag)
			{
				resultRectInfoDic = new Dictionary<int, RectInfo>(RectInfoDic);
			}
			else
			{
				Log.Debug("contentanalysis", "Fail to CalculateRGBAvg.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 197);
			}
			RectInfoDic.Clear();
			Log.Debug("contentanalysis", $"finish GetRGBAvg. ret: {flag}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "GetRGBAvg", 200);
			return flag;
		}

		public void Terminate()
		{
			Log.Error("contentanalysis", "Terminate", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "Terminate", 206);
		}

		private bool CalculateRGBAvg(int imgWidth, int imgHeight, ConfigRGBAvg configRGBAvg)
		{
			Log.Debug("contentanalysis", $"CalculateRGBAvg. imgWidth: {imgWidth}, imgHeight: {imgHeight}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "CalculateRGBAvg", 211);
			if (rgbBuf == null || rgbBuf.Length == 0 || imgWidth == 0 || imgHeight == 0)
			{
				Log.Debug("contentanalysis", "invalid param. skip calculate.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "CalculateRGBAvg", 215);
				return false;
			}
			int num = imgWidth / configRGBAvg.NumOfColumn;
			int num2 = imgHeight / configRGBAvg.NumOfRow;
			int num3 = 0;
			int num4 = 0;
			if (num * configRGBAvg.NumOfColumn < imgWidth)
			{
				num3 = imgWidth - num * configRGBAvg.NumOfColumn;
			}
			if (num2 * configRGBAvg.NumOfRow < imgHeight)
			{
				num4 = imgHeight - num2 * configRGBAvg.NumOfRow;
			}
			int num5 = 0;
			for (int i = 0; i < configRGBAvg.NumOfRow; i++)
			{
				for (int j = 0; j < configRGBAvg.NumOfColumn; j++)
				{
					int w = num;
					int h = num2;
					if (i == configRGBAvg.NumOfRow - 1)
					{
						h = num2 + num4;
					}
					if (j == configRGBAvg.NumOfColumn - 1)
					{
						w = num + num3;
					}
					RectInfo value = new RectInfo(num5, _isCompleted: false, imgWidth, imgHeight, j * num, i * num2, w, h, new RGB());
					RectInfoDic.Add(num5, value);
					num5++;
				}
			}
			Thread[] thList = new Thread[8];
			Dictionary<int, Dictionary<int, RectInfo>> dictionary = new Dictionary<int, Dictionary<int, RectInfo>>();
			int num6 = RectInfoDic.Count / 8 + 1;
			int num7 = RectInfoDic.Count;
			for (int k = 0; k < 8; k++)
			{
				Dictionary<int, RectInfo> dictionary2 = new Dictionary<int, RectInfo>();
				for (int l = 0; l < num6; l++)
				{
					if (num7 == 0)
					{
						break;
					}
					dictionary2.Add(l, RectInfoDic[k * num6 + l]);
					num7--;
				}
				dictionary.Add(k, dictionary2);
			}
			foreach (KeyValuePair<int, Dictionary<int, RectInfo>> item in dictionary)
			{
				int jobIndex = item.Key;
				Dictionary<int, RectInfo> jobObj = item.Value;
				Thread thread = new Thread((ParameterizedThreadStart)delegate
				{
					thList[jobIndex] = new Thread((ParameterizedThreadStart)delegate
					{
						Thread_CalculateRGBAvg(jobObj);
					});
					thList[jobIndex].Start();
				});
				thread.Start();
				thread.Join();
			}
			Thread.Sleep(10);
			while (true)
			{
				bool flag = true;
				foreach (RectInfo value2 in RectInfoDic.Values)
				{
					if (!value2.isCompleted)
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					break;
				}
				Thread.Sleep(1);
			}
			Log.Debug("contentanalysis", "All job is completed.", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "CalculateRGBAvg", 301);
			foreach (KeyValuePair<int, Dictionary<int, RectInfo>> item2 in dictionary)
			{
				thList[item2.Key]?.Join();
			}
			return true;
		}

		private bool Thread_ConvertYUV2RGB(ConvertJob job)
		{
			try
			{
				int num = 0;
				int num2 = 0;
				int num3 = job.JobOffset % job.ImgWidth;
				int num4 = job.JobOffset / job.ImgWidth;
				bool flag = num4 % 2 == 0;
				if (job.JobOffset < job.ImgWidth)
				{
					num = job.JobOffset;
					num2 = num;
				}
				else if (flag)
				{
					num = job.JobOffset;
					num2 = ((num3 != 0) ? (num4 * job.ImgWidth / 2 + num3) : (num / 2));
				}
				else
				{
					num = (num4 + 1) * job.ImgWidth;
					num2 = num / 2;
				}
				int num5 = num;
				int num6 = num2;
				while (num5 < job.JobSize)
				{
					int[] array = new int[4]
					{
						num5,
						num5 + 1,
						job.ImgWidth + num5,
						job.ImgWidth + num5 + 1
					};
					int num7 = cBuf[num6] - 128;
					int num8 = cBuf[num6 + 1] - 128;
					int num9 = (int)(1.772f * (float)num8);
					int num10 = (int)(0.344f * (float)num8 + 0.714f * (float)num7);
					int num11 = (int)(1.402f * (float)num7);
					for (int i = 0; i < 4 && array[i] * 4 < job.ImgSize * 4; i++)
					{
						rgbBuf[array[i] * 4] = ConvertHelper.Clamp(yBuf[array[i]] + num9);
						rgbBuf[array[i] * 4 + 1] = ConvertHelper.Clamp(yBuf[array[i]] - num10);
						rgbBuf[array[i] * 4 + 2] = ConvertHelper.Clamp(yBuf[array[i]] + num11);
						bool flag2 = true;
						rgbBuf[array[i] * 4 + 3] = byte.MaxValue;
					}
					if (num5 != 0 && (num5 + 2) % job.ImgWidth == 0)
					{
						num5 += job.ImgWidth;
					}
					num5 += 2;
					num6 += 2;
				}
				ConvertJobDic[job.Index].IsComplete = true;
				return true;
			}
			catch (Exception ex)
			{
				ConvertJobDic[job.Index].IsComplete = true;
				Log.Debug("contentanalysis", $"[jobIndex: {job.Index}] Fail to ConvertYUV2RGB. err: {ex.Message}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Tizen.TV.System.ContentAnalysis/Controls/SwCaptureControl.cs", "Thread_ConvertYUV2RGB", 395);
				return false;
			}
		}

		private void Thread_CalculateRGBAvg(Dictionary<int, RectInfo> rectInfoDic)
		{
			foreach (RectInfo value in rectInfoDic.Values)
			{
				RGB rGB = new RGB();
				long num = value.y * value.imgWidth * 4 + value.x * 4;
				for (int i = 0; i < value.h; i++)
				{
					for (int j = 0; j < value.w; j++)
					{
						rGB.sumR += rgbBuf[num];
						rGB.sumG += rgbBuf[num + 1];
						rGB.sumB += rgbBuf[num + 2];
						num += 4;
					}
					num += (value.imgWidth - value.w) * 4;
				}
				rGB.CalculateAvg(value.w * value.h);
				RectInfoDic[value.index].isCompleted = true;
				RectInfoDic[value.index].rgb = rGB;
			}
		}
	}
}
namespace Tizen.TV.System.ContentAnalysis.Configs
{
	internal class Constants
	{
		public const string LOG_TAG = "contentanalysis";

		public const string PACKAGE_NAME = "Tizen.TV.System.ContentAnalysis";

		public const string FILELOG_NAME = "/run/user/5001/contentanalysisLog.txt";

		public const string PRIVILEGE = "http://developer.samsung.com/privilege/contentanalysis";

		public const string PRIVILEGE_PLATFORM = "http://tizen.org/privilege/internal/default/platform";

		public const int MIN_NUM_OF_ROW = 1;

		public const int MAX_NUM_OF_ROW = 10;

		public const int MIN_NUM_OF_COLUMN = 1;

		public const int MAX_NUM_OF_COLUMN = 14;

		public const int DEFAULT_NUM_OF_ROW = 4;

		public const int DEFAULT_NUM_OF_COLUMN = 6;

		public const int RGB_RESOLUTION = 8;

		public const int CAPTURE_WIDTH = 960;

		public const int CAPTURE_HEIGHT = 540;

		public const int CAPTURE_QUALITY = 100;

		public const int BYTE_PER_PIXEL = 4;

		public const string CAPTRUE_FILE_PATH = "/tmp/contentanalysis/capture.jpg";

		public const int NUM_OF_JOB_THREAD = 8;

		public const int THREAD_COMPLTE_DEFAULT_WAIT_TIME = 10;

		public const int RECT_INFOS_INVOKE_INTERVAL = 80;
	}
	public class Enums
	{
		public enum RET_TYPE
		{
			SUCCESS = 0,
			ERR = -1,
			ERR_NOT_SUPPORT = -2
		}

		public enum CAPTURE_TYPE
		{
			SW_CAPTURE,
			HW_CAPTURE,
			HW_CAPTURE_CUSTOM,
			AUTO_CAPTURE
		}
	}
}
namespace Tizen.TV.System.ContentAnalysis.InteropControl
{
	internal class InteropControl
	{
		private bool usePPI = true;

		public InteropControl()
		{
			usePPI = IsUsePPI();
			Log.Error("contentanalysis", $"IsUsePPI: {usePPI}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Interop/InteropControl.cs", ".ctor", 14);
		}

		private bool IsUsePPI()
		{
			int num = -1;
			try
			{
				num = VideoEnhanceInterop.VideoEnhance.ppi_ve_get_rgb_measure_condition(out var _);
			}
			catch (Exception arg)
			{
				Log.Error("contentanalysis", $"fail to call ppi. err: {arg}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Interop/InteropControl.cs", "IsUsePPI", 26);
				return false;
			}
			return num >= 0;
		}

		public bool GetRGBMeasureCondition(out global::Interop.Libraries.VEPPIRgbMeasureInfo_t measureCondition)
		{
			int num = -1;
			measureCondition = default(global::Interop.Libraries.VEPPIRgbMeasureInfo_t);
			try
			{
				num = ((!usePPI) ? VideoEnhanceInterop.VideoEnhance.ve_api_get_rgb_measure_condition(out measureCondition) : VideoEnhanceInterop.VideoEnhance.ppi_ve_get_rgb_measure_condition(out measureCondition));
			}
			catch (Exception arg)
			{
				Log.Error("contentanalysis", $"fail GetRGBMeasureCondition. err: {arg}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Interop/InteropControl.cs", "GetRGBMeasureCondition", 50);
				return false;
			}
			return num >= 0;
		}

		public bool SetRgbMeasurePosition(int idx, int x, int y)
		{
			int num = -1;
			try
			{
				num = ((!usePPI) ? VideoEnhanceInterop.VideoEnhance.ve_api_set_rgb_measure_position(idx, x, y) : VideoEnhanceInterop.VideoEnhance.ppi_ve_set_rgb_measure_position(idx, x, y));
			}
			catch (Exception arg)
			{
				Log.Error("contentanalysis", $"fail SetRgbMeasurePosition. err: {arg}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Interop/InteropControl.cs", "SetRgbMeasurePosition", 72);
				return false;
			}
			return num >= 0;
		}

		public bool GetGgbMeasurePixel(int idx, out global::Interop.Libraries.VEPPIRgbMeasure_t info)
		{
			int num = -1;
			info = default(global::Interop.Libraries.VEPPIRgbMeasure_t);
			try
			{
				num = ((!usePPI) ? VideoEnhanceInterop.VideoEnhance.ve_api_get_rgb_measure_pixel(idx, out info) : VideoEnhanceInterop.VideoEnhance.ppi_ve_get_rgb_measure_pixel(idx, out info));
			}
			catch (Exception arg)
			{
				Log.Error("contentanalysis", $"fail GetGgbMeasurePixel. err: {arg}", "/home/abuild/rpmbuild/BUILD/csapi-tv-system-contentanalysis-9.9.4.3/Tizen.TV.System.ContentAnalysis/Interop/InteropControl.cs", "GetGgbMeasurePixel", 95);
				return false;
			}
			return num >= 0;
		}
	}
}
