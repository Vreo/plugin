using UnityEngine;
using System.Collections;                   // required for Coroutines
using System.Runtime.InteropServices;       // required for DllImport
using System;                               // requred for IntPtr
using System.IO;                            // required for File
using System.Reflection;                    // required for using potentially non-existant Vector functions

using UnityEngine.Video;                    // required for VideoPlayer
//using Unity.VectorGraphics;

namespace VREO
{
	[RequireComponent(typeof(VideoPlayer))]
	public class VreoAdCanvas : MonoBehaviour
	{
        private const float SEND_VIEW_DATA_TIME = 600.0f;

		public enum MediaLoadingState
        {
            Unknown = -1,
            Waiting = 0,
            Loading = 1,
            Succeeded = 1,
            Failed = 2,
            Showing = 3,
        };
        public enum MediaType
        {
            UNKNOWN = 0,
			MOVIE = 1,
            IMAGE = 2,
            BANNER = 3,
            LOGO_SQUARE = 4,
            LOGO_WIDE = 5,
        };

		// ==============================================================================

		public MediaType mediaType = MediaType.MOVIE;
        public string developer_game_slot_id = "0";

        public bool playOnAwake = true;
		public bool autoPlayNew = true;

		[Tooltip("Aids performance when trying to load several video ads at the same time")]
		public float initialRandomDelay = 0.0f;
		public float imageDuration = 10.0f;

        // ==============================================================================

        private bool _initialized = false;

		private MediaLoadingState loadingState = MediaLoadingState.Waiting;
		private float playingTime = 0.0f;
		private bool videoPaused = false;

		private VideoPlayer videoPlayer;
		private Renderer renderer;

		private ClassRandomAdResponse currentAdResponse;

		private VreoMovieQuad movieQuad;

        private float sendViewDataTimer = SEND_VIEW_DATA_TIME;

        // ==============================================================================

        public delegate void VideoLoadedAndReadyType(); // declare delegate type
		public VideoLoadedAndReadyType VideoIsLoadedCallback; // to store the function

		public MediaLoadingState LoadingState { get { return loadingState; } }
		public bool AdIsShowing { get { return (loadingState == MediaLoadingState.Showing); } }
		public float AdTotalShowTime { get { return playingTime; } }
        public float AdDuration
        {
            get
            {
                switch (mediaType)
                {
                    case MediaType.IMAGE:
                    case MediaType.BANNER:
                    case MediaType.LOGO_SQUARE:
                    case MediaType.LOGO_WIDE:
                        return imageDuration;

                    case MediaType.MOVIE:
                        return (videoPlayer != null && videoPlayer.clip != null) ? (float)videoPlayer.clip.length : 0;

                    default:
                        return 0;
                }
            }
        }


        public ClassRandomAdResponse CurrentAdResponse { get { return currentAdResponse; } }
        public VideoPlayer VideoPlayer { get { return videoPlayer; } }
		public VreoMovieQuad MovieQuad { get { return movieQuad; } }

		// ==============================================================================

		private void Awake()
		{
            if(!this._initialized)
            {
                sendViewDataTimer -= UnityEngine.Random.Range(1, 20);
                initialRandomDelay = UnityEngine.Random.Range(0.0f, initialRandomDelay);

                videoPlayer = GetComponent<VideoPlayer>();
                renderer = GetComponent<Renderer>();
                movieQuad = new VreoMovieQuad(this);

                if (videoPlayer == null)
                    Debug.LogError("Missing Attached VideoPlayer Component");
                else
                {
                    videoPlayer.prepareCompleted += VideoPlayer_PrepareCompleted;
                    videoPlayer.loopPointReached += VideoPlayer_LoopPointReached;
                }

                if (renderer == null)
                    Debug.LogError("Missing Attached Renderer Component");

                this._initialized = true;
            }
		}

        private void OnEnable()
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
			if (loadingState == MediaLoadingState.Waiting || loadingState == MediaLoadingState.Failed || force)
			{
				loadingState = MediaLoadingState.Loading;
				VreoCommunicate.RequestRandomAd(mediaType, RandomAdCallback);
			}
		}

		// ==============================================================================

		private void RandomAdCallback(ClassRandomAdResponse response)
		{
			// update local response reference for external access
			this.currentAdResponse = response;

			if (response.success)
			{
				if (response.result.str_MediaURL != string.Empty)
				{               
					if (response.result.str_MediaURL.ToLower().StartsWith("http"))
						StartCoroutine(DownloadAdMediaAndLoad(response));
					else
						StartCoroutine(RetrieveStreamingAsset(response));
				}
			}
            else
                StartCoroutine(RetrieveStreamingAsset(response));
        }

		// ==============================================================================

		private void VideoPlayer_PrepareCompleted(VideoPlayer source)
        {
			if (source.isPrepared)
			{
                playingTime = 0;
				loadingState = MediaLoadingState.Succeeded;

				if(initialRandomDelay <= 0)
                    videoPlayer.Play();

				if (videoPlayer.isPlaying)
					loadingState = MediaLoadingState.Showing;
			}
			else
				loadingState = MediaLoadingState.Failed;
        }

