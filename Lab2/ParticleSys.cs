using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSys : MonoBehaviour
{
    private enum SpawnShape { Point = 0, Box = 1 }

    [SerializeField] private GameObject _particlePrototype = null;
    [SerializeField] private int _numParticles;
    [SerializeField] private float _startLifetime;
    [SerializeField] [Range(1, 100)] private float _spawnSpeed;
    [SerializeField] private SpawnShape _spawnShape = SpawnShape.Point;
    [Header("Movement")]
    [SerializeField] private Vector3 _startingDirection;
    [SerializeField] private Vector2 _startSpeed;
    [SerializeField] private Vector3 _acceleration;
    [Header("Rendering")]
    [SerializeField] private Color _startColor;
    [SerializeField] private Color _endColor;
    [Header("System objects")]
    [SerializeField] private Tractor[] _tractors;

    private Renderer[] _particleRenderer;
    private Transform[] _particlesTrans;
    private float[] _particlesLife;
    private Vector3[] _particleVelocity;
    private float _spawnTimer = 0;

    private void OnDrawGizmosSelected()
    {
        if (_spawnShape == SpawnShape.Point)
        {
            Gizmos.DrawWireSphere(transform.position, 1);
        }
        else if (_spawnShape == SpawnShape.Box)
        {
            Matrix4x4 temp = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = temp;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + _startingDirection.normalized);
    }

    void Awake()
    {
        _particlesTrans = new Transform[_numParticles];
        _particlesLife = new float[_numParticles];
        _particleVelocity = new Vector3[_numParticles];
        _particleRenderer = new Renderer[_numParticles];

        for (int i = 0; i < _numParticles; i++)
        {
            GameObject go = Instantiate(_particlePrototype);
            _particlesTrans[i] = go.transform;
            _particleRenderer[i] = go.GetComponentInChildren<Renderer>();
        }
    }

    void FixedUpdate()
    {
        int numToSpawn = (int)((_spawnTimer += Time.fixedDeltaTime) / (1.0f / _spawnSpeed));
        _spawnTimer -= (1.0f / _spawnSpeed) * numToSpawn;
        for (int i = 0; i < _numParticles; i++)
        {
            if (_particlesLife[i] <= 0)
            {
                _particlesTrans[i].gameObject.SetActive(false);
                if (numToSpawn-- > 0)
                {
                    SpawnParticle(i);
                }
                continue;
            }

            Vector3 posDelta = _particleVelocity[i] * Time.fixedDeltaTime;
            _particlesTrans[i].position = _particlesTrans[i].position + posDelta;
            _particlesTrans[i].rotation = Quaternion.LookRotation(PlayerController.Instance.transform.position - _particlesTrans[i].position, Vector3.up);

            Vector3 velDelta = _acceleration * Time.fixedDeltaTime;
            foreach (Tractor t in _tractors)
            {
                Vector3 tractorToParticle = _particlesTrans[i].position - t.transform.position;
                if (tractorToParticle.magnitude < t.Range)
                {
                    velDelta += tractorToParticle * t.Strenght * (t.Type == Tractor.TractorType.Attractor ? -1 : 1) * Time.fixedDeltaTime;
                }
            }
            _particleVelocity[i] += velDelta;

            _particleRenderer[i].material.color = LerpRGB(_endColor, _startColor, _particlesLife[i] / _startLifetime);

            _particlesLife[i] -= Time.fixedDeltaTime;
        }
    }

    private void SpawnParticle(int index)
    {
        _particlesLife[index] = _startLifetime;
        _particlesTrans[index].gameObject.SetActive(true);
        _particleVelocity[index] = _startingDirection.normalized * (_startSpeed.x + Random.Range(0.0f, 1.0f) * (_startSpeed.y - _startSpeed.x));
        _particleRenderer[index].material.color = _startColor;
        if (_spawnShape == SpawnShape.Point)
        {
            _particlesTrans[index].SetPositionAndRotation(transform.position, transform.rotation);
        }
        else if (_spawnShape == SpawnShape.Box)
        {
            Vector3 pos = new Vector3(Random.Range(-.5f, .5f), Random.Range(-.5f, .5f), Random.Range(-.5f, .5f));
            _particlesTrans[index].SetPositionAndRotation(transform.localToWorldMatrix.MultiplyPoint(pos), Quaternion.identity);
        }
    }

    private static Color LerpRGB(Color a, Color b, float t)
    {
        t = Mathf.Clamp(t, 0.0f, 1.0f);
        return new Color
        (a.r + (b.r - a.r) * t,
        a.g + (b.g - a.g) * t,
        a.b + (b.b - a.b) * t,
        a.a + (b.a - a.a) * t);
    }
}