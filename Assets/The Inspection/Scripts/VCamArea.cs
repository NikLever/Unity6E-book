using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class VCamArea : MonoBehaviour
{
    public CinemachineCamera virtualCamera;
	private ShotManager shotManager;

	private void Start()
	{
		shotManager = GameObject.FindFirstObjectByType<ShotManager>();
	}

	private void OnTriggerEnter(Collider other)
	{
		shotManager.SetShot(virtualCamera);
	}
}
