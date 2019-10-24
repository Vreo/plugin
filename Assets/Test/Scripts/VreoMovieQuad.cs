using UnityEngine;

namespace VREO
{
	public class VreoMovieQuad
	{
		Camera _targetCamera;
		VreoAdCanvas _adCanvas;
		AudioListener _targetListener;

		int _numOccluderQueriesX = 4;
		int _numOccluderQueriesY = 3;

		int _numOccluderQueries;
		Vector3[] _occluderQueriesCoords;
		Vector3[] _cornerCoords;

		bool debugLinesEnabled = false;

		float _totalHitTime;
		float _avgScreenPercent;
		float _avgBlockedAreaPercent;
		float _avgVolumePercent;

		Vector2 _avgTotalScreenPosition;

		float _systemViewTime;
		float _systemTime;

		Vector3[] _buf1 = new Vector3[16]; // just temporary buffers, big enough for handling all cases
		Vector3[] _buf2 = new Vector3[16];

		// ==============================================================================

		public VreoMovieQuad(VreoAdCanvas adCanvas)
		{
			_adCanvas = adCanvas;
			_targetCamera = Camera.main;

			_targetListener = Object.FindObjectOfType<AudioListener>();

			_totalHitTime = 0.0f;
			_avgScreenPercent = 0.0f;
			_avgTotalScreenPosition = Vector2.zero;

			// create the occluder query offsets
			_numOccluderQueries = _numOccluderQueriesX * _numOccluderQueriesY;
			_occluderQueriesCoords = new Vector3[_numOccluderQueries];

			var i = 0;
			var xStep = 1f / _numOccluderQueriesX;
			var yStep = 1f / _numOccluderQueriesY;
			for (var y = 0; y < _numOccluderQueriesY; y++)
			{
				for (var x = 0; x < _numOccluderQueriesX; x++)
				{
					var xPos = x * xStep + xStep * 0.5f;
					var yPos = y * yStep + yStep * 0.5f;
					xPos -= 0.5f;
					yPos -= 0.5f;
					_occluderQueriesCoords[i++] = new Vector3(xPos, yPos, 0.0f);
				}
			}

			// create the corner coords
			_cornerCoords = new Vector3[4];
			_cornerCoords[0] = new Vector3(-0.5f, 0.5f, 0.0f);
			_cornerCoords[1] = new Vector3(0.5f, 0.5f, 0.0f);
			_cornerCoords[2] = new Vector3(0.5f, -0.5f, 0.0f);
			_cornerCoords[3] = new Vector3(-0.5f, -0.5f, 0.0f);
		}

		// ==============================================================================
		// GetViewingData
		// ==============================================================================
		public ViewDataRequest GetViewingData()
		{
			var viewStat = new ViewDataRequest
			{
				ID_Advertisement = _adCanvas.CurrentVreoResponse.result.ID_Advertisement,
				dec_TotalHitTime = _totalHitTime,
				dec_TotalScreenPercentage = _avgScreenPercent,
				dec_TotalScreenPositionX = _avgTotalScreenPosition.x,
				dec_TotalScreenPositionY = _avgTotalScreenPosition.y,
				dec_TotalBlockedPercentage = _avgBlockedAreaPercent,
				dec_TotalVolumePercentage = _avgVolumePercent
			};

			return viewStat;
		}

