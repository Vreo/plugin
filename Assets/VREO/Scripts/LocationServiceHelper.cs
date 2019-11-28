using System.Collections;
using UnityEngine;

public class LocationServiceHelper : MonoBehaviour
{
	static LocationServiceHelper _instance;

	// Prevent accidental LocationServiceHelper instantiation
	LocationServiceHelper()
	{
	}

	public static LocationServiceHelper Instance
	{
		get
		{
			if (_instance == null)
			{
				Init();
			}

			return _instance;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	internal static void Init()
	{
		if (ReferenceEquals(_instance, null))
		{
			var instances = FindObjectsOfType<LocationServiceHelper>();

			if (instances.Length > 1)
			{
				Debug.LogError(typeof(LocationServiceHelper) + " Something went really wrong " + 
				               " - there should never be more than 1 " + typeof(LocationServiceHelper) + " Reopening the scene might fix it.");
			}
			else if (instances.Length == 0)
			{
				var singleton = new GameObject {hideFlags = HideFlags.HideAndDontSave};
				_instance = singleton.AddComponent<LocationServiceHelper>();
				singleton.name = typeof(LocationServiceHelper).ToString();

				Debug.Log("[Singleton] An _instance of " + typeof(LocationServiceHelper) +
				          " is needed in the scene, so '" + singleton.name + "' was created with DontDestroyOnLoad.");
			}
			else
			{
				Debug.Log("[Singleton] Using _instance already created: " + _instance.gameObject.name);
			}
		}
	}

	public void ObtainLocationData()
	{
		if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
		{
			StartCoroutine(StartLocationService());
		}
		else
		{
			Debug.LogWarning("Location service is not available for this type of device - only Android and iOS supported");
		}
	}

	IEnumerator StartLocationService()
	{
		// First, check if user has location service enabled
		if (!Input.location.isEnabledByUser)
		{
			print("Location service is not enabled");
			yield break;
		}

		// Start service before querying location
		Input.location.Start();

		// Wait until service initializes
		int maxWait = 20;
		while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
		{
			yield return new WaitForSeconds(1);
			maxWait--;
		}

		// Service didn't initialize in 20 seconds
		if (maxWait < 1)
		{
			print("Timed out");
			yield break;
		}
		
		// Connection has failed
		if (Input.location.status == LocationServiceStatus.Failed)
		{
			print("Unable to determine device location");
			yield break;
		}
		else
		{
			// Access granted and location value could be retrieved
			print("Location: " + Input.location.lastData.latitude + " " + Input.location.lastData.longitude);

			LocationReceived = true;
			LastLocation = Input.location.lastData;
		}
		
		// Stop service since there is no need to query location updates continuously
		Input.location.Stop();
	}

	bool _locationReceived;

	public bool LocationReceived
	{
		get { return _locationReceived; }
		private set { _locationReceived = value; }
	}

	LocationInfo _lastLocation;

	public LocationInfo LastLocation
	{
		get { return _lastLocation; }
		private set { _lastLocation = value; }
	}
}