		private void VideoPlayer_LoopPointReached(VideoPlayer source)
        {
			if(!source.isLooping)
			    source.Stop();

			if(autoPlayNew)
			    ShowAd(true);
        }

		private void ShowVideo(string url)
		{         
			if (videoPlayer.isPlaying)
                videoPlayer.Stop();

            // set new video player url
			videoPlayer.url = url;
            videoPlayer.isLooping = false;

            // prepare the video
            videoPlayer.Prepare();
		}

		// ==============================================================================

		private void ShowImage(WWW wwwReader)
		{
			loadingState = MediaLoadingState.Succeeded;
            
            // apply it to the texture
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGB24, false);
            texture.anisoLevel = 8;
            texture.wrapMode = TextureWrapMode.Clamp;
            wwwReader.LoadImageIntoTexture(texture);

            if (texture != null)
            {
                renderer.material.mainTexture = texture;
                renderer.enabled = true;

                loadingState = MediaLoadingState.Showing;
                playingTime = 0;
            }
		}

        private void ShowSVGImage(WWW wwwReader)
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

        private Sprite BuildSVGSprite(string svg)
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

		private IEnumerator DownloadAdMediaAndLoad(ClassRandomAdResponse response)
        {
			int mediaType = response.result.ID_MediaType;
            string mediaFormat = response.result.str_MediaTypeName;

			switch ((MediaType)mediaType)
            {
				case MediaType.IMAGE:
                case MediaType.BANNER:
                case MediaType.LOGO_SQUARE:
                case MediaType.LOGO_WIDE:
					{
						WWW wwwReader = new WWW(response.result.str_MediaURL);                  
                        yield return wwwReader;

						if (wwwReader.error != null)
						{
							loadingState = MediaLoadingState.Failed;
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
					}
					break;

                case MediaType.MOVIE:
                    {
						ShowVideo(response.result.str_MediaURL);
                    }
                    break;
            }
        }

		private IEnumerator RetrieveStreamingAsset(ClassRandomAdResponse response)
        {
            string mediaURL = "";

            if (response.success)
                mediaURL = response.result.str_MediaURL;
            else
            {
                switch ((MediaType)response.result.ID_MediaType)
                {
                    case MediaType.MOVIE:
                        mediaURL = "vreo_placeholder_video.mp4";
                        break;
                    default:
                        mediaURL = "vreo_placeholder_image.jpg";
                        break;
                    }
            }

            //string streamingMediaPath = "file:///" + mediaFileName;
            string streamingMediaPath = "file://" + Application.streamingAssetsPath + "/" + mediaURL;

            WWW wwwReader = new WWW(streamingMediaPath);
            yield return wwwReader;

			if (wwwReader.error != null)
			{
				loadingState = MediaLoadingState.Failed;
				Debug.LogError("wwwReader error (" + streamingMediaPath + ") : " + wwwReader.error);
			}
			else
			{
				int media_type = response.result.ID_MediaType;

				switch ((MediaType)media_type)
				{
                    case MediaType.IMAGE:
                    case MediaType.BANNER:
                    case MediaType.LOGO_SQUARE:
                    case MediaType.LOGO_WIDE:
                        ShowImage(wwwReader);
						break;

					case MediaType.MOVIE:
							ShowVideo(wwwReader.url);
						break;
				}
			}
        }

		// ==============================================================================

		private void Update()
		{
			movieQuad.Update();

            // send view data intermittently
            sendViewDataTimer -= Time.deltaTime;
            if (sendViewDataTimer <= 0.0f)
            {
                sendViewDataTimer = SEND_VIEW_DATA_TIME;

                ClassViewDataRequest viewStat = this.MovieQuad.GetViewingData();
                VreoCommunicate.__SendViewData(viewStat);
            }


            if (loadingState == MediaLoadingState.Showing)
			{
				int media_type = CurrentAdResponse.result.ID_MediaType;

				switch ((MediaType)media_type)
				{
					case MediaType.MOVIE:
						{
							if (videoPlayer != null)
							{
								//increment total play time
								if (videoPaused == false && videoPlayer.isPlaying)
									playingTime += Time.deltaTime;
							}
						}
						break;
                    case MediaType.IMAGE:
                    case MediaType.BANNER:
                    case MediaType.LOGO_SQUARE:
                    case MediaType.LOGO_WIDE:
                        {
							playingTime += Time.deltaTime;

							if (autoPlayNew && playingTime >= imageDuration)
								ShowAd(true);
						}
						break;
				}
			}
			else if (loadingState == MediaLoadingState.Succeeded && mediaType == MediaType.MOVIE)
			{
				if (initialRandomDelay > 0)
				{
					initialRandomDelay -= Time.deltaTime;
					if (initialRandomDelay <= 0)
						VideoPlayer_PrepareCompleted(videoPlayer);
				}
			}
		}

		// Pauses video playback when the app loses or gains focus
		private void OnApplicationPause(bool wasPaused)
		{
			Debug.Log("OnApplicationPause: " + wasPaused);
			if (videoPlayer != null)
			{
				videoPaused = wasPaused;
				if (videoPaused && videoPlayer.isPlaying)
					videoPlayer.Pause();
				else if(videoPlayer.isPrepared)
					videoPlayer.Play();
			}
		}      

	} // VreoMoviePlayer.cs
} // Namespace

