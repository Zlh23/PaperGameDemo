using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class PaperController : MonoBehaviour
{
    public struct ColliderData
    {
        public Vector3 pos,scale;
        public Vector2[] points;
    }
    [Serializable]
    public struct Data
    {
        public Vector3 AMpos,VMpos;
        public Vector3 AMscale, VMscale;
        public RenderTexture ft, bt;
        public List<ColliderData> frontCollidersData,backCollidersData;
    }
    
    [SerializeField] private Camera frontCamera, backCamera;
    [SerializeField] private Transform FrontPaper,frontBG, frontMask, backBG, backMask ,VMask;
    [SerializeField] private RenderTexture frt, brt;
    [SerializeField] private RenderTexture ft, bt;

    private List<PolygonCollider2D> frontColliders,backColliders;

    private Transform virtualBackground_copy;

    private Vector3 mouseStartPos;

    [SerializeField]private List<Data> datas;

    private bool mouseStartPosInsideMask;

    private void OnGUI()
    {
        if(GUI.Button(new Rect(0,0,100,40),"Rewind"))
            LoadStep();
    }

    private void Awake()
    {
        datas = new List<Data>();
        frontColliders = new List<PolygonCollider2D>(frontBG.GetComponentsInChildren<PolygonCollider2D>());
        backColliders = new List<PolygonCollider2D>(backBG.GetComponentsInChildren<PolygonCollider2D>());
    }

     void Start()
    {
        //StartCoroutine(RenderPaper());
        frontCamera.Render();
        backCamera.Render();
        Graphics.Blit(frt, ft);
        Graphics.Blit(brt, bt);
        frontBG.GetComponent<Renderer>().material.SetTexture("_MainTex",ft);
        backBG.GetComponent<Renderer>().material.SetTexture("_MainTex", bt);
    }

    private void RenderPaper()
    {
        Graphics.Blit(frt, ft);
        Graphics.Blit(brt, bt);

        if (virtualBackground_copy != null)
        {
            foreach (var v in frontColliders)
            {
                v.transform.parent = frontBG;
            }
            Destroy(virtualBackground_copy.gameObject);
        }

    }

    // Update is called once per frame'
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            
            mouseStartPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseStartPosInsideMask = CheckInsideMask(mouseStartPos,frontMask);
            if (mouseStartPosInsideMask)
            {
                SaveStep();
                PreFold();
            }

        }
        if (Input.GetMouseButton(0))
        {
            if (mouseStartPosInsideMask)
            {
                var distance = Camera.main.ScreenToWorldPoint(Input.mousePosition) - mouseStartPos;
                Fold(distance);
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            EndStep();
        }
    }

    private bool CheckInsideMask(Vector3 pos,Transform mask)
    {
        var position = mask.position;
        var localScale = mask.localScale;
        var max = position + localScale/2;
        var min = position - localScale/2;
        var ret = pos.x >= min.x && pos.x <= max.x &&
                  pos.y >= min.y && pos.y <= max.y;
        return ret;
    }

    private bool IfLeft()
    {
        return mouseStartPos.x < frontMask.position.x;
    }

    private void Fold(Vector3 distance)
    {
        var d = distance.x;
        var currentData = datas[^1];
        var s = currentData.AMscale.x;
        d = IfLeft() ? Mathf.Clamp(d, 0, s) : Mathf.Clamp(d, -s, 0);
        var offset =virtualBackground_copy.position - VMask.position ;
        VMask.position = a + new Vector3(d,0,0);
        virtualBackground_copy.position = VMask.position + offset;
        MaskFoldTranslate(frontMask, d/2, currentData.AMpos, currentData.AMscale);
        MaskFoldTranslate(backMask, d/2, currentData.VMpos, currentData.VMscale);
    }

    private Vector3 a;
    
    private void PreFold()
    {
        if(datas.Count==0)
            return;
        var currentData = datas[^1];
        var backMaskScale = currentData.VMscale;
        var offset = new Vector3(frontMask.localScale.x+backMask.localScale.x,0,0)/2;
        VMask.gameObject.SetActive(true);
        
        virtualBackground_copy = Instantiate(backBG, FrontPaper,true);
        VMask.position = IfLeft() ? frontMask.position - offset : frontMask.position + offset;
        VMask.localScale = backMaskScale;
        VMask.localPosition = new Vector3(VMask.localPosition.x, VMask.localPosition.y, 10f);
        a = VMask.position;
        
        var virtualColliders = virtualBackground_copy.GetComponentsInChildren<PolygonCollider2D>();
        frontColliders.AddRange(virtualColliders);
        
        virtualBackground_copy.position = a - (backBG.position - backMask.position) - new Vector3(0,0,1);
        virtualBackground_copy.localScale -= new Vector3(virtualBackground_copy.localScale.x * 2, 0, 0);
    }

    void EndStep()
    {
        VMask.gameObject.SetActive(false);
        RenderPaper();
        RebuildColliders();
    }

    private void RebuildColliders()
    {
        for (var index = frontColliders.Count - 1; index >= 0; index--)
        {
            var v = frontColliders[index];
            var points = CutColliderByMask(v, frontMask);
            v.SetPath(0, points);
            if (points.Count == 0)
            {
                frontColliders.Remove(v);
                Destroy(v.gameObject);
            }
        }

        for (var index = backColliders.Count - 1; index >= 0; index--)
        {
            var v = backColliders[index];
            var points = CutColliderByMask(v, backMask);
            v.SetPath(0, points);
            if (points.Count == 0)
            {
                backColliders.Remove(v);
                Destroy(v.gameObject);
            }
        }
        //throw new NotImplementedException();
    }
    

    private List<Vector2> CutColliderByMask(PolygonCollider2D v, Transform mask)
    {
        List<Vector2> points = new List<Vector2>();
        var vpoints = v.GetPath(0);
        var worldPoints = new Vector2[vpoints.Length];
        
        Matrix4x4 m = v.transform.localToWorldMatrix;
        Matrix4x4 m2 = v.transform.worldToLocalMatrix;
        
        for (var index = 0; index < worldPoints.Length; index++)
        {
            worldPoints[index] = m*new Vector4(vpoints[index].x,vpoints[index].y,0,1);
        }

        bool inside_prev = CheckInsideMask(worldPoints[0],mask);
        bool inside;
        for (var i = 0; i < vpoints.Length; i++)
        {
            inside = CheckInsideMask(worldPoints[i], mask);
            if (inside != inside_prev)
            {
                points.Add((Vector2)(m2*FindIntersectPoint(worldPoints[i-1],worldPoints[i],mask,worldPoints[i])));
                inside_prev = inside;
            }
            if (inside)
            {
                points.Add(vpoints[i]);
            }
        }
        inside = CheckInsideMask(worldPoints[0],mask);
        if (inside != inside_prev)
        {
            var ret = m2*FindIntersectPoint(worldPoints[^1], worldPoints[0], mask,worldPoints[^1]);
//            print(ret+":intersectPoint");
            points.Add(ret);
        }
        return points;
    }


    private Vector4 FindIntersectPoint(Vector2 v1,Vector2 v2,Transform Mask,Vector4 backup)
    {
        var min = Mask.position - Mask.localScale/2;
        var max = Mask.position + Mask.localScale/2;
        var p0 = new Vector2(min.x, min.y);
        var p1 = new Vector2(min.x, max.y);
        var p2 = new Vector2(max.x, max.y);
        var p3 = new Vector2(max.x, min.y);
        Vector4 ret;
        var a = TryGetIntersectPoint(v1, v2, p0, p1,out ret);
        ret.w = 1;
        if (a)
            return ret;
        a = TryGetIntersectPoint(v1, v2, p1, p2,out ret);
        ret.w = 1;
        if (a)
            return ret;
        a = TryGetIntersectPoint(v1, v2, p2, p3,out ret);
        ret.w = 1;
        if (a)
            return ret;
        a = TryGetIntersectPoint(v1, v2, p3, p0,out ret);
        ret.w = 1;
        if(a)
            return ret;
        backup.w = 1;
        return backup ;
    }

    private bool TryGetIntersectPoint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector4 intersectPos)
    {
        intersectPos = Vector3.zero;

        Vector3 ab = b - a;
        Vector3 ca = a - c;
        Vector3 cd = d - c;

        Vector3 v1 = Vector3.Cross(ca, cd);

        if (Mathf.Abs(Vector3.Dot(v1, ab)) > 1e-6)
        {
            // 不共面
            return false;
        }

        if (Vector3.Cross(ab, cd).sqrMagnitude <= 1e-6)
        {
            // 平行
            return false;
        }

        Vector3 ad = d - a;
        Vector3 cb = b - c;
        // 快速排斥
        if (Mathf.Min(a.x, b.x) > Mathf.Max(c.x, d.x) || Mathf.Max(a.x, b.x) < Mathf.Min(c.x, d.x)
                  || Mathf.Min(a.y, b.y) > Mathf.Max(c.y, d.y) || Mathf.Max(a.y, b.y) < Mathf.Min(c.y, d.y)
                  || Mathf.Min(a.z, b.z) > Mathf.Max(c.z, d.z) || Mathf.Max(a.z, b.z) < Mathf.Min(c.z, d.z))
            return false;

        // 跨立试验
        if (Vector3.Dot(Vector3.Cross(-ca, ab), Vector3.Cross(ab, ad)) > 0
            && Vector3.Dot(Vector3.Cross(ca, cd), Vector3.Cross(cd, cb)) > 0)
        {
            Vector3 v2 = Vector3.Cross(cd, ab);
            float ratio = Vector3.Dot(v1, v2) / v2.sqrMagnitude;
            intersectPos = a + ab * ratio;
            return true;
        }

        return false;
    }
    private void SaveStep()
    {
        Debug.Log("SaveStep");
        var data = new Data
        {
            AMpos = frontMask.position,
            VMpos = backMask.position,
            AMscale = frontMask.localScale,
            VMscale = backMask.localScale,
            frontCollidersData  = new List<ColliderData>(),
            backCollidersData = new List<ColliderData>(),
            ft = RenderTexture.GetTemporary(ft.descriptor),
            bt = RenderTexture.GetTemporary(bt.descriptor),
        };
        Graphics.Blit(ft,data.ft);
        Graphics.Blit(bt,data.bt);
        
        foreach (var v in frontColliders)
        {
            data.frontCollidersData.Add(new ColliderData
            {
                pos = v.transform.position,
                scale = v.transform.localScale,
                points = v.GetPath(0)
            });
        }
        
        foreach (var v in backColliders)
        {
            data.backCollidersData.Add(new ColliderData
            {
                pos = v.transform.position,
                scale = v.transform.localScale,
                points = v.GetPath(0)
            });
        }
        
        datas.Add(data);
    }

    private void LoadStep()
    {
        Debug.Log("LoadStep");
        if(datas.Count==0)
            return;
        var data = datas[^1];
        datas.Remove(data);
        Graphics.Blit(data.ft,ft);
        Graphics.Blit(data.bt,bt);
        frontMask.position = data.AMpos;
        backMask.position = data.VMpos;
        frontMask.localScale = data.AMscale;
        backMask.localScale = data.VMscale;
               
        foreach (var v in frontColliders)
        {
            Destroy(v.gameObject);
        }
        frontColliders.Clear();
        
        foreach (var v in backColliders)
        {
            Destroy(v.gameObject);
        }

        backColliders.Clear();

        foreach (var v in data.frontCollidersData)
        {
            var collider = new GameObject().AddComponent<PolygonCollider2D>();
            collider.transform.parent = frontBG;
            collider.pathCount = 1;
            collider.transform.position = v.pos;
            collider.transform.localScale = v.scale;
            collider.SetPath(0,v.points);
            frontColliders.Add(collider);
        }
        foreach (var v in data.backCollidersData)
        {
            var collider = new GameObject().AddComponent<PolygonCollider2D>();
            collider.transform.parent = backBG;
            collider.pathCount = 1;
            collider.transform.position = v.pos;
            collider.transform.localScale = v.scale;
            collider.SetPath(0,v.points);
            backColliders.Add(collider);
        }
        
    }

    void MaskFoldTranslate(Transform Mask, float offset,Vector3 oriPos,Vector3 oriSize)
    {
        var localScale = Mask.localScale;
        Mask.transform.position = oriPos + new Vector3(offset/2,0,0);

        localScale = offset > 0 ? new Vector3(oriSize.x - offset,localScale.y,localScale.z) : new Vector3(oriSize.x + offset,localScale.y,localScale.z);
        Mask.localScale = localScale;
    }
}
