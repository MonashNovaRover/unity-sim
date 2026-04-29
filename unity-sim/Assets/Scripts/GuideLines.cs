using UnityEngine;

public class RoadGuidelines : MonoBehaviour
{
    public float roadW = 5f, roadL = 50f, lineW = 0.1f;
    public Color lineColour = Color.cyan;

    void Start()
    {
        CreateGuidelines("leftLine",  -roadW/2f);
        CreateGuidelines("rightLine",  roadW/2f);
    }

    void CreateGuidelines(string name, float offset)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.SetPosition(0, transform.TransformPoint(new Vector3(offset, 0f, 0f)));   // start
        lr.SetPosition(1, transform.TransformPoint(new Vector3(offset, 0f, roadL)));   // end

        lr.startWidth = lineW;
        lr.endWidth   = lineW;
        
        lr.material   = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lineColour;
        lr.endColor   = lineColour;
        lr.useWorldSpace = true;
    }
}