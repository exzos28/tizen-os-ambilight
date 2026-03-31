using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using Common.Platforms;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Org.BouncyCastle.X509;
using Tizen.Applications;
using Tizen.Applications.Notifications;
using Tizen.Network.Connection;
using Tizen.Security.SecureRepository;
using Tizen.System;
using Tizen.TV;
using Tizen.TV.Service.Sso;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETStandard,Version=v2.0", FrameworkDisplayName = ".NET Standard 2.0")]
[assembly: AssemblyCompany("Common")]
[assembly: AssemblyConfiguration("FinalTV")]
[assembly: AssemblyFileVersion("1.8.55.0")]
[assembly: AssemblyInformationalVersion("1.8.55+50a0a7e0ea5077bc142763b4588f4446d2f1dc9c")]
[assembly: AssemblyProduct("Common")]
[assembly: AssemblyTitle("Common")]
[assembly: AssemblyVersion("1.8.55.0")]
namespace Common
{
	public class AWSConnector
	{
		public static readonly AWSConnector Instance = new AWSConnector();

		private AmazonS3Client _s3Client = null;

		private AWSCredentials _cred = null;

		public AWSCredentials Credential
		{
			get
			{
				return _cred;
			}
			set
			{
				//IL_0029: Unknown result type (might be due to invalid IL or missing references)
				//IL_002f: Expected O, but got Unknown
				//IL_003c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0046: Expected O, but got Unknown
				if (value != null)
				{
					_cred = value;
					BasicAWSCredentials val = new BasicAWSCredentials(_cred.AccessKey, _cred.SecretKey);
					_s3Client = new AmazonS3Client((AWSCredentials)(object)val, _cred.Region);
				}
			}
		}

		private AWSConnector()
		{
		}

		public async Task UploadFile(string fileToUploadAbsolutePath, string toFolder, string toBucket)
		{
			if (Credential == null)
			{
				Logger.Instance.Log("AWSConnector::UploadFile - No credentials specified.");
				return;
			}
			if (!File.Exists(fileToUploadAbsolutePath))
			{
				Logger.Instance.Log("AWSConnector::UploadFile - File does not exist: " + fileToUploadAbsolutePath);
				return;
			}
			string fileName = toFolder;
			fileName += "/";
			int pos = fileToUploadAbsolutePath.LastIndexOf(Path.DirectorySeparatorChar);
			if (pos == -1)
			{
				Logger.Instance.Log("AWSConnector::UploadFile - Failed to parse file name: " + fileToUploadAbsolutePath);
				return;
			}
			fileName += fileToUploadAbsolutePath.Substring(pos + 1);
			try
			{
				PutObjectRequest request = new PutObjectRequest
				{
					BucketName = toBucket,
					Key = fileName,
					InputStream = File.OpenRead(fileToUploadAbsolutePath)
				};
				PutObjectResponse response = await _s3Client.PutObjectAsync(request, default(CancellationToken));
				if (((AmazonWebServiceResponse)response).HttpStatusCode != HttpStatusCode.OK)
				{
					Logger.Instance.Log("AWSConnector::UploadFile - Unable to upload file, http status code: " + ((AmazonWebServiceResponse)response).HttpStatusCode);
				}
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("AWSConnector::UploadFile - Exception: " + ex);
			}
		}
	}
	public class AWSCredentials
	{
		private uint[] _a = null;

		private uint[] _za = null;

		private uint[] _s = null;

		private uint[] _zs = null;

		private const string _scrap = "\\n\\\r\nMIIDVjCCAj6gAwIBAgIJAMp0kYVDHTQxMA0GCSqGSIb3DQEBCwUAMEAxCzAJBgNV\\n\\\r\nBAYTAnVzMQswCQYDVQQIDAJjYTESMBAGA1UEBwwJUGFsbyBBbHRvMRAwDgYDVQQK\\n\\\r\nDAdTaWduaWZ5MB4XDTE5MTEwNDIwNDcxMFoXDTIwMTEwMzIwNDcxMFowQDELMAkG\\n\\\r\nA1UEBhMCdXMxCzAJBgNVBAgMAmNhMRIwEAYDVQQHDAlQYWxvIEFsdG8xEDAOBgNV\\n\\\r\nBAoMB1NpZ25pZnkwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCyusTx\\n\\\r\nCLBGzkGwMxjWgeTWyaevutGUiAWnjM/vByDA5U2vz62MpCkb8H+mhnZqVUbnWAgq\\n\\\r\nllv3NnWhEvCPWl7aZgokdaNJNunKmtRDmLY4zf71VSLBwCKU+KJfO+hD468sp3sy\\n\\\r\n/xRlGPN3U/hOMaaC4zwHZxlQGy7oNaaYEozdhr6rUchxc6FG/o0OtU3Y3VHphI8E\\n\\\r\nfVa8TN1ttHDk0n/qNDAlcCDvVrRjGIsakz4/DF5M6e5EUiYfyu4oHhHKDoEbKaca\\n\\\r\neT6K3D6flesZ8txbnQdMkoajrdV8npakfCLaySJHJUfPNpCWlc/MRT9sPjVplLsZ\\n\\\r\nMM9KAjjl2QZy+8qhAgMBAAGjUzBRMB0GA1UdDgQWBBQ0GCH7Skn0SJ8mvcU32Ulw\\n\\\r\nMgQmLTAfBgNVHSMEGDAWgBQ0GCH7Skn0SJ8mvcU32UlwMgQmLTAPBgNVHRMBAf8E\\n\\\r\nBTADAQH/MA0GCSqGSIb3DQEBCwUAA4IBAQBIpY3lxy+uiT52Zc5OUuFAD+KyVrNw\\n\\\r\na7OXTuYw/iImX5Th6D3qbv2H/7sh/edtI0j5epIl6eV82olFIDxZmsgSejyozje6\\n\\\r\n/sQHklAEiMdB9HSvhxEd+2FPy8192URbTc5GJHJj+w+laaBeSF9pmDgESWgBbRtm\\n\\\r\nDWlCS+Hv/8bb/27sfb7e53oSOg4vVpkKqGI0Ps7yqbgkJ9TBEutq/jx2pfMcen6t\\n\\\r\nBqCc/e+nONLJA7cFtprQbjmMPTG94GDieYqFVfVGrCx/UeSSZ2kuTgr4iJU3Un2j\\n\\\r\nQ5ijSQwtgcwsPOIFe17/2L1DNPpf0c2fm4zT3RfE74s5oc/d7LqLzMk9";

		public RegionEndpoint Region { get; set; }

		public string AccessKey => GetAccessKey();

		public string SecretKey => GetSecretKey();

		public AWSCredentials(string accessKey, string secretKey)
		{
			EncryptCredentials(accessKey, secretKey);
		}

		public AWSCredentials(uint[] a, uint[] za, uint[] s, uint[] zs)
		{
			_a = a;
			_za = za;
			_s = s;
			_zs = zs;
		}

		private void EncryptCredentials(string accessKey, string secretKey)
		{
		}

		private string GetAccessKey()
		{
			MD5 mD = MD5.Create();
			byte[] array = mD.ComputeHash(Encoding.ASCII.GetBytes("\\n\\\r\nMIIDVjCCAj6gAwIBAgIJAMp0kYVDHTQxMA0GCSqGSIb3DQEBCwUAMEAxCzAJBgNV\\n\\\r\nBAYTAnVzMQswCQYDVQQIDAJjYTESMBAGA1UEBwwJUGFsbyBBbHRvMRAwDgYDVQQK\\n\\\r\nDAdTaWduaWZ5MB4XDTE5MTEwNDIwNDcxMFoXDTIwMTEwMzIwNDcxMFowQDELMAkG\\n\\\r\nA1UEBhMCdXMxCzAJBgNVBAgMAmNhMRIwEAYDVQQHDAlQYWxvIEFsdG8xEDAOBgNV\\n\\\r\nBAoMB1NpZ25pZnkwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCyusTx\\n\\\r\nCLBGzkGwMxjWgeTWyaevutGUiAWnjM/vByDA5U2vz62MpCkb8H+mhnZqVUbnWAgq\\n\\\r\nllv3NnWhEvCPWl7aZgokdaNJNunKmtRDmLY4zf71VSLBwCKU+KJfO+hD468sp3sy\\n\\\r\n/xRlGPN3U/hOMaaC4zwHZxlQGy7oNaaYEozdhr6rUchxc6FG/o0OtU3Y3VHphI8E\\n\\\r\nfVa8TN1ttHDk0n/qNDAlcCDvVrRjGIsakz4/DF5M6e5EUiYfyu4oHhHKDoEbKaca\\n\\\r\neT6K3D6flesZ8txbnQdMkoajrdV8npakfCLaySJHJUfPNpCWlc/MRT9sPjVplLsZ\\n\\\r\nMM9KAjjl2QZy+8qhAgMBAAGjUzBRMB0GA1UdDgQWBBQ0GCH7Skn0SJ8mvcU32Ulw\\n\\\r\nMgQmLTAfBgNVHSMEGDAWgBQ0GCH7Skn0SJ8mvcU32UlwMgQmLTAPBgNVHRMBAf8E\\n\\\r\nBTADAQH/MA0GCSqGSIb3DQEBCwUAA4IBAQBIpY3lxy+uiT52Zc5OUuFAD+KyVrNw\\n\\\r\na7OXTuYw/iImX5Th6D3qbv2H/7sh/edtI0j5epIl6eV82olFIDxZmsgSejyozje6\\n\\\r\n/sQHklAEiMdB9HSvhxEd+2FPy8192URbTc5GJHJj+w+laaBeSF9pmDgESWgBbRtm\\n\\\r\nDWlCS+Hv/8bb/27sfb7e53oSOg4vVpkKqGI0Ps7yqbgkJ9TBEutq/jx2pfMcen6t\\n\\\r\nBqCc/e+nONLJA7cFtprQbjmMPTG94GDieYqFVfVGrCx/UeSSZ2kuTgr4iJU3Un2j\\n\\\r\nQ5ijSQwtgcwsPOIFe17/2L1DNPpf0c2fm4zT3RfE74s5oc/d7LqLzMk9"));
			uint[] array2 = new uint[5]
			{
				_a[0] ^ _za[4] ^ array[0],
				_a[1] ^ _za[3] ^ array[1],
				_a[2] ^ _za[2] ^ array[2],
				_a[3] ^ _za[1] ^ array[3],
				_a[4] ^ _za[0] ^ array[0]
			};
			byte[] array3 = new byte[array2.Length * 4];
			Buffer.BlockCopy(array2, 0, array3, 0, array3.Length);
			return Encoding.ASCII.GetString(array3);
		}