		// ==============================================================================
		// Update
		// ==============================================================================
		public void Update()
		{
			var blockedAreaPercent = 0f;
			var area = 0f;
			var percentualArea = 0f;

			// ignore 1st frame, cause of initialization
			if (_systemTime > 0f)
			{
				_avgVolumePercent =
					(_avgVolumePercent * _systemTime + ((CalculateAdVolume() * 100.0f) * Time.deltaTime)) /
					(_systemTime + Time.deltaTime);

				// transform quad center point
				var worldCenterPoint = TransformToScreen(_adCanvas.transform.TransformPoint(Vector3.zero));

				// transform quad corners
				var worldCoord0 = TransformToScreen(_adCanvas.transform.TransformPoint(_cornerCoords[0]));
				var worldCoord1 = TransformToScreen(_adCanvas.transform.TransformPoint(_cornerCoords[1]));
				var worldCoord2 = TransformToScreen(_adCanvas.transform.TransformPoint(_cornerCoords[2]));
				var worldCoord3 = TransformToScreen(_adCanvas.transform.TransformPoint(_cornerCoords[3]));

				// clip quad to viewport
				var count = 0;
				var buffer = ClipPolygon(worldCoord0, worldCoord1, worldCoord2, worldCoord3, _targetCamera.pixelRect,
					ref count);
				if (buffer != null)
				{
					// calculate the 2d area of the remaining quad
					area = CalculateBufferAreaSize(buffer, count);
					if (area > 0f) // if the area is zero of negative, the quad is not visible
					{
						percentualArea = 100f * area / (_targetCamera.pixelRect.width * _targetCamera.pixelRect.height);

						var blockedCount = 0;
						for (var i = 0; i < _numOccluderQueries; i++)
						{
							if (Physics.Linecast(_targetCamera.transform.position,
								_adCanvas.transform.TransformPoint(_occluderQueriesCoords[i])))
							{
								blockedCount++;
#if UNITY_EDITOR
								if (debugLinesEnabled)
									Debug.DrawLine(_targetCamera.transform.position,
										_adCanvas.transform.TransformPoint(_occluderQueriesCoords[i]), Color.red);
#endif
							}
#if UNITY_EDITOR
							else
							{
								if (debugLinesEnabled)
									Debug.DrawLine(_targetCamera.transform.position,
										_adCanvas.transform.TransformPoint(_occluderQueriesCoords[i]), Color.green);
							}
#endif
						}

						blockedAreaPercent = 100.0f * blockedCount / _numOccluderQueries;

						var screenPercentage = area * (100.0f - blockedAreaPercent);
						screenPercentage /= _targetCamera.pixelRect.width * _targetCamera.pixelRect.height;
						_avgScreenPercent = (_avgScreenPercent * _systemViewTime + screenPercentage * Time.deltaTime) /
						                    (_systemViewTime + Time.deltaTime);

						// averaging screen position
						var x = worldCenterPoint.x - Screen.width * 0.5f;
						var y = worldCenterPoint.y - Screen.height * 0.5f;
						_avgTotalScreenPosition.x = (_avgTotalScreenPosition.x * _systemViewTime + x * Time.deltaTime) /
						                            (_systemViewTime + Time.deltaTime);
						_avgTotalScreenPosition.y = (_avgTotalScreenPosition.y * _systemViewTime + y * Time.deltaTime) /
						                            (_systemViewTime + Time.deltaTime);
						_avgTotalScreenPosition.y = y;

						_avgBlockedAreaPercent =
							(_avgBlockedAreaPercent * _systemViewTime + blockedAreaPercent * Time.deltaTime) /
							(_systemViewTime + Time.deltaTime);
						_systemViewTime += Time.deltaTime;
					}
				}

				if (area > 0.0f && blockedAreaPercent < 100f)
				{
					_totalHitTime += Time.deltaTime;
				}
			}

			_systemTime += Time.deltaTime;
		}

		// ==============================================================================
		// TransformToScreen2
		// ==============================================================================
		Vector3 TransformToScreen(Vector3 worldPt)
		{
			var screenPos = _targetCamera.WorldToScreenPoint(worldPt);
			screenPos.z = _targetCamera.nearClipPlane;
			return screenPos;
		}

