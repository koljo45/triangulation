using UnityEngine;

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(PolygonCollider2D)), RequireComponent(typeof(Rigidbody2D))]
public class MeshObject : MonoBehaviour
{
    private MeshFilter _meshFilter;
    private Rigidbody2D _rigidbody;
    private PolygonCollider2D _collider;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<PolygonCollider2D>();
        _rigidbody.isKinematic = true;
    }

    public void SetMesh(Mesh m)
    {
        _meshFilter.mesh = m;
    }

    public void SetPolygonCollider(Vector2[] polygon)
    {
        _collider.SetPath(0, polygon);
        _rigidbody.isKinematic = false;
    }
}
