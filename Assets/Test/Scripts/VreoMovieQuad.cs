using UnityEngine;

namespace VREO
{
    public class VreoMovieQuad
	{
		private Camera targetCamera;
		private VreoAdCanvas adCanvas;
        private AudioListener targetListener;

		private int numOccluderQueriesX = 4;
		private int numOccluderQueriesY = 3;
        
		private int numOccluderQueries;
		private Vector3[] occluderQueriesCoords = null;
		private Vector3[] cornerCoords = null;

		private bool debugLinesEnabled = false;

		private float total_hit_time = 0.0f;
		private float avg_screenPercent = 0.0f;
		private float avg_blockedAreaPercent = 0.0f;
        private float avg_volumePercent = 0.0f;

        private Vector2 avg_total_sceen_positon = new Vector2();

        private float systemViewTime = 0.0f;
        private float systemTime = 0.0f;

        private Vector3[] buf1 = new Vector3[16]; // just temporary buffers, big engough for handling all cases
        private Vector3[] buf2 = new Vector3[16];

        // ==============================================================================

        public VreoMovieQuad(VreoAdCanvas adCanvas)
		{
			this.adCanvas = adCanvas;
			this.targetCamera = Camera.main;

            this.targetListener = GameObject.FindObjectOfType<AudioListener>();

            total_hit_time = 0.0f;
            avg_screenPercent = 0.0f;
			avg_total_sceen_positon = Vector2.zero;

			// create the occulder query offsets
			numOccluderQueries = numOccluderQueriesX * numOccluderQueriesY;
			occluderQueriesCoords = new Vector3[numOccluderQueries];

			int i = 0;
			float xStep = 1.0f / (float)(numOccluderQueriesX);
			float yStep = 1.0f / (float)(numOccluderQueriesY);
			for (int y = 0; y < numOccluderQueriesY; y++)
			{
				for (int x = 0; x < numOccluderQueriesX; x++)
				{
					float xpos = (float)x * xStep + xStep * 0.5f;
					float ypos = (float)y * yStep + yStep * 0.5f;
					xpos = xpos - 0.5f; // center
					ypos = ypos - 0.5f;
					occluderQueriesCoords[i++] = new Vector3(xpos, ypos, 0.0f);
				}
			}

			// create the cornder coords
			cornerCoords = new Vector3[4];
			cornerCoords[0] = new Vector3(-0.5f, 0.5f, 0.0f);
			cornerCoords[1] = new Vector3(0.5f, 0.5f, 0.0f);
			cornerCoords[2] = new Vector3(0.5f, -0.5f, 0.0f);
			cornerCoords[3] = new Vector3(-0.5f, -0.5f, 0.0f);
		}

        // ==============================================================================
        // GetViewingData
        // ==============================================================================
        public ClassViewDataRequest GetViewingData()
        {
            ClassViewDataRequest viewStat = new ClassViewDataRequest();

            //viewStat.str_DevAccessToken = this.adCanvas.CurrentAdResponse.body.result.developer_transaction_token;
            //viewStat.developer_game_slot_id = this.adCanvas.developer_game_slot_id;

            //viewStat.advert = this.adCanvas.CurrentAdResponse.body.result.advertiser_ad_id;
            //viewStat.advertiser_ad_is_visual = true;
            //viewStat.advertiser_ad_is_aural = (this.adCanvas.mediaType == VreoAdCanvas.MediaType.MOVIE);

            viewStat.ID_Advertisement = adCanvas.CurrentAdResponse.result.ID_Advertisement.ToString();

            viewStat.dec_TotalHitTime = total_hit_time;
            viewStat.dec_TotalScreenPercentage = avg_screenPercent;
            viewStat.dec_TotalScreenPositionX = avg_total_sceen_positon.x;
            viewStat.dec_TotalScreenPositionY = avg_total_sceen_positon.y;
            viewStat.dec_TotalBlockedPercentage = avg_blockedAreaPercent;
            viewStat.dec_TotalVolumePercentage = avg_volumePercent;

            return viewStat;
        }

