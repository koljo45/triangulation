using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PolygonManager : MonoBehaviour
{
    [SerializeField] private Camera _raycastCamera;
    [SerializeField] private GameObject _pointPrefab;
    [SerializeField] private MeshObject _outputGameObjectPrefab;
    [SerializeField] private Vector3 _positionOffset = new Vector3(0, 0, 0.01f);
    [SerializeField] private bool _convexSupport = true;

    private LineRenderer _lineRenderer;
    private List<GameObject> _polygonOutline = new List<GameObject>();
    private bool _gateEdgeOverlap = false;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        // Mouse left button released
        if (Input.GetMouseButtonUp(0))
        {
            Ray ray = _raycastCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                _lineRenderer.startColor = Color.white;
                _lineRenderer.endColor = _lineRenderer.startColor;
                if (_polygonOutline.Count > 1)
                {
                    _lineRenderer.startColor = Color.green;
                    _lineRenderer.endColor = _lineRenderer.startColor;
                }

                _gateEdgeOverlap = false;
                if (_lineRenderer.positionCount > 2)
                {
                    if (EdgeOverlapsOutline(_lineRenderer.GetPosition(_lineRenderer.positionCount - 1), hit.point))
                    {
                        return;
                    }

                    if (EdgeOverlapsOutline(hit.point, _lineRenderer.GetPosition(0)))
                    {
                        _lineRenderer.startColor = Color.red;
                        _lineRenderer.endColor = _lineRenderer.startColor;
                        _gateEdgeOverlap = true;
                    }
                }

                GameObject go = Instantiate(_pointPrefab);
                go.transform.position = hit.point + _positionOffset;

                _polygonOutline.Add(go);

                _lineRenderer.positionCount = _polygonOutline.Count;
                _lineRenderer.SetPositions(_polygonOutline.Select(gam => gam.transform.position).ToArray());
            }
            else
            {
                Debug.LogError("Mouse position raycast failed, this should never happen!");
            }
        }
        else if (Input.GetKeyUp(KeyCode.Return) && !_gateEdgeOverlap)
        {
            if (_polygonOutline.Count < 3)
            {
                Debug.LogWarning("Cannot triangulate only one edge.");
                return;
            }

            Mesh m = new Mesh();
            Vector3[] edge = _polygonOutline.Select(gam => gam.transform.position).ToArray();
            m.vertices = edge;
            Triangulation.CapMesh(m, Enumerable.Range(0, edge.Length).ToList(), 0, _convexSupport);

            MeshObject outputGO = Instantiate(_outputGameObjectPrefab.gameObject).GetComponent<MeshObject>();
            outputGO.SetMesh(m);
            outputGO.SetPolygonCollider(edge.Select(v3 => new Vector2(v3.x, v3.y)).ToArray());

            _polygonOutline.ForEach(go => Destroy(go));
            _polygonOutline.Clear();
            _lineRenderer.positionCount = _polygonOutline.Count;
            _lineRenderer.startColor = Color.white;
            _lineRenderer.endColor = _lineRenderer.startColor;
        }
    }

    private bool EdgeOverlapsOutline(Vector3 v11, Vector3 v12)
    {
        for (int i = 0; i < _lineRenderer.positionCount - 1; i++)
        {
            Vector3 v21 = _lineRenderer.GetPosition(i);
            Vector3 v22 = _lineRenderer.GetPosition(i + 1);
            if (LinesOverlap(v11, v12, v21, v22))
            {
                return true;
            }
        }
        return false;
    }

    private bool LinesOverlap(Vector3 v11, Vector3 v12, Vector3 v21, Vector3 v22)
    {
        float s21 = Sign(v21, v12, v11);
        float s22 = Sign(v22, v12, v11);
        float s11 = Sign(v11, v22, v21);
        float s12 = Sign(v12, v22, v21);

        s21 = s21 == 0 ? s22 : s21;
        s22 = s22 == 0 ? s21 : s22;
        s11 = s11 == 0 ? s12 : s11;
        s12 = s12 == 0 ? s11 : s12;
        return (Mathf.Sign(s21) != Mathf.Sign(s22)) && (Mathf.Sign(s11) != Mathf.Sign(s12));
    }

    private float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 v13 = (p1 - p3).normalized;
        Vector3 v23 = (p2 - p3).normalized;
        //float sign = (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        float sign = v13.x * v23.y - v23.x * v13.y;
        return sign;
    }
}
