using UnityEngine;
using System.Collections;

/// <summary>Disables the preview objects when entering play mode.</summary>

public class HideOnPlay : MonoBehaviour {

	// Use this for initialization
	void Start () {
		gameObject.SetActive (false);
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
