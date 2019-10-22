using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Video;

namespace VREO
{
	[RequireComponent(typeof(VideoPlayer))]
	public class VreoAdCanvas : MonoBehaviour
	{
		const float SendViewDataTime = 600.0f;

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
			Movie = 1,
			Image = 2,
			Banner = 3,
			LogoSquare = 4,
			LogoWide = 5,
		}

		public enum ResolutionType
		{
			MediumRectangle = 1,
			LargeRectangle = 2,
			WideSkyscraper = 3,
			Leaderboard = 4,
			LandscapeVideo = 5,
			PortraitVideo = 6
		}

		// ==============================================================================

		public MediaType mediaType = MediaType.Movie;
		public string developerGameSlotId = "0";

		public bool playOnAwake = true;
		public bool autoPlayNew = true;

		[Tooltip("Aids performance when trying to load several video ads at the same time")]
		public float initialRandomDelay;

		public float imageDuration = 10.0f;

		// ==============================================================================

		bool _initialized;

		MediaLoadingState _loadingState = MediaLoadingState.Waiting;
		float _playingTime;
		bool _videoPaused;

		Renderer _renderer;

		float sendViewDataTimer = SendViewDataTime;

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
					case MediaType.Image:
					case MediaType.Banner:
					case MediaType.LogoSquare:
					case MediaType.LogoWide:
						return imageDuration;

					case MediaType.Movie:
						return (VideoPlayer != null && VideoPlayer.clip != null) ? (float) VideoPlayer.clip.length : 0;

					default:
						return 0;
				}
			}
		}


		public VreoResponse CurrentVreoResponse { get; private set; }

		public VideoPlayer VideoPlayer { get; private set; }
		public VreoMovieQuad MovieQuad { get; private set; }

		public static Vector2Int SizeForResolutionType(ResolutionType type)
		{
			Vector2Int result;
			switch (type)
			{
				case ResolutionType.MediumRectangle:
					result = new Vector2Int(300, 250);
					break;
				case ResolutionType.LargeRectangle:
					result = new Vector2Int(300, 600);
					break;
				case ResolutionType.WideSkyscraper:
					result = new Vector2Int(160, 600);
					break;
				case ResolutionType.Leaderboard:
					result = new Vector2Int(728, 90);
					break;
				case ResolutionType.LandscapeVideo:
					result = new Vector2Int(540, 300);
					break;
				case ResolutionType.PortraitVideo:
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
			if (!_initialized)
			{
				sendViewDataTimer -= UnityEngine.Random.Range(1, 20);
				initialRandomDelay = UnityEngine.Random.Range(0.0f, initialRandomDelay);

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
				VreoCommunicate.RequestRandomAd(mediaType, RandomAdCallback);
			}
		}

		// ==============================================================================

		void RandomAdCallback(VreoResponse response)
		{
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
			if (source.isPrepared)
			{
				_playingTime = 0;
				_loadingState = MediaLoadingState.Succeeded;

				if (initialRandomDelay <= 0)
					VideoPlayer.Play();

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
			var mediaType = response.result.ID_MediaType;
			string mediaFormat = response.result.str_MediaTypeName;

			switch ((MediaType) mediaType)
			{
				case MediaType.Image:
				case MediaType.Banner:
				case MediaType.LogoSquare:
				case MediaType.LogoWide:
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
				case MediaType.Movie:
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
					case MediaType.Movie:
						mediaUrl = "vreo_placeholder_video.mp4";
						break;
					default:
						mediaUrl = "vreo_placeholder_image.jpg";
						break;
				}
			}

			//string streamingMediaPath = "file:///" + mediaFileName;
			string streamingMediaPath = "file://" + Application.streamingAssetsPath + "/" + mediaUrl;

			WWW wwwReader = new WWW(streamingMediaPath);
			yield return wwwReader;

			if (wwwReader.error != null)
			{
				_loadingState = MediaLoadingState.Failed;
				Debug.LogError("wwwReader error (" + streamingMediaPath + ") : " + wwwReader.error);
			}
			else
			{
				int media_type = response.result.ID_MediaType;

				switch ((MediaType) media_type)
				{
					case MediaType.Image:
					case MediaType.Banner:
					case MediaType.LogoSquare:
					case MediaType.LogoWide:
						ShowImage(wwwReader);
						break;

					case MediaType.Movie:
						ShowVideo(wwwReader.url);
						break;
				}
			}
		}

		// ==============================================================================

		void Update()
		{
			MovieQuad.Update();

			// send view data intermittently
			sendViewDataTimer -= Time.deltaTime;
			if (sendViewDataTimer <= 0.0f)
			{
				sendViewDataTimer = SendViewDataTime;

				ViewDataRequest viewStat = MovieQuad.GetViewingData();
				VreoCommunicate.__SendViewData(viewStat);
			}


			if (_loadingState == MediaLoadingState.Showing)
			{
				int media_type = CurrentVreoResponse.result.ID_MediaType;

				switch ((MediaType) media_type)
				{
					case MediaType.Movie:
					{
						if (VideoPlayer != null)
						{
							//increment total play time
							if (_videoPaused == false && VideoPlayer.isPlaying)
								_playingTime += Time.deltaTime;
						}
					}
						break;
					case MediaType.Image:
					case MediaType.Banner:
					case MediaType.LogoSquare:
					case MediaType.LogoWide:
					{
						_playingTime += Time.deltaTime;

						if (autoPlayNew && _playingTime >= imageDuration)
							ShowAd(true);
					}
						break;
				}
			}
			else if (_loadingState == MediaLoadingState.Succeeded && mediaType == MediaType.Movie)
			{
				if (initialRandomDelay > 0)
				{
					initialRandomDelay -= Time.deltaTime;
					if (initialRandomDelay <= 0)
						VideoPlayer_PrepareCompleted(VideoPlayer);
				}
			}
		}

		// Pauses video playback when the app loses or gains focus
		void OnApplicationPause(bool wasPaused)
		{
			Debug.Log("OnApplicationPause: " + wasPaused);
			if (VideoPlayer != null)
			{
				_videoPaused = wasPaused;
				if (_videoPaused && VideoPlayer.isPlaying)
					VideoPlayer.Pause();
				else if (VideoPlayer.isPrepared)
					VideoPlayer.Play();
			}
		}
	} // VreoMoviePlayer.cs
} // Namespace