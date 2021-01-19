using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance
    {
        get; private set;
    }

    [SerializeField] private float _movementSpeed = 1f;
    [SerializeField] private float _rotationSpeed = 20f;

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        Vector3 pos = transform.position;
        pos += transform.right * Input.GetAxis("Horizontal") * _movementSpeed * Time.deltaTime;
        pos += transform.forward * Input.GetAxis("Vertical") * _movementSpeed * Time.deltaTime;

        Quaternion rot = transform.rotation;
        if (Input.GetKey(KeyCode.I))
        {
            rot *= Quaternion.AngleAxis(-_rotationSpeed * Time.deltaTime, Vector3.right);
        }
        else if (Input.GetKey(KeyCode.K))
        {
            rot *= Quaternion.AngleAxis(_rotationSpeed * Time.deltaTime, Vector3.right);
        }

        if (Input.GetKey(KeyCode.J))
        {
            rot *= Quaternion.AngleAxis(-_rotationSpeed * Time.deltaTime, Vector3.up);
        }
        else if (Input.GetKey(KeyCode.L))
        {
            rot *= Quaternion.AngleAxis(_rotationSpeed * Time.deltaTime, Vector3.up);
        }

        transform.SetPositionAndRotation(pos, rot);
    }
}
