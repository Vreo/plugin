using UnityEngine;
using System.Collections.Generic;

public class GlowObject : MonoBehaviour
{
	public Color HighlightColor;

	void Start()
	{
		var Renderers = GetComponentsInChildren<Renderer>();

		var materials = new List<Material>();
		
		foreach (var renderer in Renderers)
		{	
			materials.AddRange(renderer.materials);
		}

		for (int i = 0; i < materials.Count; i++)
		{
			materials[i].SetColor("_GlowColor", HighlightColor);
		}
	}
}
