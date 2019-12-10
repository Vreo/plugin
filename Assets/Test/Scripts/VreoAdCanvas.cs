using System;
using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Video;
using Random = UnityEngine.Random;

namespace VREO
{
	[RequireComponent(typeof(VideoPlayer))]
	[ExecuteInEditMode] 
	public class VreoAdCanvas : MonoBehaviour
	{
		const float SendViewDataTime = 600.0f;
		const int BaseSideLength = 300;

		public enum MediaLoadingState
		{
			Unknown = -1,
			Waiting = 0,
			Loading = 1,
			Succeeded = 1,
			Failed = 2,
			Showing = 3,
		}

		public enum MediaType
		{
			Unknown = 0,
			MediumRectangle = 1,
			LargeRectangle = 2,
			WideSkyscraper = 3,
			Leaderboard = 4,
			LandscapeVideo = 5,
			PortraitVideo = 6
		}

		public enum Category
		{
			Unknown = 0,
			AlcoholicBeverages = 1,
			Automotive = 2,
			BusinessAndIndustrialServices = 3,
			ClothingAndAccessories = 4,
			ComputingProductsAndConsumerElectronics = 5,
			Construction = 6,
			ConsultingAndLegal = 7,
			EnergyOilGasUtilities = 8,
			Entertainment = 9,
			FinancialServices = 10,
			Food = 11,
			HomeAndGarden = 12,
			Insurance = 13,
			JobsAndEducation = 14,
			MediaAndCommunications = 15,
			Mining = 16,
			NonProfitAndSocial = 17,
			PharmaceuticalAndHealthcare = 18,
			Political = 19,
			RealEstate = 20,
			Retail = 21,
			SoftDrinks = 22,
			SportAndFitness = 23,
			TelecomAndInternet = 24,
			Toys = 25,
			TravelAndTourism = 26,
			VideoGames = 27
		}

		// ==============================================================================

		public MediaType mediaType = MediaType.LandscapeVideo;
		public Category category = Category.Unknown;
		public string developerGameSlotId = "0";

		public bool playOnAwake = true;
		public bool autoPlayNew = true;

		[Tooltip("Aids performance when trying to load several video ads at the same time")]
		public float initialRandomDelay;

		public float imageDuration = 10.0f;

		public string spotId;

		public float proximity = 3.0f;

		public bool isClickable;
		
		public bool isRegistered = false;
		
		// ==============================================================================

		bool _initialized;

		MediaLoadingState _loadingState = MediaLoadingState.Waiting;
		float _playingTime;
		bool _videoPaused;

		Renderer _renderer;

		float _sendViewDataTimer = SendViewDataTime;

		// ==============================================================================

		public delegate void VideoLoadedAndReadyType(); // declare delegate type

		public VideoLoadedAndReadyType VideoIsLoadedCallback; // to store the function

		public MediaLoadingState LoadingState
		{
			get { return _loadingState; }
		}

		public bool AdIsShowing
		{
			get { return (_loadingState == MediaLoadingState.Showing); }
		}

		public float AdTotalShowTime => _playingTime;

		public float AdDuration
		{
			get
			{
				switch (mediaType)
				{
					case MediaType.MediumRectangle:
					case MediaType.LargeRectangle:
					case MediaType.WideSkyscraper:
					case MediaType.Leaderboard:
						return imageDuration;

					case MediaType.LandscapeVideo:
					case MediaType.PortraitVideo:
						return VideoPlayer != null && VideoPlayer.clip != null ? (float) VideoPlayer.clip.length : 0;
					default:
						return 0;
				}
			}
		}


		public VreoResponse CurrentVreoResponse { get; private set; }

		public VideoPlayer VideoPlayer { get; private set; }
		public VreoMovieQuad MovieQuad { get; private set; }

		public static Vector2Int SizeForResolutionType(MediaType type)
		{
			Vector2Int result;
			switch (type)
			{
				case MediaType.MediumRectangle:
					result = new Vector2Int(300, 250);
					break;
				case MediaType.LargeRectangle:
					result = new Vector2Int(300, 600);
					break;
				case MediaType.WideSkyscraper:
					result = new Vector2Int(160, 600);
					break;
				case MediaType.Leaderboard:
					result = new Vector2Int(728, 90);
					break;
				case MediaType.LandscapeVideo:
					result = new Vector2Int(540, 300);
					break;
				case MediaType.PortraitVideo:
					result = new Vector2Int(300, 540);
					break;
				default:
					result = Vector2Int.zero;
					Debug.LogError("Resolution type is invalid. Returning 0x0");
					break;
			}

			return result;
		}

		// ==============================================================================

		void Awake()
		{
			if (!Application.isPlaying)
				return;
			
			if (!_initialized)
			{
				_sendViewDataTimer -= Random.Range(1, 20);
				initialRandomDelay = Random.Range(0.0f, initialRandomDelay);

				VideoPlayer = GetComponent<VideoPlayer>();
				_renderer = GetComponent<Renderer>();
				MovieQuad = new VreoMovieQuad(this);

				if (VideoPlayer == null)
					Debug.LogError("Missing Attached VideoPlayer Component");
				else
				{
					VideoPlayer.prepareCompleted += VideoPlayer_PrepareCompleted;
					VideoPlayer.loopPointReached += VideoPlayer_LoopPointReached;
				}

				if (_renderer == null)
					Debug.LogError("Missing Attached Renderer Component");

				_initialized = true;
			}
		}

