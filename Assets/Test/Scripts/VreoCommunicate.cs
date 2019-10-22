using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;


namespace VREO
{   
    [Serializable]
	public class AdRequest
	{
		public int ID_GameDev;
		public string str_DevAccessToken;
		public int ID_Game;
		public int ID_MediaType;
		public string str_Device;
		public string dat_Timestamp;
		public float dec_Latitude;
		public float dec_Longitude;
	}

	[Serializable]
	public class VreoResponse
	{
		public string message;
		public Result result;
		public bool success;
		public string str_Link;
	}

	// ==============================================================================

    [Serializable]
	public class ViewDataRequest
	{
        public string str_DevAccessToken;
        public int ID_Game;
        public int ID_Advertisement;

        public float dec_TotalHitTime;
		public float dec_TotalScreenPercentage;
		public float dec_TotalScreenPositionX;
		public float dec_TotalScreenPositionY;
		public float dec_TotalBlockedPercentage;
		public float dec_TotalVolumePercentage;

		public string str_Device;
        public string dat_Timestamp;
        public int bit_withVR;
        public string str_Platform;
		public string str_IDFA;
		public float dec_Latitude;
		public float dec_Longitude;
	}
    
    [Serializable]
    public class Result
    {
	    public int ID_Advertisement;
	    public int ID_MediaType;
	    public int ID_Request;

	    public string dat_Timestamp;
	    public string str_MediaTypeName;
	    public string str_MediaURL;
    }

	// ==============================================================================

	public class VreoCommunicate : MonoBehaviour
	{
		[SerializeField]
		int developerId, developerGameId, developerGameSlotId;

		[SerializeField]
		string developerAccessToken;
		
		string _advertiserId = "";
		bool _advertiserIdSupportEnabled = true;

		bool _requestingAdvertiserId;
		bool HasAdvertiserId => !_advertiserId.Equals("");

		string _deviceId;

		public string DeviceId => _deviceId ?? (_deviceId = SystemInfo.deviceUniqueIdentifier);

		// ==============================================================================

		const string RequestRandomAdUrl = "http://api.vreo.io/ad/request";
		const string SendViewDataUrl = "http://api.vreo.io/ad/views";

		static VreoCommunicate _instance;
		static VreoCommunicate Instance
		{
			get
			{
				if (_instance == null)
					_instance = FindObjectOfType<VreoCommunicate>();

				return _instance;
			}
		}

		public delegate void RandomAdRequestCallback(VreoResponse response);

        // ==============================================================================
        // RequestAdvertisingIdentifier
        // ==============================================================================

        static void RequestAdvertisingIdentifier()
        {
            if (Instance.HasAdvertiserId && !Instance._requestingAdvertiserId)
            {
                Instance._requestingAdvertiserId = true;
                Instance._advertiserIdSupportEnabled = Application.RequestAdvertisingIdentifierAsync((advertisingId, trackingEnabled, error) =>
                    {
                        Debug.Log("AdvertisingId: " + advertisingId + " Tracking:" + trackingEnabled + " " + error);

                        Instance._advertiserId = advertisingId;
                        Instance._requestingAdvertiserId = false;
                    }
                );
            }
        }

        // ==============================================================================
        // RequestRandomAd
        // ==============================================================================

        public static void RequestRandomAd(VreoAdCanvas.MediaType mediaType, RandomAdRequestCallback callback)
		{
			Instance.StartCoroutine(Instance.RequestAd(mediaType, callback));
        }

        IEnumerator RequestAd(VreoAdCanvas.MediaType mediaType, RandomAdRequestCallback callback = null)
		{
            // request and wait for advertiser id
            RequestAdvertisingIdentifier();
            yield return new WaitWhile(() => HasAdvertiserId && _advertiserIdSupportEnabled);

            // create new request
            var randomAdRequest = new AdRequest
            {
				ID_GameDev = developerId,
				str_DevAccessToken = developerAccessToken,
				ID_Game = developerGameId,
				ID_MediaType = (int) mediaType,
				str_Device = DeviceId,
				dat_Timestamp = GetTimestampString()
            };
            
			var jsonString = JsonUtility.ToJson(randomAdRequest);

			var pData = System.Text.Encoding.ASCII.GetBytes(jsonString.ToCharArray());

			var request = CreateWebRequest(RequestRandomAdUrl, pData);

			// Wait until the response is returned
			yield return request.SendWebRequest();

			if (!string.IsNullOrEmpty(request.error))
			{
				if (Debug.isDebugBuild)
				{
					print("Error response: " + request.error);
					print("Error Message: " + request.downloadHandler.text);
				}

				var randomVreoResponse = new VreoResponse
				{
					success = false, result = new Result {ID_MediaType = randomAdRequest.ID_MediaType}
				};
				
				callback?.Invoke(randomVreoResponse);
			}
			else
			{
				var randomVreoResponse = JsonUtility.FromJson<VreoResponse>(request.downloadHandler.text);
				callback?.Invoke(randomVreoResponse);
			}         
		}

        // ==============================================================================
        // SendViewData
        // ==============================================================================

        public static void __SendViewData(ViewDataRequest viewDataRequest)
        {
            Instance.SendViewData(viewDataRequest);
        }
		public void SendViewData(ViewDataRequest viewdataRequest)
		{
            viewdataRequest.ID_Game = developerGameId;
            viewdataRequest.str_DevAccessToken = developerAccessToken;

            viewdataRequest.str_Device = DeviceId;
            viewdataRequest.str_Platform = Application.platform.ToString();
            viewdataRequest.bit_withVR = UnityEngine.XR.XRDevice.isPresent ? 1 : 0;

            viewdataRequest.dat_Timestamp = GetTimestampString();
			viewdataRequest.str_IDFA = HasAdvertiserId ? _advertiserId : "";

            StartCoroutine(RequestSendViewData(viewdataRequest, SendViewDataUrl));
		}      

		// ==============================================================================
		// RequestSendViewData_Coroutine
		// ==============================================================================

		IEnumerator RequestSendViewData(ViewDataRequest viewdataRequest, string requestUrl)
		{
            var jsonString = JsonUtility.ToJson(viewdataRequest);

            var pData = System.Text.Encoding.ASCII.GetBytes(jsonString.ToCharArray());
            var request = CreateWebRequest(SendViewDataUrl, pData);

            yield return request.SendWebRequest();

            if (!string.IsNullOrEmpty(request.error))
            {
	            if (Debug.isDebugBuild)
	            {
		            print("Error response: " + request.error);
		            print("Error Message: " + request.downloadHandler.text);
	            }
            }
            else
            {
	            print("VIEW DATA RESPONSE: " + request.downloadHandler.text);

                var randomVreoResponse = JsonUtility.FromJson<VreoResponse>(request.downloadHandler.text);
            }
        }

		// ==============================================================================

		static string GetTimestampString()
		{
			return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
		}

		UnityWebRequest CreateWebRequest(string url, byte[] data)
		{
			var request = new UnityWebRequest(url, "POST")
			{
				uploadHandler = new UploadHandlerRaw(data), downloadHandler = new DownloadHandlerBuffer()
			};
			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Authorization", developerAccessToken);

			return request;
		}

	} // VreoCommunicate.cs
} // Namespace

