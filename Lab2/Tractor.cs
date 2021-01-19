using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tractor : MonoBehaviour
{
    public enum TractorType { Attractor, Repeller }

    [SerializeField] private TractorType _type;
    [SerializeField] private float _range;
    [SerializeField] private float _strenght;

    public TractorType Type { get { return _type; } }
    public float Range { get { return _range; } }
    public float Strenght { get { return _strenght; } }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, _range);
    }
}