		void OnEnable()
		{
			if (!Application.isPlaying)
				return;
			
			if (playOnAwake)
				ShowAd(true);
		}

		// ==============================================================================

		/// <summary>
		/// Shows a new ad on the canvas if one is not already shown.
		/// </summary>
		/// <param name="force">If set to <c>true</c> force a new ad to be shown immediately.</param>
		public void ShowAd(bool force = false)
		{
			if (_loadingState == MediaLoadingState.Waiting || _loadingState == MediaLoadingState.Failed || force)
			{
				_loadingState = MediaLoadingState.Loading;
				VreoCommunicate.RequestRandomAd(mediaType, category, spotId, isClickable, RandomAdCallback);
			}
		}

		// ==============================================================================

		void RandomAdCallback(VreoResponse response)
		{
			if (response == null)
			{
				return;
			}
			// update local response reference for external access
			CurrentVreoResponse = response;

			if (response.success)
			{
				if (!string.IsNullOrEmpty(response.result.str_MediaURL))
				{
					if (response.result.str_MediaURL.ToLower().StartsWith("http"))
					{
						StartCoroutine(DownloadAdMediaAndLoad(response));
					}
					else
					{
						StartCoroutine(RetrieveStreamingAsset(response));
					}
				}
			}
			else
			{
				StartCoroutine(RetrieveStreamingAsset(response));
			}
		}

		// ==============================================================================

		void VideoPlayer_PrepareCompleted(VideoPlayer source)
		{
			if (source.isPrepared )
			{
				_playingTime = 0;
				_loadingState = MediaLoadingState.Succeeded;

				if ((initialRandomDelay <= 0) && (MovieQuad.ScreenPercentage > proximity))
				{
					VideoPlayer.Play();
				}

				if (VideoPlayer.isPlaying)
					_loadingState = MediaLoadingState.Showing;
			}
			else
				_loadingState = MediaLoadingState.Failed;
		}

		void VideoPlayer_LoopPointReached(VideoPlayer source)
		{
			if (!source.isLooping)
				source.Stop();

			if (autoPlayNew)
				ShowAd(true);
		}

		void ShowVideo(string url)
		{
			if (VideoPlayer.isPlaying)
				VideoPlayer.Stop();

			// set new video player url
			VideoPlayer.url = url;
			VideoPlayer.isLooping = false;

			// prepare the video
			VideoPlayer.Prepare();
		}

		// ==============================================================================

		void ShowImage(WWW wwwReader)
		{
			_loadingState = MediaLoadingState.Succeeded;

			// apply it to the texture
			Texture2D texture = new Texture2D(1, 1, TextureFormat.RGB24, false);
			texture.anisoLevel = 8;
			texture.wrapMode = TextureWrapMode.Clamp;
			wwwReader.LoadImageIntoTexture(texture);

			if (texture != null)
			{
				_renderer.material.mainTexture = texture;
				_renderer.enabled = true;

				_loadingState = MediaLoadingState.Showing;
				_playingTime = 0;
			}
		}

		void ShowSVGImage(WWW wwwReader)
		{
			/*
			loadingState = MediaLoadingState.Succeeded;

			Sprite svgSprite = BuildSVGSprite(wwwReader.text);

			if (svgSprite != null)
			{
			    renderer.enabled = false;

			    svgRenderer.sprite = svgSprite;
			    svgRenderer.enabled = true;

			    loadingState = MediaLoadingState.Showing;
			    playingTime = 0;
			}
			*/
		}

		Sprite BuildSVGSprite(string svg)
		{
			/*
			var tessOptions = new VectorUtils.TessellationOptions()
			{
			    StepDistance = 100.0f,
			    MaxCordDeviation = 0.5f,
			    MaxTanAngleDeviation = 0.1f,
			    SamplingStepSize = 0.01f
			};

			// Dynamically import the SVG data, and tessellate the resulting vector scene.
			var sceneInfo = SVGParser.ImportSVG(new StringReader(svg));
			var geoms = VectorUtils.TessellateScene(sceneInfo.Scene, tessOptions);

			// Build a sprite with the tessellated geometry.
			var sprite = VectorUtils.BuildSprite(geoms, 512.0f, VectorUtils.Alignment.Center, Vector2.zero, 128, true);
			return sprite;
			*/

			Debug.LogError("Retrieved SVG Asset is unsupported.");
			return null;
		}

		// ==============================================================================