		private string GetSecretKey()
		{
			MD5 mD = MD5.Create();
			byte[] array = mD.ComputeHash(Encoding.ASCII.GetBytes("\\n\\\r\nMIIDVjCCAj6gAwIBAgIJAMp0kYVDHTQxMA0GCSqGSIb3DQEBCwUAMEAxCzAJBgNV\\n\\\r\nBAYTAnVzMQswCQYDVQQIDAJjYTESMBAGA1UEBwwJUGFsbyBBbHRvMRAwDgYDVQQK\\n\\\r\nDAdTaWduaWZ5MB4XDTE5MTEwNDIwNDcxMFoXDTIwMTEwMzIwNDcxMFowQDELMAkG\\n\\\r\nA1UEBhMCdXMxCzAJBgNVBAgMAmNhMRIwEAYDVQQHDAlQYWxvIEFsdG8xEDAOBgNV\\n\\\r\nBAoMB1NpZ25pZnkwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCyusTx\\n\\\r\nCLBGzkGwMxjWgeTWyaevutGUiAWnjM/vByDA5U2vz62MpCkb8H+mhnZqVUbnWAgq\\n\\\r\nllv3NnWhEvCPWl7aZgokdaNJNunKmtRDmLY4zf71VSLBwCKU+KJfO+hD468sp3sy\\n\\\r\n/xRlGPN3U/hOMaaC4zwHZxlQGy7oNaaYEozdhr6rUchxc6FG/o0OtU3Y3VHphI8E\\n\\\r\nfVa8TN1ttHDk0n/qNDAlcCDvVrRjGIsakz4/DF5M6e5EUiYfyu4oHhHKDoEbKaca\\n\\\r\neT6K3D6flesZ8txbnQdMkoajrdV8npakfCLaySJHJUfPNpCWlc/MRT9sPjVplLsZ\\n\\\r\nMM9KAjjl2QZy+8qhAgMBAAGjUzBRMB0GA1UdDgQWBBQ0GCH7Skn0SJ8mvcU32Ulw\\n\\\r\nMgQmLTAfBgNVHSMEGDAWgBQ0GCH7Skn0SJ8mvcU32UlwMgQmLTAPBgNVHRMBAf8E\\n\\\r\nBTADAQH/MA0GCSqGSIb3DQEBCwUAA4IBAQBIpY3lxy+uiT52Zc5OUuFAD+KyVrNw\\n\\\r\na7OXTuYw/iImX5Th6D3qbv2H/7sh/edtI0j5epIl6eV82olFIDxZmsgSejyozje6\\n\\\r\n/sQHklAEiMdB9HSvhxEd+2FPy8192URbTc5GJHJj+w+laaBeSF9pmDgESWgBbRtm\\n\\\r\nDWlCS+Hv/8bb/27sfb7e53oSOg4vVpkKqGI0Ps7yqbgkJ9TBEutq/jx2pfMcen6t\\n\\\r\nBqCc/e+nONLJA7cFtprQbjmMPTG94GDieYqFVfVGrCx/UeSSZ2kuTgr4iJU3Un2j\\n\\\r\nQ5ijSQwtgcwsPOIFe17/2L1DNPpf0c2fm4zT3RfE74s5oc/d7LqLzMk9"));
			uint[] array2 = new uint[10]
			{
				_s[0] ^ _zs[9] ^ array[0],
				_s[1] ^ _zs[8] ^ array[1],
				_s[2] ^ _zs[7] ^ array[2],
				_s[3] ^ _zs[6] ^ array[3],
				_s[4] ^ _zs[5] ^ array[0],
				_s[5] ^ _zs[4] ^ array[1],
				_s[6] ^ _zs[3] ^ array[2],
				_s[7] ^ _zs[2] ^ array[3],
				_s[8] ^ _zs[1] ^ array[0],
				_s[9] ^ _zs[0] ^ array[1]
			};
			byte[] array3 = new byte[array2.Length * 4];
			Buffer.BlockCopy(array2, 0, array3, 0, array3.Length);
			return Encoding.ASCII.GetString(array3);
		}
	}
	public static class CertificateHelper
	{
		private static X509CertificateParser _certParser = new X509CertificateParser();

		public static bool IsSelfSignedCertificate(X509Certificate2 certificate)
		{
			if (_certParser == null)
			{
				return false;
			}
			X509Certificate val = _certParser.ReadCertificate(certificate.GetRawCertData());
			try
			{
				val.Verify(val.GetPublicKey());
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}
	}
	public class CertificateStore
	{
		public static X509Certificate2[] Certificates { get; } = new X509Certificate2[1]
		{
			new X509Certificate2(Convert.FromBase64String("MIICMjCCAdigAwIBAgIUO7FSLbaxikuXAljzVaurLXWmFw4wCgYIKoZIzj0EAwIw\r\nOTELMAkGA1UEBhMCTkwxFDASBgNVBAoMC1BoaWxpcHMgSHVlMRQwEgYDVQQDDAty\r\nb290LWJyaWRnZTAiGA8yMDE3MDEwMTAwMDAwMFoYDzIwMzgwMTE5MDMxNDA3WjA5\r\nMQswCQYDVQQGEwJOTDEUMBIGA1UECgwLUGhpbGlwcyBIdWUxFDASBgNVBAMMC3Jv\r\nb3QtYnJpZGdlMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEjNw2tx2AplOf9x86\r\naTdvEcL1FU65QDxziKvBpW9XXSIcibAeQiKxegpq8Exbr9v6LBnYbna2VcaK0G22\r\njOKkTqOBuTCBtjAPBgNVHRMBAf8EBTADAQH/MA4GA1UdDwEB/wQEAwIBhjAdBgNV\r\nHQ4EFgQUZ2ONTFrDT6o8ItRnKfqWKnHFGmQwdAYDVR0jBG0wa4AUZ2ONTFrDT6o8\r\nItRnKfqWKnHFGmShPaQ7MDkxCzAJBgNVBAYTAk5MMRQwEgYDVQQKDAtQaGlsaXBz\r\nIEh1ZTEUMBIGA1UEAwwLcm9vdC1icmlkZ2WCFDuxUi22sYpLlwJY81Wrqy11phcO\r\nMAoGCCqGSM49BAMCA0gAMEUCIEBYYEOsa07TH7E5MJnGw557lVkORgit2Rm1h3B2\r\nsFgDAiEA1Fj/C3AN5psFMjo0//mrQebo0eKd3aWRx+pQY08mk48="))
		};
	}
	public static class CryptoUtil
	{
		public static byte[] Hash(string data, string salt = "")
		{
			string s = salt + data;
			SHA256 sHA = SHA256.Create();
			return sHA.ComputeHash(Encoding.ASCII.GetBytes(s));
		}
	}
	public class FileDataStore : IFileDataStore
	{
		private string _path;

		public string Path
		{
			get
			{
				return _path;
			}
			set
			{
				_path = value;
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(value));
			}
		}

		public async Task<bool> Save<T>(T obj)
		{
			Logger.Instance.Log("FileDataStore.Save: " + Path);
			TextWriter writer = null;
			try
			{
				string contentsToWriteToFile = JsonConvert.SerializeObject((object)obj);
				writer = new StreamWriter(Path, append: false);
				await writer.WriteAsync(contentsToWriteToFile);
			}
			finally
			{
				writer?.Close();
			}
			return true;
		}

		public async Task<T> Load<T>() where T : new()
		{
			Logger.Instance.Log("FileDataStore.Load: " + Path);
			if (!File.Exists(Path))
			{
				return new T();
			}
			TextReader reader = null;
			try
			{
				reader = new StreamReader(Path);
				string fileContents = await reader.ReadToEndAsync();
				JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
				{
					NullValueHandling = (NullValueHandling)1
				};
				return JsonConvert.DeserializeObject<T>(fileContents, jsonSerializerSettings);
			}
			finally
			{
				reader?.Close();
			}
		}

		public void Delete()
		{
			Logger.Instance.Log("FileDataStore.Delete: " + Path);
			if (!File.Exists(Path))
			{
				return;
			}
			try
			{
				File.Delete(Path);
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("Unable to delete file: " + Path + " Exception is " + ex);
			}
		}
	}
	public class GenericHttpContent : IHttpContent, IDisposable
	{
		private HttpContent _content = null;

		public GenericHttpContent(HttpContent content)
		{
			_content = content;
		}

		public Task<string> ReadAsStringAsync()
		{
			return _content.ReadAsStringAsync();
		}

		public void Dispose()
		{
			_content?.Dispose();
			_content = null;
		}
	}
	public class GenericHttpClientHandler : IHttpClientHandler, IDisposable
	{
		private HttpClientHandler _handler = new HttpClientHandler();

		public int MaxConnectionsPerServer
		{
			get
			{
				return _handler.MaxConnectionsPerServer;
			}
			set
			{
				_handler.MaxConnectionsPerServer = value;
			}
		}

		public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> ServerCertificateCustomValidationCallback
		{
			get
			{
				return _handler.ServerCertificateCustomValidationCallback;
			}
			set
			{
				_handler.ServerCertificateCustomValidationCallback = value;
			}
		}

		public HttpClientHandler NativeHandler => _handler;

		public void Dispose()
		{
			_handler.Dispose();
		}
	}
	public class GenericHttpHeaderValueCollection<T> : IHttpHeaderValueCollection<T>, ICollection<T>, IEnumerable<T>, IEnumerable where T : class
	{
		private HttpHeaderValueCollection<T> _collection;

		public int Count => _collection.Count;

		public bool IsReadOnly => _collection.IsReadOnly;

		public void Add(T item)
		{
			_collection.Add(item);
		}

		public void Clear()
		{
			_collection.Clear();
		}

		public bool Contains(T item)
		{
			return _collection.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			_collection.CopyTo(array, arrayIndex);
		}

		public IEnumerator<T> GetEnumerator()
		{
			return _collection.GetEnumerator();
		}

		public void ParseAdd(string input)
		{
			_collection.ParseAdd(input);
		}

		public bool Remove(T item)
		{
			return _collection.Remove(item);
		}

		public override string ToString()
		{
			return _collection.ToString();
		}

