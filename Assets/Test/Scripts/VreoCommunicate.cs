using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;


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

		const string RequestRandomAdUrl = "http://api.vreo.io/ad-request";
		const string SendViewDataUrl = "http://api.vreo.io/view-data";

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
			print("yo");
			Instance.StartCoroutine(Instance.RequestAd(mediaType, callback));
        }

        IEnumerator RequestAd(VreoAdCanvas.MediaType mediaType, RandomAdRequestCallback callback = null)
		{
            // request and wait for advertiser id
            RequestAdvertisingIdentifier();
            yield return new WaitWhile(() => (HasAdvertiserId && _advertiserIdSupportEnabled));

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
			var headers = new Dictionary<string, string>
			{
				{"Content-Type", "application/json"}, {"Authorization", developerAccessToken}
			};
			
			print("Request sent");

			var response = new WWW(RequestRandomAdUrl, pData, headers);

			// Wait until the response is returned
			yield return response;
			
			print("Response received");

			if (!string.IsNullOrEmpty(response.error))
			{
				Debug.LogError("Error response: " + response.error);
				Debug.Log(response.text);

				var randomVreoResponse = new VreoResponse
				{
					success = false, result = new Result {ID_MediaType = randomAdRequest.ID_MediaType}
				};


				callback?.Invoke(randomVreoResponse);
			}
			else
			{
				// debug log the raw response text
				//Debug.Log(response.text);

				// replace "false" and "true" to false and true
				var resultJsonString = Regex.Replace(response.text, "\"false\"", "false", RegexOptions.IgnoreCase);
				resultJsonString = Regex.Replace(resultJsonString, "\"true\"", "true", RegexOptions.IgnoreCase);

				var randomVreoResponse = JsonUtility.FromJson<VreoResponse>(resultJsonString);

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

			Debug.Log("SEND VIEW DATA: " + jsonString);

            var pData = System.Text.Encoding.ASCII.GetBytes(jsonString.ToCharArray());
            var headers = new Dictionary<string, string>
            {
	            {"Authorization", developerAccessToken},
	            {"Content-Type", "application/json"}
            };


            var response = new WWW(SendViewDataUrl, pData, headers);

            // Wait until the response is returned
            yield return response;

            if (!string.IsNullOrEmpty(response.error))
            {
                Debug.LogError("Error response: " + response.error);
				Debug.Log("Error Message: " + response.text);
            }
            else
            {
                Debug.Log("VIEW DATA RESPONSE: " + response.text);

                // replace "false" and "true" to false and true
                var resultJsonString = Regex.Replace(response.text, "\"false\"", "false", RegexOptions.IgnoreCase);
                resultJsonString = Regex.Replace(resultJsonString, "\"true\"", "true", RegexOptions.IgnoreCase);

                var randomVreoResponse = JsonUtility.FromJson<VreoResponse>(resultJsonString);
            }
        }

		// ==============================================================================

		static string GetTimestampString()
		{
			return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
		}

	} // VreoCommunicate.cs
} // Namespace

