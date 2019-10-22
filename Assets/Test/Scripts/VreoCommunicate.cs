using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace VREO
{   
    [System.Serializable]
	public class ClassRandomAdRequest
	{
		public string ID_GameDev;
		public string str_DevAccessToken;
		public string ID_Game;
		public string ID_MediaType;
		public string str_Device;
        public string GeoID;
        public string dat_Timestamp;
	}

	[System.Serializable]
	public class ClassRandomAdResponse
	{
		[System.Serializable]
		public class Result
		{
			public int ID_Advertisement;
			public int ID_MediaType;
			public int ID_Request;

			public string dat_Timestamp;
			public string str_MediaTypeName;
			public string str_MediaURL;
		}

		public string message;
		public Result result;
		public bool success;
	}

	// ==============================================================================

    [System.Serializable]
	public class ClassViewDataRequest
	{
		
		public string ID_GameDev;
        public string str_DevAccessToken;
        public string ID_Game;
        public string ID_Advertisement;

        public float dec_TotalHitTime;
		public float dec_TotalScreenPercentage;
		public float dec_TotalScreenPositionX;
		public float dec_TotalScreenPositionY;
		public float dec_TotalBlockedPercentage;
		public float dec_TotalVolumePercentage;

		public string str_Device;
        public string dat_Timestamp;
        public string str_Platform;
        public int bit_withVR;
		public string str_IDFA;
	}

    [System.Serializable]
    public class ClassViewDataResponse
    {
        [System.Serializable]
        public class Body
        {
            public bool success;
        }
        public Body body;
    }

	// ==============================================================================

	public class VreoCommunicate : MonoBehaviour
	{

		private static VreoCommunicate _instance;
		private static VreoCommunicate Instance
		{
			get
			{
				if (_instance == null)
					_instance = GameObject.FindObjectOfType<VreoCommunicate>();

				return _instance;
			}
		}

		public string developerId;
		public string developerAccessToken;
        public string developerGameId;

        [Space]
        public string developerGameSlotId;


        [HideInInspector]
        private string advertiser_id = "";
        private bool advertiserIDSupportEnabled = true;

        private bool requestingAdvertiserID = false;
        private bool HasAdvertiserID { get { return advertiser_id != ""; } }

        private string _device_id = "";
        private string device_id
        {
            get
            {
                if (this._device_id == "")
                    this._device_id = SystemInfo.deviceUniqueIdentifier;

                return this._device_id;
            } 
        }

        // ==============================================================================

        private string requestRandomAd_URL = "http://api.vreo.io/ad-request";
		private string sendViewData_URL = "http://api.vreo.io/view-data";


        public delegate void RandomAdRequestCallback(ClassRandomAdResponse response);

        // ==============================================================================
        // RequestAdvertisingIdentigier
        // ==============================================================================

        public static void RequestAdvertisingIdentifier()
        {
            if (Instance.advertiser_id == "" && !Instance.requestingAdvertiserID)
            {
                Instance.requestingAdvertiserID = true;
                Instance.advertiserIDSupportEnabled = Application.RequestAdvertisingIdentifierAsync((string advertisingId, bool trackingEnabled, string error) =>
                    {
                        Debug.Log("AdvertisingId: " + advertisingId + " Tracking:" + trackingEnabled + " " + error);

                        Instance.advertiser_id = advertisingId;
                        Instance.requestingAdvertiserID = false;
                    }
                );
            }
        }

        // ==============================================================================
        // RequestRandomAd
        // ==============================================================================

        public static void RequestRandomAd(VreoAdCanvas.MediaType mediaType, RandomAdRequestCallback callback)
		{
			Instance.StartCoroutine(Instance.RequestRandomAd_Coroutine(mediaType, callback));
        }

        private IEnumerator RequestRandomAd_Coroutine(VreoAdCanvas.MediaType mediaType, RandomAdRequestCallback callback = null)
		{
            // request and wait for advertiser id
            RequestAdvertisingIdentifier();
            yield return new WaitWhile(() => (!HasAdvertiserID && advertiserIDSupportEnabled));

            // create new request
            ClassRandomAdRequest randomAdRequest = new ClassRandomAdRequest
            {
				//ID_Request = "",
				ID_GameDev = developerId,
				str_DevAccessToken = developerAccessToken,
				ID_Game = developerGameId,
				ID_MediaType = ((int)mediaType).ToString(),
				str_Device = "1ab2",//device_id,
				GeoID = "",
				dat_Timestamp = GetTimestampString(),
				//bit_success = "true",

                //advertiser_id = HasAdvertiserID ? advertiser_id : "1e061df9"
            };
            
			string jsonString = JsonUtility.ToJson(randomAdRequest);

			//Debug.Log(jsonString);

			byte[] pData = System.Text.Encoding.ASCII.GetBytes(jsonString.ToCharArray());         
			Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Content-Type", "application/json");
			headers.Add("Authorization", developerAccessToken);

			WWW response = new WWW(requestRandomAd_URL, pData, headers);

			// Wait until the response is returned
			yield return response;

			if (!string.IsNullOrEmpty(response.error))
			{
				Debug.LogError("Error response: " + response.error);
				Debug.Log(response.text);

				ClassRandomAdResponse randomAdResponse = new ClassRandomAdResponse();
                randomAdResponse.success = false;

                randomAdResponse.result = new ClassRandomAdResponse.Result();
                int.TryParse(randomAdRequest.ID_MediaType, out randomAdResponse.result.ID_MediaType);
				

				if (callback != null)
                    callback(randomAdResponse);
			}
			else
			{
				// debug log the raw response text
				//Debug.Log(response.text);

				// replace "false" and "true" to false and true
				string resultJsonString = Regex.Replace(response.text, "\"false\"", "false", RegexOptions.IgnoreCase);
				resultJsonString = Regex.Replace(resultJsonString, "\"true\"", "true", RegexOptions.IgnoreCase);

				ClassRandomAdResponse randomAdResponse = JsonUtility.FromJson<ClassRandomAdResponse>(resultJsonString);

				if (callback != null)
                    callback(randomAdResponse);
			}         
		}

        // ==============================================================================
        // SendViewData
        // ==============================================================================

        public static void __SendViewData(ClassViewDataRequest viewDataRequest)
        {
            Instance.SendViewData(viewDataRequest);
        }
		public void SendViewData(ClassViewDataRequest viewdataRequest)
		{
            viewdataRequest.ID_GameDev = developerId;
            viewdataRequest.ID_Game = developerGameId;
            viewdataRequest.str_DevAccessToken = developerAccessToken;

            viewdataRequest.str_Device = device_id;
            viewdataRequest.str_Platform = Application.platform.ToString();
            viewdataRequest.bit_withVR = UnityEngine.XR.XRDevice.isPresent ? 1 : 0;

            viewdataRequest.dat_Timestamp = GetTimestampString();
			viewdataRequest.str_IDFA = HasAdvertiserID ? advertiser_id : "";

            StartCoroutine(RequestSendViewData_Coroutine(viewdataRequest, sendViewData_URL));
		}      

		// ==============================================================================
		// RequestSendViewData_Coroutine
		// ==============================================================================

		private IEnumerator RequestSendViewData_Coroutine(ClassViewDataRequest viewdataRequest, string requestUrl)
		{
            string jsonString = JsonUtility.ToJson(viewdataRequest);

			Debug.Log("SEND VIEW DATA: " + jsonString);

            byte[] pData = System.Text.Encoding.ASCII.GetBytes(jsonString.ToCharArray());
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Authorization", developerAccessToken);
            headers.Add("Content-Type", "application/json");
			

            WWW response = new WWW(sendViewData_URL, pData, headers);

            // Wait until the response is returned
            yield return response;

            if (!string.IsNullOrEmpty(response.error))
            {
                Debug.LogError("Error response: " + response.error);
				Debug.Log("Error Message: " + response.text);

                ClassViewDataResponse viewDataResponse = new ClassViewDataResponse();
                viewDataResponse.body = new ClassViewDataResponse.Body();
                viewDataResponse.body.success = false;
            }
            else
            {
                // debug log the raw response text
                Debug.Log("VIEW DATA RESPONSE: " + response.text);


                // replace "false" and "true" to false and true
                string resultJsonString = Regex.Replace(response.text, "\"false\"", "false", RegexOptions.IgnoreCase);
                resultJsonString = Regex.Replace(resultJsonString, "\"true\"", "true", RegexOptions.IgnoreCase);

                ClassRandomAdResponse randomAdResponse = JsonUtility.FromJson<ClassRandomAdResponse>(resultJsonString);
            }
        }

		// ==============================================================================

		public static string GetTimestampString()
		{
			string dateString = System.DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
			return dateString + " " + System.DateTime.UtcNow.ToString("HH:mm:ss");
		}

	} // VreoCommunicate.cs
} // Namespace