		public bool TryParseAdd(string input)
		{
			return _collection.TryParseAdd(input);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public GenericHttpHeaderValueCollection(HttpHeaderValueCollection<T> collection)
		{
			_collection = collection;
		}
	}
	public class GenericHttpRequestHeaders : IHttpRequestHeaders
	{
		private HttpRequestHeaders _headers;

		private GenericHttpHeaderValueCollection<MediaTypeWithQualityHeaderValue> _acceptCollection;

		public IHttpHeaderValueCollection<MediaTypeWithQualityHeaderValue> Accept => _acceptCollection;

		public GenericHttpRequestHeaders(HttpRequestHeaders headers)
		{
			_headers = headers;
			_acceptCollection = new GenericHttpHeaderValueCollection<MediaTypeWithQualityHeaderValue>(headers.Accept);
		}

		public void Add(string name, string value)
		{
			_headers.Add(name, value);
		}
	}
	public class GenericHttpClient : IHttpClient, IDisposable
	{
		private HttpClient _httpClient;

		private GenericHttpClientHandler _handler;

		private GenericHttpRequestHeaders _headers;

		public TimeSpan Timeout
		{
			get
			{
				return _httpClient.Timeout;
			}
			set
			{
				_httpClient.Timeout = value;
			}
		}

		public string BaseAddress
		{
			get
			{
				return _httpClient.BaseAddress.ToString();
			}
			set
			{
				_httpClient.BaseAddress = new Uri(value);
			}
		}

		public IHttpRequestHeaders DefaultRequestHeaders => _headers;

		public IHttpClientHandler Handler => _handler;

		public GenericHttpClient()
		{
			_handler = new GenericHttpClientHandler();
			_httpClient = new HttpClient(_handler.NativeHandler);
			_headers = new GenericHttpRequestHeaders(_httpClient.DefaultRequestHeaders);
		}

		public Task<HttpResponseMessage> GetAsync(string requestUri)
		{
			return _httpClient.GetAsync(requestUri);
		}

		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
		{
			return _httpClient.SendAsync(request);
		}

		public Task<Stream> GetStreamAsync(string requestUri)
		{
			return _httpClient.GetStreamAsync(requestUri);
		}

		public void Dispose()
		{
			_httpClient.Dispose();
			_httpClient = null;
			_handler.Dispose();
			_handler = null;
		}
	}
	public interface IFileDataStore
	{
		string Path { get; set; }

		Task<bool> Save<T>(T obj);

		Task<T> Load<T>() where T : new();

		void Delete();
	}
	public interface IHttpContent : IDisposable
	{
		Task<string> ReadAsStringAsync();
	}
	public interface IHttpClientHandler : IDisposable
	{
		int MaxConnectionsPerServer { get; set; }

		Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> ServerCertificateCustomValidationCallback { get; set; }
	}
	public interface IHttpHeaderValueCollection<T> : ICollection<T>, IEnumerable<T>, IEnumerable
	{
	}
	public interface IHttpRequestHeaders
	{
		IHttpHeaderValueCollection<MediaTypeWithQualityHeaderValue> Accept { get; }

		void Add(string name, string value);
	}
	public interface IHttpClient : IDisposable
	{
		TimeSpan Timeout { get; set; }

		IHttpClientHandler Handler { get; }

		string BaseAddress { get; set; }

		IHttpRequestHeaders DefaultRequestHeaders { get; }

		Task<HttpResponseMessage> GetAsync(string requestUri);

		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);

		Task<Stream> GetStreamAsync(string requestUri);
	}
	public delegate void NetworkDisconnectEvent(bool networkConnected, bool internetConnected);
	public delegate void NetworkReconnectEvent(bool networkConnected, bool internetConnected);
	public interface INetworkMonitor
	{
		event NetworkDisconnectEvent NetworkDisconnect;

		event NetworkReconnectEvent NetworkReconnect;

		void Start();

		bool IsNetworkConnected();

		bool IsInternetConnected();
	}
	public class Logger
	{
		public enum LogLevel
		{
			INFO,
			WARNING,
			ERROR
		}

		public class LogLine
		{
			public string Line { get; set; }

			public string Tag { get; set; }

			public int Number { get; set; }
		}

		public static readonly Logger Instance = new Logger();

		private HttpClient _httpClient = null;

		private int _traceId = 0;

		private static string _appName = "";

		private static bool _headerIsWritten = false;

		private static string _logFilePath = "";

		private static string _oldLogFilePath = "";

		private static JsonSerializerSettings _jsonSerializerSettings;

		private static TextWriter _logFileWriter = null;

		public string RemoteConsoleURL { get; set; }

		public LogLevel Level { get; set; } = LogLevel.INFO;

		public string LogFilePath => _logFilePath;

		public string OldLogFilePath => _oldLogFilePath;

		public bool Enabled { get; set; }

		public static void Init(string appName)
		{
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Expected O, but got Unknown
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Expected O, but got Unknown
			//IL_0073: Expected O, but got Unknown
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Expected O, but got Unknown
			_appName = appName;
			string applicationDataFolder = Platform.Instance.ApplicationDataFolder;
			char directorySeparatorChar = Path.DirectorySeparatorChar;
			string text = directorySeparatorChar.ToString();
			string appName2 = _appName;
			directorySeparatorChar = Path.DirectorySeparatorChar;
			string text2 = applicationDataFolder + text + appName2 + directorySeparatorChar;
			_logFilePath = text2 + "log.txt";
			_oldLogFilePath = text2 + "log.old.txt";
			JsonSerializerSettings val = new JsonSerializerSettings();
			val.ContractResolver = (IContractResolver)new DefaultContractResolver
			{
				NamingStrategy = (NamingStrategy)new SnakeCaseNamingStrategy()
			};
			val.Formatting = (Formatting)0;
			val.NullValueHandling = (NullValueHandling)1;
			val.Converters.Add((JsonConverter)new StringEnumConverter(typeof(SnakeCaseNamingStrategy)));
			_jsonSerializerSettings = val;
		}

		~Logger()
		{
			_logFileWriter?.Close();
			_httpClient?.Dispose();
			_httpClient = null;
		}

		public void Log(string msg, LogLevel level = LogLevel.INFO)
		{
			if (!Enabled || level < Level)
			{
				return;
			}
			TextWriter logWriter = GetLogWriter();
			if (logWriter == null)
			{
				return;
			}
			if (!_headerIsWritten)
			{
				if (logWriter != null)
				{
					logWriter.WriteLine("");
					logWriter.WriteLine("-------------------- New log at " + DateTime.Now);
					logWriter.WriteLine("");
				}
				_headerIsWritten = true;
			}
			TimeSpan timeSpan = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
			string[] obj = new string[7]
			{
				Platform.Instance.FrontendVersion,
				"#",
				Platform.Instance.AppVersion,
				"#",
				null,
				null,
				null
			};
			TimeSpan timeSpan2 = timeSpan;
			obj[4] = timeSpan2.ToString();
			obj[5] = ": ";
			obj[6] = msg;
			logWriter.WriteLine(string.Concat(obj));
			logWriter.Flush();
		}