		IEnumerator DownloadAdMediaAndLoad(VreoResponse response)
		{
			var type = response.result.ID_MediaType;
			var mediaFormat = response.result.str_MediaTypeName;

			switch ((MediaType) type)
			{
				case MediaType.MediumRectangle:
				case MediaType.LargeRectangle:
				case MediaType.WideSkyscraper:
				case MediaType.Leaderboard:
				{
					WWW wwwReader = new WWW(response.result.str_MediaURL);
					yield return wwwReader;

					if (wwwReader.error != null)
					{
						_loadingState = MediaLoadingState.Failed;
						Debug.LogError("wwwReader error: " + wwwReader.error);
					}
					else
					{
						switch (mediaFormat)
						{
							case "svg":
								ShowSVGImage(wwwReader);
								break;
							default:
								ShowImage(wwwReader);
								break;
						}
					}
					break;
				}
				case MediaType.LandscapeVideo:
				case MediaType.PortraitVideo:
				{
					ShowVideo(response.result.str_MediaURL);
					break;
				}
			}
		}

		IEnumerator RetrieveStreamingAsset(VreoResponse response)
		{
			string mediaUrl;

			if (response.success)
				mediaUrl = response.result.str_MediaURL;
			else
			{
				switch ((MediaType) response.result.ID_MediaType)
				{
					case MediaType.LandscapeVideo:
					case MediaType.PortraitVideo:
						mediaUrl = "vreo_placeholder_video.mp4";
						break;
					default:
						mediaUrl = "vreo_placeholder_image.jpg";
						break;
				}
			}

			var streamingMediaPath = Path.Combine(Application.streamingAssetsPath, mediaUrl);

			WWW wwwReader = new WWW(streamingMediaPath);
			yield return wwwReader;

			if (wwwReader.error != null)
			{
				_loadingState = MediaLoadingState.Failed;
				Debug.LogError("wwwReader error (" + streamingMediaPath + ") : " + wwwReader.error);
			}
			else
			{
				switch ((MediaType) response.result.ID_MediaType)
				{
					case MediaType.MediumRectangle:
					case MediaType.LargeRectangle:
					case MediaType.WideSkyscraper:
					case MediaType.Leaderboard:
						ShowImage(wwwReader);
						break;

					case MediaType.LandscapeVideo:
					case MediaType.PortraitVideo:
						ShowVideo(wwwReader.url);
						break;
				}
			}
		}

		// ==============================================================================

		public void OnMediaTypeChanged(MediaType type)
		{
			var desiredSize = SizeForResolutionType(type);
			var newScale = new Vector3((float) desiredSize.x / BaseSideLength, (float) desiredSize.y / BaseSideLength, 1.0f);
			transform.localScale = newScale;
		}
		
		void Update()
		{
			if (!Application.isPlaying)
				return;

			MovieQuad.Update();

			// send view data intermittently
			_sendViewDataTimer -= Time.deltaTime;
			if (_sendViewDataTimer <= 0.0f)
			{
				_sendViewDataTimer = SendViewDataTime;

				var viewStat = MovieQuad.GetViewingData();
				VreoCommunicate.__SendViewData(viewStat);
			}


			if (_loadingState == MediaLoadingState.Showing)
			{
				var type = (MediaType) CurrentVreoResponse.result.ID_MediaType;

				switch (type)
				{
					case MediaType.LandscapeVideo:
					case MediaType.PortraitVideo:
					{
						if (VideoPlayer)
						{
							//increment total play time
							if (_videoPaused == false && VideoPlayer.isPlaying)
								_playingTime += Time.deltaTime;
						}
						break;
					}
					case MediaType.MediumRectangle:
					case MediaType.LargeRectangle:
					case MediaType.WideSkyscraper:
					case MediaType.Leaderboard:
					{
						_playingTime += Time.deltaTime;

						if (autoPlayNew && _playingTime >= imageDuration)
							ShowAd(true);
						break;
					}
				}
			}
			else if (_loadingState == MediaLoadingState.Succeeded && (mediaType == MediaType.LandscapeVideo || mediaType == MediaType.PortraitVideo))
			{
				if (initialRandomDelay > 0)
				{
					initialRandomDelay -= Time.deltaTime;
					if (initialRandomDelay <= 0)
						VideoPlayer_PrepareCompleted(VideoPlayer);
				}
				else
				{
					if (MovieQuad.ScreenPercentage > proximity)
					{
						VideoPlayer_PrepareCompleted(VideoPlayer);
					}
				}
			}
		}

		// Pauses video playback when the app loses or gains focus
		void OnApplicationPause(bool wasPaused)
		{
			//Debug.Log("OnApplicationPause: " + wasPaused);
			if (VideoPlayer != null)
			{
				_videoPaused = wasPaused;
				if (_videoPaused && VideoPlayer.isPlaying)
					VideoPlayer.Pause();
				else if (VideoPlayer.isPrepared && (MovieQuad.ScreenPercentage > proximity))
					VideoPlayer.Play();
			}
		}

		void OnMouseUpAsButton()
		{
			if (!isClickable)
				return;

			print($"Ad spot with ID {spotId} was clicked.");

			if (CurrentVreoResponse != null && CurrentVreoResponse.result != null && !string.IsNullOrEmpty(CurrentVreoResponse.result.str_MediaURL))
			{
				Application.OpenURL(CurrentVreoResponse.result.str_Link);
			}
		}
	} // VreoMoviePlayer.cs
} // Namespace