        // ==============================================================================
        // Update
        // ==============================================================================
        public void Update()
		{
			float blockedAreaPercent = 0.0f;
			int blockedCount = 0;
			float area = 0.0f;
			float percentualArea = 0.0f;

			// ignore 1st frame, cause of initialization
			if (systemTime > 0.0f)
			{
                avg_volumePercent = (avg_volumePercent * systemTime + ((CalculateAdVolume() * 100.0f) * Time.deltaTime)) / (systemTime + Time.deltaTime);

                // transform quad center point
                Vector3 worldCenterPoint = TransformToScreen(adCanvas.transform.TransformPoint(Vector3.zero));

				// transform quad corners
				Vector3 worldCoord0 = TransformToScreen(adCanvas.transform.TransformPoint(cornerCoords[0]));
				Vector3 worldCoord1 = TransformToScreen(adCanvas.transform.TransformPoint(cornerCoords[1]));
				Vector3 worldCoord2 = TransformToScreen(adCanvas.transform.TransformPoint(cornerCoords[2]));
				Vector3 worldCoord3 = TransformToScreen(adCanvas.transform.TransformPoint(cornerCoords[3]));

				// clip quad to viewport
				Vector3[] buffer;
				int count = 0;
				buffer = ClipPolygon(worldCoord0, worldCoord1, worldCoord2, worldCoord3, targetCamera.pixelRect, ref count);
				if (buffer != null)
				{
					// calculate the 2d area of the remaining quad
					area = CalculateBufferAreaSize(buffer, count);
                    if (area > 0.0f) // if the area is zero of negative, the quad is not visible
                    {
                        percentualArea = 100.0f * area / (float)(targetCamera.pixelRect.width * targetCamera.pixelRect.height);

                        blockedCount = 0;
                        for (int i = 0; i < numOccluderQueries; i++)
                        {
                            if (Physics.Linecast(targetCamera.transform.position, adCanvas.transform.TransformPoint(occluderQueriesCoords[i])))
                            {
                                blockedCount++;
#if UNITY_EDITOR
                                if (debugLinesEnabled == true)
                                    Debug.DrawLine(targetCamera.transform.position, adCanvas.transform.TransformPoint(occluderQueriesCoords[i]), Color.red);
#endif
                            }
#if UNITY_EDITOR
                            else
                            {
                                if (debugLinesEnabled == true)
                                    Debug.DrawLine(targetCamera.transform.position, adCanvas.transform.TransformPoint(occluderQueriesCoords[i]), Color.green);
                            }
#endif
                        }

                        blockedAreaPercent = 100.0f * (float)blockedCount / (float)numOccluderQueries;

                        float screen_percentage = area * (100.0f - blockedAreaPercent);
                        screen_percentage /= targetCamera.pixelRect.width * targetCamera.pixelRect.height;
                        avg_screenPercent = (avg_screenPercent * systemViewTime + screen_percentage * Time.deltaTime) / (systemViewTime + Time.deltaTime);

                        // averaging screen position
                        float x = worldCenterPoint.x - Screen.width * 0.5f;
                        float y = worldCenterPoint.y - Screen.height * 0.5f;
                        avg_total_sceen_positon.x = (avg_total_sceen_positon.x * systemViewTime + x * Time.deltaTime) / (systemViewTime + Time.deltaTime);
                        avg_total_sceen_positon.y = (avg_total_sceen_positon.y * systemViewTime + y * Time.deltaTime) / (systemViewTime + Time.deltaTime);
                        avg_total_sceen_positon.y = y;

                        avg_blockedAreaPercent = (avg_blockedAreaPercent * systemViewTime + blockedAreaPercent * Time.deltaTime) / (systemViewTime + Time.deltaTime);
                        systemViewTime += Time.deltaTime;
                    }
                }

				if (area > 0.0f && blockedAreaPercent < 100.0f)
				{
					total_hit_time += Time.deltaTime;
				}
			}

			systemTime += Time.deltaTime;
		}

		// ==============================================================================
		// TransformToScreen2
		// ==============================================================================
		private Vector3 TransformToScreen(Vector3 worldPt)
		{
			Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPt);
			screenPos.z = targetCamera.nearClipPlane;
			return screenPos;
		}