		private static TextWriter GetLogWriter()
		{
			if (_logFilePath.Length == 0)
			{
				return null;
			}
			try
			{
				if (!File.Exists(_logFilePath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
					_logFileWriter = TextWriter.Synchronized(new StreamWriter(_logFilePath, append: true));
				}
				else
				{
					FileInfo fileInfo = new FileInfo(_logFilePath);
					if (fileInfo.Length > 1048576)
					{
						_logFileWriter.Close();
						_logFileWriter = null;
						fileInfo.CopyTo(_oldLogFilePath, overwrite: true);
						fileInfo.Delete();
					}
					if (_logFileWriter == null)
					{
						_logFileWriter = TextWriter.Synchronized(new StreamWriter(_logFilePath, append: true));
					}
				}
				return _logFileWriter;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
	public delegate void LoggerFoundDelegate(string ip);
	public interface ILoggerDiscoverer
	{
		event LoggerFoundDelegate LoggerFound;

		void Start();

		void Stop();
	}
	public class LoggerDiscoverer : ILoggerDiscoverer
	{
		private const string PingSentence = "[Discover LogServer] Ping";

		private const string PongSentence = "[Discover LogServer] Pong";

		private readonly UdpClient _updClient;

		private readonly int _port;

		private bool _running;

		private string _pingIp;

		public event LoggerFoundDelegate LoggerFound;

		public LoggerDiscoverer()
		{
			_port = 9876;
			_updClient = new UdpClient();
			try
			{
				_updClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
				_updClient.Client.ReceiveTimeout = 5000;
			}
			catch
			{
			}
		}

		public void Start()
		{
			_running = true;
			_pingIp = "";
			Task.Run(delegate
			{
				while (_running)
				{
					try
					{
						IPEndPoint remoteEP = new IPEndPoint(0L, 0);
						string text = Encoding.ASCII.GetString(_updClient.Receive(ref remoteEP));
						if (text.StartsWith("[Discover LogServer] Pong"))
						{
							if (PongHasNoUrl(text))
							{
								_pingIp = remoteEP.Address.ToString();
								DoPing();
							}
							else
							{
								string ip = text.Substring("[Discover LogServer] Pong".Length);
								this.LoggerFound?.Invoke(ip);
								_running = false;
							}
						}
					}
					catch
					{
					}
				}
			});
			Task.Run(delegate
			{
				while (_running)
				{
					DoPing();
					Thread.Sleep(5000);
				}
			});
		}

		private static bool PongHasNoUrl(string msg)
		{
			return msg.Length <= "[Discover LogServer] Pong".Length;
		}

		public void Stop()
		{
			_running = false;
		}

		private void DoPing()
		{
			string s = "[Discover LogServer] Ping" + _pingIp;
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			_updClient.Send(bytes, bytes.Length, "255.255.255.255", _port);
			_pingIp = "";
		}
	}
	public class PerformanceCounter
	{
		private Stopwatch _timer = new Stopwatch();

		private long _callCount = 0L;

		private long _printDelay = 0L;

		private long _timeElapsedTotal = 0L;

		private long _lastTimeElapsed = 0L;

		private string _name;

		private bool _print = false;

		private bool _printFPS = false;

		public PerformanceCounter(string name, bool print, bool printFPS = true, long printDelay = 3000L)
		{
			_name = name;
			_print = print;
			_printFPS = printFPS;
			_printDelay = printDelay;
		}

		public void Start(bool forceRestart = false)
		{
			if (_timer.IsRunning)
			{
				if (forceRestart)
				{
					_timer.Restart();
					_lastTimeElapsed = 0L;
				}
			}
			else
			{
				_callCount = 0L;
				_timeElapsedTotal = 0L;
				_lastTimeElapsed = 0L;
				_timer.Restart();
			}
		}

		public void Stop()
		{
			_timer.Stop();
		}

		public double GetAverageFrameDuration()
		{
			return Math.Round((double)_timeElapsedTotal / (double)_callCount, 2, MidpointRounding.ToEven);
		}

		public double GetAverageFramePerSecond()
		{
			return Math.Round(1000.0 / ((double)_timeElapsedTotal / (double)_callCount), 2, MidpointRounding.ToEven);
		}

		public void Tick()
		{
			_callCount++;
			_timeElapsedTotal += _timer.ElapsedMilliseconds - _lastTimeElapsed;
			_lastTimeElapsed = _timer.ElapsedMilliseconds;
			if (_print && _timeElapsedTotal >= _printDelay)
			{
				if (_printFPS)
				{
					Logger.Instance.Log(_name + ": " + GetAverageFramePerSecond() + " fps");
				}
				else
				{
					Logger.Instance.Log(_name + ": " + GetAverageFrameDuration() + " ms");
				}
				_timer.Restart();
				_callCount = 0L;
				_timeElapsedTotal = 0L;
				_lastTimeElapsed = 0L;
			}
		}
	}
	public class ScopeExit : IDisposable
	{
		private Action _callback;

		public ScopeExit(Action callback)
		{
			_callback = callback;
		}

		public void Dispose()
		{
			_callback();
		}
	}
	public static class StringUtils
	{
		public static string ToCamelCase(this string str)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Expected O, but got Unknown
			return (str == null) ? null : new DefaultContractResolver
			{
				NamingStrategy = (NamingStrategy)new CamelCaseNamingStrategy()
			}.GetResolvedPropertyName(str);
		}

		public static string ToSnakeCase(this string str)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Expected O, but got Unknown
			return (str == null) ? null : new DefaultContractResolver
			{
				NamingStrategy = (NamingStrategy)new SnakeCaseNamingStrategy()
			}.GetResolvedPropertyName(str);
		}
	}
	public class Module
	{
		public static IServiceProvider ServiceProvider { get; set; }

		public static void RegisterServices(IServiceCollection collection)
		{
			collection.AddTransient<IHttpClient, GenericHttpClient>();
			collection.AddTransient<IFileDataStore, SecureDataStore>();
			collection.AddSingleton<ILoggerDiscoverer, LoggerDiscoverer>();
			collection.AddSingleton<INetworkMonitor, TizenNetworkMonitor>();
		}
	}
	public class Platform : IPlatform
	{
		public static string AppBackendPrefix;

		public static string AppFrontentPrefix;

		public static IPlatform Instance;

		private readonly TizenDisplayHandler _displayHandler;

		private readonly TizenNotificationManager _notificationManager;

		private readonly TizenGuiEventGenerator _guiEventGenerator;

		private bool _keepAlive = false;

		public SystemState SystemState => _displayHandler.SystemState;

		public string AppVersion => PackageManager.GetPackage(Application.Current.ApplicationInfo.PackageId).Version;

		public string FrontendVersion => PackageManager.GetPackage("kupCeIdDkX").Version;

		public string DeviceMacAddress => ConnectionManager.GetMacAddress((ConnectionType)1);

		public string UniqueDeviceId
		{
			get
			{
				byte[] inArray = CryptoUtil.Hash(DeviceMacAddress, "f74v5rc45v3r0qs1");
				string text = Convert.ToBase64String(inArray);
				text = text.Split(new char[1] { '=' })[0];
				text = text.Replace('+', '-');
				return text.Replace('/', '-');
			}
		}

		public string OSName => "Tizen";

		public string OSVersion
		{
			get
			{
				string result = default(string);
				Information.TryGetValue<string>("http://tizen.org/system/build.string", ref result);
				return result;
			}
		}

		public string Language => new CultureInfo(SystemSettings.LocaleLanguage).EnglishName;

		public int MemorySize => new SystemMemoryUsage().Total / 1024;

		public string DeviceModel
		{
			get
			{
				string result = default(string);
				Information.TryGetValue<string>("http://tizen.org/system/model_name", ref result);
				return result;
			}
		}

		public string DeviceType
		{
			get
			{
				string result = default(string);
				Information.TryGetValue<string>("http://tizen.org/system/device_type", ref result);
				return result;
			}
		}

		public string Ip => ConnectionManager.GetIPAddress((AddressFamily)0).ToString();

		public string PlatformName
		{
			get
			{
				string result = default(string);
				Information.TryGetValue<string>("http://tizen.org/system/platform.name", ref result);
				return result;
			}
		}

		public string UserId => Sso.GetLoginUid();

		public string Country => Environment.SmartHubConfig.Country;

		public bool IsGuiVisible => ApplicationManager.IsRunning("kupCeIdDkX.HueSync");

		public string ApplicationDataFolder
		{
			get
			{
				IEnumerable<Storage> storages = StorageManager.Storages;
				Storage val = storages.Where((Storage s) => (int)s.StorageType == 0).FirstOrDefault();
				return val.GetAbsolutePath((DirectoryType)7);
			}
		}

		public bool KeepAlive
		{
			get
			{
				return _keepAlive;
			}
			set
			{
				bool keepAlive = _keepAlive;
				_keepAlive = value;
				if (_keepAlive != keepAlive)
				{
					this.KeepAliveChanged?.Invoke();
				}
			}
		}

		public event ResumeEvent Resume
		{
			add
			{
				_displayHandler.Resume += value;
			}
			remove
			{
				_displayHandler.Resume -= value;
			}
		}

		public event SleepEvent Sleep
		{
			add
			{
				_displayHandler.Sleep += value;
			}
			remove
			{
				_displayHandler.Sleep -= value;
			}
		}

		public event GuiHiddenEvent GuiHidden
		{
			add
			{
				_guiEventGenerator.GuiHidden += value;
			}
			remove
			{
				_guiEventGenerator.GuiHidden -= value;
			}
		}

		public event GuiVisibleEvent GuiVisible
		{
			add
			{
				_guiEventGenerator.GuiVisible += value;
			}
			remove
			{
				_guiEventGenerator.GuiVisible -= value;
			}
		}

		public event KeepAliveChangedEvent KeepAliveChanged;

		static Platform()
		{
			AppBackendPrefix = "com.lighting.HueSyncService";
			AppFrontentPrefix = "kupCeIdDkX.HueSync";
			Instance = new Platform();
		}

		private Platform()
		{
			_notificationManager = new TizenNotificationManager();
			_displayHandler = new TizenDisplayHandler(this);
			_guiEventGenerator = new TizenGuiEventGenerator(this);
			Task.Run(async delegate
			{
				await Init();
			});
		}

		private async Task Init()
		{
			Logger.Instance.Log("[Platform.Init]");
			await TizenPlatformConfig.Load();
			_displayHandler.Start();
			_guiEventGenerator.Start();
		}

		public void PostNotification(Notification notification)
		{
			_notificationManager.PostNotification(notification);
		}

		public void OnAppControlReceived(AppControlReceivedEventArgs e)
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			string text = "[Platform.OnAppControlReceived] --------------------\n\r    Operation: " + ((AppControl)e.ReceivedAppControl).Operation + "\n    CallerApplicationId: " + e.ReceivedAppControl.CallerApplicationId + "\n    Category: " + ((AppControl)e.ReceivedAppControl).Category + "\n    ComponentId: " + ((AppControl)e.ReceivedAppControl).ComponentId + "\n" + $"    LaunchMode: {((AppControl)e.ReceivedAppControl).LaunchMode}\n" + "    Mime: " + ((AppControl)e.ReceivedAppControl).Mime + "\n" + $"    IsReplyRequest: {e.ReceivedAppControl.IsReplyRequest}\n" + "    Uri: " + ((AppControl)e.ReceivedAppControl).Uri;
			foreach (string key in ((AppControl)e.ReceivedAppControl).ExtraData.GetKeys())
			{
				object arg = ((AppControl)e.ReceivedAppControl).ExtraData.Get(key);
				text += $"    Data: {key} = {arg}\n";
			}
			Logger.Instance.Log(text);
			if (e.ReceivedAppControl.CallerApplicationId == "com.samsung.tv.tvsensor-iot-service")
			{
				KeepAlive = true;
			}
			try
			{
				_notificationManager.ProcessAppControl(((AppControl)e.ReceivedAppControl).ExtraData);
			}
			catch (Exception arg2)
			{
				Logger.Instance.Log($"[OnAppControlReceived] Error processing AppControl {arg2}", Logger.LogLevel.ERROR);
			}
		}

		public void StartFrontend()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			AppControl val = new AppControl();
			val.ApplicationId = AppFrontentPrefix;
			AppControl.SendLaunchRequest(val);
		}
	}
	public class SecureDataStore : IFileDataStore
	{
		private const string Pass = "blddf3jdf388jfgjhdsfjhg67y7";

		private static readonly string AppId;

		private string _alias;

		private int _currentCount;

		public string Path
		{
			get
			{
				return _alias;
			}
			set
			{
				_alias = AppId + " " + System.IO.Path.GetFileNameWithoutExtension(value);
				Init();
			}
		}

		static SecureDataStore()
		{
			AppId = Application.Current.ApplicationInfo.PackageId;
		}

		private void Init()
		{
			List<string> aliasesFromDataManager = GetAliasesFromDataManager();
			List<string> aliasesOfThisStore = FilterAliasesForThisStore(aliasesFromDataManager);
			_currentCount = GetCurrentCountOfAliases(aliasesOfThisStore);
			RemoveAllButCurrent(aliasesOfThisStore);
		}

		private List<string> GetAliasesFromDataManager()
		{
			try
			{
				return DataManager.GetAliases().ToList();
			}
			catch (InvalidOperationException)
			{
			}
			catch (ArgumentException)
			{
			}
			catch (Exception ex3)
			{
				Logger.Instance.Log("[SecureDataStore.GetAliasesFromDataManager] failed for: " + _alias + " " + ex3.Message, Logger.LogLevel.ERROR);
			}
			return new List<string>();
		}

