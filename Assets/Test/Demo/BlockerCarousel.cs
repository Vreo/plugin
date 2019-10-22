using UnityEngine;
using System.Collections;

public class BlockerCarousel : MonoBehaviour 
{
    public float radius = 10.0f;
    private float oldradius = -999999989.0f;
    public int numObjects = 10;
    public GameObject myobject;
    private GameObject[] myobjects;


    void Awake()
    {
        myobjects = new GameObject[ numObjects ]; 
        for ( int i = 0; i < numObjects; i++ )
        {
            myobjects[ i ] = Instantiate( myobject );
        }
        Destroy( myobject );
        Regenerate();
    }



    void Update()
    {
        if ( radius != oldradius )
        {
            Regenerate();
        }
    }



    void Regenerate()
    {
        if ( radius != oldradius )
        {
            oldradius = radius;

            for ( int i = 0; i < numObjects; i++ )
            {
                float x = radius * Mathf.Cos( (float)i*2.0f*Mathf.PI/(float)(numObjects) );
                float y = 0.0f;
                float z = radius * -Mathf.Sin( (float)i*2.0f*Mathf.PI/(float)(numObjects) );

                myobjects[ i ].transform.position = new Vector3( x, y, z );
            }

        }
    }


}


