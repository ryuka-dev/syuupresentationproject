using UnityEngine;

/// <summary>
/// DonutAoE 範囲提示コントローラー。
/// Setup(innerRadius, outerRadius) を呼ぶと、内半径から外半径の間のみを
/// 手続き生成したリングメッシュで塗りつぶす。
/// 内側の安全区は一切レンダリングしない。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DonutAoETelegraphController : MonoBehaviour
{
    [Header("リングの見た目")]
    [SerializeField] private Color  ringColor    = new Color(1f, 0.12f, 0.05f, 0.55f);
    [SerializeField] private int    segments     = 64;

    private MeshFilter   _meshFilter;
    private MeshRenderer _meshRenderer;

    private void Awake()
    {
        _meshFilter   = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows    = false;
    }

    /// <summary>
    /// 読条開始時にコルーチンから呼ばれる。
    /// 指定した内半径～外半径のリングメッシュとマテリアルを設定する。
    /// </summary>
    public void Setup(float innerRadius, float outerRadius)
    {
        _meshFilter.mesh  = GenerateRingMesh(innerRadius, outerRadius, segments);
        _meshRenderer.sharedMaterial = BuildTransparentMat(ringColor);
    }

    // ─── リングメッシュ生成 ───────────────────────────────────────

    private static Mesh GenerateRingMesh(float inner, float outer, int seg)
    {
        var mesh  = new Mesh { name = "DonutRing" };
        int verts = seg * 2;

        var vertices  = new Vector3[verts];
        var uvs       = new Vector2[verts];
        var triangles = new int[seg * 6];

        for (int i = 0; i < seg; i++)
        {
            float angle = (float)i / seg * Mathf.PI * 2f;
            float cos   = Mathf.Cos(angle);
            float sin   = Mathf.Sin(angle);

            // 内頂点
            vertices[i * 2]     = new Vector3(cos * inner, 0f, sin * inner);
            uvs[i * 2]          = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);

            // 外頂点
            vertices[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
            uvs[i * 2 + 1]      = new Vector2(cos, sin);
        }

        for (int i = 0; i < seg; i++)
        {
            int next = (i + 1) % seg;
            int ti   = i * 6;

            // 内→次内→外
            triangles[ti]     = i    * 2;
            triangles[ti + 1] = next * 2;
            triangles[ti + 2] = i    * 2 + 1;

            // 次内→次外→外
            triangles[ti + 3] = next * 2;
            triangles[ti + 4] = next * 2 + 1;
            triangles[ti + 5] = i    * 2 + 1;
        }

        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ─── 半透明マテリアル生成 ────────────────────────────────────

    private static Material BuildTransparentMat(Color col)
    {
        var mat = new Material(Shader.Find("Standard")) { color = col };
        mat.SetFloat("_Mode",     3f);
        mat.SetInt("_SrcBlend",   (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",   (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",     0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }
}