		private void RemoveAllButCurrent(List<string> aliasesOfThisStore)
		{
			if (CurrentExists())
			{
				string text = CurrentAlias();
				Logger.Instance.Log("[SecureDataStore.RemoveAllButCurrent] " + _alias + " toKeep: " + text + " ");
				aliasesOfThisStore.Remove(text);
			}
			foreach (string item in aliasesOfThisStore)
			{
				try
				{
					Logger.Instance.Log("[SecureDataStore.RemoveAllButCurrent] " + _alias + " removing: " + item + " ");
					Manager.RemoveAlias(item);
				}
				catch
				{
					Logger.Instance.Log("[SecureDataStore.RemoveAllButCurrent] " + _alias + " failed removing: " + item + " ", Logger.LogLevel.ERROR);
				}
			}
		}

		private string CurrentAlias()
		{
			return _alias + _currentCount;
		}

		private string NextAlias()
		{
			return _alias + (_currentCount + 1);
		}

		private bool CurrentExists()
		{
			return _currentCount != -1;
		}

		private int GetCurrentCountOfAliases(List<string> aliasesOfThisStore)
		{
			int num = -1;
			foreach (string item in aliasesOfThisStore)
			{
				if (int.TryParse(item.Substring(_alias.Length), out var result) && result > num)
				{
					num = result;
				}
			}
			return num;
		}

		private List<string> FilterAliasesForThisStore(List<string> aliases)
		{
			return aliases.Where((string a) => a.StartsWith(_alias)).ToList();
		}

		public Task<bool> Save<T>(T obj)
		{
			string text = CurrentAlias();
			bool flag = CurrentExists();
			string text2 = NextAlias();
			try
			{
				Logger.Instance.Log("[SecureDataStore.Save] " + text2);
				SaveInDataManager(text2, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject((object)obj)));
				_currentCount++;
				if (flag)
				{
					Manager.RemoveAlias(text);
				}
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[SecureDataStore.Save] failed for: " + text2 + " " + ex.Message, Logger.LogLevel.ERROR);
				Init();
			}
			return Task.FromResult(result: true);
		}

