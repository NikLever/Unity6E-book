using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//TODO: This was hard to do from the animator so I made a script. Not sure what the best way to do it is
[ExecuteAlways]
public class TopFoldout : MonoBehaviour
{
    public float RotationSpeed;
    public float PieceOffset;

    private Transform[] children;

    // Start is called before the first frame update
    void Start()
    {
        children = transform.GetComponentsInChildren<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 1; i < children.Length; i++)
        {
            Transform child = children[i];
            Vector3 pos = child.localPosition;
            pos.y = 0;
            pos.x = 0;
            pos.z = 0;
            pos += transform.InverseTransformDirection(child.up) * PieceOffset;
            child.localPosition = pos;
        }
        
        transform.Rotate(Vector3.up, RotationSpeed * Time.deltaTime);
    }
}
