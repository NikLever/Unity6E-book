using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class ShotManager : MonoBehaviour
{
	public CinemachineCamera activeVCam;

    public void SetShot(CinemachineCamera newVCam)
	{
		activeVCam.Priority = 0;
		newVCam.Priority = 100;

		activeVCam = newVCam;
	}
}