		private static void SaveInDataManager(string alias, byte[] bytes)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Expected O, but got Unknown
			DataManager.Save(alias, bytes, new Policy("blddf3jdf388jfgjhdsfjhg67y7", true));
		}

		public Task<T> Load<T>() where T : new()
		{
			string text = CurrentAlias();
			if (!CurrentExists())
			{
				return Task.FromResult(new T());
			}
			try
			{
				byte[] bytes = DataManager.Get(text, "blddf3jdf388jfgjhdsfjhg67y7");
				string text2 = Encoding.UTF8.GetString(bytes);
				return Task.FromResult(JsonConvert.DeserializeObject<T>(text2));
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[SecureDataStore.Load] '" + _alias + "' failed: " + ex.Message, Logger.LogLevel.ERROR);
				Init();
			}
			return Task.FromResult(new T());
		}

		public void Delete()
		{
			string text = CurrentAlias();
			if (CurrentExists())
			{
				try
				{
					Manager.RemoveAlias(text);
				}
				catch (Exception ex)
				{
					Logger.Instance.Log("[SecureDataStore.Delete] '" + Path + "' failed: " + ex.Message, Logger.LogLevel.ERROR);
				}
				Init();
			}
		}
	}
	public class TizenDisplayEventGenerator
	{
		private const int Interval = 1000;

		private readonly Platform _platform;

		private readonly System.Timers.Timer _timer;

		private SystemState _systemState;

		public event ResumeEvent Resume;

		public event SleepEvent Sleep;

		public TizenDisplayEventGenerator(Platform platform)
		{
			_platform = platform;
			_timer = new System.Timers.Timer();
		}

		public void Start()
		{
			Logger.Instance.Log("[TizenDisplayEventGenerator.Start]");
			_timer.Enabled = true;
			_timer.Interval = 1000.0;
			_timer.AutoReset = true;
			_timer.Elapsed += TimerElapsed;
		}

		public void Stop()
		{
			Logger.Instance.Log("[TizenDisplayEventGenerator.Stop]");
			_timer.Enabled = false;
		}

		private void TimerElapsed(object sender, ElapsedEventArgs e)
		{
			SystemState systemState = _platform.SystemState;
			if (_systemState != systemState)
			{
				Logger.Instance.Log($"[TizenPlatformMonitor.OnTimerCheckDisplayState] {_systemState} -> {systemState}");
				_systemState = systemState;
				InvokeSystemStateEvent(_systemState);
			}
		}

		private void InvokeSystemStateEvent(SystemState state)
		{
			switch (state)
			{
			case SystemState.Normal:
				Logger.Instance.Log("[TizenPlatformMonitor.InvokeSystemStateEvent] Normal");
				this.Resume?.Invoke();
				break;
			case SystemState.Sleep:
				Logger.Instance.Log("[TizenPlatformMonitor.InvokeSystemStateEvent] Sleep");
				this.Sleep?.Invoke();
				break;
			default:
				Logger.Instance.Log("[TizenPlatformMonitor.InvokeSystemStateEvent] Ignore");
				break;
			}
		}
	}
	public class TizenDisplayHandler
	{
		private readonly Platform _platform;

		private SystemState _systemState;

		private TizenDisplayEventGenerator _displayEventGenerator;

		public SystemState SystemState => MapDisplayStateToSystemState(Display.State);

		public event ResumeEvent Resume;

		public event SleepEvent Sleep;

		public TizenDisplayHandler(Platform platform)
		{
			_platform = platform;
			_systemState = SystemState;
		}

		public void Start()
		{
			Display.StateChanged += DisplayOnStateChanged;
			if (!TizenPlatformConfig.Instance.SupportsDisplayEvent)
			{
				_displayEventGenerator = new TizenDisplayEventGenerator(_platform);
				_displayEventGenerator.Start();
				_displayEventGenerator.Sleep += delegate
				{
					this.Sleep?.Invoke();
				};
				_displayEventGenerator.Resume += delegate
				{
					this.Resume?.Invoke();
				};
			}
		}

		~TizenDisplayHandler()
		{
			_displayEventGenerator?.Stop();
		}

		private void DisplayOnStateChanged(object sender, DisplayStateChangedEventArgs e)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			Logger.Instance.Log("[TizenDisplayHandler.DisplayOnStateChanged] " + ((object)e.State/*cast due to .constrained prefix*/).ToString());
			if (!TizenPlatformConfig.Instance.SupportsDisplayEvent)
			{
				Logger.Instance.Log("[TizenDisplayHandler.DisplayOnStateChanged] we are on a system where StateChange works");
				TizenPlatformConfig.Instance.SupportsDisplayEvent = true;
				_displayEventGenerator.Stop();
			}
			SystemState systemState = MapDisplayStateToSystemState(e.State);
			if (_systemState == systemState)
			{
				Logger.Instance.Log("[TizenDisplayHandler.DisplayOnStateChanged] ignoring duplicate state change");
				return;
			}
			_systemState = systemState;
			InvokeSystemStateEvent(_systemState);
		}

		private void InvokeSystemStateEvent(SystemState state)
		{
			switch (state)
			{
			case SystemState.Normal:
				Logger.Instance.Log("[TizenDisplayHandler.InvokeSystemStateEvent] Normal");
				this.Resume?.Invoke();
				break;
			case SystemState.Sleep:
				Logger.Instance.Log("[TizenDisplayHandler.InvokeSystemStateEvent] Sleep");
				this.Sleep?.Invoke();
				break;
			default:
				Logger.Instance.Log("[TizenDisplayHandler.InvokeSystemStateEvent] Ignore");
				break;
			}
		}

		private unsafe SystemState MapDisplayStateToSystemState(DisplayState state)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Invalid comparison between Unknown and I4
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Expected I4, but got Unknown
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			if (TizenPlatformConfig.Instance.SupportsDisplayEvent)
			{
				Logger.Instance.Log("[TizenDisplayHandler.MapDisplayStateToSystemState] " + ((object)(*(DisplayState*)(&state))/*cast due to .constrained prefix*/).ToString());
			}
			if ((int)state == -1)
			{
				Logger.Instance.Log("[TizenDisplayHandler.MapDisplayStateToSystemState] we are on a system that with wrong DisplayState values: mapping -1 to Off");
				TizenPlatformConfig.Instance.WrongDisplayState = true;
				state = (DisplayState)2;
			}
			DisplayState val = state;
			DisplayState val2 = val;
			switch ((int)val2)
			{
			case 0:
				return SystemState.Normal;
			case 2:
				return SystemState.Sleep;
			default:
				Logger.Instance.Log($"[TizenDisplayHandler.MapDisplayStateToSystemState] unexpected DisplayState: {state}");
				return SystemState.Normal;
			}
		}
	}
	public class TizenGuiEventGenerator
	{
		private readonly Platform _platform;

		private readonly System.Timers.Timer _timer;

		private bool _isGuiVisible;

		private const int Interval = 10000;

		public event GuiHiddenEvent GuiHidden;

		public event GuiVisibleEvent GuiVisible;

		public TizenGuiEventGenerator(Platform platform)
		{
			_platform = platform;
			_timer = new System.Timers.Timer();
			_isGuiVisible = _platform.IsGuiVisible;
		}

		public void Start()
		{
			_timer.Enabled = true;
			_timer.Interval = 10000.0;
			_timer.AutoReset = true;
			_timer.Elapsed += TimerElapsed;
		}

		public void Stop()
		{
			_timer.Enabled = false;
		}

		private void TimerElapsed(object sender, ElapsedEventArgs e)
		{
			bool isGuiVisible = _platform.IsGuiVisible;
			if (isGuiVisible != _isGuiVisible)
			{
				Logger.Instance.Log($"[TizenGuiEventGenerator.OnTimerCheckGuiState] {_isGuiVisible} -> {isGuiVisible}");
				_isGuiVisible = isGuiVisible;
				InvokeGuiStateEvent(_isGuiVisible);
			}
		}

		private void InvokeGuiStateEvent(bool isGuiVisible)
		{
			if (isGuiVisible)
			{
				this.GuiVisible?.Invoke();
			}
			else
			{
				this.GuiHidden?.Invoke();
			}
		}
	}
	public class TizenNetworkMonitor : INetworkMonitor
	{
		private const double InternetConnectedCheckIntervalMs = 20000.0;

		private const double InternetDisconnectedCheckIntervalMs = 5000.0;

		private bool _networkIsConnected = false;

		private bool _internetIsConnected = false;

		private HttpClient _httpClient = null;

		private System.Timers.Timer _internetCheckTimer;

		private Mutex _lock = new Mutex();

		public event NetworkDisconnectEvent NetworkDisconnect;

		public event NetworkReconnectEvent NetworkReconnect;

		~TizenNetworkMonitor()
		{
			_internetCheckTimer?.Dispose();
			_httpClient?.Dispose();
			_httpClient = null;
		}

		public void Start()
		{
			Logger.Instance.Log("[TizenNetworkMonitor.Start]");
			if (_httpClient != null)
			{
				return;
			}
			_httpClient = new HttpClient();
			_httpClient.Timeout = TimeSpan.FromSeconds(3.0);
			_networkIsConnected = IsNetworkConnectedInternal();
			_internetIsConnected = _networkIsConnected && IsInternetConnectedInternal();
			ConnectionManager.ConnectionTypeChanged += delegate(object s, ConnectionTypeEventArgs e)
			{
				//IL_002b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0060: Unknown result type (might be due to invalid IL or missing references)
				//IL_0066: Invalid comparison between Unknown and I4
				//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b9: Invalid comparison between Unknown and I4
				//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c2: Invalid comparison between Unknown and I4
				_lock.WaitOne();
				using (new ScopeExit(delegate
				{
					_lock.ReleaseMutex();
				}))
				{
					Logger.Instance.Log($"[TizenNetworkMonitor.ConnectionTypeChanged] {e.ConnectionType}, {_networkIsConnected}, {_internetIsConnected}");
					if (_networkIsConnected && (int)e.ConnectionType == 0)
					{
						_networkIsConnected = false;
						_internetIsConnected = false;
						_internetCheckTimer.Interval = 5000.0;
						this.NetworkDisconnect?.Invoke(networkConnected: false, internetConnected: false);
					}
					else if (!_networkIsConnected && ((int)e.ConnectionType == 1 || (int)e.ConnectionType == 3))
					{
						_networkIsConnected = true;
						_internetIsConnected = IsInternetConnectedInternal();
						_internetCheckTimer.Interval = (_internetIsConnected ? 20000.0 : 5000.0);
						this.NetworkReconnect?.Invoke(networkConnected: true, _internetIsConnected);
					}
				}
			};
			_internetCheckTimer = new System.Timers.Timer(_internetIsConnected ? 20000.0 : 5000.0);
			_internetCheckTimer.Elapsed += delegate
			{
				_lock.WaitOne();
				using (new ScopeExit(delegate
				{
					_lock.ReleaseMutex();
				}))
				{
					if (_networkIsConnected)
					{
						bool flag = IsInternetConnectedInternal();
						if (_internetIsConnected && !flag)
						{
							Logger.Instance.Log("[TizenNetworkMonitor._internetCheckTimer] Internet disconnected");
							_internetIsConnected = false;
							_internetCheckTimer.Interval = 5000.0;
							this.NetworkDisconnect?.Invoke(networkConnected: true, internetConnected: false);
						}
						else if (!_internetIsConnected && flag)
						{
							Logger.Instance.Log("[TizenNetworkMonitor._internetCheckTimer] Internet reconnected");
							_internetIsConnected = true;
							_internetCheckTimer.Interval = 20000.0;
							this.NetworkReconnect?.Invoke(networkConnected: true, internetConnected: true);
						}
					}
				}
			};
			_internetCheckTimer.AutoReset = true;
			_internetCheckTimer.Enabled = true;
		}

		public bool IsNetworkConnected()
		{
			return _networkIsConnected;
		}

		public bool IsInternetConnected()
		{
			return _internetIsConnected;
		}

		private bool IsNetworkConnectedInternal()
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Invalid comparison between Unknown and I4
			try
			{
				return (int)ConnectionManager.CurrentConnection.State == 2;
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[TizenNetworkMonitor.IsNetworkConnectedInternal] Error checking network connection: " + ex.Message);
			}
			return false;
		}

		private bool IsInternetConnectedInternal()
		{
			try
			{
				if (_networkIsConnected)
				{
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, "http://www.google.com");
					Task<HttpResponseMessage> task = _httpClient.SendAsync(request);
					task.Wait();
					return task.Result.StatusCode == HttpStatusCode.OK;
				}
			}
			catch (Exception ex)
			{
				Logger.Instance.Log("[TizenNetworkMonitor.IsInternetConnectedInternal] Error checking Internet connection: " + ex.Message);
			}
			return false;
		}
	}
	internal class TizenNotificationManager
	{
		private static class Definitions
		{
			internal static class ActionIds
			{
				public static readonly string HideExternal = "hide_external";

				public static readonly string HideTimeout = "hide_timeout";

				public static readonly string HideUserAction = "hide_user_action";
			}
		}

		private static int _notificationIdCounter;

		private readonly Dictionary<string, Notification> _notifications;

		public TizenNotificationManager()
		{
			_notifications = new Dictionary<string, Notification>();
		}

		public void PostNotification(Notification notification)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0082: Expected O, but got Unknown
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e0: Expected O, but got Unknown
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_012f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Expected O, but got Unknown
			string text = $"{Platform.AppBackendPrefix}.notification.{_notificationIdCounter++}";
			_notifications.Add(text, notification);
			ActiveStyle val = new ActiveStyle
			{
				HiddenByExternalAction = CreateAction(text, Definitions.ActionIds.HideExternal, notification.EnableActions),
				HiddenByTimeoutAction = CreateAction(text, Definitions.ActionIds.HideTimeout, notification.EnableActions),
				HiddenByUserAction = CreateAction(text, Definitions.ActionIds.HideUserAction, notification.EnableActions)
			};
			val.SetRemoveTime(15, 15);
			foreach (NotificationButton button in notification.Buttons)
			{
				ButtonAction val2 = new ButtonAction
				{
					Action = CreateAction(text, "button." + button.Title, notification.EnableActions),
					Text = button.Title
				};
				val.AddButtonAction(val2);
			}
			Notification val3 = new Notification
			{
				Title = notification.Title,
				Content = notification.Content,
				Tag = Platform.AppBackendPrefix,
				Icon = Application.Current.DirectoryInfo.SharedResource + notification.Icon
			};
			val3.AddStyle((StyleBase)(object)val);
			Logger.Instance.Log("[PostNotification] create notification with " + text);
			NotificationManager.Post(val3);
		}

		public void ProcessAppControl(ExtraDataCollection data)
		{
			IEnumerable<string> keys = data.GetKeys();
			foreach (string item in keys)
			{
				if (_notifications.ContainsKey(item) && ProcessNotification(data.Get<string>(item), item))
				{
					_notifications.Remove(item);
				}
			}
		}

		private bool ProcessNotification(string action, string id)
		{
			Logger.Instance.Log("[ProcessAppControl] id: " + id + " action: " + action);
			if (action == Definitions.ActionIds.HideExternal)
			{
				Logger.Instance.Log("[ProcessAppControl] hide notification: " + id + " " + action);
				_notifications[id].Hide?.Invoke();
				return true;
			}
			if (action == Definitions.ActionIds.HideTimeout)
			{
				Logger.Instance.Log("[ProcessAppControl] hide notification: " + id + " " + action);
				_notifications[id].Timeout?.Invoke();
				return true;
			}
			if (action == Definitions.ActionIds.HideUserAction)
			{
				Logger.Instance.Log("[ProcessAppControl] hide notification: " + id + " " + action + ", waiting for user action");
				return false;
			}
			foreach (NotificationButton button in _notifications[id].Buttons)
			{
				if ("button." + button.Title != action)
				{
					continue;
				}
				Logger.Instance.Log("[OnAppControlReceived] button action: " + action);
				button.Click?.Invoke();
				return true;
			}
			Logger.Instance.Log("[ProcessAppControl] unprocessed notification: " + id + " " + action);
			return false;
		}

		private AppControl CreateAction(string id, string value, bool enableAction)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Expected O, but got Unknown
			AppControl val = new AppControl();
			if (enableAction)
			{
				val.ApplicationId = "com.lighting.HueSyncService";
				val.ExtraData.Add(id, value);
			}
			return val;
		}
	}
	public class TizenPlatformConfig
	{
		public static TizenPlatformConfig Instance;

		private static readonly SecureDataStore Store;

		private bool _wrongDisplayState;

		private bool _supportsDisplayEvent;

		public bool SupportsDisplayEvent
		{
			get
			{
				return _supportsDisplayEvent;
			}
			set
			{
				if (value != _supportsDisplayEvent)
				{
					_supportsDisplayEvent = value;
					Save();
				}
			}
		}

		public bool WrongDisplayState
		{
			get
			{
				return _wrongDisplayState;
			}
			set
			{
				if (value != _wrongDisplayState)
				{
					_wrongDisplayState = value;
					Save();
				}
			}
		}

		static TizenPlatformConfig()
		{
			Store = new SecureDataStore
			{
				Path = "PlatformConfig.json"
			};
			Instance = new TizenPlatformConfig();
		}

		public static async Task Load()
		{
			Instance = await Store.Load<TizenPlatformConfig>();
		}

		private void Save()
		{
			Store.Save(this);
		}
	}
}
namespace Common.Rest
{
	public delegate object CustomObjectCreator(Type discriminatedType);
	public delegate void JsonPreprocessor(string discriminator, JObject jsonObject);
	public abstract class DiscriminatorOptions
	{
		public abstract Type BaseType { get; }

		public abstract string DiscriminatorFieldName { get; }

		public abstract bool SerializeDiscriminator { get; }

		public CustomObjectCreator Activator { get; protected set; } = null;

		public JsonPreprocessor Preprocessor { get; protected set; } = null;

		public abstract IEnumerable<(string TypeName, Type Type)> GetDiscriminatedTypes();
	}
	public sealed class DiscriminatedJsonConverter : JsonConverter
	{
		private readonly DiscriminatorOptions _discriminatorOptions;

		public DiscriminatedJsonConverter(Type concreteDiscriminatorOptionsType)
			: this((DiscriminatorOptions)Activator.CreateInstance(concreteDiscriminatorOptionsType))
		{
		}

		public DiscriminatedJsonConverter(DiscriminatorOptions discriminatorOptions)
		{
			_discriminatorOptions = discriminatorOptions ?? throw new ArgumentNullException("discriminatorOptions");
		}