		// ==============================================================================
		// ClipTriangle
		// ==============================================================================
		private Vector3[] ClipPolygon(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Rect cameraPixelRect, ref int count)
		{
			float left = cameraPixelRect.xMin;
			float right = cameraPixelRect.xMax;
			float bottom = cameraPixelRect.yMin;
			float top = cameraPixelRect.yMax;
			float t;

			int i, count1, count2;

			buf1[0] = v0;
			buf1[1] = v1;
			buf1[2] = v2;
			buf1[3] = v3;
			buf1[4] = v0; // circular buffer
			count1 = 4;

			// --- CLIP LEFT ------------------------------------------------------
			count2 = 0;
			for (i = 0; i < count1; i++)
			{
				if (buf1[i].x >= left) // p1 in
				{
					if (buf1[i + 1].x >= left) // p2 in
						buf2[count2++] = buf1[i];
					else // p2 out
					{
						buf2[count2++] = buf1[i];
						t = (left - buf1[i].x) / (buf1[i + 1].x - buf1[i].x);
						buf2[count2++] = buf1[i] + t * (buf1[i + 1] - buf1[i]);
					}
				}
				else // p1 out
				{
					if (buf1[i + 1].x >= left) // p2 in
					{
						t = (left - buf1[i].x) / (buf1[i + 1].x - buf1[i].x);
						buf2[count2++] = buf1[i] + t * (buf1[i + 1] - buf1[i]);
					}
				}
			}
			if (count2 == 0) // return if empty
				return null;
			buf2[count2] = buf2[0]; // close circular buffer


			// --- CLIP RIGHT ------------------------------------------------------
			count1 = 0;
			for (i = 0; i < count2; i++)
			{
				if (buf2[i].x < right) // p1 in
				{
					if (buf2[i + 1].x < right) // p2 in
						buf1[count1++] = buf2[i];
					else // p2 out
					{
						buf1[count1++] = buf2[i];
						t = (right - buf2[i].x) / (buf2[i + 1].x - buf2[i].x);
						buf1[count1++] = buf2[i] + t * (buf2[i + 1] - buf2[i]);
					}
				}
				else // p1 out
				{
					if (buf2[i + 1].x < right) // p2 in
					{
						t = (right - buf2[i].x) / (buf2[i + 1].x - buf2[i].x);
						buf1[count1++] = buf2[i] + t * (buf2[i + 1] - buf2[i]);
					}
				}
			}
			if (count1 == 0) // return if empty
				return null;
			buf1[count1] = buf1[0]; // close circular buffer


			// --- CLIP BOTTOM ------------------------------------------------------
			count2 = 0;
			for (i = 0; i < count1; i++)
			{
				if (buf1[i].y >= bottom) // p1 in
				{
					if (buf1[i + 1].y >= bottom) // p2 in
						buf2[count2++] = buf1[i];
					else // p2 out
					{
						buf2[count2++] = buf1[i];
						t = (bottom - buf1[i].y) / (buf1[i + 1].y - buf1[i].y);
						buf2[count2++] = buf1[i] + t * (buf1[i + 1] - buf1[i]);
					}
				}
				else // p1 out
				{
					if (buf1[i + 1].y >= bottom) // p2 in
					{
						t = (bottom - buf1[i].y) / (buf1[i + 1].y - buf1[i].y);
						buf2[count2++] = buf1[i] + t * (buf1[i + 1] - buf1[i]);
					}
				}
			}
			if (count2 == 0) // return if empty
				return null;
			buf2[count2] = buf2[0]; // close circular buffer


			// --- CLIP TOP ------------------------------------------------------
			count1 = 0;
			for (i = 0; i < count2; i++)
			{
				if (buf2[i].y < top) // p1 in
				{
					if (buf2[i + 1].y < top) // p2 in
						buf1[count1++] = buf2[i];
					else // p2 out
					{
						buf1[count1++] = buf2[i];
						t = (top - buf2[i].y) / (buf2[i + 1].y - buf2[i].y);
						buf1[count1++] = buf2[i] + t * (buf2[i + 1] - buf2[i]);
					}
				}
				else // p1 out
				{
					if (buf2[i + 1].y < top) // p2 in
					{
						t = (top - buf2[i].y) / (buf2[i + 1].y - buf2[i].y);
						buf1[count1++] = buf2[i] + t * (buf2[i + 1] - buf2[i]);
					}
				}
			}
			if (count1 == 0) // return if empty
				return null;
			buf1[count1] = buf1[0]; // close circular buffer

			count = count2;

			return buf1;

		}


		// ==============================================================================
		// CalculateBufferAreaSiz
		// ==============================================================================
		private float CalculateBufferAreaSize(Vector3[] buffer, int count)
		{
			// now we triangulate the clipped polygon to calculate it's total area
			float totalArea = 0.0f;
			Vector3 p0, p1, p2;
			p0 = buffer[0];
			for (int i = 0; i < count - 2; i++)
			{
				p1 = buffer[i + 2] - p0;
				p2 = buffer[i + 1] - p0;
				totalArea += p1.x * p2.y - p1.y * p2.x; // cross product
			}

			return totalArea * 0.5f; // divided by 2

		}

        // ==============================================================================
        // CalculateAdVolume
        // ==============================================================================
        private float CalculateAdVolume()
        {
            if (this.adCanvas.mediaType == VreoAdCanvas.MediaType.MOVIE)
            {
                AudioSource audioSource = this.adCanvas.VideoPlayer.GetTargetAudioSource(0);

                float adVolume = 0;
                float listenerDistance = Vector3.Distance(audioSource.transform.position, this.targetListener.transform.position);

                switch (audioSource.rolloffMode)
                {
                    case AudioRolloffMode.Custom:
                        adVolume = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(listenerDistance);
                        break;
                    case AudioRolloffMode.Linear:
                        adVolume = AnimationCurve.Linear(audioSource.minDistance, 1, audioSource.maxDistance, 0).Evaluate(listenerDistance);
                        break;
                    case AudioRolloffMode.Logarithmic:
                        float t = (listenerDistance - audioSource.minDistance) / (audioSource.maxDistance - audioSource.minDistance);
                        adVolume = logerp(1, 0, t);
                        break;
                }

                adVolume *= audioSource.spatialBlend;
                adVolume *= audioSource.volume;

                return adVolume;
            }

            return 0;
        }

        private float logerp(float a, float b, float t)
        {
            return a * Mathf.Pow(b / a, t);
        }

    } // VreoMovieQuad.cs
} // Namespace