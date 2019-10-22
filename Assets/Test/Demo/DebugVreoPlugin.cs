using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using TMPro;

using VREO;

public class DebugVreoPlugin: MonoBehaviour
{
    public VreoCommunicate communicator;
    public VreoAdCanvas target_vrAdCanvas;

    public TextMeshProUGUI debugText;

    private void Start()
    {
        //TestSendViewingData();
    }

    // ==============================================================================

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        if (Input.GetKeyDown(KeyCode.S))
            TestSendViewingData();

		if (target_vrAdCanvas.AdIsShowing)
		{
			// print stats on screen every 5 frames whilst ad is showing
			ClassViewDataRequest viewStat = target_vrAdCanvas.MovieQuad.GetViewingData();
			if (viewStat != null && Time.frameCount % 5 == 0)
			{
				debugText.text =
					//"developer_id = "+communicator.randomAdRequest.developer_id+"\n"+
					//"developer_access_token = "+communicator.randomAdRequest.developer_access_token+"\n"+
					//"developer_game_id = "+communicator.randomAdRequest.developer_game_id+"\n"+
					"url_media = " + target_vrAdCanvas.CurrentAdResponse.result.str_MediaURL + "\n" +
                    "type_media_format = " + target_vrAdCanvas.CurrentAdResponse.result.str_MediaTypeName + "\n" +
                    "type_media_format_ids = " + target_vrAdCanvas.CurrentAdResponse.result.ID_MediaType.ToString() + "\n" +
					//"device_id = "+communicator.randomAdRequest.device_id+"\n"+
					"total_hit_time = " + viewStat.dec_TotalHitTime.ToString("F1") + "\n" +
					"total_screen_percentage = " + viewStat.dec_TotalScreenPercentage.ToString("F1") + "\n" +
					"total_blocked_percentage = " + viewStat.dec_TotalBlockedPercentage.ToString("F1") + "\n" +
					"position  = " + (viewStat.dec_TotalScreenPositionX).ToString("F1") + ", " + (viewStat.dec_TotalScreenPositionY).ToString("F1") + "\n" +
                    "total_volume_percentage = " + viewStat.dec_TotalVolumePercentage.ToString("F1") + "\n";
			}
		}
    }
   
    // ==============================================================================

    public void TestSendViewingData()
    {
		ClassViewDataRequest viewStat = target_vrAdCanvas.MovieQuad.GetViewingData();
        communicator.SendViewData( viewStat );
    }   
}