		public override bool CanConvert(Type objectType)
		{
			return _discriminatorOptions.BaseType.IsAssignableFrom(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Invalid comparison between Unknown and I4
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			if ((int)reader.TokenType == 11)
			{
				return null;
			}
			JObject val = JObject.Load(reader);
			JProperty val2 = val.Property(_discriminatorOptions.DiscriminatorFieldName);
			if (val2 == null)
			{
				ITraceWriter traceWriter = serializer.TraceWriter;
				if (traceWriter != null && traceWriter.LevelFilter >= TraceLevel.Error)
				{
					serializer.TraceWriter.Trace(TraceLevel.Error, "Could not find discriminator field '" + _discriminatorOptions.DiscriminatorFieldName + "'.", (Exception)null);
				}
				throw new JsonSerializationException("Could not find discriminator field with name '" + _discriminatorOptions.DiscriminatorFieldName + "'.");
			}
			string discriminatorFieldValue = ((object)val2.Value).ToString();
			ITraceWriter traceWriter2 = serializer.TraceWriter;
			if (traceWriter2 != null && traceWriter2.LevelFilter >= TraceLevel.Info)
			{
				serializer.TraceWriter.Trace(TraceLevel.Info, "Found discriminator field '" + val2.Name + "' with value '" + discriminatorFieldValue + "'.", (Exception)null);
			}
			Type type = _discriminatorOptions.GetDiscriminatedTypes().FirstOrDefault(((string TypeName, Type Type) tuple) => tuple.TypeName == discriminatorFieldValue).Type;
			if (type == null)
			{
				type = objectType;
				ITraceWriter traceWriter3 = serializer.TraceWriter;
				if (traceWriter3 != null && traceWriter3.LevelFilter >= TraceLevel.Warning)
				{
					serializer.TraceWriter.Trace(TraceLevel.Warning, $"Discriminator value '{discriminatorFieldValue}' has no corresponding Type. Continuing anyway with Type '{objectType}'.", (Exception)null);
				}
			}
			else
			{
				ITraceWriter traceWriter4 = serializer.TraceWriter;
				if (traceWriter4 != null && traceWriter4.LevelFilter >= TraceLevel.Warning)
				{
					serializer.TraceWriter.Trace(TraceLevel.Info, $"Discriminator value '{discriminatorFieldValue}' was used to select Type '{type}'.", (Exception)null);
				}
			}
			_discriminatorOptions.Preprocessor?.Invoke(discriminatorFieldValue, val);
			if (!_discriminatorOptions.SerializeDiscriminator)
			{
				((JToken)val2).Remove();
			}
			if (type != objectType && type.CustomAttributes.Any((CustomAttributeData attribute) => attribute.AttributeType == typeof(JsonConverterAttribute)))
			{
				return serializer.Deserialize(((JToken)val).CreateReader(), type);
			}
			object obj = _discriminatorOptions.Activator?.Invoke(type) ?? Activator.CreateInstance(type);
			serializer.Populate(((JToken)val).CreateReader(), obj);
			return obj;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotSupportedException("DiscriminatedJsonConverter should only be used while deserializing.");
		}
	}
	public class HttpResponse<T>
	{
		public HttpStatusCode StatusCode { get; set; }

		public T Body { get; set; }
	}
	public static class DictExtentions
	{
		public static Dictionary<string, string> Merge(Dictionary<string, string> a, Dictionary<string, string> b)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			foreach (KeyValuePair<string, string> item in a)
			{
				dictionary[item.Key] = item.Value;
			}
			foreach (KeyValuePair<string, string> item2 in b)
			{
				dictionary[item2.Key] = item2.Value;
			}
			return dictionary;
		}
	}
	public class RestClient : IDisposable
	{
		public delegate void SSEEventHandler(string rawSse);

		private const int DefaultTimeout = 20000;

		private IHttpClient _httpClient;

		private IHttpClient _sseHttpClient;

		private StreamReader _sseStreamReader;

		private byte[] _expectedCertificate;

		private string _expectedCn;

		private bool _isWeb;

		private string _rootCaThumbPrint;

		private int _chainCount = 0;

		private bool _forceTrustSelfSignedCertificate = false;

		private X509Chain _chain;

		private DateTime _startTime;

		private DateTime? _endTime;

		public byte[] ExpectedCertificate
		{
			get
			{
				return _expectedCertificate;
			}
			private set
			{
				_expectedCertificate = value;
				if (_expectedCertificate != null && _chain != null)
				{
					_chain.ChainPolicy.ExtraStore.Add(new X509Certificate2(_expectedCertificate));
				}
			}
		}

		public Dictionary<string, string> DefaultHeaders { get; }

		public Dictionary<string, string> DefaultParameters { get; set; }

		public Dictionary<string, string> QueryParameters { get; set; }

		public string BaseAddress { get; }

		public string SseUri { get; }

		public RestClient(RestClientSettings settings)
		{
			DefaultHeaders = settings.DefaultHeaders;
			BaseAddress = settings.BaseAddress;
			SseUri = settings.SseUri;
			DefaultParameters = settings.Parameters;
			QueryParameters = settings.QueryParameters;
			InitCertChain(settings);
			if (BaseAddress != null)
			{
				_httpClient = CreateNewHttpClient(TimeSpan.FromMilliseconds(20000.0), stream: false);
			}
			if (SseUri != null)
			{
				_sseHttpClient = CreateNewHttpClient(Timeout.InfiniteTimeSpan, stream: true);
			}
		}

		private void InitCertChain(RestClientSettings settings)
		{
			_chain = new X509Chain();
			_isWeb = settings.IsWeb;
			_expectedCn = settings.ExpectedCn;
			_forceTrustSelfSignedCertificate = settings.ForceTrustSelfSignedCertificate;
			ExpectedCertificate = settings.ExpectedCertificate;
			if (_isWeb)
			{
				_rootCaThumbPrint = settings.RootCaThumbPrint;
				_chainCount = settings.ChainCount;
			}
			else
			{
				_chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
			}
			X509Certificate2[] certificates = settings.Certificates;
			foreach (X509Certificate2 certificate in certificates)
			{
				_chain.ChainPolicy.ExtraStore.Add(certificate);
			}
		}

		~RestClient()
		{
			Cleanup();
		}

		public void Dispose()
		{
			Cleanup();
			GC.SuppressFinalize(this);
		}

		public async Task<RestResponse> Execute(RestRequest request)
		{
			RestResponse restResponse = new RestResponse();
			HttpRequestMessage httpRequestMessage = null;
			try
			{
				httpRequestMessage = CreateHttpRequestMessage(request);
				CheckMethod(request);
				await Send(request, httpRequestMessage, restResponse);
			}
			catch (Exception ex)
			{
				Exception exception = ex;
				_httpClient = CreateNewHttpClient(TimeSpan.FromMilliseconds(20000.0), stream: false);
				restResponse.StatusCode = HttpStatusCode.RequestTimeout;
				LogHttpRequestResult(request, httpRequestMessage, restResponse, "exception:" + exception);
			}
			return restResponse;
		}

		private async Task Send(RestRequest request, HttpRequestMessage httpRequest, RestResponse response)
		{
			_startTime = DateTime.Now;
			_endTime = null;
			using HttpResponseMessage responseMessage = await _httpClient.SendAsync(httpRequest);
			_endTime = DateTime.Now;
			response.StatusCode = responseMessage.StatusCode;
			response.Headers = responseMessage.Headers;
			if (responseMessage.IsSuccessStatusCode)
			{
				response.Content = await responseMessage.Content.ReadAsStringAsync();
			}
			else
			{
				LogHttpRequestResult(request, httpRequest, response, "Not SuccessStatusCode");
			}
		}

		private void LogHttpRequestResult(RestRequest request, HttpRequestMessage httpRequest, RestResponse response, string error = "")
		{
			Logger.LogLevel level = ((!string.IsNullOrEmpty(error)) ? Logger.LogLevel.ERROR : Logger.LogLevel.INFO);
			Logger.Instance.Log("########################################################", level);
			Logger.Instance.Log("Url          : " + (httpRequest?.Method)?.ToString() + " " + httpRequest?.RequestUri, level);
			Logger.Instance.Log("Body request : " + request.Body, level);
			Logger.Instance.Log("Body response: " + response.Content, level);
			Logger.Instance.Log("StatusCode   : " + response.StatusCode, level);
			if (!string.IsNullOrEmpty(error))
			{
				Logger.Instance.Log("Error      : " + error);
			}
			if (!_endTime.HasValue)
			{
				Logger.Instance.Log("Send      : " + _startTime.ToString("HH:mm:ss:FFF"), level);
			}
			else
			{
				Logger.Instance.Log("Send    : " + _startTime.ToString("HH:mm:ss:FFF") + " | Receive: " + _endTime.Value.ToString("HH:mm:ss:FFF") + " | Duration: " + Math.Round(_endTime.Value.Subtract(_startTime).TotalSeconds, 3) + "s");
			}
			Logger.Instance.Log("########################################################", level);
		}

		private HttpRequestMessage CreateHttpRequestMessage(RestRequest request)
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage
			{
				Content = CreateContent(request),
				Method = request.Method,
				RequestUri = CompleteUrl(request),
				Version = new Version(2, 0)
			};
			httpRequestMessage.Headers.Authorization = GetAuthenticationHeader(request);
			return httpRequestMessage;
		}

		private AuthenticationHeaderValue GetAuthenticationHeader(RestRequest request)
		{
			if (!string.IsNullOrEmpty(request.AuthorizationScheme) && !string.IsNullOrEmpty(request.AuthorizationParameter))
			{
				return new AuthenticationHeaderValue(request.AuthorizationScheme, request.AuthorizationParameter);
			}
			return null;
		}

		private StringContent CreateContent(RestRequest request)
		{
			StringContent stringContent = new StringContent(request.Body, Encoding.UTF8, GetContentType(request));
			foreach (KeyValuePair<string, string> header in request.Headers)
			{
				stringContent.Headers.Add(header.Key, header.Value);
			}
			return stringContent;
		}