		// ==============================================================================
		// ClipTriangle
		// ==============================================================================
		Vector3[] ClipPolygon(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Rect cameraPixelRect,
			ref int count)
		{
			var left = cameraPixelRect.xMin;
			var right = cameraPixelRect.xMax;
			var bottom = cameraPixelRect.yMin;
			var top = cameraPixelRect.yMax;
			float t;

			_buf1[0] = v0;
			_buf1[1] = v1;
			_buf1[2] = v2;
			_buf1[3] = v3;
			_buf1[4] = v0; // circular buffer
			var count1 = 4;

			// --- CLIP LEFT ------------------------------------------------------
			var count2 = 0;
			for (var i = 0; i < count1; i++)
			{
				if (_buf1[i].x >= left) // p1 in
				{
					if (_buf1[i + 1].x >= left) // p2 in
						_buf2[count2++] = _buf1[i];
					else // p2 out
					{
						_buf2[count2++] = _buf1[i];
						t = (left - _buf1[i].x) / (_buf1[i + 1].x - _buf1[i].x);
						_buf2[count2++] = _buf1[i] + t * (_buf1[i + 1] - _buf1[i]);
					}
				}
				else // p1 out
				{
					if (_buf1[i + 1].x >= left) // p2 in
					{
						t = (left - _buf1[i].x) / (_buf1[i + 1].x - _buf1[i].x);
						_buf2[count2++] = _buf1[i] + t * (_buf1[i + 1] - _buf1[i]);
					}
				}
			}

			if (count2 == 0) // return if empty
				return null;
			_buf2[count2] = _buf2[0]; // close circular buffer


			// --- CLIP RIGHT ------------------------------------------------------
			count1 = 0;
			for (var i = 0; i < count2; i++)
			{
				if (_buf2[i].x < right) // p1 in
				{
					if (_buf2[i + 1].x < right) // p2 in
						_buf1[count1++] = _buf2[i];
					else // p2 out
					{
						_buf1[count1++] = _buf2[i];
						t = (right - _buf2[i].x) / (_buf2[i + 1].x - _buf2[i].x);
						_buf1[count1++] = _buf2[i] + t * (_buf2[i + 1] - _buf2[i]);
					}
				}
				else // p1 out
				{
					if (_buf2[i + 1].x < right) // p2 in
					{
						t = (right - _buf2[i].x) / (_buf2[i + 1].x - _buf2[i].x);
						_buf1[count1++] = _buf2[i] + t * (_buf2[i + 1] - _buf2[i]);
					}
				}
			}

			if (count1 == 0) // return if empty
				return null;
			_buf1[count1] = _buf1[0]; // close circular buffer


			// --- CLIP BOTTOM ------------------------------------------------------
			count2 = 0;
			for (var i = 0; i < count1; i++)
			{
				if (_buf1[i].y >= bottom) // p1 in
				{
					if (_buf1[i + 1].y >= bottom) // p2 in
						_buf2[count2++] = _buf1[i];
					else // p2 out
					{
						_buf2[count2++] = _buf1[i];
						t = (bottom - _buf1[i].y) / (_buf1[i + 1].y - _buf1[i].y);
						_buf2[count2++] = _buf1[i] + t * (_buf1[i + 1] - _buf1[i]);
					}
				}
				else // p1 out
				{
					if (_buf1[i + 1].y >= bottom) // p2 in
					{
						t = (bottom - _buf1[i].y) / (_buf1[i + 1].y - _buf1[i].y);
						_buf2[count2++] = _buf1[i] + t * (_buf1[i + 1] - _buf1[i]);
					}
				}
			}

			if (count2 == 0) // return if empty
				return null;
			_buf2[count2] = _buf2[0]; // close circular buffer


			// --- CLIP TOP ------------------------------------------------------
			count1 = 0;
			for (var i = 0; i < count2; i++)
			{
				if (_buf2[i].y < top) // p1 in
				{
					if (_buf2[i + 1].y < top) // p2 in
						_buf1[count1++] = _buf2[i];
					else // p2 out
					{
						_buf1[count1++] = _buf2[i];
						t = (top - _buf2[i].y) / (_buf2[i + 1].y - _buf2[i].y);
						_buf1[count1++] = _buf2[i] + t * (_buf2[i + 1] - _buf2[i]);
					}
				}
				else // p1 out
				{
					if (_buf2[i + 1].y < top) // p2 in
					{
						t = (top - _buf2[i].y) / (_buf2[i + 1].y - _buf2[i].y);
						_buf1[count1++] = _buf2[i] + t * (_buf2[i + 1] - _buf2[i]);
					}
				}
			}

			if (count1 == 0) // return if empty
				return null;
			_buf1[count1] = _buf1[0]; // close circular buffer

			count = count2;

			return _buf1;
		}


		// ==============================================================================
		// CalculateBufferAreaSiz
		// ==============================================================================
		float CalculateBufferAreaSize(Vector3[] buffer, int count)
		{
			// now we triangulate the clipped polygon to calculate it's total area
			var totalArea = 0f;
			for (var i = 0; i < count - 2; i++)
			{
				var p0 = buffer[0];
				var p1 = buffer[i + 2] - p0;
				var p2 = buffer[i + 1] - p0;
				totalArea += (p1.x * p2.y - p1.y * p2.x)  / 2;
			}

			return totalArea;
		}

		// ==============================================================================
		// CalculateAdVolume
		// ==============================================================================
		float CalculateAdVolume()
		{
			if (_adCanvas.mediaType == VreoAdCanvas.MediaType.PortraitVideo || _adCanvas.mediaType == VreoAdCanvas.MediaType.LandscapeVideo)
			{
				var audioSource = _adCanvas.VideoPlayer.GetTargetAudioSource(0);

				var adVolume = 0f;
				var listenerDistance = Vector3.Distance(audioSource.transform.position,
					_targetListener.transform.position);

				switch (audioSource.rolloffMode)
				{
					case AudioRolloffMode.Custom:
						adVolume = audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff)
							.Evaluate(listenerDistance);
						break;
					case AudioRolloffMode.Linear:
						adVolume = AnimationCurve.Linear(audioSource.minDistance, 1, audioSource.maxDistance, 0)
							.Evaluate(listenerDistance);
						break;
					case AudioRolloffMode.Logarithmic:
						var t = (listenerDistance - audioSource.minDistance) /
						          (audioSource.maxDistance - audioSource.minDistance);
						adVolume = Logerp(1, 0, t);
						break;
				}

				adVolume *= audioSource.spatialBlend;
				adVolume *= audioSource.volume;

				return adVolume;
			}

			return 0;
		}

		float Logerp(float a, float b, float t)
		{
			return a * Mathf.Pow(b / a, t);
		}
	} // VreoMovieQuad.cs
} // Namespace