		private string GetContentType(RestRequest request)
		{
			return request.ContentType switch
			{
				ContentType.Json => "application/json", 
				ContentType.Form => "application/x-www-form-urlencoded", 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		private Uri CompleteUrl(RestRequest request)
		{
			string uri = (request.Uri.StartsWith("https://") ? request.Uri : (BaseAddress + request.Uri));
			uri = FillInUriParameters(uri, DefaultParameters, request.Parameters);
			return new Uri(uri);
		}

		private void CheckMethod(RestRequest request)
		{
			if (!(request.Method == HttpMethod.Get) && !(request.Method == HttpMethod.Put) && !(request.Method == HttpMethod.Post) && !(request.Method == HttpMethod.Delete))
			{
				throw new RestException($"Unknown http method '{request.Method}'");
			}
		}

		private string FillInUriParameters(string uri, Dictionary<string, string> defaultParameters, Dictionary<string, string> requestParameters)
		{
			return FillInUriParameters(uri, DictExtentions.Merge(defaultParameters, requestParameters));
		}

		private string FillInUriParameters(string uri, Dictionary<string, string> requestParameters)
		{
			foreach (KeyValuePair<string, string> requestParameter in requestParameters)
			{
				uri = uri.Replace("{" + requestParameter.Key + "}", HttpUtility.UrlEncode(requestParameter.Value));
			}
			return uri;
		}

		public async void ListenToSSE(SSEEventHandler sseEventHandler, AutoResetEvent readySignal)
		{
			Logger.Instance.Log("[RestClient.ListenToSSE] Start");
			try
			{
				_sseStreamReader?.Dispose();
				_sseStreamReader = new StreamReader(await _sseHttpClient.GetStreamAsync(FillInUriParameters(SseUri, DefaultParameters)));
				while (true)
				{
					string message = await _sseStreamReader.ReadLineAsync();
					if (!string.IsNullOrEmpty(message))
					{
						readySignal.Set();
						Logger.Instance.Log("[RestClient.ListenToSSE] Received message: " + message);
						if (message.StartsWith("data: ["))
						{
							JsonConvert.DeserializeObject<List<ServerSentEvent>>(message.Substring(6));
							sseEventHandler?.Invoke(message.Substring(6));
						}
					}
					await Task.Delay(TimeSpan.FromMilliseconds(30.0));
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Logger.Instance.Log("[RestClient.ListenToSSE] SSE Error: " + ex2.Message);
				readySignal.Set();
			}
		}

		public void CancelSSE()
		{
			_sseStreamReader?.Dispose();
		}

		private IHttpClient CreateNewHttpClient(TimeSpan timeout, bool stream)
		{
			IHttpClient service = Module.ServiceProvider.GetService<IHttpClient>();
			service.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			if (stream)
			{
				service.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
			}
			foreach (KeyValuePair<string, string> defaultHeader in DefaultHeaders)
			{
				service.DefaultRequestHeaders.Add(defaultHeader.Key, defaultHeader.Value);
			}
			service.Timeout = timeout;
			service.Handler.MaxConnectionsPerServer = 1;
			service.Handler.ServerCertificateCustomValidationCallback = HandlerServerCertificateCustomValidationCallback;
			return service;
		}

		private bool HandlerServerCertificateCustomValidationCallback(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
		{
			if (DoesCNMatch(certificate))
			{
				return IsCertValid(certificate, chain);
			}
			return false;
		}

		private bool IsCertValid(X509Certificate2 certificate, X509Chain chain)
		{
			bool result = false;
			if (CertificateHelper.IsSelfSignedCertificate(certificate))
			{
				if (!_isWeb)
				{
					if (ExpectedCertificate == null)
					{
						ExpectedCertificate = certificate.GetRawCertData();
						result = true;
					}
					else if (_forceTrustSelfSignedCertificate)
					{
						result = true;
					}
					else
					{
						_chain.Build(certificate);
						result = _chain.ChainStatus.Length == 1 && (_chain.ChainStatus.First().Status == X509ChainStatusFlags.UntrustedRoot || _chain.ChainStatus.First().Status == X509ChainStatusFlags.PartialChain) && ExpectedCertificate.SequenceEqual(certificate.GetRawCertData());
					}
				}
			}
			else if (_isWeb)
			{
				result = true;
			}
			else
			{
				result = _chain.Build(certificate);
				if (_chain.ChainStatus.Length == 1 && _chain.ChainStatus.First().Status == X509ChainStatusFlags.UntrustedRoot && _chain.ChainPolicy.ExtraStore.Contains(_chain.ChainElements[_chain.ChainElements.Count - 1].Certificate))
				{
					result = true;
				}
			}
			return result;
		}

		private bool DoesCNMatch(X509Certificate2 certificate)
		{
			bool result = false;
			if (string.IsNullOrEmpty(_expectedCn))
			{
				if (!_isWeb)
				{
					result = true;
				}
			}
			else
			{
				string[] array = certificate.Subject.Replace(" ", "").Split(new char[1] { ',' });
				string text = "cn=" + _expectedCn.ToLower();
				string[] array2 = array;
				foreach (string text2 in array2)
				{
					if (text2.ToLower() == text)
					{
						result = true;
						break;
					}
				}
			}
			return result;
		}

		private void Cleanup()
		{
			_httpClient = null;
			_sseStreamReader?.Dispose();
			_sseStreamReader = null;
			_sseHttpClient = null;
			_chain.Dispose();
		}
	}
	public class RestClientSettings
	{
		public string SseUri;

		public string BaseAddress;

		public bool IsWeb = false;

		public string RootCaThumbPrint;

		public int ChainCount;

		public string ExpectedCn;

		public byte[] ExpectedCertificate;

		public bool ForceTrustSelfSignedCertificate;

		public X509Certificate2[] Certificates = Array.Empty<X509Certificate2>();

		public Dictionary<string, string> DefaultHeaders = new Dictionary<string, string>();

		public Dictionary<string, string> Parameters = (Dictionary<string, string>)(object)new AutoConstructedDictionary<string, string>();

		public Dictionary<string, string> QueryParameters = (Dictionary<string, string>)(object)new AutoConstructedDictionary<string, string>();
	}
	public class RestException : Exception
	{
		public RestException(string message)
			: base(message)
		{
		}
	}
	public enum ContentType
	{
		Json,
		Form
	}
	public class RestRequest
	{
		public string Uri { get; set; }

		public HttpMethod Method { get; set; }

		public string Body { get; set; } = "";

		public ContentType ContentType { get; set; }

		public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

		public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

		public Dictionary<string, string> QueryParameters { get; set; } = new Dictionary<string, string>();

		public string AuthorizationScheme { get; set; }

		public string AuthorizationParameter { get; set; }

		public RestRequest(string uri, HttpMethod method)
		{
			Uri = uri;
			Method = method;
			ContentType = ContentType.Json;
		}

		public void AddJsonBody(object body)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Expected O, but got Unknown
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0020: Expected O, but got Unknown
			//IL_0026: Expected O, but got Unknown
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Expected O, but got Unknown
			ContentType = ContentType.Json;
			JsonSerializerSettings val = new JsonSerializerSettings();
			val.ContractResolver = (IContractResolver)new DefaultContractResolver
			{
				NamingStrategy = (NamingStrategy)new SnakeCaseNamingStrategy()
			};
			val.Formatting = (Formatting)1;
			val.NullValueHandling = (NullValueHandling)1;
			val.Converters.Add((JsonConverter)new StringEnumConverter(typeof(SnakeCaseNamingStrategy)));
			JsonSerializerSettings val2 = val;
			Body = JsonConvert.SerializeObject(body, val2);
		}

		public void AddQueryParamsToBody()
		{
			ContentType = ContentType.Form;
			Body = "";
			foreach (KeyValuePair<string, string> queryParameter in QueryParameters)
			{
				Body = Body + HttpUtility.UrlEncode(queryParameter.Key) + "=" + HttpUtility.UrlEncode(queryParameter.Value) + "&";
			}
			if (Body.EndsWith("&"))
			{
				Body = Body.Substring(0, Body.Length - 1);
			}
		}

		public void AddParameter(string name, string value)
		{
			Parameters.Add(name, value);
		}
	}
	public class RestResponse
	{
		public HttpStatusCode StatusCode { get; set; }

		public string Content { get; set; }

		public HttpResponseHeaders Headers { get; set; }
	}
	public class SafeStringEnumConverter : StringEnumConverter
	{
		public object DefaultValue { get; }

		public SafeStringEnumConverter(object defaultValue)
		{
			DefaultValue = defaultValue;
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			try
			{
				return ((StringEnumConverter)this).ReadJson(reader, objectType, existingValue, serializer);
			}
			catch
			{
				return DefaultValue;
			}
		}
	}
	[Serializable]
	public class ServerSentEventId
	{
		public string Id { get; set; }

		public string Id_V1 { get; set; }

		public string Type { get; set; }
	}
	[Serializable]
	public class ServerSentEvent
	{
		public string CreationTime { get; set; }

		public string Id { get; set; }

		public string Type { get; set; }

		public List<ServerSentEventId> Data { get; set; }
	}
}
namespace Common.Platforms
{
	public enum SystemState
	{
		Normal,
		Sleep
	}
	public delegate void ButtonCallback();
	public delegate void TimeoutCallback();
	public delegate void HideCallback();
	public class NotificationButton
	{
		public string Title { get; set; }

		public ButtonCallback Click { get; set; }
	}
	public class Notification
	{
		public string Title { get; set; }

		public string Content { get; set; }

		public string Icon { get; set; }

		public bool EnableActions { get; set; }

		public List<NotificationButton> Buttons { get; set; }

		public TimeoutCallback Timeout { get; set; }

		public HideCallback Hide { get; set; }

		public Notification()
		{
			Buttons = new List<NotificationButton>();
			EnableActions = true;
		}
	}
	public delegate void ResumeEvent();
	public delegate void SleepEvent();
	public delegate void GuiVisibleEvent();
	public delegate void GuiHiddenEvent();
	public delegate void KeepAliveChangedEvent();
	public interface IPlatform
	{
		string AppVersion { get; }

		string FrontendVersion { get; }

		string DeviceMacAddress { get; }

		string UniqueDeviceId { get; }

		string OSName { get; }

		string OSVersion { get; }

		string Language { get; }

		int MemorySize { get; }

		string Ip { get; }

		string PlatformName { get; }

		string DeviceModel { get; }

		string DeviceType { get; }

		string UserId { get; }

		string Country { get; }

		bool IsGuiVisible { get; }

		SystemState SystemState { get; }

		string ApplicationDataFolder { get; }

		bool KeepAlive { get; set; }

		event ResumeEvent Resume;

		event SleepEvent Sleep;

		event GuiHiddenEvent GuiHidden;

		event GuiVisibleEvent GuiVisible;

		event KeepAliveChangedEvent KeepAliveChanged;

		void PostNotification(Notification notification);

		void StartFrontend();
	}
}
